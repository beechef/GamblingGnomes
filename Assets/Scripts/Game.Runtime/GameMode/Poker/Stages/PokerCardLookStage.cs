using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	// Everybody turns their own cards over at once. Nobody holds a turn: the table is waiting on all of
	// them rather than on one of them, so there is no order to take and no seat to pass — the stage is
	// over when the last player has spent their looks.
	//
	// It is its own step rather than something bolted onto the street it follows, because the question is
	// a different one: the street asks which cap, this asks which cards, and folding those two together
	// would leave a turn that ends on two unrelated answers.
	[CreateAssetMenu(fileName = "PokerStage_CardLook", menuName = "Game/Poker/Stages/Card Look")]
	public class PokerCardLookStage : PokerStage
	{
		[Header("Timing")]
		[Tooltip("Seconds everybody has to choose. Zero or less runs no clock at all: the table simply waits until the last card is turned.")]
		[SerializeField] private float _duration = -1f;

		[Tooltip("Seconds the turned cards are left on the table once everybody is done, before the next step.")]
		[MinValue(0f)]
		[SerializeField] private float _settleDuration = 1f;

		[Header("Choice")]
		[Tooltip("On, every card a player may see goes into their hand as the stage opens, for a round where there is nothing to choose. The settle duration then has to cover the pick-up flight.")]
		[SerializeField] private bool _putEveryCardInHand;

		[Tooltip("With Put Every Card In Hand: seconds the dealt cards lie on the table before everybody picks them up, so the pick-up reads as its own beat.")]
		[MinValue(0f)]
		[SerializeField] private float _pickUpDelay;

		private bool _settling;
		private float _pickUpTimer;

		// Whether anybody is asked which cards to turn. A round that picks them up for everyone offers nothing
		// to point at, so no screen opens the table for picking.
		public bool PlayersChoose => !_putEveryCardInHand;

		protected override void OnStartStage()
		{
			Data.Phase.Value = PokerPhase.Looking;
			GameMode.ClearTurn();

			_settling = false;
			_pickUpTimer = _putEveryCardInHand ? _pickUpDelay : 0f;

			if (_putEveryCardInHand && _pickUpTimer <= 0f) PickUpEveryCard();

			// Nobody was dealt a hand they may look at — a table configured without a look limit, or one
			// where everybody has already folded out. There is nothing to wait for.
			if (EveryoneDone())
			{
				Settle();
				return;
			}

			if (_duration > 0f) GameMode.BeginStageTimer(_duration);
		}

		protected override void OnEndStage() => _settling = false;

		// Genuinely a poll, and deliberately: what is being waited on is a value on every seated player's
		// own object rather than one event on the table, and subscribing to all of them for a step this
		// short buys nothing but a set of handlers to leak. The check is a walk of four seats.
		protected override void OnTickStage(float deltaTime)
		{
			if (_pickUpTimer > 0f)
			{
				_pickUpTimer -= deltaTime;
				if (_pickUpTimer <= 0f) PickUpEveryCard();
				return;
			}

			if (_settling)
			{
				if (GameMode.IsStageTimerExpired()) FinishStage();
				return;
			}

			// A clock that ran out ends the looking with whatever each player chose to turn, which is a
			// real answer: leaving a card face down is a decision the second street can be read off.
			if (Data.HasStageTimer && GameMode.IsStageTimerExpired())
			{
				Settle();
				return;
			}

			if (EveryoneDone()) Settle();
		}

		private void PickUpEveryCard()
		{
			foreach (var player in GameMode.SeatedPlayers)
			{
				if (IsWaitedOn(player)) player.Data.ServerLookAtEveryHoleCard();
			}
		}

		private void Settle()
		{
			_settling = true;
			GameMode.ClearStageTimer();

			if (_settleDuration <= 0f)
			{
				FinishStage();
				return;
			}

			GameMode.BeginStageTimer(_settleDuration);
		}

		// Seated and still in the hand, the same question the street asks — a player who folded before the
		// deal is not being waited on, and neither is one the round never dealt to.
		private bool EveryoneDone()
		{
			foreach (var player in GameMode.SeatedPlayers)
			{
				if (!IsWaitedOn(player)) continue;

				var data = player.Data;
				if (data.LookedAtCount < data.ViewableHoleCards.Value) return false;
			}

			return true;
		}

		private static bool IsWaitedOn(PokerPlayer player)
		{
			if (!player || !player.Data) return false;

			var data = player.Data;
			if (!data.IsSeated || !data.IsAlive) return false;
			if (data.Status.Value == PokerPlayerStatus.Folded) return false;

			return data.HasLookLimit;
		}
	}
}
