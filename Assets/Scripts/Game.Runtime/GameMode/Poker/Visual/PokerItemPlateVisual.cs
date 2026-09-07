using System.Collections.Generic;
using DG.Tweening;
using Game.Runtime.GameMode.Poker.Items;
using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// What this player still has to swallow, sitting in front of them. Lives on the player rather than on
	// the table for the same reason the hand does: the plate belongs to whoever has to work through it,
	// and the table's own visual is about the pot, which is empty by the time this fills.
	//
	// It draws the replicated queue and nothing else, so a cap leaving the list is a cap being eaten and
	// the two cannot disagree about how much is left.
	public class PokerItemPlateVisual : NetworkBehaviour
	{
		[Header("Cap")]
		[SerializeField] private PokerItemDatabase _database;

		[Tooltip("Stands in for a kind whose own model has not landed yet. Each kind names its prefab on the database; this is only the last resort.")]
		[SerializeField] private GameObject _fallbackPrefab;

		[SerializeField] private float _capScale = 1f;

		[Header("Layout")]
		[SerializeField] private float _capSpacing = 0.07f;

		[Tooltip("How many caps sit in a row before the next starts a second row behind it.")]
		[MinValue(1)]
		[SerializeField] private int _capsPerRow = 3;

		[SerializeField] private float _rowSpacing = 0.07f;

		[Header("Serving")]
		[Tooltip("How an item shows up in front of this player. An asset, because what the moment looks like is art's call and not yet decided — dropped onto the table, handed across from whoever staked it, faded in. Empty puts it down with no animation at all.")]
		[SerializeField] private PokerItemArrival _arrival;

		[Header("Eating")]
		[Tooltip("How long a cap takes to travel to its eater's mouth once it leaves the plate. Shorter than the stage's bite, so the swallow lands inside the gesture.")]
		[SerializeField] private float _eatDuration = 0.5f;

		[SerializeField] private float _eatRise = 0.35f;

		[Tooltip("Seconds the remaining items take to close the gap once one is eaten.")]
		[SerializeField] private float _relayoutDuration = 0.25f;

		[Tooltip("Which chair this plate belongs in front of.")]
		[SerializeField] private Player.PokerPlayerData _data;

		[Header("References")]
		[SerializeField] private PokerPlayerItemController _items;

		private readonly List<GameObject> _caps = new();

		public override void OnNetworkSpawn()
		{
			if (!_items) _items = GetComponentInParent<PokerPlayerItemController>();
			if (!_data) _data = GetComponentInParent<Player.PokerPlayerData>();
			if (!_items) return;

			_items.Pending.OnListChanged += HandlePlateChanged;

			// Late join: whatever is still on this plate, as it stands.
			RebuildAll();
		}

		public override void OnNetworkDespawn()
		{
			if (!_items) return;

			_items.Pending.OnListChanged -= HandlePlateChanged;

			ClearCaps();
		}

		private void HandlePlateChanged(NetworkListEvent<byte> change)
		{
			switch (change.Type)
			{
				case NetworkListEvent<byte>.EventType.Add:
					AddCap(change.Value, _caps.Count, true);
					break;

				// The front of the queue is the cap being eaten, so it goes up rather than out.
				case NetworkListEvent<byte>.EventType.RemoveAt:
				case NetworkListEvent<byte>.EventType.Remove:
					EatCap(change.Index);
					break;

				case NetworkListEvent<byte>.EventType.Clear:
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

			if (!_items) return;

			for (var i = 0; i < _items.Pending.Count; i++) AddCap(_items.Pending[i], i, false);
		}

		private void AddCap(byte itemType, int slot, bool animate)
		{
			var anchor = ResolveAnchor();
			if (!anchor) return;

			var prefab = PrefabFor(itemType);
			if (!prefab) return;

			var cap = Instantiate(prefab, anchor);
			_caps.Add(cap);

			cap.transform.localScale = Vector3.one * _capScale;
			cap.transform.localRotation = Quaternion.identity;

			var resting = SlotPosition(slot);

			if (!animate || !_arrival)
			{
				cap.transform.localPosition = resting;
				return;
			}

			// Where it came from, so an arrival that travels has somewhere to travel from. The winner is who
			// the settlement took it off, and the table already remembers them — a second value naming the
			// source would be one more thing to keep in step with the hand that decided it.
			_arrival.Play(cap.transform, resting, ResolveSourceAnchor());
		}

		// Lifted and gone, rather than simply disappearing: this is the moment the round's consequence
		// actually happens, and it has to be watchable.
		private void EatCap(int index)
		{
			if (index < 0 || index >= _caps.Count) return;

			var cap = _caps[index];
			_caps.RemoveAt(index);

			if (!cap) { Relayout(); return; }

			cap.transform.DOKill();

			if (_eatDuration <= 0f)
			{
				Destroy(cap);
				Relayout();
				return;
			}

			var lifted = cap.transform.localPosition + Vector3.up * _eatRise;
			cap.transform.DOLocalMove(lifted, _eatDuration).SetEase(Ease.InQuad)
				.OnComplete(() => { if (cap) Destroy(cap); });

			cap.transform.DOScale(Vector3.zero, _eatDuration).SetEase(Ease.InQuad);

			Relayout();
		}

		// Closing the gap an eaten item left. Its own duration rather than the arrival's: this is the plate
		// tidying itself, not something showing up, and borrowing the arrival's timing would tie a shuffle
		// to however long art decides a handover should take.
		private void Relayout()
		{
			for (var i = 0; i < _caps.Count; i++)
			{
				if (!_caps[i]) continue;

				_caps[i].transform.DOKill();
				_caps[i].transform.DOLocalMove(SlotPosition(i), _relayoutDuration).SetEase(Ease.OutQuad);
			}
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

			// A kind whose model has not landed yet still has to be *there*: a plate that quietly draws
			// nothing reads as a player who was served nothing.
			return _fallbackPrefab;
		}

		// The chair owns the spot, the way it owns where the cards lie. A body that is not at the table
		// has nowhere to put a plate down.
		private Transform ResolveAnchor() => AnchorForSeat(_data ? _data.SeatIndex.Value : -1);

		// Whoever the settlement took these off. Their own place is the honest source: the items really
		// were sitting there a moment ago, which is what makes the handover read as a handover.
		private Transform ResolveSourceAnchor()
		{
			var mode = PokerGameMode.Instance;
			if (!mode || !mode.Data) return null;

			var winner = mode.FindSeatedPlayer(mode.Data.LastWinnerClientId.Value);
			if (!winner || !winner.Data) return null;

			// Their own plate is not a handover. An arrival that travels falls back to appearing in place.
			if (_data && winner.Data.SeatIndex.Value == _data.SeatIndex.Value) return null;

			return AnchorForSeat(winner.Data.SeatIndex.Value);
		}

		private static Transform AnchorForSeat(int seatIndex)
		{
			var mode = PokerGameMode.Instance;
			if (!mode || seatIndex < 0) return null;

			foreach (var seat in mode.Seats)
			{
				if (seat && seat.SeatIndex == seatIndex) return seat.ItemAnchor;
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
		}
	}
}
