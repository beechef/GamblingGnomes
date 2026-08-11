using Game.Runtime.GameMode.Poker.Hands;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker
{
	[CreateAssetMenu(fileName = "PokerRuleSettings", menuName = "Game/Poker Rule Settings")]
	public class PokerRuleSettings : ScriptableObject
	{
		[Header("Hands")]
		[Tooltip("Which hands this table recognises and how they rank. Swap the asset to change the ranking wholesale.")]
		[SerializeField] private PokerHandDatabase _handDatabase;

		[Header("Table")]
		[SerializeField] private int _minimumPlayersToStart = 2;
		[SerializeField] private int _startingChips = 1000;
		[SerializeField] private int _smallBlind = 10;
		[SerializeField] private int _bigBlind = 20;

		[Header("Cards")]
		[SerializeField] private int _holeCardsPerPlayer = 2;
		[SerializeField] private int _communityCardCount = 5;

		[Header("Timing")]
		[SerializeField] private float _turnDuration = 30f;
		[SerializeField] private float _dealDuration = 1.5f;
		[SerializeField] private float _showdownDuration = 5f;

		[Header("Actions")]
		[SerializeField] private bool _allowCheckWhenNoBet = true;
		[SerializeField] private bool _allowAllIn = true;
		[SerializeField] private int _minimumRaiseMultiplier = 2;

		[Header("Timeout")]
		[Tooltip("On, a player who runs out of time checks when nothing is owed and folds otherwise. Off, they always fold.")]
		[SerializeField] private bool _timeoutChecksWhenFree = true;

		public PokerHandDatabase HandDatabase => _handDatabase;
		public int MinimumPlayersToStart => Mathf.Max(2, _minimumPlayersToStart);
		public int StartingChips => _startingChips;
		public int SmallBlind => _smallBlind;
		public int BigBlind => _bigBlind;
		public int HoleCardsPerPlayer => Mathf.Max(1, _holeCardsPerPlayer);
		public int CommunityCardCount => Mathf.Max(0, _communityCardCount);
		public float TurnDuration => _turnDuration;
		public float DealDuration => _dealDuration;
		public float ShowdownDuration => _showdownDuration;
		public bool AllowCheckWhenNoBet => _allowCheckWhenNoBet;
		public bool AllowAllIn => _allowAllIn;
		public int MinimumRaiseMultiplier => Mathf.Max(1, _minimumRaiseMultiplier);
		public bool TimeoutChecksWhenFree => _timeoutChecksWhenFree;
	}
}
