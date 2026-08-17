using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Abilities
{
	// The abilities a table deals from. Content only — how many go out and to whom is the module's
	// business, so two tables can share one pool and deal it very differently.
	[CreateAssetMenu(fileName = "PokerAbilityPool", menuName = "Game/Poker/Ability Pool")]
	public class PokerAbilityPool : ScriptableObject
	{
		[Tooltip("Archetypes, not copies — one entry can be dealt to any number of seats in the same round.")]
		[SerializeField] private List<PokerAbility> _abilities = new();

		private readonly List<PokerAbility> _cheats = new();
		private readonly List<PokerAbility> _normals = new();

		public IReadOnlyList<PokerAbility> Abilities => _abilities;

		public PokerAbility FindById(string abilityId)
		{
			if (string.IsNullOrEmpty(abilityId)) return null;

			foreach (var ability in _abilities)
			{
				if (ability && ability.AbilityId == abilityId) return ability;
			}

			return null;
		}

		// One handful per seat, empty chairs included — a chair's cards nobody holds is what keeps the
		// count of cheats in circulation from giving away who drew them. Exactly guaranteedCheats of
		// them are cheats, as far as the pool can supply the kinds asked for.
		public void DrawDeal(int cardCount, int guaranteedCheats, List<PokerAbility> buffer)
		{
			buffer.Clear();
			if (cardCount <= 0) return;

			SplitByKind();

			var cheats = Mathf.Clamp(guaranteedCheats, 0, cardCount);

			for (var i = 0; i < cheats && _cheats.Count > 0; i++)
			{
				buffer.Add(_cheats[Random.Range(0, _cheats.Count)]);
			}

			while (buffer.Count < cardCount)
			{
				// A pool with no normal abilities left to offer falls back to cheats rather than
				// dealing short — every chair gets a card.
				var source = _normals.Count > 0 ? _normals : _cheats;
				if (source.Count == 0) break;

				buffer.Add(source[Random.Range(0, source.Count)]);
			}

			Shuffle(buffer);
		}

		private void SplitByKind()
		{
			_cheats.Clear();
			_normals.Clear();

			foreach (var ability in _abilities)
			{
				if (!ability) continue;

				if (ability.Kind == PokerAbilityKind.Cheat) _cheats.Add(ability);
				else _normals.Add(ability);
			}
		}

		private static void Shuffle(List<PokerAbility> list)
		{
			for (var i = list.Count - 1; i > 0; i--)
			{
				var j = Random.Range(0, i + 1);
				(list[i], list[j]) = (list[j], list[i]);
			}
		}
	}
}
