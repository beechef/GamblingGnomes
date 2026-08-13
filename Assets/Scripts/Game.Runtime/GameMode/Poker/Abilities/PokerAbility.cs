using Game.Runtime.GameMode.Poker.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Abilities
{
	// One trick a player can pull, as an asset — the pool deals these the way the deck deals cards.
	// Stateless: everything an activation touches lives on the table or the player, so one asset
	// serves every player holding a copy of it.
	public abstract class PokerAbility : ScriptableObject
	{
		[Header("Ability")]
		[Tooltip("Replicated to the holder so their UI can name the card. Empty falls back to the asset name.")]
		[SerializeField] private string _abilityId;

		[SerializeField] private string _displayName;

		[Tooltip("Cheat abilities mark their user as a cheater for the round — the thing a report accuses them of.")]
		[SerializeField] private PokerAbilityKind _kind;

		public string AbilityId => string.IsNullOrEmpty(_abilityId) ? name : _abilityId;
		public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
		public PokerAbilityKind Kind => _kind;

		// Whether the trick actually happened — a fizzled ability is not consumed and taints nobody.
		public bool ActivateServer(PokerGameMode gameMode, PokerPlayer player)
		{
			if (!gameMode || !player || !player.Data) return false;

			return OnActivateServer(gameMode, player);
		}

		protected abstract bool OnActivateServer(PokerGameMode gameMode, PokerPlayer player);
	}
}
