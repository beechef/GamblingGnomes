using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker
{
	// What the table is, not how it plays: everything about the round — durations, stake sizes, which
	// actions are on offer — belongs to the stage that runs it.
	[CreateAssetMenu(fileName = "PokerRuleSettings", menuName = "Game/Poker/Rule Settings")]
	public class PokerRuleSettings : ScriptableObject
	{
		[Header("Table")]
		[SerializeField] private int _minimumPlayersToStart = 2;

		[Tooltip("How many chairs this table is laid with, and so the most players it holds. A property of the table rather than of the lobby: the room screen no longer offers it.")]
		[PropertyRange(2, 6)]
		[SerializeField] private int _seatCount = 4;

		public int MinimumPlayersToStart => Mathf.Max(2, _minimumPlayersToStart);
		public int SeatCount => Mathf.Clamp(_seatCount, 2, 6);
	}
}
