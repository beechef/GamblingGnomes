using System.Collections.Generic;
using DG.Tweening;
using Game.Runtime.GameMode.Poker.Items;
using Game.Runtime.Player;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// The plate, as objects on the table rather than a number on the HUD. A wager has no size, so the pot
	// is a count of caps and every one of them is worth seeing: what somebody put up is the whole read of
	// the round.
	//
	// Each cap lands in front of the chair that owns it — PokerBetItem stamps the owner, and the seat owns
	// the spot the way it owns where the cards lie. Piling them all in the middle would throw away the one
	// thing the table is actually reading. The settlement rewrites those owners rather than copying the
	// caps onto a second list, so a cap changing hands is drawn by the one visual that draws every cap in
	// the game.
	//
	// Driven off PotItems, the ledger PokerTableUtility already writes beside the scalar, so there is no
	// second seeder and a late join replicates the plate as it stands. _caps is index-aligned with that
	// list — a cap that could not be spawned holds a null slot rather than shifting everything after it,
	// because an entry being eaten is named by its index.
	public class PokerItemPotVisual : PokerVisual
	{
		[Header("Cap")]
		[SerializeField] private PokerItemDatabase _database;

		[Tooltip("Stands in for a kind whose own model has not landed yet. Each kind names its prefab on the database; this is only the last resort.")]
		[SerializeField] private GameObject _fallbackPrefab;

		[SerializeField] private float _capScale = 1f;

		[Header("Layout")]
		[Tooltip("Gap between caps along the seat anchor's right, so a player who has staked twice has two caps side by side rather than one inside the other.")]
		[SerializeField] private float _capSpacing = 0.07f;

		[Tooltip("How many caps sit in a row in front of one chair before the next one starts a second row behind it.")]
		[MinValue(1)]
		[SerializeField] private int _capsPerRow = 3;

		[SerializeField] private float _rowSpacing = 0.07f;

		[Header("Drop")]
		[Tooltip("How far above its spot a cap starts. It is put down rather than appearing, so the table can see it arrive.")]
		[SerializeField] private float _dropHeight = 0.35f;

		[SerializeField] private float _dropDuration = 0.35f;
		[SerializeField] private Ease _dropEase = Ease.OutBounce;

		// A cap staked on a wager street is picked up and put down by the bet gesture rather than dropped
		// from the air. The gesture is played off the same change that stakes the cap, so both clocks start
		// together and the cap only has to wait for the gesture's own frames. Caps served by the settlement
		// or by the Colorful pick carry another street and are dropped as before.
		[Header("Bet")]
		[Tooltip("Seconds after a cap is staked before it appears in its staker's hand — the frame the bet gesture's hand reaches the table.")]
		[Min(0f)]
		[SerializeField] private float _betGrabDelay;

		[Tooltip("Seconds after a cap is staked before it leaves the hand for its spot — the frame the bet gesture puts it down.")]
		[Min(0f)]
		[SerializeField] private float _betReleaseDelay = 0.8f;

		[Tooltip("Seconds the cap takes from the hand to its spot on the table.")]
		[Min(0f)]
		[SerializeField] private float _betPlaceDuration = 0.25f;

		[SerializeField] private Ease _betPlaceEase = Ease.OutQuad;

		[Tooltip("Where the cap sits in the hand, in the hand bone's own space.")]
		[SerializeField] private Vector3 _handPosition;

		[SerializeField] private Vector3 _handRotation;

		[Header("Eating")]
		[Tooltip("How long a cap takes to travel to its eater's mouth once it leaves the table. Shorter than the consume stage's bite, so the swallow lands inside the gesture.")]
		[SerializeField] private float _eatDuration = 0.5f;

		[SerializeField] private float _eatRise = 0.35f;

		[Tooltip("Seconds the caps left in front of a player take to close the gap once one is eaten.")]
		[SerializeField] private float _relayoutDuration = 0.25f;

		private readonly List<GameObject> _caps = new();

		// Caps still being carried by a bet gesture. A re-layout leaves them alone: they are on their way to
		// the spot it would send them to, and killing their tween would leave them stuck in the hand.
		private readonly HashSet<GameObject> _carried = new();

		protected override void OnBind()
		{
			Data.OnPotItemsChanged += HandlePotItemsChanged;

			// Late join: the plate as it stands, with nothing to replay.
			RebuildAll();
		}

		protected override void OnUnbind()
		{
			if (Data) Data.OnPotItemsChanged -= HandlePotItemsChanged;

			ClearCaps();
		}

		private void HandlePotItemsChanged(NetworkListEvent<PokerBetItem> change)
		{
			switch (change.Type)
			{
				case NetworkListEvent<PokerBetItem>.EventType.Add:
					AddCap(change.Value, true);
					break;

				// One cap off the table is one being eaten, so it goes up rather than simply disappearing:
				// this is the moment the round's consequence actually happens and it has to be watchable.
				case NetworkListEvent<PokerBetItem>.EventType.RemoveAt:
				case NetworkListEvent<PokerBetItem>.EventType.Remove:
					EatCap(change.Index);
					break;

				case NetworkListEvent<PokerBetItem>.EventType.Clear:
					ClearCaps();
					break;

				default:
					RebuildAll();
					break;
			}
		}

		private void RebuildAll()
		{
			ClearCaps();

			if (!Data) return;

			foreach (var item in Data.PotItems) AddCap(item, false);
		}

		private void AddCap(PokerBetItem item, bool animate)
		{
			// Counted before this one joins the list, so a second stake from the same player is placed
			// beside the first rather than on top of it.
			var slot = CountCapsBefore(item.OwnerClientId, _caps.Count);

			var cap = Spawn(item);
			_caps.Add(cap);

			if (!cap) return;

			// Announced as it is put out, so anything painting the caps catches one arriving while it was
			// already running.
			PokerItemCapRegistry.Add(cap);

			var resting = SlotPosition(slot);

			// The item anchor stands upright, unlike the card one: a prop is a thing that sits on the table
			// the right way up, so it is instantiated unrotated and dropped straight down.
			if (!animate || _dropDuration <= 0f)
			{
				cap.transform.localPosition = resting;
				return;
			}

			if (IsStakedOnWager(item) && TryGetHand(item.OwnerClientId, out var hand))
			{
				Carry(cap, hand, resting);
				return;
			}

			cap.transform.localPosition = resting + Vector3.up * _dropHeight;
			cap.transform.DOLocalMove(resting, _dropDuration).SetEase(_dropEase);
		}

		private static bool IsStakedOnWager(PokerBetItem item) => item.Phase is PokerPhase.FirstWager or PokerPhase.SecondWager;

		// The hand on whichever rig this client draws for that player — the owner's own hands or the body
		// everybody else sees — so the cap is in the hand that is actually on screen.
		private bool TryGetHand(ulong clientId, out Transform hand)
		{
			hand = null;
			if (!GameMode) return false;

			var player = GameMode.FindSeatedPlayer(clientId);
			if (!player || !player.Rig) return false;

			hand = player.Rig.GetBone(PlayerBone.HandRight);
			return hand;
		}

		// Hidden until the hand reaches the table, then held until the gesture puts it down and it slides into
		// its spot. One sequence targeting the cap's transform, so a re-layout or a clear that kills the cap's
		// tweens takes this with it.
		private void Carry(GameObject cap, Transform hand, Vector3 resting)
		{
			var anchor = cap.transform.parent;
			var worldScale = cap.transform.lossyScale;

			cap.SetActive(false);
			_carried.Add(cap);

			var sequence = DOTween.Sequence().SetTarget(cap.transform).SetLink(cap);

			sequence.AppendInterval(_betGrabDelay);
			sequence.AppendCallback(() =>
			{
				cap.SetActive(true);
				cap.transform.SetParent(hand, false);
				cap.transform.localPosition = _handPosition;
				cap.transform.localRotation = Quaternion.Euler(_handRotation);

				// Kept at the size it has on the table, whatever scale the hand bone carries.
				var handScale = hand.lossyScale;
				cap.transform.localScale = new Vector3(worldScale.x / handScale.x, worldScale.y / handScale.y, worldScale.z / handScale.z);
			});

			sequence.AppendInterval(Mathf.Max(0f, _betReleaseDelay - _betGrabDelay));
			sequence.AppendCallback(() => cap.transform.SetParent(anchor, true));
			sequence.Append(cap.transform.DOLocalMove(resting, _betPlaceDuration).SetEase(_betPlaceEase));
			sequence.Join(cap.transform.DOLocalRotateQuaternion(Quaternion.identity, _betPlaceDuration));
			sequence.Join(cap.transform.DOScale(Vector3.one * _capScale, _betPlaceDuration));

			// Wherever it was when the sequence ended — landed or killed — it is back on the table's books.
			sequence.OnKill(() =>
			{
				_carried.Remove(cap);
				if (!cap || cap.transform.parent == anchor) return;

				cap.SetActive(true);
				cap.transform.SetParent(anchor, false);
				cap.transform.localPosition = resting;
				cap.transform.localRotation = Quaternion.identity;
				cap.transform.localScale = Vector3.one * _capScale;
			});
		}

		private GameObject Spawn(PokerBetItem item)
		{
			var seat = FindSeat(item.OwnerClientId);
			if (!seat || !seat.ItemAnchor) return null;

			var prefab = PrefabFor(item.ItemType);
			if (!prefab) return null;

			var cap = Instantiate(prefab, seat.ItemAnchor);

			cap.transform.localScale = Vector3.one * _capScale;
			cap.transform.localRotation = Quaternion.identity;

			return cap;
		}

		// Lifted and gone. Taken off the register as it is destroyed rather than as it leaves the ledger:
		// it is on screen for the whole swallow, and dropping it early would have it shed whatever a
		// hallucination had painted on it halfway to the eater's mouth.
		private void EatCap(int index)
		{
			if (index < 0 || index >= _caps.Count) { RebuildAll(); return; }

			var cap = _caps[index];
			_caps.RemoveAt(index);

			if (!cap) { Relayout(); return; }

			cap.transform.DOKill();

			if (_eatDuration <= 0f)
			{
				PokerItemCapRegistry.Remove(cap);
				Destroy(cap);
				Relayout();
				return;
			}

			var lifted = cap.transform.localPosition + Vector3.up * _eatRise;
			cap.transform.DOLocalMove(lifted, _eatDuration).SetEase(Ease.InQuad)
				.OnComplete(() => { PokerItemCapRegistry.Remove(cap); if (cap) Destroy(cap); });

			cap.transform.DOScale(Vector3.zero, _eatDuration).SetEase(Ease.InQuad);

			Relayout();
		}

		// Closing the gap an eaten cap left, in front of every chair at once: the caps are index-aligned
		// with the ledger, so each one's slot is however many of its owner's caps come before it.
		private void Relayout()
		{
			if (!Data) return;

			for (var i = 0; i < _caps.Count && i < Data.PotItems.Count; i++)
			{
				if (!_caps[i] || _carried.Contains(_caps[i])) continue;

				var slot = SlotPosition(CountCapsBefore(Data.PotItems[i].OwnerClientId, i));

				_caps[i].transform.DOKill();
				_caps[i].transform.DOLocalMove(slot, _relayoutDuration).SetEase(Ease.OutQuad);
			}
		}

		// How many of this owner's caps sit in the ledger before the given index.
		private int CountCapsBefore(ulong ownerClientId, int before)
		{
			if (!Data) return 0;

			var count = 0;
			for (var i = 0; i < before && i < Data.PotItems.Count; i++)
			{
				if (Data.PotItems[i].OwnerClientId == ownerClientId) count++;
			}

			return count;
		}

		private Vector3 SlotPosition(int slot)
		{
			var perRow = Mathf.Max(1, _capsPerRow);
			var column = slot % perRow;
			var row = slot / perRow;

			return new Vector3((column - (perRow - 1) * 0.5f) * _capSpacing, 0f, row * _rowSpacing);
		}

		// The kind's own model, looked up by type. A cap is a thing, not a colour: two mushrooms that
		// differ only in tint are a placeholder, and the database is where the difference belongs.
		private GameObject PrefabFor(PokerItemType itemType)
		{
			if (_database && _database.TryGetEntry(itemType, out var entry) && entry.WorldPrefab) return entry.WorldPrefab;

			// A kind whose model has not landed yet still has to be *there*: a pot that quietly draws
			// nothing reads as a broken pot rather than as missing art.
			return _fallbackPrefab;
		}

		// The seat rather than the player: a cap on the table belongs to the chair it is sitting in front
		// of, and a player who has left mid-hand has still staked it.
		private PokerSeat FindSeat(ulong clientId)
		{
			if (!GameMode) return null;

			var player = GameMode.FindSeatedPlayer(clientId);
			if (!player || !player.Data) return null;

			var seatIndex = player.Data.SeatIndex.Value;

			foreach (var seat in GameMode.Seats)
			{
				if (seat && seat.SeatIndex == seatIndex) return seat;
			}

			return null;
		}

		private void ClearCaps()
		{
			foreach (var cap in _caps)
			{
				if (!cap) continue;

				PokerItemCapRegistry.Remove(cap);
				cap.transform.DOKill();
				Destroy(cap);
			}

			_caps.Clear();
		}
	}
}
