using Game.Runtime.Player;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Player
{
	// Holding cards is a pose, and whether a player is holding any is already on the wire — so it is
	// derived rather than toggled. The button that used to ask for it was a second answer to a question
	// the cards themselves answer: a player with three cards up is holding three cards, and one with an
	// empty table in front of them has their hands down on it.
	//
	// This is the poker half. What the flag means lives here; putting it on a rig is
	// PlayerAnimatorStateController's job, and that piece knows nothing about cards.
	public class PokerHandPoseController : NetworkBehaviour
	{
		[Header("Animator")]
		[Tooltip("Bool parameter set while this player is holding any card off the table. A controller without it is skipped, so the pose can be driven before the art lands.")]
		[SerializeField] private string _holdingCardsParameter = "IsHaveCardOnHand";

		[Tooltip("Bool parameter set while this hand is turned over for the table. Putting cards down to show them is a throw, not the same lay-down a player makes when they are done looking, so the animator takes a different way out of holding them.")]
		[SerializeField] private string _showingCardsParameter = "IsShowingCards";

		[Header("References")]
		[SerializeField] private PokerPlayerData _data;

		[Required]
		[SerializeField] private PlayerAnimatorStateController _animatorStates;

		public override void OnNetworkSpawn()
		{
			if (!_data) _data = GetComponentInParent<PokerPlayerData>();
			if (!IsServer || !_data) return;

			_data.OnHoleCardPresentationChanged += Refresh;
			_data.OnHoleCardsChanged += HandleHoleCardsChanged;

			Refresh();
		}

		public override void OnNetworkDespawn()
		{
			if (!IsServer || !_data) return;

			_data.OnHoleCardsChanged -= HandleHoleCardsChanged;
			_data.OnHoleCardPresentationChanged -= Refresh;
		}

		// Both, because putting the cards down writes two replicated changes — the hand itself and where
		// it is — and which arrives first is not decided.
		private void HandleHoleCardsChanged(NetworkListEvent<CardData> change) => Refresh();

		private void Refresh()
		{
			if (!_animatorStates || !_data) return;

			// The showing flag first: both flags land in the same replicated write, but the animator reads
			// its transitions in a set order, and a frame where holding had gone false while showing was not
			// yet true would take the ordinary lay-down instead.
			_animatorStates.ServerSetBool(_showingCardsParameter, _data.HandRevealed.Value);
			_animatorStates.ServerSetBool(_holdingCardsParameter, _data.IsHoldingCards);
		}
	}
}
