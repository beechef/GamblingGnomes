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

			Data.Phase.OnValueChanged += HandlePhaseChanged;
			GameMode.OnSeatedPlayersChanged += Refresh;
			LocalData.SeatIndex.OnValueChanged += HandleSeatChanged;

			Refresh();
		}

		protected override void OnUnbind()
		{
			if (_startButton) _startButton.OnClick -= HandleStartClicked;

			Data.Phase.OnValueChanged -= HandlePhaseChanged;
			GameMode.OnSeatedPlayersChanged -= Refresh;
			LocalData.SeatIndex.OnValueChanged -= HandleSeatChanged;

			if (_panel) _panel.SetActive(false);
		}

		private void HandlePhaseChanged(PokerPhase previous, PokerPhase current) => Refresh();
		private void HandleSeatChanged(int previous, int current) => Refresh();

		private void Refresh()
		{
			var isHost = NetworkManager.Singleton && NetworkManager.Singleton.IsHost;
			var visible = isHost && LocalData.IsSeated && Data.Phase.Value == PokerPhase.Waiting;

			if (_panel && _panel.activeSelf != visible) _panel.SetActive(visible);
			if (!visible) return;

			// Counted the way the server counts it, so the button is never offered for a table that would
			// refuse to deal — a seat filled by a player with nothing left to bet is not company.
			var readyCount = GameMode.FundedPlayerCount;
			var required = GameMode.Rules ? GameMode.Rules.MinimumPlayersToStart : 2;
			var canStart = readyCount >= required;

			if (_startButton) _startButton.IsInteractable = canStart;

			if (_hintLabel)
			{
				_hintLabel.text = canStart
					? $"{readyCount} players ready"
					: $"Waiting for players ({readyCount}/{required})";
			}
		}

		private void HandleStartClicked()
		{
			if (GameMode) GameMode.RequestStartGameRPC();
		}
	}
}
