using Game.Runtime.Player;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Player
{
	// The poker half of the glasses. The glasses themselves are cosmetic — a gesture, a prop, a clock —
	// and everything they mean at this table lives here: how many face-down board cards the wearer's own
	// client turns over, held owner-read so the table sees the act and never the strength behind it. The
	// sight is gated on the wear everyone can see, so it can never outlast the act that paid for it.
	public class PokerBoardPeekController : NetworkBehaviour
	{
		[Header("References")]
		[SerializeField] private PlayerGlassesController _glasses;

		// Zero on the pair that is only a prop — which is the whole bluff.
		[HideInInspector] public NetworkVariable<int> PeekCards = new(0,
			readPerm: NetworkVariableReadPermission.Owner, writePerm: NetworkVariableWritePermission.Server);

		private bool _ruleInstalled;

		public override void OnNetworkSpawn()
		{
			if (!_glasses) _glasses = GetComponent<PlayerGlassesController>();
			if (!_glasses) return;

			_glasses.IsWorn.OnValueChanged += HandleWornChanged;

			// Only the wearer's own client is ever granted the sight, so only their copy installs the rule.
			if (!IsOwner) return;

			PokerGameData.AddCommunityVisibilityProvider(CanSeeCommunityCard);
			PeekCards.OnValueChanged += HandleGrantChanged;

			_ruleInstalled = true;
		}

		public override void OnNetworkDespawn()
		{
			if (_glasses) _glasses.IsWorn.OnValueChanged -= HandleWornChanged;

			// Ownership can pass to the server as a client leaves, so the instance that installed the rule
			// is the one that takes it back out — asking whether we are the owner is too late by now.
			if (!_ruleInstalled) return;

			_ruleInstalled = false;

			PeekCards.OnValueChanged -= HandleGrantChanged;

			PokerGameData.RemoveCommunityVisibilityProvider(CanSeeCommunityCard);
			PokerGameData.NotifyCommunityVisibilityRulesChanged();
		}

		// The whole peek in one call: the cosmetic wear the table watches, and the grant only the wearer
		// learns. The ability hands over zero for the card that is nothing but the act.
		public void ServerPeek(float duration, int cardCount)
		{
			if (!IsServer || !_glasses) return;

			PeekCards.Value = Mathf.Max(0, cardCount);
			_glasses.ServerWear(duration);
		}

		// The sight covers the next few cards past what the table has been shown, counted off the public
		// number — so a street opening mid-wear slides the window along rather than stacking on top of it.
		private bool CanSeeCommunityCard(int index)
		{
			if (!_glasses.IsWorn.Value) return false;

			var granted = PeekCards.Value;
			if (granted <= 0) return false;

			var gameMode = PokerGameMode.Instance;
			if (!gameMode || !gameMode.Data) return false;

			return index < gameMode.Data.RevealedCommunityCards.Value + granted;
		}

		private void HandleWornChanged(bool previous, bool current)
		{
			// The grant comes off with the glasses. The sight is already gated on the wear, so this only
			// keeps a stale number from surviving into somebody's next pair.
			if (IsServer && !current && PeekCards.Value != 0) PeekCards.Value = 0;

			if (_ruleInstalled) PokerGameData.NotifyCommunityVisibilityRulesChanged();
		}

		private void HandleGrantChanged(int previous, int current) => PokerGameData.NotifyCommunityVisibilityRulesChanged();
	}
}
