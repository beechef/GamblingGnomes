using System;
using System.Collections.Generic;
using DG.Tweening;
using Game.Runtime.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Player
{
	// A cap in this player's hand, for as long as an animation is holding it. Two acts need that and they
	// differ only in how they end — a bet puts the cap down on the table, a mouthful swallows it — so a
	// carry holds its own pair of cues and its own ending rather than this branching on which act it is.
	//
	// It lives on the player because everything it knows is the player's — which rig is drawn, where a fist
	// closes, where the arm is aimed, and which frame of the clip the hand arrives on. The table owns the
	// ledger and the spot, and hands a cap over.
	//
	// When the hand arrives is the clip's own business, raised as an animation cue rather than counted in
	// seconds here. A second clip therefore needs no number in this file: it carries its own frames.
	public class PokerBetItemCarryController : MonoBehaviour
	{
		[SerializeField] private PlayerRigController _rig;
		[SerializeField] private PlayerHandIkController _handIk;

		[Header("Bet")]
		[Tooltip("Cue raised when the bet gesture's hand reaches the table for the cap.")]
		[SerializeField] private string _betGrabCue = "BetGrab";

		[Tooltip("Cue raised when the bet gesture puts the cap down.")]
		[SerializeField] private string _betReleaseCue = "BetRelease";

		[Tooltip("Seconds the cap takes from the hand to its spot on the table once the gesture lets go.")]
		[Min(0f)]
		[SerializeField] private float _placeDuration = 0.25f;

		[SerializeField] private Ease _placeEase = Ease.OutQuad;

		[Header("Eating")]
		[Tooltip("Cue raised when the eating gesture's hand picks the cap up off the table.")]
		[SerializeField] private string _eatGrabCue = "EatGrab";

		[Tooltip("Cue raised at the frame the cap goes into the mouth and is gone.")]
		[SerializeField] private string _eatSwallowCue = "EatSwallow";

		[Header("Safety")]
		[Tooltip("How long a cap waits for a cue that never comes before it ends anyway. A clip missing its cues must not leave a cap invisible for the rest of the hand.")]
		[Min(0f)]
		[SerializeField] private float _cueTimeout = 3f;

		[Tooltip("How recently a grab cue may have fired and still take a cap handed over after it. The gesture and the ledger change that hands the cap over replicate separately and arrive in either order, and a grab a couple of frames into its clip is easily passed before the cap turns up.")]
		[Min(0f)]
		[SerializeField] private float _lateCueWindow = 0.5f;

		private readonly List<PlayerAnimationEventRelay> _relays = new();
		private readonly List<Carried> _carrying = new();
		private readonly Dictionary<string, float> _lastCueTimes = new();

		private class Carried
		{
			public GameObject Cap;
			public Transform ReturnParent;
			public Vector3 Resting;
			public Vector3 WorldScale;
			public Vector3 RestingScale;
			public string GrabCue;
			public string EndCue;

			// What happens at the end cue. Null puts the cap back on the table; anything else is handed the
			// cap and owns it from there — the pot visual destroys the one that was eaten.
			public Action<GameObject> OnEnd;

			public bool Held;
			public Tween Timeout;
		}

		private void Awake()
		{
			if (!_rig) _rig = GetComponentInParent<PlayerRigController>(true);
			if (!_handIk) _handIk = _rig ? _rig.GetComponentInChildren<PlayerHandIkController>(true) : null;
		}

		// Both rigs, each gating itself on being drawn, rather than the one RenderedRig answers with today:
		// ownership is not settled when a spawned prefab wakes, and a relay picked once here would be the
		// wrong rig for the rest of the session on whichever machine lost that race.
		private void OnEnable()
		{
			if (_rig) _rig.GetComponentsInChildren(true, _relays);

			foreach (var relay in _relays) relay.OnAnimationCue += HandleCue;
		}

		private void OnDisable()
		{
			foreach (var relay in _relays) relay.OnAnimationCue -= HandleCue;

			_relays.Clear();

			for (var i = _carrying.Count - 1; i >= 0; i--) End(_carrying[i]);

			_carrying.Clear();
		}

		// Staked: taken off the table, hidden where it lay until the gesture's hand comes down for it, and
		// put back down on the spot the table picked.
		public void Carry(GameObject cap, Transform returnParent, Vector3 resting, Transform placementAnchor)
		{
			if (!cap) return;

			if (_handIk && placementAnchor) _handIk.Aim(placementAnchor);

			Begin(cap, returnParent, resting, _betGrabCue, _betReleaseCue, null, true);
		}

		// Eaten: the same hold, ending at the mouth. The caller is handed the cap back rather than this
		// destroying it, because what a swallowed cap costs — coming off the registry, off the ledger — is
		// the business of whoever was drawing it. It stays visible where it lies until the hand takes it,
		// since a cap already on the table has no reason to vanish first.
		public void Consume(GameObject cap, Action<GameObject> onSwallowed)
		{
			if (!cap) return;

			Begin(cap, cap.transform.parent, cap.transform.localPosition, _eatGrabCue, _eatSwallowCue, onSwallowed, false);
		}

		private void Begin(GameObject cap, Transform returnParent, Vector3 resting, string grabCue, string endCue, Action<GameObject> onEnd, bool hideUntilGrabbed)
		{
			var carried = new Carried
			{
				Cap = cap,
				ReturnParent = returnParent,
				Resting = resting,
				WorldScale = cap.transform.lossyScale,
				RestingScale = cap.transform.localScale,
				GrabCue = grabCue,
				EndCue = endCue,
				OnEnd = onEnd
			};

			carried.Timeout = DOVirtual.DelayedCall(_cueTimeout, () => Release(cap), false);

			_carrying.Add(carried);

			// The hand already closed on it: the gesture arrived first and this cap caught up afterwards.
			if (WasCueJustRaised(grabCue))
			{
				Grab(carried);
				return;
			}

			if (hideUntilGrabbed) cap.SetActive(false);
		}

		// A cap on its way is one the table must leave alone, and one a clear has to be able to take back.
		public bool IsCarrying(GameObject cap)
		{
			foreach (var carried in _carrying)
			{
				if (carried.Cap == cap) return true;
			}

			return false;
		}

		public void Release(GameObject cap)
		{
			for (var i = _carrying.Count - 1; i >= 0; i--)
			{
				if (_carrying[i].Cap != cap) continue;

				End(_carrying[i]);
				_carrying.RemoveAt(i);
				return;
			}
		}

		private bool WasCueJustRaised(string cue) =>
			!string.IsNullOrEmpty(cue) && _lastCueTimes.TryGetValue(cue, out var time) && Time.time - time <= _lateCueWindow;

		private void HandleCue(string cue)
		{
			if (!string.IsNullOrEmpty(cue)) _lastCueTimes[cue] = Time.time;

			// A cap destroyed under us — the pot cleared, or one taken mid-gesture — is dropped here rather
			// than left for its timeout, so nothing is ever put back onto a corpse.
			for (var i = _carrying.Count - 1; i >= 0; i--)
			{
				if (_carrying[i].Cap) continue;

				KillTimeout(_carrying[i]);
				_carrying.RemoveAt(i);
			}

			// Each carry answers to its own pair, so a mouthful and a bet can be in flight at once without
			// either one hearing the other's cue.
			for (var i = _carrying.Count - 1; i >= 0; i--)
			{
				var carried = _carrying[i];

				if (!carried.Held && cue == carried.GrabCue)
				{
					Grab(carried);
					continue;
				}

				if (cue != carried.EndCue) continue;

				Finish(carried);
				_carrying.RemoveAt(i);
			}
		}

		// Held where the rig says a hand holds something. The cap hangs on the hold point at zero: CapHold
		// is the pose, so retuning the grip is a drag in the scene view and never a number in this file.
		private void Grab(Carried carried)
		{
			if (carried.Held || !carried.Cap) return;

			var holder = HoldPoint();
			if (!holder) return;

			carried.Held = true;
			carried.Cap.SetActive(true);
			carried.Cap.transform.SetParent(holder, false);
			carried.Cap.transform.localPosition = Vector3.zero;
			carried.Cap.transform.localRotation = Quaternion.identity;

			// Kept at the size it has on the table, whatever scale the chain down to the hold point carries.
			var scale = holder.lossyScale;
			carried.Cap.transform.localScale = new Vector3(carried.WorldScale.x / scale.x, carried.WorldScale.y / scale.y, carried.WorldScale.z / scale.z);
		}

		// The end cue arrived: a mouthful is handed to whoever owns it, a bet slides to its spot.
		private void Finish(Carried carried)
		{
			KillTimeout(carried);

			if (!carried.Cap) return;

			if (carried.OnEnd != null)
			{
				carried.OnEnd(carried.Cap);
				return;
			}

			carried.Cap.SetActive(true);
			carried.Cap.transform.SetParent(carried.ReturnParent, true);

			if (_placeDuration <= 0f)
			{
				Land(carried);
				return;
			}

			carried.Cap.transform.DOLocalMove(carried.Resting, _placeDuration).SetEase(_placeEase);
			carried.Cap.transform.DOLocalRotateQuaternion(Quaternion.identity, _placeDuration);
			carried.Cap.transform.DOScale(carried.RestingScale, _placeDuration);
		}

		// No cue came, or the carry was cancelled. A bet goes back on the table; a mouthful is still owed
		// to whoever handed it over, so it ends the way its cue would have ended it rather than being left
		// in the fist.
		private void End(Carried carried)
		{
			KillTimeout(carried);

			if (!carried.Cap) return;

			if (carried.OnEnd != null)
			{
				carried.OnEnd(carried.Cap);
				return;
			}

			Land(carried);
		}

		private static void KillTimeout(Carried carried)
		{
			var timeout = carried.Timeout;
			carried.Timeout = null;
			timeout?.Kill();
		}

		// Wherever it had got to, it is back on the table's books at its spot.
		private void Land(Carried carried)
		{
			if (!carried.Cap) return;

			carried.Cap.transform.DOKill();
			carried.Cap.SetActive(true);
			carried.Cap.transform.SetParent(carried.ReturnParent, false);
			carried.Cap.transform.localPosition = carried.Resting;
			carried.Cap.transform.localRotation = Quaternion.identity;
			carried.Cap.transform.localScale = carried.RestingScale;
		}

		// Where a held cap sits is a point authored in the hand on both rigs, handed over by the rig by what
		// it is rather than found by name. A rig without one is a setup that cannot look right, so it says so
		// and holds the cap at the wrist rather than not at all.
		private Transform HoldPoint()
		{
			if (!_rig) return null;

			if (_rig.TryGetBone(PlayerBone.HoldRight, out var hold)) return hold;

			Debug.LogWarning($"[{nameof(PokerBetItemCarryController)}] {_rig.RenderedRig} has no {nameof(PlayerBone.HoldRight)} bound; the cap is held at the wrist.", this);

			return _rig.GetBone(PlayerBone.HandRight);
		}
	}
}
