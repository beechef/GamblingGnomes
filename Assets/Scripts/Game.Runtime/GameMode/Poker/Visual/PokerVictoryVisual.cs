using System;
using System.Threading;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.GameMode.Poker.Stages;
using Game.Runtime.Player.Camera;
using Game.Runtime.Utility;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// The match's survivor on show: lifted onto the table where everyone can see them, turned to face the
	// chair they left, and every screen cut to the same two shots from that side — wide while they dance,
	// then face to face while the table names them. The empty chair is the one direction no other player
	// stands in the way. Read off the replicated victory shot, so every screen, the survivor's own included,
	// watches the same thing, and a late joiner lands in whichever shot is up.
	//
	// The shots are spectator cameras, so while one is live every player's whole body is drawn.
	public class PokerVictoryVisual : PokerVisual
	{
		[Header("Stage")]
		[Tooltip("Where the survivor stands. Its yaw is turned to face the survivor's own chair; both cameras are its children, so they stand on that side too.")]
		[Required]
		[SerializeField] private Transform _stage;

		[Header("Shots")]
		[Tooltip("The whole survivor dancing on the table, from the side of their chair.")]
		[FormerlySerializedAs("_orbitCamera")]
		[Required]
		[SerializeField] private PlayerSpectatorCamera _wideCamera;

		[Tooltip("Face to face with the survivor while the table names them.")]
		[Required]
		[SerializeField] private PlayerSpectatorCamera _closeUpCamera;

		private PokerVictoryShot _shown;
		private PokerWinnerPoseController _onStage;
		private bool _wideHeld;
		private bool _closeUpHeld;

		// When this screen's own eye finished closing for the match-over blink, unscaled.
		private float _shutAt;
		private CancellationTokenSource _pending;

		private void OnDestroy() => CancelPending();

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

		private void HandleShotChanged(PokerVictoryShot previous, PokerVictoryShot current) => Refresh();

		private void HandleResettingChanged(bool previous, bool current)
		{
			if (current) _shutAt = Time.unscaledTime + CloseDuration;
		}

		private float CloseDuration
		{
			get
			{
				var stage = GameMode.FindStage(Data.StageId.Value.ToString()) as PokerMatchOverStage;
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

			SetHeld(ref _wideHeld, _wideCamera, shot == PokerVictoryShot.Wide);
			SetHeld(ref _closeUpHeld, _closeUpCamera, shot == PokerVictoryShot.CloseUp);
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
