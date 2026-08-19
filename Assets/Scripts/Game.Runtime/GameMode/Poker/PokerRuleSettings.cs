using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker
{
	// What the table is, not how it plays: everything about the round — durations, bet sizes, which
	// actions are on offer — belongs to the stage that runs it.
	[CreateAssetMenu(fileName = "PokerRuleSettings", menuName = "Game/Poker/Rule Settings")]
	public class PokerRuleSettings : ScriptableObject
	{
		[Header("Table")]
		[SerializeField] private int _minimumPlayersToStart = 2;

		[Tooltip("What a player must be carrying to take a chair here. A stake nobody can meet is a seat that only ever folds, and it still fills a place the table counts as ready to deal — so the door is where it is checked rather than the first street.")]
		[MinValue(1)]
		[SerializeField] private int _minimumMoneyToSit = 1;

		public int MinimumPlayersToStart => Mathf.Max(2, _minimumPlayersToStart);
		public int MinimumMoneyToSit => Mathf.Max(1, _minimumMoneyToSit);
	}
}
