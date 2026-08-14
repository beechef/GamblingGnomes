using Game.Runtime.Player;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Player
{
	// What the glasses mean at a poker table: while they are on and their grant is more than a prop's,
	// the wearer's own client turns over that many of the board's face-down cards — and nobody else's
	// does. The glasses themselves stay a generic player piece; this is only poker's reading of them,
	// so a mode without a board can read the same wear as something else entirely.
	public class PokerBoardPeekController : NetworkBehaviour
	{
		[Header("References")]
		[SerializeField] private PlayerGlassesController _glasses;

		private bool _ruleInstalled;

		public override void OnNetworkSpawn()
		{
			if (!_glasses) _glasses = GetComponent<PlayerGlassesController>();

			// Only the wearer's own client is ever granted the sight, so only their copy installs the rule.
			if (!IsOwner || !_glasses) return;

			PokerGameData.AddCommunityVisibilityProvider(CanSeeCommunityCard);

			_glasses.IsWorn.OnValueChanged += HandleWornChanged;
			_glasses.OwnerGrant.OnValueChanged += HandleGrantChanged;

			_ruleInstalled = true;
		}

		public override void OnNetworkDespawn()
		{
			// Ownership can pass to the server as a client leaves, so the instance that installed the rule
			// is the one that takes it back out — asking whether we are the owner is too late by now.
			if (!_ruleInstalled) return;

			_ruleInstalled = false;

			_glasses.OwnerGrant.OnValueChanged -= HandleGrantChanged;
			_glasses.IsWorn.OnValueChanged -= HandleWornChanged;

			PokerGameData.RemoveCommunityVisibilityProvider(CanSeeCommunityCard);
			PokerGameData.NotifyCommunityVisibilityRulesChanged();
		}

		// The sight covers the next few cards past what the table has been shown, counted off the public
		// number — so a street opening mid-wear slides the window along rather than stacking on top of it.
		private bool CanSeeCommunityCard(int index)
		{
			if (!_glasses.IsWorn.Value) return false;

			var granted = _glasses.OwnerGrant.Value;
			if (granted <= 0) return false;

			var gameMode = PokerGameMode.Instance;
			if (!gameMode || !gameMode.Data) return false;

			return index < gameMode.Data.RevealedCommunityCards.Value + granted;
		}

		private void HandleWornChanged(bool previous, bool current) => PokerGameData.NotifyCommunityVisibilityRulesChanged();
		private void HandleGrantChanged(int previous, int current) => PokerGameData.NotifyCommunityVisibilityRulesChanged();
	}
}
