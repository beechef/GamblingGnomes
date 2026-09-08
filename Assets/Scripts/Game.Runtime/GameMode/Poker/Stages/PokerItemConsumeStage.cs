using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	// Where the round's consequence actually happens. Everything the settlement served is swallowed here,
	// one player at a time and one cap at a time, so the table watches what the hand cost each of them
	// rather than reading it off a number that changed while the ranking board was up.
	//
	// One at a time on purpose. The effects are what the whole round is played for, and a beat where four
	// players' worth of them land in the same frame is a beat nobody can follow — so this is a queue with
	// a bite duration, and the next hand does not begin until the last plate is clear.
	[CreateAssetMenu(fileName = "PokerStage_ItemConsume", menuName = "Game/Poker/Stages/Item Consume")]
	public class PokerItemConsumeStage : PokerStage
	{
		[Header("Timing")]
		[Tooltip("Seconds one cap takes to go down — the gesture's length, and the gap before the next one.")]
		[MinValue(0.1f)]
		[SerializeField] private float _biteDuration = 1.2f;

		[Tooltip("Seconds between one player finishing their plate and the next starting theirs, so two players eating do not read as one.")]
		[MinValue(0f)]
		[SerializeField] private float _handoverDuration = 0.4f;

		[Header("References")]
		[Tooltip("Where the next hand begins. Named rather than left to the sequence, which wraps to its first entry — and that is the waiting room.")]
		[Required]
		[SerializeField] private PokerStage _nextStage;

		private float _timer;
		private int _seatIndex;
		private bool _waitingToHandOver;

		protected override void OnStartStage()
		{
			Data.Phase.Value = PokerPhase.Eating;
			GameMode.ClearTurn();

			_seatIndex = -1;
			_timer = 0f;
			_waitingToHandOver = false;

			// Nobody was served anything — everyone folded out, or the settlement had nothing to give.
			if (!AdvanceToNextEater()) FinishStage(_nextStage);
		}

		protected override void OnTickStage(float deltaTime)
		{
			_timer -= deltaTime;
			if (_timer > 0f) return;

			if (_waitingToHandOver)
			{
				_waitingToHandOver = false;
				if (!AdvanceToNextEater()) FinishStage(_nextStage);
				return;
			}

			var eater = CurrentEater();

			// They left, or went under mid-plate. Whatever is left on it goes with them: eating is a thing
			// a player does, not a debt the table collects.
			if (!eater || !TakeOneBite(eater))
			{
				_waitingToHandOver = true;
				_timer = _handoverDuration;
				return;
			}

			_timer = _biteDuration;
		}

		// Seat order, so the eating goes round the table the way everything else does rather than in
		// whatever order the settlement happened to serve.
		private bool AdvanceToNextEater()
		{
			var seatCount = Mathf.Max(1, Data.ActiveSeatCount.Value);

			for (var step = _seatIndex + 1; step < seatCount; step++)
			{
				var player = FindSeatedPlayerAtSeat(step);
				if (!player || !player.Items || player.Items.PendingCount == 0) continue;

				_seatIndex = step;
				_timer = 0f;
				return true;
			}

			return false;
		}

		private PokerPlayer CurrentEater() => FindSeatedPlayerAtSeat(_seatIndex);

		// True while there was something left to swallow. The cap comes off the plate as it goes down
		// rather than after, because the plate is what the visual draws.
		private bool TakeOneBite(PokerPlayer eater)
		{
			if (!eater.Items || !eater.Items.ServerTakeNext(out var itemType)) return false;

			eater.ActionAnimator?.ServerPlay(PlayerActionIds.ConsumeItem);

			var database = GameMode.ItemDatabase;
			if (database && database.TryGetEntry(itemType, out var entry) && entry.Effect)
			{
				entry.Effect.ConsumeServer(GameMode, eater, itemType);
			}

			return true;
		}

		private PokerPlayer FindSeatedPlayerAtSeat(int seatIndex)
		{
			if (seatIndex < 0) return null;

			foreach (var player in GameMode.SeatedPlayers)
			{
				if (player && player.Data && player.Data.SeatIndex.Value == seatIndex) return player;
			}

			return null;
		}
	}
}
