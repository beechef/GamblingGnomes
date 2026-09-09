using Game.Runtime.GameMode.Poker.Hallucination;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	// Where the round's consequence actually happens. Every cap the settlement left standing in front of
	// somebody is swallowed here, one player at a time and one cap at a time, so the table watches what
	// the hand cost each of them rather than reading it off a number that changed while the ranking board
	// was up.
	//
	// One at a time on purpose. The effects are what the whole round is played for, and a beat where four
	// players' worth of them land in the same frame is a beat nobody can follow — so this is a queue with
	// a bite duration, and the next hand does not begin until the last plate is clear.
	[CreateAssetMenu(fileName = "PokerStage_ItemConsume", menuName = "Game/Poker/Stages/Item Consume")]
	public class PokerItemConsumeStage : PokerStage
	{
		[Header("Timing")]
		[Tooltip("Seconds one cap takes to go down — the length of the gesture itself.")]
		[MinValue(0.1f)]
		[SerializeField] private float _biteDuration = 1.2f;

		[Tooltip("Seconds of quiet between one cap going down and the same player starting the next, so a plate of three reads as three mouthfuls rather than one long one.")]
		[MinValue(0f)]
		[SerializeField] private float _gapBetweenBites;

		[Tooltip("Added to that gap when the cap just eaten pushed its eater across a hallucination rung. The screen spends the controller's own transition blinking, and a bite landing inside that blink is a bite nobody saw — so the wait is however long the blink is, read off the player rather than typed here.")]
		[SerializeField] private bool _waitForHallucinationTransition = true;

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

			// Nobody was handed anything — everyone folded out, or the settlement had nothing to give.
			if (!AdvanceToNextEater()) FinishEating();
		}

		protected override void OnTickStage(float deltaTime)
		{
			_timer -= deltaTime;
			if (_timer > 0f) return;

			if (_waitingToHandOver)
			{
				_waitingToHandOver = false;
				if (!AdvanceToNextEater()) FinishEating();
				return;
			}

			var eater = CurrentEater();

			// They left, or went under mid-plate. Whatever is left on it goes with them: eating is a thing
			// a player does, not a debt the table collects.
			if (!eater || !TakeOneBite(eater, out var extra))
			{
				_waitingToHandOver = true;
				_timer = _handoverDuration;
				return;
			}

			_timer = _biteDuration + _gapBetweenBites + extra;
		}

		// The table is cleared on the way out. Anything still standing belongs to somebody who left or went
		// under mid-plate — eating is a thing a player does, not a debt the table collects — and the pot is
		// carried into the next round rather than wiped by the deal, so a cap nobody is going to swallow
		// would sit there gathering the next hand's stakes around it.
		private void FinishEating()
		{
			PokerTableUtility.ResetPot(Data);
			FinishStage(_nextStage);
		}

		// Seat order, so the eating goes round the table the way everything else does rather than in
		// whatever order the settlement happened to serve.
		private bool AdvanceToNextEater()
		{
			var seatCount = Mathf.Max(1, Data.ActiveSeatCount.Value);

			for (var step = _seatIndex + 1; step < seatCount; step++)
			{
				var player = FindSeatedPlayerAtSeat(step);
				if (!player || PokerTableUtility.CountPotItems(Data, player.ClientId) == 0) continue;

				_seatIndex = step;
				_timer = 0f;
				return true;
			}

			return false;
		}

		private PokerPlayer CurrentEater() => FindSeatedPlayerAtSeat(_seatIndex);

		// True while there was something left to swallow. The cap comes off the table as it goes down
		// rather than after, because the ledger is what the visual draws.
		private bool TakeOneBite(PokerPlayer eater, out float extraWait)
		{
			extraWait = 0f;

			if (!PokerTableUtility.ServerTakePotItem(Data, eater.ClientId, out var itemType)) return false;

			eater.ActionAnimator?.ServerPlay(PlayerActionIds.ConsumeItem);

			// Read either side of the effect rather than predicted from it: what a cap costs depends on
			// whether this eater has met that kind before, so only the rate itself can say where they
			// landed.
			var before = eater.Data ? eater.Data.HallucinationRate.Value : 0;

			var database = GameMode.ItemDatabase;
			if (database && database.TryGetEntry(itemType, out var entry) && entry.Effect)
			{
				entry.Effect.ConsumeServer(GameMode, eater, itemType);
			}

			var after = eater.Data ? eater.Data.HallucinationRate.Value : before;

			extraWait = WaitForTransition(eater, before, after);

			return true;
		}

		// The blink the eater's own screen is about to spend changing rooms. Their controller owns both the
		// ladder and how long the blink takes, so it is asked rather than a second copy of either being
		// kept here — and a bite that changed nothing waits for nothing.
		private float WaitForTransition(PokerPlayer eater, int before, int after)
		{
			if (!_waitForHallucinationTransition || before == after) return 0f;

			var hallucination = eater.GetComponentInChildren<PokerHallucinationController>(true);
			if (!hallucination || !hallucination.CrossesRung(before, after)) return 0f;

			return hallucination.TransitionDuration;
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
