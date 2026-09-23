using System;
using System.Collections.Generic;
using System.Threading;
using Game.Runtime.GameMode.Poker.Hallucination;
using Game.Runtime.GameMode.Poker.BetItems;
using Game.Runtime.Player;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Player
{
	// This player eating, and the record of what they have eaten. Asked to eat their plate — every cap the
	// ledger leaves in front of them — it swallows it mouthful by mouthful: the cap goes down, the hit plays
	// if it lifted them onto a new rung, and only then does the effect land, each waited out before the next.
	// Whoever asks only hears when the plate is finished; how it is eaten is decided here, so anything can
	// feed a player without a stage around it.
	//
	// What a mouthful *costs* belongs to the effect that was eaten, because two kinds can charge differently
	// for the same record and only the effect knows its own prices. A stockpile of things a player can choose
	// to use is a different lifetime and wants its own component.
	public class PokerBetItemConsumeController : NetworkBehaviour
	{
		[Header("Pacing")]
		[Tooltip("Every wait one mouthful makes — the bite, the hit, a Colorful roll's sweep and hold.")]
		[Required]
		[SerializeField] private PokerConsumePacing _pacing;

		// Every kind this player has met, in the order they met them, lasting the whole match. A list of
		// the kinds themselves rather than a bitmask over them: the mask could only say yes or no about
		// the first thirty-one values, needed a static TypeBit helper nobody could read at the call site,
		// and quietly answered "never eaten" for anything past its width. Public for the same reason the
		// rate is — what a player can still be hurt by is part of reading them.
		public readonly NetworkList<PokerBetItemUnit> Consumed = new(null,
			NetworkVariableReadPermission.Everyone,
			NetworkVariableWritePermission.Server);

		private static readonly PokerBiteRule DefaultBiteRule = new();

		// A mouthful that has been taken off the table and not yet paid for. The effect is what starts the
		// blink, so it is held back until the hit is over — otherwise the room changes on top of the impact
		// instead of after it.
		private readonly List<PokerBetItemType> _pendingBetItems = new();

		private PokerPlayer _player;
		private CancellationTokenSource _eating;
		private PokerBiteRule _rule;

		public PokerConsumePacing Pacing => _pacing;
		public bool IsEating => _eating != null;

		// Server only: the plate is finished, emptied or cut short. Raised once per ServerEatPlate that returned true.
		public event Action<PokerBetItemConsumeController> OnPlateFinished;

		private void Awake()
		{
			_player = GetComponentInParent<PokerPlayer>();
		}

		public override void OnNetworkDespawn()
		{
			// Leaving mid-plate: whatever is left goes with them, and nothing half-eaten lands afterwards.
			_eating?.Cancel();
		}

		public bool HasConsumed(PokerBetItemType itemType)
		{
			// A unit with no identity is not a kind anybody can have met before, so it never counts as one.
			if (itemType == PokerBetItemType.PlainChip) return false;

			foreach (var consumed in Consumed)
			{
				if (consumed == itemType) return true;
			}

			return false;
		}

		// Written after whatever read it, so an effect asking whether this kind is new gets the answer for
		// the mouthful being taken rather than for the one after it. Recorded once: the list says which
		// kinds have been met, not how many times.
		public void ServerRecordConsumed(PokerBetItemType itemType)
		{
			if (!IsServer || itemType == PokerBetItemType.PlainChip) return;
			if (HasConsumed(itemType)) return;

			Consumed.Add(itemType);
		}

		// The record belongs to the match it was built up in. Not swept per hand: what a player has met
		// before is what sets the price of the next cap, all match long.
		public void ServerResetForMatch()
		{
			if (!IsServer) return;

			Consumed.Clear();
		}

		// Conscious and with something in front of them. A player who went under stops eating: whatever is
		// left on their plate stays there until the table is cleared.
		public bool CanEat => _player && _player.Data && _player.Data.IsAlive;

		public bool HasPlate
		{
			get
			{
				var mode = PokerGameMode.Instance;
				return CanEat && mode && mode.Data && PokerTableUtility.CountPotEntries(mode.Data, _player.ClientId) > 0;
			}
		}

		// Starts eating everything the ledger leaves in front of this player. False, and no OnPlateFinished,
		// when there is nothing to eat or a plate is already being eaten.
		public bool ServerEatPlate(PokerBiteRule rule = null)
		{
			if (!IsServer || IsEating || !HasPlate) return false;

			_rule = rule ?? DefaultBiteRule;
			_eating = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);

			_ = EatPlateAsync(_eating.Token);
			return true;
		}

		// Cut short from outside (the beat ended, the match is over). The mouthful already taken off the table
		// is still paid for and a queued roll still rolls, or stopping would be a way to survive it.
		public void ServerStopEating()
		{
			if (!IsServer || !IsEating) return;

			_eating.Cancel();

			if (_pendingBetItems.Count > 0) ApplyPendingEffects();
			if (_player && _player.HallucinationRoll) _player.HallucinationRoll.ServerFlushQueuedRoll();
		}

		private async Awaitable EatPlateAsync(CancellationToken ct)
		{
			try
			{
				while (CanEat && TakeBite())
				{
					var climbs = WouldClimbRung();
					await WaitAsync(_pacing.BiteDuration, ct);

					if (climbs)
					{
						_player.ActionAnimator?.ServerPlay(PlayerActionIds.Impact);
						await WaitAsync(_pacing.ImpactDuration, ct);
					}

					await WaitAsync(Land(), ct);
				}
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception exception)
			{
				Debug.LogException(exception, this);
			}
			finally
			{
				_pendingBetItems.Clear();

				var eating = _eating;
				_eating = null;
				eating?.Dispose();

				OnPlateFinished?.Invoke(this);
			}
		}

		private static async Awaitable WaitAsync(float seconds, CancellationToken ct)
		{
			if (seconds > 0f) await Awaitable.WaitForSecondsAsync(seconds, ct);
			else ct.ThrowIfCancellationRequested();
		}

		// True while there was something left to swallow. One gesture per mouthful; each cap comes off the
		// table as it goes down rather than after, because the ledger is what the visual draws.
		private bool TakeBite()
		{
			var mode = PokerGameMode.Instance;
			var data = mode ? mode.Data : null;
			if (!data || PokerTableUtility.CountPotEntries(data, _player.ClientId) == 0) return false;

			_pendingBetItems.Clear();

			if (_rule.EatWholePlate) TakeWholePlate(data);
			else TakeBetItems(data, _rule.BiteSize);

			if (_pendingBetItems.Count == 0) return false;

			_player.ActionAnimator?.ServerPlay(PlayerActionIds.ConsumeItem);
			return true;
		}

		private void TakeBetItems(PokerGameData data, int count)
		{
			for (var i = 0; i < count; i++)
			{
				if (!PokerTableUtility.ServerTakePotEntry(data, _player.ClientId, out var itemType)) break;

				_pendingBetItems.Add(itemType);
			}
		}

		// Everything shared goes down together; once only the kinds eaten on their own are left, one of them.
		private void TakeWholePlate(PokerGameData data)
		{
			while (PokerTableUtility.ServerTakePotEntry(data, _player.ClientId, _rule.IsEatenWithOthers, out var itemType))
			{
				_pendingBetItems.Add(itemType);
			}

			if (_pendingBetItems.Count > 0) return;

			if (PokerTableUtility.ServerTakePotEntry(data, _player.ClientId, _rule.IsEatenOnItsOwn, out var alone))
			{
				_pendingBetItems.Add(alone);
			}
		}

		// Asked of the effects before the bite is paid for, so the hit can be played in front of the change
		// rather than on top of it. Exact rather than predicted: the only thing a price turns on is a record
		// nothing has written yet. A climb only — coming down a rung is relief, not a hit.
		private bool WouldClimbRung()
		{
			var mode = PokerGameMode.Instance;
			var database = mode ? mode.BetItemDatabase : null;
			var gain = 0;

			foreach (var itemType in _pendingBetItems)
			{
				if (database && database.TryGetEntry(itemType, out var entry) && entry.Effect)
				{
					gain += entry.Effect.PreviewHallucinationGain(mode, _player, itemType);
				}
			}

			if (gain <= 0) return false;

			var hallucination = _player.GetComponentInChildren<PokerHallucinationController>(true);
			var before = _player.Data.HallucinationRate.Value;

			return hallucination && hallucination.CrossesRung(before, before + gain);
		}

		// The hit is over, so the world is allowed to change: the effect lands, which is what starts the
		// eater's blink, and the next mouthful waits that out. Returns how long. The rate is read either side
		// rather than taken from the preview, because a Colorful roll can move it further than any preview.
		private float Land()
		{
			var before = _player.Data.HallucinationRate.Value;

			ApplyPendingEffects();

			// A Colorful cap queued its roll rather than playing it, so the sweep runs on this pacing.
			if (_player.HallucinationRoll)
			{
				_player.HallucinationRoll.ServerStartQueuedRoll(_pacing.RollLeadIn, _pacing.RollSweepDuration, _pacing.RollResultHold);
			}

			var after = _player.Data.HallucinationRate.Value;

			return Mathf.Max(TransitionWait(before, after), RollWait(after)) + _pacing.GapBetweenBites;
		}

		private void ApplyPendingEffects()
		{
			var mode = PokerGameMode.Instance;
			var database = mode ? mode.BetItemDatabase : null;

			foreach (var itemType in _pendingBetItems)
			{
				if (database && database.TryGetEntry(itemType, out var entry) && entry.Effect)
				{
					entry.Effect.ConsumeServer(mode, _player, itemType);
				}
			}

			_pendingBetItems.Clear();
		}

		// A Colorful roll is still being shown when the effect returns: the skull sweeps every bar and a
		// fatal one only puts its eater under once it stops. The next mouthful waits out the whole of that,
		// and then the whole death the roll sets off.
		private float RollWait(int rate)
		{
			var roll = _player.HallucinationRoll;
			if (!roll) return 0f;

			var remaining = roll.ServerRollRemaining;
			if (remaining <= 0f) return 0f;

			if (!roll.ServerRollFatal) return remaining;

			return remaining + DeathWait(rate, PokerPlayerData.MaxHallucination);
		}

		// How long the room spends changing, asked of the controllers that own the ladder, the blink and the
		// death. A mouthful that puts its eater under is waited out to the end of the death, whatever the
		// blink setting: whatever follows it must not land on top of the fall.
		private float TransitionWait(int before, int after)
		{
			if (before == after) return 0f;

			if (before < PokerPlayerData.MaxHallucination && after >= PokerPlayerData.MaxHallucination)
				return DeathWait(before, after);

			if (!_pacing.WaitForHallucinationTransition) return 0f;

			var hallucination = _player.GetComponentInChildren<PokerHallucinationController>(true);

			return hallucination ? hallucination.BlinkWait(before, after) : 0f;
		}

		private float DeathWait(int before, int after)
		{
			var pose = _player.GetComponentInChildren<PokerDeathPoseController>(true);

			return pose ? pose.DeathWait(before, after) : 0f;
		}
	}
}
