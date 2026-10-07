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
		[Tooltip("Handed out in order, lowest free index first. Order is meaningful: the first player at a table always wears the first colour. The four suit icons' colours: heart red, club teal, spade purple, diamond gold.")]
		[SerializeField]
		private List<Color> _colors = new()
		{
			new Color(0.831f, 0.220f, 0.220f),
			new Color(0.157f, 0.533f, 0.455f),
			new Color(0.298f, 0.220f, 0.580f),
			new Color(0.706f, 0.533f, 0.173f)
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
