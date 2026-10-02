using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Player
{
	// The colours players are told apart by, in the order they are handed out. An index rather than a
	// colour is what travels, so this list can be restyled without a word of it reaching the network —
	// and so two clients can never disagree about who is wearing what.
	//
	// A table with more players than colours wraps rather than running out: two identical hats read badly,
	// but a hat with no colour at all reads as a bug.
	[CreateAssetMenu(fileName = "PlayerColorDatabase", menuName = "Game/Player/Color Database")]
	public class PlayerColorDatabase : ScriptableObject
	{
		[Tooltip("Handed out in order, lowest free index first. Order is meaningful: the first player at a table always wears the first colour. Matches the four bettable mushroom kinds (PokerBetItemDatabase colours): red, purple, green, yellow.")]
		[SerializeField]
		private List<Color> _colors = new()
		{
			new Color(0.82f, 0.27f, 0.26f),
			new Color(0.55f, 0.36f, 0.71f),
			new Color(0.42f, 0.72f, 0.36f),
			new Color(0.90f, 0.76f, 0.28f)
		};

		public int Count => _colors.Count;

		public Color Get(int index)
		{
			if (_colors.Count == 0) return Color.white;

			// Negative means nobody has handed this player an index yet, which is a moment rather than a
			// state — it lands as soon as the server gets round to it, and white in the meantime is quieter
			// than whatever colour zero happens to be.
			if (index < 0) return Color.white;

			return _colors[index % _colors.Count];
		}
	}
}
