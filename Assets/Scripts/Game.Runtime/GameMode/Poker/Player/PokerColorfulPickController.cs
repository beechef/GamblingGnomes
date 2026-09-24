using Game.Runtime.GameMode.Poker.Stages;
using Game.Runtime.UI.CursorVisuals;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Player
{
	// The hand's winner naming who eats the Colorful cap, by pointing at them. A row of name buttons was a
	// second place the same players existed, kept in step by hand, and it asked the winner to read a list
	// at the one moment the whole table is watching faces.
	//
	// Owner-only. The pointing itself is PokerTargetPointer's; this only decides when the winner is being
	// asked and who may be lit, with the very method the server checks the choice with.
	public class PokerColorfulPickController : NetworkBehaviour
	{
		[Header("References")]
		[Tooltip("The player's pointer. Found on the player when left empty.")]
		[SerializeField] private PokerTargetPointer _pointer;

		private PokerColorfulPickStage _stage;
		private PokerTargetQuery _query;
		private bool _pointing;

		private FixedString32Bytes _lastStageId;
		private ulong _lastTurn = ulong.MaxValue;

		public override void OnNetworkSpawn()
		{
			if (!IsOwner) return;

			if (!_pointer)
			{
				var player = GetComponentInParent<PokerPlayer>();
				if (player) _pointer = player.GetComponentInChildren<PokerTargetPointer>(true);
			}

			// Our own body is skipped by the pointer; naming yourself is the name on your own hallucination bar
			// instead (UIPokerColorfulSelfPick).
			_query = new PokerTargetQuery
			{
				AcceptPlayer = player => _stage && _stage.CanBeFed(player),
				Cursor = CursorVisualState.Skull
			};
		}

		public override void OnNetworkDespawn() => StopPointing();

		// The stage is looked up only when one of the two values it depends on has moved, so this costs a
		// comparison a frame. There is no event for "this client became the one choosing" that spans both.
		private void Update()
		{
			if (!IsOwner) return;

			var mode = PokerGameMode.Instance;
			var data = mode ? mode.Data : null;

			var stageId = data ? data.StageId.Value : default;
			var turn = data ? data.CurrentTurnClientId.Value : PokerGameData.NoTurn;

			if (stageId.Equals(_lastStageId) && turn == _lastTurn) return;

			_lastStageId = stageId;
			_lastTurn = turn;
			_stage = ResolvePickStage(mode);

			if (_stage) StartPointing();
			else StopPointing();
		}

		// Only while this client is the one being asked. Everybody else is watching the winner choose, and a
		// cursor that lit people up for them would be a second pointer answering a question nobody put.
		private PokerColorfulPickStage ResolvePickStage(PokerGameMode mode)
		{
			if (!mode || !mode.Data) return null;
			if (mode.Data.CurrentTurnClientId.Value != OwnerClientId) return null;

			return mode.FindStage(mode.Data.StageId.Value.ToString()) as PokerColorfulPickStage;
		}

		private void StartPointing()
		{
			if (_pointing || !_pointer) return;

			_pointing = true;
			_pointer.Begin(_query, HandlePicked);
		}

		private void StopPointing()
		{
			if (!_pointing) return;

			_pointing = false;
			if (_pointer) _pointer.End();
		}

		private void HandlePicked(PokerTarget target)
		{
			if (!target.Player || !target.Player.Data) return;

			var mode = PokerGameMode.Instance;
			if (mode) mode.SubmitActionRPC(PokerActionType.Target, target.Player.Data.SeatIndex.Value);
		}
	}
}
