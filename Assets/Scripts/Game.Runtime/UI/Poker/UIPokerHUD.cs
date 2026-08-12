using Game.Runtime.GameMode.Poker;
using Game.Runtime.Player;
using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	public class UIPokerHUD : UIPokerView
	{
		[Header("Table")]
		[SerializeField] private CanvasGroup _root;
		[SerializeField] private TextMeshProUGUI _phaseLabel;
		[SerializeField] private TextMeshProUGUI _potLabel;
		[SerializeField] private TextMeshProUGUI _stageLabel;

		[Header("Player")]
		[Tooltip("What the player owns, which is also what they have to bet with — there is no separate stack at the table.")]
		[SerializeField] private TextMeshProUGUI _moneyLabel;

		[Header("Result")]
		[SerializeField] private GameObject _winnerPanel;
		[SerializeField] private TextMeshProUGUI _winnerLabel;

		protected override void OnBind()
		{
			Data.Phase.OnValueChanged += HandlePhaseChanged;
			Data.Pot.OnValueChanged += HandlePotChanged;
			Data.StageId.OnValueChanged += HandleStageChanged;
			Data.LastWinnerClientId.OnValueChanged += HandleWinnerChanged;

			if (Wallet) Wallet.Money.OnValueChanged += HandleMoneyChanged;

			Refresh();
		}

		protected override void OnUnbind()
		{
			Data.Phase.OnValueChanged -= HandlePhaseChanged;
			Data.Pot.OnValueChanged -= HandlePotChanged;
			Data.StageId.OnValueChanged -= HandleStageChanged;
			Data.LastWinnerClientId.OnValueChanged -= HandleWinnerChanged;

			if (Wallet) Wallet.Money.OnValueChanged -= HandleMoneyChanged;
		}

		private PlayerData Wallet => LocalPlayer ? LocalPlayer.Wallet : null;

		private void HandlePhaseChanged(PokerPhase previous, PokerPhase current) => Refresh();
		private void HandlePotChanged(int previous, int current) => Refresh();
		private void HandleStageChanged(Unity.Collections.FixedString32Bytes previous, Unity.Collections.FixedString32Bytes current) => Refresh();
		private void HandleWinnerChanged(ulong previous, ulong current) => Refresh();
		private void HandleMoneyChanged(int previous, int current) => Refresh();

		private void Refresh()
		{
			if (!Data) return;

			if (_root) _root.alpha = 1f;
			if (_phaseLabel) _phaseLabel.text = Data.Phase.Value.ToString();
			if (_potLabel) _potLabel.text = Data.Pot.Value.ToString();
			if (_stageLabel) _stageLabel.text = Data.StageId.Value.ToString();
			if (_moneyLabel) _moneyLabel.text = Wallet ? Wallet.Money.Value.ToString() : "0";

			var showWinner = Data.Phase.Value is PokerPhase.Showdown or PokerPhase.Finished
			                 && Data.LastWinnerClientId.Value != PokerGameData.NoTurn;

			if (_winnerPanel) _winnerPanel.SetActive(showWinner);

			if (showWinner && _winnerLabel)
			{
				var winnerClientId = Data.LastWinnerClientId.Value;
				_winnerLabel.text = winnerClientId == LocalClientId ? "You win" : $"Player {winnerClientId} wins";
			}
		}
	}
}
