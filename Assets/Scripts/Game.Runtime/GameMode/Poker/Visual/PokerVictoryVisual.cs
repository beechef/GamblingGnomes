using System;
using System.Threading;
using Game.Runtime.Audio;
using Game.Runtime.Player;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.GameMode.Poker.Stages;
using Game.Runtime.Player.Camera;
using Game.Runtime.Utility;
using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// The match's survivor on show: lifted onto the table where everyone can see them, turned to face the
	// chair they left, and every screen cut to the same camera from that side: it comes round from behind
	// them and closes in on their face while they dance, then holds there while the table names them. The
	// empty chair is the one direction no other player stands in the way. Read off the replicated victory
	// shot, so every screen, the survivor's own included, watches the same thing, and a late joiner lands in
	// whichever part is up.
	//
	// The camera is a spectator camera, so while it is live every player's whole body is drawn.
	public class PokerVictoryVisual : PokerVisual
	{
		[Header("Stage")]
		[Tooltip("Where the survivor stands. Its yaw is turned to face the survivor's own chair; the camera is its child, so it stands on that side too.")]
		[Required]
		[SerializeField] private Transform _stage;

		[Header("Shot")]
		[Tooltip("Comes round from behind the survivor along its spline and closes in on their face on the side of their chair. The spline spirals in, so the move is the zoom.")]
		[FormerlySerializedAs("_orbitCamera")]
		[Required]
		[SerializeField] private PlayerSpectatorCamera _wideCamera;

		[Tooltip("Carries the camera along its spline, from behind the survivor to close in front of them. Restarted from the beginning every time the shot goes up.")]
		[Required]
		[SerializeField] private CinemachineSplineDolly _wideDolly;

		[Tooltip("Seconds the camera takes coming round from behind to close in front, where it then holds. Starts with the dance, on PokerMatchOverStage.PerformanceDelay.")]
		[MinValue(0.01f)]
		[SerializeField] private float _orbitDuration = 3f;

		[Tooltip("Played as the performance starts, with the camera's move and the dance.")]
		[SerializeField] private AudioEvent _performanceSound;

		private PokerVictoryShot _shown;
		private PokerWinnerPoseController _onStage;
		private bool _wideHeld;
		private bool _tagsHidden;
		private CancellationTokenSource _performance;

		// When this screen's own eye finished closing for the match-over blink, unscaled.
		private float _shutAt;
		private CancellationTokenSource _pending;

		private void OnDestroy()
		{
			CancelPending();
			CancelPerformance();
		}

		protected override void OnBind()
		{
			Data.VictoryShot.OnValueChanged += HandleShotChanged;
			Data.SurvivorClientId.OnValueChanged += HandleSurvivorChanged;
			Data.MatchResetting.OnValueChanged += HandleResettingChanged;

			Refresh();
		}

		protected override void OnUnbind()
		{
			Data.MatchResetting.OnValueChanged -= HandleResettingChanged;
			Data.SurvivorClientId.OnValueChanged -= HandleSurvivorChanged;
			Data.VictoryShot.OnValueChanged -= HandleShotChanged;

			CancelPending();
			Apply(PokerVictoryShot.None);
		}

		private PokerMatchOverStage RunningStage => GameMode.FindStage(Data.StageId.Value.ToString()) as PokerMatchOverStage;

		private void HandleShotChanged(PokerVictoryShot previous, PokerVictoryShot current) => Refresh();

		private void HandleResettingChanged(bool previous, bool current)
		{
			if (current) _shutAt = Time.unscaledTime + CloseDuration;
		}

		private float CloseDuration
		{
			get
			{
				var stage = RunningStage;
				return stage && stage.Blink ? stage.Blink.CloseDuration : 0f;
			}
		}

		private void HandleSurvivorChanged(ulong previous, ulong current) => PlaceSurvivor(_shown);

		// The server names the shot once its own eye is shut, and every screen's blink is driven by the same
		// replicated flag, so the shot normally arrives behind this screen's shut eye too. A screen whose packets
		// bunched up can hear both together, while its eye is still closing: the cut then waits out the rest of
		// that close, so nobody ever sees the survivor lifted onto the table. Ending lands at once, behind the
		// eye the reset is written under.
		private void Refresh()
		{
			CancelPending();

			var shot = Data.VictoryShot.Value;
			if (shot == _shown) return;

			var wait = _shutAt - Time.unscaledTime;
			if (shot != PokerVictoryShot.None && _shown == PokerVictoryShot.None && Data.MatchResetting.Value && wait > 0f)
			{
				_pending = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
				_ = ApplyAfter(wait, _pending.Token);
				return;
			}

			Apply(shot);
		}

		private async Awaitable ApplyAfter(float seconds, CancellationToken ct)
		{
			try
			{
				await AwaitableUtility.WaitUnscaledAsync(seconds, ct);
			}
			catch (OperationCanceledException)
			{
				return;
			}

			if (IsBound) Apply(Data.VictoryShot.Value);
		}

		private void CancelPending()
		{
			if (_pending == null) return;

			_pending.Cancel();
			_pending.Dispose();
			_pending = null;
		}

		private void Apply(PokerVictoryShot shot)
		{
			_shown = shot;

			PlaceSurvivor(shot);

			if (shot == PokerVictoryShot.Wide && !_wideHeld) BeginPerformance();
			if (shot == PokerVictoryShot.CloseUp && !_wideHeld) HoldOrbitAtEnd();
			if (shot == PokerVictoryShot.None) CancelPerformance();

			SetHeld(ref _wideHeld, _wideCamera, shot != PokerVictoryShot.None);
			SetTagsHidden(shot != PokerVictoryShot.None);
		}

		// The camera waits at the start of its path and goes on the same number the server starts the dance on,
		// counted from this screen's own shut eye, so the move, the dance and the sound begin together. A screen
		// that joined late, with no close of its own to count from, starts at once.
		private void BeginPerformance()
		{
			CancelPerformance();
			HoldOrbitAtStart();

			var stage = RunningStage;
			var delay = (stage ? stage.PerformanceDelay : 0f) - Mathf.Max(0f, Time.unscaledTime - _shutAt);
			if (_shutAt <= 0f || delay <= 0f)
			{
				StartPerformance();
				return;
			}

			_performance = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
			_ = StartPerformanceAfter(delay, _performance.Token);
		}

		private async Awaitable StartPerformanceAfter(float seconds, CancellationToken ct)
		{
			try
			{
				await AwaitableUtility.WaitUnscaledAsync(seconds, ct);
			}
			catch (OperationCanceledException)
			{
				return;
			}

			if (IsBound && _shown == PokerVictoryShot.Wide) StartPerformance();
		}

		private void StartPerformance()
		{
			if (_wideDolly && _wideDolly.AutomaticDolly.Method is SplineAutoDolly.FixedSpeed speed) speed.Speed = 1f / _orbitDuration;
			if (_performanceSound && AudioManager.Instance) AudioManager.Instance.PlayOneShot(_performanceSound, _stage.position);
		}

		private void CancelPerformance()
		{
			if (_performance == null) return;

			_performance.Cancel();
			_performance.Dispose();
			_performance = null;
		}

		private void SetTagsHidden(bool hidden)
		{
			if (_tagsHidden == hidden) return;

			_tagsHidden = hidden;
			if (hidden) PlayerNameTagVisual.RequestAllHidden();
			else PlayerNameTagVisual.ReleaseAllHidden();
		}

		private void PlaceSurvivor(PokerVictoryShot shot)
		{
			var survivor = shot == PokerVictoryShot.None ? null : PokerPlayer.Find(Data.SurvivorClientId.Value);
			var pose = survivor ? survivor.WinnerPose : null;

			if (_onStage && _onStage != pose) _onStage.LeaveStage();

			_onStage = pose;
			if (!_onStage) return;

			FaceChair(survivor.transform.position);
			_onStage.PlaceOnStage(_stage);
		}

		// Cinemachine's own auto-dolly moves it at a fixed speed along the spline (positions authored Normalized,
		// so 1 is the whole path) and stops at the end of an open spline: the move ends in front of the survivor
		// and holds there. Held still at the start until the performance begins.
		private void HoldOrbitAtStart()
		{
			if (!_wideDolly) return;

			_wideDolly.CameraPosition = 0f;
			if (_wideDolly.AutomaticDolly.Method is SplineAutoDolly.FixedSpeed speed) speed.Speed = 0f;
		}

		// A screen that joins during the announcement lands where the move ends, close in front of the survivor.
		private void HoldOrbitAtEnd()
		{
			if (!_wideDolly) return;

			_wideDolly.CameraPosition = 1f;
			if (_wideDolly.AutomaticDolly.Method is SplineAutoDolly.FixedSpeed speed) speed.Speed = 0f;
		}

		// The player root stays in the chair while the body is on the table, so it is where the chair is.
		private void FaceChair(Vector3 chair)
		{
			var toChair = chair - _stage.position;
			toChair.y = 0f;
			if (toChair.sqrMagnitude > 0.0001f) _stage.rotation = Quaternion.LookRotation(toChair, Vector3.up);
		}

		private static void SetHeld(ref bool held, PlayerSpectatorCamera camera, bool hold)
		{
			if (!camera || held == hold) return;

			held = hold;
			if (hold) camera.Hold();
			else camera.Release();
		}
	}
}
