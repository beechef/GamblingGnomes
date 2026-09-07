using System.Collections.Generic;
using DG.Tweening;
using Game.Runtime.GameMode.Poker.Mushrooms;
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
	//
	// Placeholder art: one prefab tinted with the kind's own colour. A cap per kind is a prefab field on
	// the database entry the day the models arrive.
	public class PokerMushroomPotVisual : PokerVisual
	{
		[Header("Cap")]
		[Tooltip("Placeholder cap. Tinted per kind from the table's mushroom database.")]
		[SerializeField] private GameObject _capPrefab;

		[SerializeField] private PokerMushroomDatabase _database;

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

		// How many caps each chair is already holding, so a second stake from the same player is placed
		// beside the first rather than on top of it.
		private readonly Dictionary<int, int> _capsPerSeat = new();

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
			if (!_capPrefab) return;

			var seat = FindSeat(item.OwnerClientId);
			if (!seat) return;

			var anchor = seat.MushroomAnchor;
			if (!anchor) return;

			_capsPerSeat.TryGetValue(seat.SeatIndex, out var placed);
			_capsPerSeat[seat.SeatIndex] = placed + 1;

			var cap = Instantiate(_capPrefab, anchor);
			_caps.Add(cap);

			cap.transform.localScale = Vector3.one * _capScale;
			cap.transform.localRotation = Quaternion.identity;

			Tint(cap, item.ItemTypeIndex);

			// The anchor lies flat with its forward pointing up, the same frame the cards are laid out in:
			// local X runs across the seat and local Y runs away from whoever is sitting there.
			var column = placed % Mathf.Max(1, _capsPerRow);
			var row = placed / Mathf.Max(1, _capsPerRow);
			var resting = new Vector3(
				(column - (Mathf.Max(1, _capsPerRow) - 1) * 0.5f) * _capSpacing,
				row * _rowSpacing,
				0f);

			if (!animate || _dropDuration <= 0f)
			{
				cap.transform.localPosition = resting;
				return;
			}

			cap.transform.localPosition = resting + Vector3.forward * _dropHeight;
			cap.transform.DOLocalMove(resting, _dropDuration).SetEase(_dropEase);
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

		private void Tint(GameObject cap, byte itemType)
		{
			if (!_database || !_database.TryGetEntry(itemType, out var entry)) return;

			// A property block rather than a material instance: every cap of a kind is the same material,
			// and one instance per cap on the plate is a leak nobody would notice.
			var block = new MaterialPropertyBlock();

			foreach (var renderer in cap.GetComponentsInChildren<Renderer>(true))
			{
				if (!renderer.sharedMaterial || !renderer.sharedMaterial.HasProperty(BaseColor)) continue;

				renderer.GetPropertyBlock(block);
				block.SetColor(BaseColor, entry.Color);
				renderer.SetPropertyBlock(block);
			}
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
		}

		private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
	}
}
