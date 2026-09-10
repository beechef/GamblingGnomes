using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player.Camera;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Camera
{
	// Puts the view where the running beat says it belongs. The stage names a shot, this finds the state
	// that declares that shot, and the state works out what looking at it means — so the round's shape is
	// authored in the sequence and the framing is authored in the prefab, and neither knows the other.
	//
	// Owner-only: a camera state is about what this client is looking at.
	public class PokerStageCameraController : NetworkBehaviour
	{
		[Header("References")]
		[SerializeField] private PlayerCameraController _camera;
		[SerializeField] private PokerPlayerData _data;

		private readonly List<PlayerCameraState> _states = new();

		private int _handle;
		private PlayerCameraState _held;

		private FixedString32Bytes _lastStageId;
		private ulong _lastTurn;
		private bool _lastInMatch;

		public override void OnNetworkSpawn()
		{
			if (!IsOwner) return;

			if (!_camera) _camera = GetComponentInParent<PlayerCameraController>();
			if (!_data) _data = GetComponentInParent<PokerPlayerData>();

			GetComponentsInChildren(true, _states);

			Refresh();
		}

		public override void OnNetworkDespawn()
		{
			if (!IsOwner) return;

			Release();
		}

		// Read rather than subscribed, for the reason PokerCardPickController already carries: the table's
		// data may not have spawned when this does, and a subscription taken then binds to nothing in
		// silence — the symptom being a beat that opens and never closes. Three comparisons a frame, and
		// nothing at all happens while none of them has moved.
		private void Update()
		{
			if (!IsOwner) return;

			var mode = PokerGameMode.Instance;
			var data = mode ? mode.Data : null;

			var stageId = data ? data.StageId.Value : default;
			var turn = data ? data.CurrentTurnClientId.Value : PokerGameData.NoTurn;
			var inMatch = _data && _data.InMatch.Value;

			if (stageId.Equals(_lastStageId) && turn == _lastTurn && inMatch == _lastInMatch) return;

			_lastStageId = stageId;
			_lastTurn = turn;
			_lastInMatch = inMatch;

			Refresh();
		}

		private void Refresh()
		{
			var wanted = Resolve(WantedShot());

			if (wanted == _held) return;

			// Asked for before the old one is let go, so the stack is never empty between two beats — it
			// would fall back to free flight for a frame, and a round where nearly every stage has a shot
			// would flick back to the player's own view at every handover.
			var previous = _handle;

			_handle = wanted && _camera ? _camera.Request(wanted) : 0;
			_held = _handle != 0 ? wanted : null;

			if (previous != 0 && _camera) _camera.Release(previous);
		}

		private PokerCameraShot WantedShot()
		{
			var mode = PokerGameMode.Instance;
			var data = mode ? mode.Data : null;
			if (!mode || !data) return PokerCameraShot.None;

			var stage = mode.FindStage(data.StageId.Value.ToString());
			if (!stage) return PokerCameraShot.None;

			var shot = data.CurrentTurnClientId.Value == NetworkManager.LocalClientId
				? stage.CameraShotOnOwnTurn
				: stage.CameraShot;

			// A player who took a chair after the match began is a viewer: the round does nothing to them,
			// so there is no hand of theirs to look down at and no cap of theirs to be put down. What the
			// *table* is doing they watch like everybody else, which is why only these two are refused.
			var inMatch = _data && _data.InMatch.Value;
			if (!inMatch && (shot == PokerCameraShot.OwnCards || shot == PokerCameraShot.OwnItems))
				return PokerCameraShot.None;

			return shot;
		}

		private PlayerCameraState Resolve(PokerCameraShot shot)
		{
			if (shot == PokerCameraShot.None) return null;

			foreach (var state in _states)
			{
				if (state is IPokerCameraShot declared && declared.Shot == shot) return state;
			}

			return null;
		}

		private void Release()
		{
			if (_handle != 0 && _camera) _camera.Release(_handle);

			_handle = 0;
			_held = null;
		}
	}
}
