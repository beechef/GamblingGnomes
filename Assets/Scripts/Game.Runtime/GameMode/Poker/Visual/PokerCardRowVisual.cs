using Game.Runtime.GameMode.Poker.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// The cards lying in front of a chair. Flat and spread wider than a hand is: these are what a player
	// reaches for, and two cards overlapping by a fan's margin are two cards a raycast cannot tell apart.
	public class PokerCardRowVisual : PokerCardGroupVisual
	{
		[Tooltip("Gap between the cards lying on the table.")]
		[SerializeField] private float _spacing = 0.06f;

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

		protected override Vector3 SlotPosition(int slot, int count)
		{
			var offset = (slot - (count - 1) * 0.5f) * _spacing;

			// Negative, like everything else that lifts a card: a sprite is read from its own -Z, so that is
			// the side the table anchor points at the ceiling and the direction anything coming off the
			// table has to travel. A card dealt later rests on the ones already there.
			return new Vector3(offset, 0f, -slot * DepthStep);
		}
	}
}
