using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.BetItems;
using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	// Where the round's consequence actually happens. Every cap the settlement left standing in front of
	// somebody is swallowed here, so the table watches what the hand cost each of them rather than reading it
	// off a number that changed while the ranking board was up. One player at a time with the room turned to
	// each, or everyone at once with the view left free and turned only to whoever is rolling (_order).
	//
	// This beat only says whose turn it is to eat. How a plate goes down — mouthful by mouthful, the hit, the
	// blink, a roll, a death — is the eater's PokerBetItemConsumeController, which says when it is finished; the
	// next hand does not begin until the last plate is clear.
	[CreateAssetMenu(fileName = "PokerStage_Consume", menuName = "Game/Poker/Stages/Bet Item Consume")]
	public class PokerBetItemConsumeStage : PokerStage
	{
		[Header("Plate")]
		[Tooltip("How much one mouthful takes, handed to every eater at this table.")]
		[SerializeField] private PokerBiteRule _biteRule = new();

		[Header("Turns")]
		[Tooltip("One after another: seat order, the room watching each eater. All at once: every plate starts together, the view stays free and turns only to whoever is rolling.")]
		[SerializeField] private PokerEatingOrder _order = PokerEatingOrder.OneAfterAnother;

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
		private PokerBetItemConsumeController _eater;

		// All at once: everyone still eating, and the roller the room is turned to.
		private readonly List<PokerPlayer> _eaters = new();
		private PokerPlayer _watchedRoller;

		protected override void OnStartStage()
		{
			Data.Phase.Value = PokerPhase.Eating;
			GameMode.ClearTurn();

			_seatIndex = -1;
			_handingOver = false;

			if (_order == PokerEatingOrder.AllAtOnce)
			{
				StartEveryPlate();
				return;
			}

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
				if (_order == PokerEatingOrder.AllAtOnce) FinishEating();
				else StartNextPlate();
				return;
			}

			if (_order == PokerEatingOrder.AllAtOnce)
			{
				TickTable();
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

			foreach (var player in _eaters)
			{
				if (!player || !player.BetItemConsume) continue;

				player.BetItemConsume.OnPlateFinished -= HandleTablePlateFinished;
				player.BetItemConsume.ServerStopEating();
			}

			_eaters.Clear();
			_watchedRoller = null;
		}

		// Every plate at once. Nobody is watched until somebody rolls.
		private void StartEveryPlate()
		{
			GameMode.ServerSetFocus(PokerGameData.NoTurn);

			var seatCount = Mathf.Max(1, Data.ActiveSeatCount.Value);
			for (var seat = 0; seat < seatCount; seat++)
			{
				var player = GameMode.FindSeatedPlayerAtSeat(seat);
				var consume = player ? player.BetItemConsume : null;
				if (!consume || !consume.HasPlate) continue;

				consume.OnPlateFinished += HandleTablePlateFinished;
				if (consume.ServerEatPlate(_biteRule)) _eaters.Add(player);
				else consume.OnPlateFinished -= HandleTablePlateFinished;
			}

			if (_eaters.Count == 0) FinishEating();
		}

		private void TickTable()
		{
			// A body that went mid-plate is a plate that is over.
			for (var i = _eaters.Count - 1; i >= 0; i--)
			{
				if (!_eaters[i] || !_eaters[i].BetItemConsume) _eaters.RemoveAt(i);
			}

			WatchRoller();

			if (_eaters.Count == 0) HandOver();
		}

		// The room turns to whoever is rolling and stays on them through the result (and the fall, when it is
		// fatal), then to the next roller, then lets go. Read off each roller's own clock, which the server owns.
		private void WatchRoller()
		{
			if (_watchedRoller && _watchedRoller.HallucinationRoll && _watchedRoller.HallucinationRoll.ServerOutcomeRemaining > 0f) return;

			PokerPlayer next = null;
			foreach (var player in _eaters)
			{
				if (player && player.HallucinationRoll && player.HallucinationRoll.ServerRollRemaining > 0f)
				{
					next = player;
					break;
				}
			}

			if (next == _watchedRoller) return;

			_watchedRoller = next;
			GameMode.ServerSetFocus(next ? next.ClientId : PokerGameData.NoTurn);
		}

		private void HandleTablePlateFinished(PokerBetItemConsumeController eater)
		{
			eater.OnPlateFinished -= HandleTablePlateFinished;
			_eaters.RemoveAll(player => !player || player.BetItemConsume == eater);
		}

		// Seat order, so the eating goes round the table the way everything else does rather than in
		// whatever order the settlement happened to serve.
		private void StartNextPlate()
		{
			var seatCount = Mathf.Max(1, Data.ActiveSeatCount.Value);

			for (var step = _seatIndex + 1; step < seatCount; step++)
			{
				var player = GameMode.FindSeatedPlayerAtSeat(step);
				var consume = player ? player.BetItemConsume : null;
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

		private void HandlePlateFinished(PokerBetItemConsumeController eater)
		{
			if (eater != _eater) return;

			HandOver();
		}

		private void HandOver()
		{
			Release();
			if (_order == PokerEatingOrder.AllAtOnce) GameMode.ServerSetFocus(PokerGameData.NoTurn);

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
	}
}
