using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// The cards lying in front of a chair. Flat and spread wider than a hand is: these are what a player
	// reaches for, and two cards overlapping by a fan's margin are two cards a raycast cannot tell apart.
	public class PokerCardRowVisual : PokerCardGroupVisual
	{
		[Tooltip("Space left between the edges of neighbouring cards lying on the table.")]
		[MinValue(0f)]
		[SerializeField] private float _gap = 0.006f;

		[Tooltip("Places the row is spaced for however many cards have arrived, so a board dealt one card at a time lands each card on its final spot instead of the row re-centring under it. Zero spaces for the cards held.")]
		[MinValue(0)]
		[SerializeField] private int _minimumSlots;

		[Header("References")]
		[SerializeField] private PokerPlayerData _data;

		private void Awake()
		{
			if (!_data) _data = GetComponentInParent<PokerPlayerData>();
		}

		// The seat owns where the cards lie: it is authored in the chair prefab, so retuning it reaches
		// every seat at once. A body with no chair has no spot on the table at all, and its cards stay on
		// this object rather than being drawn somewhere nobody chose.
		protected override Transform ResolveAnchor()
		{
			if (!_data) return transform;

			var mode = PokerGameMode.Instance;
			if (!mode) return transform;

			foreach (var seat in mode.Seats)
			{
				if (seat && seat.SeatIndex == _data.SeatIndex.Value && seat.CardAnchor) return seat.CardAnchor;
			}

			return transform;
		}

		// Every card keeps the place it was dealt into, out of the whole hand, whichever of them are still
		// lying here. Picking some up leaves gaps, as a hand on a real table does — closing them slid the
		// rest across the wood while the picked cards were still lying there waiting their turn to fly, and
		// the two overlapped. A card put back down returns to its own place.
		protected override int SlotOf(int index) => OrderAt(index);

		protected override int SlotCount => Mathf.Max(Mathf.Max(_data ? _data.CardCount : 0, HighestOrder + 1), _minimumSlots);

		protected override Vector3 SlotPosition(int slot, int count)
		{
			// The step is measured off the card, so new art of another width keeps the same gap instead of
			// sliding the cards into each other on one plane.
			var offset = (slot - (count - 1) * 0.5f) * (CardSize.x + _gap);

			// Negative, like everything else that lifts a card: a sprite is read from its own -Z, so that is
			// the side the table anchor points at the ceiling and the direction anything coming off the
			// table has to travel. A card dealt later rests on the ones already there.
			return new Vector3(offset, 0f, -slot * DepthStep);
		}
	}
}
