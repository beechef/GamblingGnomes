using Game.Runtime.Player;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Player
{
	// The poker half of the neck stretch. The stretch itself is cosmetic — a neck across the table, on a
	// clock — and everything it means here lives on this component: whether the look actually reads the
	// hand it hangs over, held owner-read so the table sees the lean and never the strength behind it.
	// The sight is gated on the stretch everyone can see, so it can never outlast the act that paid for
	// it. It has to be a client side rule: the cards are replicated to everyone already, and the decision
	// to draw them face down is the only thing keeping them secret.
	public class PokerPeekController : NetworkBehaviour
	{
		[Header("Debug")]
		[Tooltip("Writes one line per peek saying whether this one granted sight and who it landed on. An honest card granting nothing is the bluff working, not a fault — which is exactly why the two cases are impossible to tell apart without asking.")]
		[SerializeField] private bool _logGrants = true;

		[Header("References")]
		[SerializeField] private PlayerHeadStretchController _headStretch;

		// False on the lean that is only an act — which is the whole bluff.
		[HideInInspector] public NetworkVariable<bool> PeekRevealsHand = new(false,
			readPerm: NetworkVariableReadPermission.Owner, writePerm: NetworkVariableWritePermission.Server);

		private bool _ruleInstalled;

		public override void OnNetworkSpawn()
		{
			if (!_headStretch) _headStretch = GetComponent<PlayerHeadStretchController>();
			if (!_headStretch) return;

			_headStretch.Target.OnValueChanged += HandleTargetChanged;

			// Only the peeker's own client is ever granted the sight, so only their copy installs the rule.
			if (!IsOwner) return;

			PokerPlayerData.AddHandVisibilityProvider(CanSeeHandOf);
			PeekRevealsHand.OnValueChanged += HandleRevealChanged;

			_ruleInstalled = true;
		}

		public override void OnNetworkDespawn()
		{
			if (_headStretch) _headStretch.Target.OnValueChanged -= HandleTargetChanged;

			// Ownership can pass to the server as a client leaves, so the instance that installed the rule
			// is the one that takes it back out — asking whether we are the owner is too late by now.
			if (!_ruleInstalled) return;

			_ruleInstalled = false;

			PeekRevealsHand.OnValueChanged -= HandleRevealChanged;

			PokerPlayerData.RemoveHandVisibilityProvider(CanSeeHandOf);
			PokerPlayerData.NotifyHandVisibilityRulesChanged();
		}

		// The whole peek in one call: the lean the table watches, and the grant only the peeker learns.
		public void ServerPeek(PlayerRigController target, float duration, bool revealsHand)
		{
			if (!IsServer || !_headStretch) return;

			PeekRevealsHand.Value = revealsHand;
			_headStretch.ServerStretchTo(target, duration);
		}

		private void LogGrant(string reason)
		{
			if (!_logGrants || !_ruleInstalled) return;

			var target = _headStretch ? _headStretch.TargetRig : null;

			Debug.Log($"[PokerPeekController] {reason}: revealsHand={PeekRevealsHand.Value} target={(target ? target.name : "none")}");
		}

		private bool CanSeeHandOf(PokerPlayerData other)
		{
			if (!other || !PeekRevealsHand.Value) return false;

			var target = _headStretch.TargetRig;

			// The rig and the hand belong to the same player object, which is what makes them the same
			// player without either side having to know about the other.
			return target && target.NetworkObjectId == other.NetworkObjectId;
		}

		private void HandleRevealChanged(bool previous, bool current)
		{
			LogGrant("grant changed");

			PokerPlayerData.NotifyHandVisibilityRulesChanged();
		}

		private void HandleTargetChanged(NetworkBehaviourReference previous, NetworkBehaviourReference current)
		{
			// The grant comes home with the neck. The sight is already gated on the stretch, so this only
			// keeps a stale flag from surviving into the next lean.
			if (IsServer && !current.TryGet(out PlayerRigController _) && PeekRevealsHand.Value) PeekRevealsHand.Value = false;

			LogGrant("target changed");

			if (_ruleInstalled) PokerPlayerData.NotifyHandVisibilityRulesChanged();
		}
	}
}
