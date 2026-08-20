using Game.Runtime.GameMode.Poker;
using Game.Runtime.UI.Button;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// Looking at your own cards is not one of the street's answers, so it does not belong on the pad that
	// carries them. It answers to the chair instead: it stays on screen for as long as the player is
	// sitting at this table — before the deal, between hands, underneath an accusation — where living
	// inside a pad that hides on every one of those took it away exactly when there was nothing else to
	// do but look. It goes dark rather than away once there is no hand to look at, which is what a fold
	// leaves behind.
	//
	// Its own view for that reason: the pads come and go with the moment, this answers only to the chair.
	public class UIPokerCheckPanel : UIPokerView
	{
		[Header("Panel")]
		[Tooltip("A child, never this view's own GameObject — a view that switches itself off can never hear the event that would wake it.")]
		[Required]
		[SerializeField] private GameObject _panel;

		[Header("References")]
		[Required]
		[SerializeField] private UIButton _checkButton;

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnBind()
		{
			if (_checkButton) _checkButton.OnClick += HandleCheckCards;

			LocalData.OnStateChanged += Refresh;

			// Subscribed on its own because the cards are their own event: OnStateChanged speaks for the
			// status, and folding writes the status and clears the hand as two separate replicated changes
			// whose order of arrival is not decided. Hearing only the first leaves the button lit over a
			// hand that has already been mucked.
			LocalData.OnHoleCardsChanged += HandleHoleCardsChanged;

			Data.Phase.OnValueChanged += HandlePhaseChanged;

			Refresh();
		}

		protected override void OnUnbind()
		{
			if (Data) Data.Phase.OnValueChanged -= HandlePhaseChanged;

			if (LocalData)
			{
				LocalData.OnHoleCardsChanged -= HandleHoleCardsChanged;
				LocalData.OnStateChanged -= Refresh;
			}

			if (_checkButton) _checkButton.OnClick -= HandleCheckCards;

			if (_panel) _panel.SetActive(false);
		}

		private void HandlePhaseChanged(PokerPhase previous, PokerPhase current) => Refresh();

		private void HandleHoleCardsChanged(NetworkListEvent<CardData> change) => Refresh();

		private void Refresh()
		{
			// Standing up is the only thing that takes it away. Every other moment — no cards yet, cards
			// mucked, an overlay up — is a moment the player is still sitting at this table.
			var visible = LocalData.IsSeated;
			if (_panel && _panel.activeSelf != visible) _panel.SetActive(visible);
			if (!visible) return;

			// Peeking is free and asks the server for nothing but the pose, so the only gate is having
			// something to look at. Folding takes that away by mucking the hand, which is what stops a
			// player who is out of the hand from going on holding nothing up to their face.
			var peek = LocalPlayer.HandPeek;

			if (_checkButton) _checkButton.IsInteractable = peek && LocalData.IsInHand && LocalData.CardCount > 0;
		}

		private void HandleCheckCards()
		{
			if (LocalPlayer.HandPeek) LocalPlayer.HandPeek.TogglePeekRPC();
		}
	}
}
