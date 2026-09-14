using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.GameMode.Poker.Stages;
using Game.Runtime.Player.Camera;
using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Camera
{
	// Which beats of the round its own shot belongs to. This is the only piece here that knows poker
	// exists: a stage carries no opinion about the camera, so the dependency runs one way — the camera
	// side reads the round, and nothing in the round reads back.
	//
	// One of these sits beside each state, so adding a shot is one object carrying two components and the
	// hierarchy is readable as the round itself. Composition rather than a base class, because the state
	// it drives already has one (PlayerCameraLookAtState) and a shot is not a kind of stage watcher.
	public class PokerStageCameraBinder : NetworkBehaviour
	{
		[Header("Shot")]
		[Required]
		[Tooltip("The state this puts up. Normally the one on this same object.")]
		[SerializeField] private PlayerCameraState _state;

		[Tooltip("Where the view goes back to. Empty finds it up the hierarchy.")]
		[SerializeField] private PlayerCameraController _camera;

		[Header("Beats")]
		[Tooltip("The stages this shot is up for. Picked rather than named, so a stage that is renamed or replaced cannot leave a shot pointing at nothing.")]
		[SerializeField] private List<PokerStage> _stages = new();

		[Header("Rules")]
		[Tooltip("On, this shot is only for somebody with a place in the running match. A player who took a chair mid match has no row of cards and no cap of their own, so a shot about their own seat would park their view on an empty patch of table. Off for anything about the table or about somebody else, which a viewer watches like everybody else.")]
		[SerializeField] private bool _requiresPlaceInMatch;

		[Header("References")]
		[SerializeField] private PokerPlayerData _data;

		private readonly List<FixedString32Bytes> _ids = new();

		private int _handle;
		private FixedString32Bytes _lastStageId;
		private bool _lastEligible;

		public override void OnNetworkSpawn()
		{
			if (!IsOwner) return;

			if (!_state) _state = GetComponent<PlayerCameraState>();
			if (!_camera) _camera = GetComponentInParent<PlayerCameraController>();
			if (!_data) _data = GetComponentInParent<PokerPlayerData>();

			// Resolved once: a stage id is a string on an asset and comparing it raw every frame would mean
			// a ToString and its allocation on the per-frame path.
			_ids.Clear();
			foreach (var stage in _stages)
				if (stage) _ids.Add(new FixedString32Bytes(stage.StageId));

			Refresh();
		}

		public override void OnNetworkDespawn()
		{
			if (!IsOwner) return;

			Release();
		}

		// Read rather than subscribed, and for the reason PokerCardPickController already carries: the
		// table's data may not have spawned when this does, and a subscription taken then binds to nothing
		// in silence — the symptom being a beat that opens and never closes. Two comparisons a frame, and
		// nothing at all happens while neither has moved.
		private void Update()
		{
			if (!IsOwner) return;

			var mode = PokerGameMode.Instance;
			var stageId = mode && mode.Data ? mode.Data.StageId.Value : default;
			var eligible = !_requiresPlaceInMatch || (_data && _data.InMatch.Value);

			if (stageId.Equals(_lastStageId) && eligible == _lastEligible) return;

			_lastStageId = stageId;
			_lastEligible = eligible;

			Refresh();
		}

		private void Refresh()
		{
			if (Wanted()) Hold();
			else Release();
		}

		private bool Wanted()
		{
			if (!_state || !_camera) return false;
			if (_requiresPlaceInMatch && (!_data || !_data.InMatch.Value)) return false;

			var mode = PokerGameMode.Instance;
			if (!mode || !mode.Data) return false;

			var running = mode.Data.StageId.Value;

			foreach (var id in _ids)
				if (id.Equals(running)) return true;

			return false;
		}

		private void Hold()
		{
			if (_handle != 0) return;

			_handle = _camera.Request(_state);
		}

		private void Release()
		{
			if (_handle == 0) return;

			if (_camera) _camera.Release(_handle);
			_handle = 0;
		}
	}
}
