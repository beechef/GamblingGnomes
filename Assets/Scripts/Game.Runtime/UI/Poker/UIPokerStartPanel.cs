using Game.Runtime.GameMode.Poker;
using Game.Runtime.UI.Button;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The host's seat is the dealer's chair: only they get the button, and only once they are sitting
	// at the table with enough company to deal.
	public class UIPokerStartPanel : UIPokerView
	{
		[Header("References")]
		[SerializeField] private GameObject _panel;
		[SerializeField] private UIButton _startButton;
		[SerializeField] private TextMeshProUGUI _hintLabel;

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnBind()
		{
			if (_startButton) _startButton.OnClick += HandleStartClicked;
		}

		protected override void OnUnbind()
		{
			if (_startButton) _startButton.OnClick -= HandleStartClicked;
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnTick()
		{
			var isHost = NetworkManager.Singleton && NetworkManager.Singleton.IsHost;
			var isSeated = GameMode.FindSeatedPlayer(LocalClientId);
			var isWaiting = Data.Phase.Value == PokerPhase.Waiting;
			var visible = isHost && isSeated && isWaiting;

			if (_panel && _panel.activeSelf != visible) _panel.SetActive(visible);
			if (!visible) return;

			var seatedCount = GameMode.SeatedPlayers.Count;
			var required = GameMode.Rules ? GameMode.Rules.MinimumPlayersToStart : 2;
			var canStart = seatedCount >= required;

			if (_startButton) _startButton.IsInteractable = canStart;

			if (_hintLabel)
			{
				_hintLabel.text = canStart
					? $"{seatedCount} players seated"
					: $"Waiting for players ({seatedCount}/{required})";
			}
		}

		private void HandleStartClicked()
		{
			if (!GameMode) return;

			GameMode.RequestStartGameRPC();
		}
	}
}
