using System;
using System.Threading;
using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Utility;
using Unity.Netcode;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// Runs the rungs this player has climbed. Owner-only, because what a hallucination looks like is
	// drawn on one screen and is the one thing at this table that is deliberately not shared.
	//
	// A rung's effect is picked when the rung is *climbed*, not when it is read: sobering back down takes
	// it off, and climbing again draws a fresh one. That is what makes an item that lowers the rate
	// visible — the world clears, and coming back is a different room rather than the same one returning.
	public class PokerHallucinationController : NetworkBehaviour
	{
		[Header("References")]
		[SerializeField] private PokerPlayer _player;
		[SerializeField] private PokerPlayerData _data;

		[Tooltip("Which rungs exist and what each can draw. Empty plays the round with no hallucinations at all, which is what a table testing the card rules wants.")]
		[SerializeField] private PokerHallucinationTiers _tiers;

		[Tooltip("How long the blink takes and how long the effects ease behind it. Empty applies a rung change outright, which is what a table testing the ladder wants.")]
		[SerializeField] private PokerHallucinationPacing _pacing;

		[Tooltip("Where the running effects are hung. Empty hangs them on this object, which is what a player prefab wants.")]
		[SerializeField] private Transform _effectRoot;

		// Rung index to the objects running for it. Absent means the rung is not climbed; a rung may draw
		// several at once, and they come off together.
		private readonly Dictionary<int, List<PokerHallucinationEffectBehaviour>> _active = new();

		// Pool indices, shuffled as far as a draw needs. Reused rather than allocated per climb.
		private readonly List<int> _drawOrder = new();

		// The same set in rung order, for anything that wants to say what a player is under. Kept beside the
		// dictionary rather than sorted on demand: it is rebuilt when a rung is climbed or lost, which is the
		// only time it can change.
		private readonly List<PokerHallucinationEffectBehaviour> _running = new();

		private bool _transitioning;

		public int ActiveCount => _running.Count;

#if UNITY_EDITOR
		// What this player is under, in rung order. The effects are objects in the hierarchy now, but their
		// names carry the rung and the asset, and reading them off one line beats picking through children
		// while trying to work out why the room looks like that. Editor only: it builds strings, and nothing
		// at runtime asks.
		[ShowInInspector, ReadOnly, PropertyOrder(100), LabelText("Running")]
		private List<string> RunningEffects
		{
			get
			{
				var names = new List<string>();

				foreach (var effect in _running)
				{
					if (effect) names.Add(effect.name);
				}

				return names;
			}
		}

		// Runs one effect outright, outside the ladder, to judge a look without climbing to its rung. Only the
		// owner draws hallucinations, so it only runs on your own player; the ladder never sees these.
		private readonly List<PokerHallucinationEffectBehaviour> _debugRunning = new();

		[FoldoutGroup("Debug"), PropertyOrder(200), ShowInInspector, LabelText("Effect")]
		[ValueDropdown(nameof(DebugEffectChoices))]
		[InfoBox("Play mode, on your own player only.", InfoMessageType.None, nameof(CannotRunDebug))]
		private PokerHallucinationEffect _debugEffect;

		private bool CanRunDebug => Application.isPlaying && IsSpawned && IsOwner;
		private bool CannotRunDebug => !CanRunDebug;

		[FoldoutGroup("Debug"), PropertyOrder(201), ShowInInspector, ReadOnly, LabelText("Debug Running")]
		private List<string> DebugRunningEffects
		{
			get
			{
				var names = new List<string>();

				foreach (var effect in _debugRunning)
				{
					if (effect) names.Add(effect.name);
				}

				return names;
			}
		}

		[FoldoutGroup("Debug"), PropertyOrder(202), Button("Run"), EnableIf(nameof(CanRunDebug))]
		private void RunDebugEffect()
		{
			if (!CanRunDebug || !_debugEffect) return;

			var behaviour = _debugEffect.Run(_effectRoot ? _effectRoot : transform, _player, _pacing);
			if (!behaviour) return;

			behaviour.name = $"Debug - {_debugEffect.name}";
			_debugRunning.Add(behaviour);
		}

		[FoldoutGroup("Debug"), PropertyOrder(203), Button("Stop All"), EnableIf(nameof(CanRunDebug))]
		private void StopDebugEffects()
		{
			foreach (var behaviour in _debugRunning)
			{
				if (behaviour) behaviour.Stop();
			}

			_debugRunning.Clear();
		}

		// Grouped by effect type, so a long catalogue reads as a tree.
		private static IEnumerable<ValueDropdownItem<PokerHallucinationEffect>> DebugEffectChoices()
		{
			foreach (var guid in UnityEditor.AssetDatabase.FindAssets($"t:{nameof(PokerHallucinationEffect)}"))
			{
				var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<PokerHallucinationEffect>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
				if (!asset) continue;

				var kind = asset.GetType().Name.Replace("PokerHallucination", "").Replace("Effect", "");
				yield return new ValueDropdownItem<PokerHallucinationEffect>($"{kind}/{asset.name}", asset);
			}
		}
#endif

		// The whole blink, start to finish: closing, held shut, opening. The rungs land ApplyDelay into it and
		// the eye stays shut for HoldDuration after, and the view that draws the eyelids reads these same
		// numbers rather than carrying its own to keep in step.
		public float TransitionDuration => _pacing ? _pacing.TransitionDuration : 0f;

		public float ApplyDelay => _pacing ? _pacing.CloseDuration : 0f;

		public float HoldDuration => _pacing ? _pacing.HoldDuration : 0f;

		public float OpenDuration => _pacing ? _pacing.OpenDuration : 0f;

		// How long a beat about this player waits for the blink a change between these rates sets off, which
		// is none at all when no rung is crossed.
		public float BlinkWait(int previousRate, int currentRate) =>
			CrossesRung(previousRate, currentRate) ? TransitionDuration : 0f;

		// The ladder itself, for anything drawing where its rungs sit. Read as authored, so every screen shows the
		// same marks whether or not it is the one running the effects.
		public PokerHallucinationTiers Tiers => _tiers;

		// Whether moving between these two rates climbs or loses a rung — which is exactly when the screen
		// spends TransitionDuration blinking. Asked by the server, which has this component too and can read
		// the ladder off it: anything pacing a beat around the blink has to know whether one is coming, and
		// the alternative is a second copy of the thresholds somewhere that only ever drifts.
		public bool CrossesRung(int previousRate, int currentRate)
		{
			if (!_tiers) return false;

			var rungs = _tiers.Rungs;

			for (var i = 0; i < rungs.Count; i++)
			{
				if (rungs[i] == null) continue;

				if (previousRate >= rungs[i].Threshold != (currentRate >= rungs[i].Threshold)) return true;
			}

			return false;
		}

		public event Action OnTransitionStarted;

		public override void OnNetworkSpawn()
		{
			if (!IsOwner) return;

			if (!_player) _player = GetComponentInParent<PokerPlayer>();
			if (!_data) _data = GetComponentInParent<PokerPlayerData>();
			if (!_data) return;

			_data.OnHallucinationChanged += HandleChanged;

			// Read rather than waited for: a client arriving at a table already halfway under has no
			// change coming to tell it so.
			Refresh();
		}

		public override void OnNetworkDespawn()
		{
			if (_data) _data.OnHallucinationChanged -= HandleChanged;

			// Everything comes off on the way out: an effect is a change to this client's whole view, and
			// leaving one running would outlive the table it belonged to.
			EndAll();
#if UNITY_EDITOR
			StopDebugEffects();
#endif
		}

		private void HandleChanged(int previous, int current) => Refresh();

		// The blink is there to cover the swap, so the rungs are not allowed to land while the eye is open.
		// A change arriving mid-blink is folded into the one already running rather than queued behind it:
		// ApplyRungs reads the rate at the moment it runs, so the later change is what lands anyway, and a
		// queue would spend a second blink saying nothing new.
		private void Refresh()
		{
			if (!_tiers || !_data) return;
			if (!AnyRungWouldChange()) return;

			if (TransitionDuration <= 0f)
			{
				ApplyRungs();
				return;
			}

			if (_transitioning) return;

			_ = RunTransition(destroyCancellationToken);
		}

		private async Awaitable RunTransition(CancellationToken ct)
		{
			_transitioning = true;

			try
			{
				OnTransitionStarted?.Invoke();

				// Unscaled: a blink is exactly the sort of beat a paused game would otherwise hold open
				// forever.
				await AwaitableUtility.WaitUnscaledAsync(ApplyDelay, ct);

				ApplyRungs();

				// Held shut while the effects ease into place. A change landing in the hold is still behind a
				// closed eye, so it is applied before the eye opens rather than left to a blink of its own.
				if (HoldDuration > 0f)
				{
					await AwaitableUtility.WaitUnscaledAsync(HoldDuration, ct);

					ApplyRungs();
				}
			}
			finally
			{
				_transitioning = false;
			}

			// Whatever arrived while the eye was shut has already landed, but a rate that moved again after
			// ApplyRungs deserves its own beat rather than being lost to the guard above.
			Refresh();
		}

		private bool AnyRungWouldChange()
		{
			var rate = _data.HallucinationRate.Value;
			var rungs = _tiers.Rungs;

			for (var i = 0; i < rungs.Count; i++)
			{
				if (rungs[i] == null) continue;

				if (rate >= rungs[i].Threshold != _active.ContainsKey(i)) return true;
			}

			return false;
		}

		private void ApplyRungs()
		{
			if (!_tiers || !_data) return;

			var rate = _data.HallucinationRate.Value;
			var rungs = _tiers.Rungs;

			for (var i = 0; i < rungs.Count; i++)
			{
				var rung = rungs[i];
				if (rung == null) continue;

				var climbed = rate >= rung.Threshold;
				var running = _active.ContainsKey(i);

				if (climbed == running) continue;

				if (climbed) BeginRung(i, rung);
				else EndRung(i);
			}

			RebuildRunning();
		}

		private void RebuildRunning()
		{
			_running.Clear();

			if (_tiers != null)
			{
				for (var i = 0; i < _tiers.Rungs.Count; i++)
				{
					if (!_active.TryGetValue(i, out var behaviours)) continue;

					foreach (var behaviour in behaviours)
					{
						if (behaviour) _running.Add(behaviour);
					}
				}
			}
		}

		private void BeginRung(int index, PokerHallucinationTiers.Rung rung)
		{
			var pool = rung.Pool;
			if (pool == null || pool.Count == 0) return;

			// Drawn here rather than held on the rung, so two players on the same rung are not looking at
			// the same thing and one player climbing it twice is not either. Shuffled rather than picked one
			// at a time, because a rung asking for two must not be able to hand out the same effect twice —
			// stacking a thing on itself is at best nothing and at worst two callers fighting over one bone.
			_drawOrder.Clear();
			for (var i = 0; i < pool.Count; i++) _drawOrder.Add(i);

			var wanted = Mathf.Min(rung.DrawCount, _drawOrder.Count);

			for (var i = 0; i < wanted; i++)
			{
				var pick = UnityEngine.Random.Range(i, _drawOrder.Count);
				(_drawOrder[i], _drawOrder[pick]) = (_drawOrder[pick], _drawOrder[i]);
			}

			List<PokerHallucinationEffectBehaviour> running = null;

			for (var i = 0; i < wanted; i++)
			{
				var asset = pool[_drawOrder[i]];
				if (!asset) continue;

				// The asset is config and the object is the effect. The same effect is allowed to sit in two
				// pools on purpose, so anything it mutates has to live per rung — an asset holding what it
				// spawned would let the lower rung's End tear down what the higher one believes it owns, and
				// one of the two would silently do nothing. An object per draw makes stacking work by
				// construction, and it names what this player is seeing in the hierarchy, where it can be
				// watched and retuned while it is on screen.
				var behaviour = asset.Run(_effectRoot ? _effectRoot : transform, _player, _pacing);
				if (!behaviour) continue;

				behaviour.name = $"Rung {index} ({rung.Threshold}%) - {asset.name}";

				running ??= new List<PokerHallucinationEffectBehaviour>();
				running.Add(behaviour);
			}

			// A rung whose pool held nothing usable is left unclimbed rather than marked as running with an
			// empty list, or the next Refresh reads it as done and never tries again.
			if (running != null) _active[index] = running;
		}

		private void EndRung(int index)
		{
			if (!_active.TryGetValue(index, out var behaviours)) return;

			_active.Remove(index);

			foreach (var behaviour in behaviours)
			{
				// Stop is what takes the object down, on its own terms: an effect that eases out keeps its
				// host alive for exactly as long as the ease takes.
				if (behaviour) behaviour.Stop();
			}
		}

		private void EndAll()
		{
			foreach (var pair in _active)
			{
				foreach (var behaviour in pair.Value)
				{
					if (behaviour) behaviour.Stop();
				}
			}

			_active.Clear();
			RebuildRunning();
		}
	}
}
