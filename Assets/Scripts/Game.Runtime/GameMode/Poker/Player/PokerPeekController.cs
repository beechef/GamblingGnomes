using Game.Runtime.Player;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Player
{
	// The sight half of a peek. While this player's head is over somebody else's cards and the card they
	// played was the one that reads them, their own client — and nobody else's — turns that hand face up.
	// It has to be a client side rule: the cards are replicated to everyone already, so the only thing
	// keeping a hand secret is the decision to draw it face down, and this is where that decision is made.
	//
	// The sight is gated on the lean everyone can see rather than timed on its own, so it can never
	// outlast the act that paid for it.
	public class PokerPeekController : NetworkBehaviour
	{
		[Header("References")]
		[SerializeField] private PokerPlayerData _data;
		[SerializeField] private PlayerHeadStretchController _headStretch;

		private bool _ruleInstalled;

		public override void OnNetworkSpawn()
		{
			if (!_data) _data = GetComponent<PokerPlayerData>();
			if (!_headStretch) _headStretch = GetComponent<PlayerHeadStretchController>();

			// Only the peeker's own client is ever granted the sight, so only their copy installs the rule.
			if (!IsOwner || !_data || !_headStretch) return;

			PokerPlayerData.AddHandVisibilityProvider(CanSeeHandOf);

			_data.PeekRevealsHand.OnValueChanged += HandleRevealChanged;
			_headStretch.Target.OnValueChanged += HandleTargetChanged;

			_ruleInstalled = true;
		}

		public override void OnNetworkDespawn()
		{
			// Ownership can pass to the server as a client leaves, so the instance that installed the rule
			// is the one that takes it back out — asking whether we are the owner is too late by now.
			if (!_ruleInstalled) return;

			_ruleInstalled = false;

			_headStretch.Target.OnValueChanged -= HandleTargetChanged;
			_data.PeekRevealsHand.OnValueChanged -= HandleRevealChanged;

			PokerPlayerData.RemoveHandVisibilityProvider(CanSeeHandOf);
			PokerPlayerData.NotifyHandVisibilityRulesChanged();
		}

		private bool CanSeeHandOf(PokerPlayerData other)
		{
			if (!other || !_data.PeekRevealsHand.Value) return false;

			var target = _headStretch.TargetRig;

			// The rig and the hand belong to the same player object, which is what makes them the same
			// player without either side having to know about the other.
			return target && target.NetworkObjectId == other.NetworkObjectId;
		}

		private void HandleRevealChanged(bool previous, bool current) => PokerPlayerData.NotifyHandVisibilityRulesChanged();

		private void HandleTargetChanged(NetworkBehaviourReference previous, NetworkBehaviourReference current)
		{
			PokerPlayerData.NotifyHandVisibilityRulesChanged();
		}
	}
}
