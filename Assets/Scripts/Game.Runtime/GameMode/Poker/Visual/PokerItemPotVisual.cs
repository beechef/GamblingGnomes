using System;
using System.Collections.Generic;
using DG.Tweening;
using Game.Runtime.GameMode.Poker.Items;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// The plate, as objects on the table rather than a number on the HUD. A wager has no size, so the pot
	// is a count of caps and every one of them is worth seeing: what somebody put up is the whole read of
	// the round.
	//
	// Each cap lands in front of the chair that staked it — PokerBetItem stamps the owner, and the seat
	// owns the spot the way it owns where the cards lie. Piling them all in the middle would throw away
	// the one thing the table is actually reading.
	//
	// Driven off PotItems, the ledger PokerTableUtility already writes beside the scalar, so there is no
	// second seeder and a late join replicates the plate as it stands.
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

		private readonly List<GameObject> _caps = new();

		// Registered in its own lifecycle, the shape PokerScenery and GameCamera already take: a cap is
		// spawned at runtime and nothing can serialize a reference to the plate it lands on.
		public static PokerItemPotVisual Instance { get; private set; }

		public IReadOnlyList<GameObject> Caps => _caps;

		// The plate is emptied and filled again every round, so anything drawn on a cap comes off on its own
		// while whatever put it there has not moved. Static because the things that care are about the table,
		// not about one seat.
		public static event Action OnAnyPotChanged;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			OnAnyPotChanged = null;
			Instance = null;
		}

		// How many caps each chair is already holding, so a second stake from the same player is placed
		// beside the first rather than on top of it.
		private readonly Dictionary<int, int> _capsPerSeat = new();

		protected override void OnBind()
		{
			Instance = this;

			Data.OnPotItemsChanged += HandlePotItemsChanged;

			// Late join: the plate as it stands, with nothing to replay.
			RebuildAll();
		}

		protected override void OnUnbind()
		{
			if (Data) Data.OnPotItemsChanged -= HandlePotItemsChanged;

			if (Instance == this) Instance = null;

			ClearCaps();
		}

		private void HandlePotItemsChanged(NetworkListEvent<PokerBetItem> change)
		{
			switch (change.Type)
			{
				case NetworkListEvent<PokerBetItem>.EventType.Add:
					AddCap(change.Value, true);
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
			var seat = FindSeat(item.OwnerClientId);
			if (!seat) return;

			var anchor = seat.ItemAnchor;
			if (!anchor) return;

			// Resolved before the seat's counter moves: a kind with no model must not burn the slot the
			// next cap is going to land in.
			var prefab = PrefabFor(item.ItemTypeIndex);
			if (!prefab) return;

			_capsPerSeat.TryGetValue(seat.SeatIndex, out var placed);
			_capsPerSeat[seat.SeatIndex] = placed + 1;

			var cap = Instantiate(prefab, anchor);
			_caps.Add(cap);

			cap.transform.localScale = Vector3.one * _capScale;
			cap.transform.localRotation = Quaternion.identity;

			var resting = SlotPosition(placed);

			// The item anchor stands upright, unlike the card one: a prop is a thing that sits on the table
			// the right way up, so it is instantiated unrotated and dropped straight down.
			if (!animate || _dropDuration <= 0f)
			{
				cap.transform.localPosition = resting;
				OnAnyPotChanged?.Invoke();
				return;
			}

			cap.transform.localPosition = resting + Vector3.up * _dropHeight;
			cap.transform.DOLocalMove(resting, _dropDuration).SetEase(_dropEase);

			OnAnyPotChanged?.Invoke();
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
		private GameObject PrefabFor(byte itemType)
		{
			if (_database && _database.TryGetEntry(itemType, out var entry) && entry.WorldPrefab) return entry.WorldPrefab;

			// A kind whose model has not landed yet still has to be *there*: a pot that quietly draws
			// nothing reads as a broken pot rather than as missing art.
			return _fallbackPrefab;
		}

		// The seat rather than the player: a cap already on the table belongs to the chair that put it
		// there, and a player who has left mid-hand has still staked it.
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

				cap.transform.DOKill();
				Destroy(cap);
			}

			_caps.Clear();
			_capsPerSeat.Clear();

			OnAnyPotChanged?.Invoke();
		}
	}
}
