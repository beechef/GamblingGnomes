using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Items;
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
		[Header("Bite")]
		[Tooltip("Caps one eating gesture swallows. The animation is one mouthful however many go into it, so this is how many a mouthful is.")]
		[MinValue(1)]
		[SerializeField] private int _itemsPerBite = 1;

		[Tooltip("On, a player's whole plate goes down in one mouthful, except the kinds eaten on their own, which follow one per mouthful. Items Per Bite is not used then.")]
		[SerializeField] private bool _eatWholePlate;

		[Tooltip("Kinds never swallowed with anything else while the whole plate is eaten at once: each is its own mouthful, after the rest. Colorful, whose roll is its own beat.")]
		[ShowIf(nameof(_eatWholePlate))]
		[SerializeField] private List<PokerItemType> _eatenOnTheirOwn = new() { PokerItemType.Colorful };

		[Header("Pacing")]
		[Tooltip("Every wait this beat makes — the mouthful, the hit, the handover, a Colorful roll's sweep and hold. One asset, so the beat is retuned in one place.")]
		[Required]
		[SerializeField] private PokerConsumePacing _pacing;

		[Header("References")]
		[Tooltip("Where the next hand begins. Named rather than left to the sequence, which wraps to its first entry — and that is the waiting room.")]
		[Required]
		[SerializeField] private PokerStage _nextStage;

		[Tooltip("Where the table goes when the eating leaves fewer players in the running than a hand needs. Empty carries on to the next stage as before.")]
		[SerializeField] private PokerStage _matchOverStage;

		private float _timer;
		private int _seatIndex;
		private bool _waitingToHandOver;

		// A mouthful that has been taken off the table and not yet paid for. The effect is what starts the
		// blink, so it is held back until the hit is over — otherwise the room changes on top of the impact
		// instead of after it, which is the one beat the whole round is played for.
		private readonly List<PokerItemType> _pendingItems = new();
		private bool _pendingImpact;

		protected override void OnStartStage()
		{
			Data.Phase.Value = PokerPhase.Eating;
			GameMode.ClearTurn();

			_seatIndex = -1;
			_timer = 0f;
			_waitingToHandOver = false;

			_pendingItems.Clear();
			_pendingImpact = false;

			// Nobody was handed anything — everyone folded out, or the settlement had nothing to give.
			if (!AdvanceToNextEater()) FinishEating();
		}

		protected override void OnTickStage(float deltaTime)
		{
			_timer -= deltaTime;
			if (_timer > 0f) return;

			// One mouthful runs as three beats in a row, never together: it goes down, then the hit plays if
			// it moved them onto a new rung, and only then does the world change. Each is a step through
			// this rather than a duration added to the one before it, so nothing can overlap.
			if (_pendingItems.Count > 0)
			{
				if (_pendingImpact)
				{
					PlayImpact();
					return;
				}

				ApplyPending();
				return;
			}

			if (_waitingToHandOver)
			{
				_waitingToHandOver = false;
				if (!AdvanceToNextEater()) FinishEating();
				return;
			}

			var eater = CurrentEater();

			// They left, or went under mid-plate. Whatever is left on it goes with them: eating is a thing
			// a player does, not a debt the table collects.
			if (!eater || !TakeBite(eater))
			{
				_waitingToHandOver = true;
				_timer = _pacing.HandoverDuration;
			}
		}

		// The table is cleared on the way out. Anything still standing belongs to somebody who left or went
		// under mid-plate — eating is a thing a player does, not a debt the table collects — and the pot is
		// carried into the next round rather than wiped by the deal, so a cap nobody is going to swallow
		// would sit there gathering the next hand's stakes around it.
		// Cut short by anything — the match ending, a stage pushed over it — a roll that was queued and never
		// started still has to be paid, or leaving the beat would be a way to survive the Colorful cap.
		protected override void OnEndStage()
		{
			foreach (var player in GameMode.SeatedPlayers)
			{
				if (player && player.HallucinationRoll) player.HallucinationRoll.ServerFlushQueuedRoll();
			}
		}

		private void FinishEating()
		{
			PokerTableUtility.ResetPot(Data);
			FinishStage(_matchOverStage && !GameMode.CanDealAnotherHand ? _matchOverStage : _nextStage);
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

				// Nobody is being asked anything here, so there is no turn to carry the room's attention —
				// this beat says who it is about itself.
				GameMode.ServerSetFocus(player.ClientId);
				return true;
			}

			GameMode.ServerSetFocus(PokerGameData.NoTurn);
			return false;
		}

		private PokerPlayer CurrentEater() => FindSeatedPlayerAtSeat(_seatIndex);

		// True while there was something left to swallow. One gesture per mouthful and _itemsPerBite caps
		// in it; each cap comes off the table as it goes down rather than after, because the ledger is what
		// the visual draws.
		private bool TakeBite(PokerPlayer eater)
		{
			if (PokerTableUtility.CountPotItems(Data, eater.ClientId) == 0) return false;

			eater.ActionAnimator?.ServerPlay(PlayerActionIds.ConsumeItem);

			// Off the table now, paid for later: the cap leaving the ledger is what the visual plays as the
			// mouthful, and that has to happen while the gesture is running rather than after it.
			_pendingItems.Clear();

			if (_eatWholePlate) TakeWholePlate(eater);
			else TakeItems(eater, Mathf.Max(1, _itemsPerBite));

			if (_pendingItems.Count == 0) return false;

			_pendingImpact = WouldClimbRung(eater);
			_timer = _pacing.BiteDuration;

			return true;
		}

		private void TakeItems(PokerPlayer eater, int count)
		{
			for (var i = 0; i < count; i++)
			{
				if (!PokerTableUtility.ServerTakePotItem(Data, eater.ClientId, out var itemType)) break;

				_pendingItems.Add(itemType);
			}
		}

		// Everything shared goes down together; once only the kinds eaten on their own are left, one of them.
		private void TakeWholePlate(PokerPlayer eater)
		{
			while (PokerTableUtility.ServerTakePotItem(Data, eater.ClientId, IsEatenWithOthers, out var itemType))
			{
				_pendingItems.Add(itemType);
			}

			if (_pendingItems.Count > 0) return;

			if (PokerTableUtility.ServerTakePotItem(Data, eater.ClientId, IsEatenOnItsOwn, out var alone))
			{
				_pendingItems.Add(alone);
			}
		}

		private bool IsEatenOnItsOwn(PokerItemType itemType) => _eatenOnTheirOwn.Contains(itemType);
		private bool IsEatenWithOthers(PokerItemType itemType) => !_eatenOnTheirOwn.Contains(itemType);

		// Asked of the effects before the bite is paid for, so the hit can be played in front of the change
		// rather than on top of it. Exact rather than predicted: the only thing a price turns on is a record
		// nothing has written yet. A climb only — coming down a rung is relief, not a hit.
		private bool WouldClimbRung(PokerPlayer eater)
		{
			if (!eater.Data) return false;

			var database = GameMode.ItemDatabase;
			var gain = 0;

			foreach (var itemType in _pendingItems)
			{
				if (database && database.TryGetEntry(itemType, out var entry) && entry.Effect)
				{
					gain += entry.Effect.PreviewHallucinationGain(GameMode, eater, itemType);
				}
			}

			if (gain <= 0) return false;

			var hallucination = eater.GetComponentInChildren<PokerHallucinationController>(true);
			var before = eater.Data.HallucinationRate.Value;

			return hallucination && hallucination.CrossesRung(before, before + gain);
		}

		private void PlayImpact()
		{
			_pendingImpact = false;
			_timer = _pacing.ImpactDuration;

			FindSeatedPlayerAtSeat(_seatIndex)?.ActionAnimator?.ServerPlay(PlayerActionIds.Impact);
		}

		// The hit is over, so the world is allowed to change: the effect lands, which is what starts the
		// eater's blink, and the next mouthful waits that out — one landing inside the blink is one nobody
		// saw. The rate is read either side rather than taken from the preview, because a Colorful roll can
		// have moved it further than any preview could say.
		private void ApplyPending()
		{
			var eater = FindSeatedPlayerAtSeat(_seatIndex);
			var database = GameMode.ItemDatabase;
			var before = eater && eater.Data ? eater.Data.HallucinationRate.Value : 0;

			foreach (var itemType in _pendingItems)
			{
				if (eater && database && database.TryGetEntry(itemType, out var entry) && entry.Effect)
				{
					entry.Effect.ConsumeServer(GameMode, eater, itemType);
				}
			}

			_pendingItems.Clear();

			// A Colorful cap queued its roll rather than playing it: this beat owns when things happen, so it
			// starts the sweep with its own pacing and then waits it out below.
			if (eater && eater.HallucinationRoll)
			{
				eater.HallucinationRoll.ServerStartQueuedRoll(_pacing.RollLeadIn, _pacing.RollSweepDuration, _pacing.RollResultHold);
			}

			var after = eater && eater.Data ? eater.Data.HallucinationRate.Value : before;

			_timer = Mathf.Max(TransitionWait(eater, before, after), RollWait(eater, after)) + _pacing.GapBetweenBites;
		}

		// A Colorful roll is still being shown when the effect returns: the skull sweeps every bar and a
		// fatal one only puts its eater under once it stops. The next mouthful waits out the whole of that,
		// and then the whole death the roll sets off, asked of the eater's own death pose.
		private static float RollWait(PokerPlayer eater, int rate)
		{
			var roll = eater ? eater.HallucinationRoll : null;
			if (!roll) return 0f;

			var remaining = roll.ServerRollRemaining;
			if (remaining <= 0f) return 0f;

			if (!roll.ServerRollFatal) return remaining;

			return remaining + DeathWait(eater, rate, PokerPlayerData.MaxHallucination);
		}

		// How long the room spends changing. Their controllers own the ladder, the blink and the death, so
		// they are asked rather than a second copy of any being kept here. A mouthful that puts its eater under
		// is waited out to the end of the death, whatever the blink setting: whatever follows it (the next
		// eater, the match ending) must not land on top of the fall.
		private float TransitionWait(PokerPlayer eater, int before, int after)
		{
			if (!eater || before == after) return 0f;

			if (before < PokerPlayerData.MaxHallucination && after >= PokerPlayerData.MaxHallucination)
				return DeathWait(eater, before, after);

			if (!_pacing.WaitForHallucinationTransition) return 0f;

			var hallucination = eater.GetComponentInChildren<PokerHallucinationController>(true);

			return hallucination ? hallucination.BlinkWait(before, after) : 0f;
		}

		private static float DeathWait(PokerPlayer eater, int before, int after)
		{
			var pose = eater.GetComponentInChildren<PokerDeathPoseController>(true);

			return pose ? pose.DeathWait(before, after) : 0f;
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
