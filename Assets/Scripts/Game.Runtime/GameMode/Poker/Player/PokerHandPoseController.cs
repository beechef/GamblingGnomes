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

			_animatorStates.ServerSetBool(_holdingCardsParameter, HoldingAnything());
		}

		private bool HoldingAnything()
		{
			for (var slot = 0; slot < _data.CardCount; slot++)
			{
				if (_data.IsHoleCardInHand(slot)) return true;
			}

			return false;
		}
	}
}
