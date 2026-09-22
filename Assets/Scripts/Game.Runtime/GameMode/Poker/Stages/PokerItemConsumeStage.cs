using Game.Runtime.GameMode.Poker.Items;
using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	// Where the round's consequence actually happens. Every cap the settlement left standing in front of
	// somebody is swallowed here, one player at a time, so the table watches what the hand cost each of them
	// rather than reading it off a number that changed while the ranking board was up.
	//
	// This beat only says whose turn it is to eat. How a plate goes down — mouthful by mouthful, the hit, the
	// blink, a roll, a death — is the eater's PokerItemConsumeController, which says when it is finished; the
	// next hand does not begin until the last plate is clear.
	[CreateAssetMenu(fileName = "PokerStage_ItemConsume", menuName = "Game/Poker/Stages/Item Consume")]
	public class PokerItemConsumeStage : PokerStage
	{
		[Header("Plate")]
		[Tooltip("How much one mouthful takes, handed to every eater at this table.")]
		[SerializeField] private PokerBiteRule _biteRule = new();

		[Header("Turns")]
		[Tooltip("Seconds between one player finishing their plate and the next starting theirs, so two players eating do not read as one.")]
		[MinValue(0f)]
		[SerializeField] private float _handoverDuration = 0.4f;

		[Header("References")]
		[Tooltip("Where the next hand begins. Named rather than left to the sequence, which wraps to its first entry — and that is the waiting room.")]
		[Required]
		[SerializeField] private PokerStage _nextStage;

		[Tooltip("Where the table goes when the eating leaves fewer players in the running than a hand needs. Empty carries on to the next stage as before.")]
		[SerializeField] private PokerStage _matchOverStage;

		private int _seatIndex;
		private float _timer;
		private bool _handingOver;
		private PokerItemConsumeController _eater;

		protected override void OnStartStage()
		{
			Data.Phase.Value = PokerPhase.Eating;
			GameMode.ClearTurn();

			_seatIndex = -1;
			_handingOver = false;

			// Nobody was handed anything — everyone folded out, or the settlement had nothing to give.
			StartNextPlate();
		}

		protected override void OnTickStage(float deltaTime)
		{
			if (_handingOver)
			{
				_timer -= deltaTime;
				if (_timer > 0f) return;

				_handingOver = false;
				StartNextPlate();
				return;
			}

			// The eater's body went with them mid-plate.
			if (_eater is not null && !_eater) HandOver();
		}

		// The table is cleared on the way out. Cut short by anything — the match ending, a stage pushed over
		// it — the plate being eaten is stopped, and the eater pays for the mouthful they had already taken.
		protected override void OnEndStage()
		{
			var eater = _eater;
			Release();

			if (eater) eater.ServerStopEating();
		}

		// Seat order, so the eating goes round the table the way everything else does rather than in
		// whatever order the settlement happened to serve.
		private void StartNextPlate()
		{
			var seatCount = Mathf.Max(1, Data.ActiveSeatCount.Value);

			for (var step = _seatIndex + 1; step < seatCount; step++)
			{
				var player = FindSeatedPlayerAtSeat(step);
				var consume = player ? player.ItemConsume : null;
				if (!consume || !consume.HasPlate) continue;

				_seatIndex = step;
				_eater = consume;
				_eater.OnPlateFinished += HandlePlateFinished;

				// Nobody is being asked anything here, so there is no turn to carry the room's attention —
				// this beat says who it is about itself.
				GameMode.ServerSetFocus(player.ClientId);

				if (_eater.ServerEatPlate(_biteRule)) return;

				Release();
			}

			GameMode.ServerSetFocus(PokerGameData.NoTurn);
			FinishEating();
		}

		private void HandlePlateFinished(PokerItemConsumeController eater)
		{
			if (eater != _eater) return;

			HandOver();
		}

		private void HandOver()
		{
			Release();

			_handingOver = true;
			_timer = _handoverDuration;
		}

		private void Release()
		{
			if (_eater is not null) _eater.OnPlateFinished -= HandlePlateFinished;
			_eater = null;
		}

		// Anything still standing belongs to somebody who left or went under mid-plate — eating is a thing a
		// player does, not a debt the table collects — and the pot is carried into the next round rather than
		// wiped by the deal, so a cap nobody is going to swallow would sit there gathering the next hand's
		// stakes around it.
		private void FinishEating()
		{
			PokerTableUtility.ResetPot(Data);
			FinishStage(_matchOverStage && !GameMode.CanDealAnotherHand ? _matchOverStage : _nextStage);
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
