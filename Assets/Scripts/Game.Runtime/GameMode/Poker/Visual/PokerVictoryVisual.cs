using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player.Camera;
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

			Apply(PokerVictoryShot.None);
		}

		private void HandleShotChanged(PokerVictoryShot previous, PokerVictoryShot current) => Refresh();

		private void HandleResettingChanged(bool previous, bool current) => Refresh();

		private void HandleSurvivorChanged(ulong previous, ulong current) => PlaceSurvivor(_shown);

		// The cut waits behind the blink: a shot named while the eye is closing goes up only as it opens, so
		// nobody sees the survivor lifted onto the table. Ending is written behind the shut eye, so it lands at once.
		private void Refresh()
		{
			var shot = Data.VictoryShot.Value;
			if (shot != PokerVictoryShot.None && Data.MatchResetting.Value && _shown == PokerVictoryShot.None) return;
			if (shot == _shown) return;

			Apply(shot);
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
