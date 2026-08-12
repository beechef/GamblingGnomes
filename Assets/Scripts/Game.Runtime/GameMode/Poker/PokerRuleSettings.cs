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
		[SerializeField] private int _startingChips = 1000;

		public int MinimumPlayersToStart => Mathf.Max(2, _minimumPlayersToStart);
		public int StartingChips => _startingChips;
	}
}
