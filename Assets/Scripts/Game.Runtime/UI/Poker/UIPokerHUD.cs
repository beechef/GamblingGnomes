using Game.Runtime.GameMode.Poker;
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

		[Header("Result")]
		[SerializeField] private GameObject _winnerPanel;
		[SerializeField] private TextMeshProUGUI _winnerLabel;

		protected override void OnBind()
		{
			Data.Phase.OnValueChanged += HandlePhaseChanged;
			Data.Pot.OnValueChanged += HandlePotChanged;
			Data.StageId.OnValueChanged += HandleStageChanged;
			Data.LastWinnerClientId.OnValueChanged += HandleWinnerChanged;

			Refresh();
		}

		protected override void OnUnbind()
		{
			Data.Phase.OnValueChanged -= HandlePhaseChanged;
			Data.Pot.OnValueChanged -= HandlePotChanged;
			Data.StageId.OnValueChanged -= HandleStageChanged;
			Data.LastWinnerClientId.OnValueChanged -= HandleWinnerChanged;
		}

		private void HandlePhaseChanged(PokerPhase previous, PokerPhase current) => Refresh();
		private void HandlePotChanged(int previous, int current) => Refresh();
		private void HandleStageChanged(Unity.Collections.FixedString32Bytes previous, Unity.Collections.FixedString32Bytes current) => Refresh();
		private void HandleWinnerChanged(ulong previous, ulong current) => Refresh();

		private void Refresh()
		{
			if (!Data) return;

			if (_root) _root.alpha = 1f;
			if (_phaseLabel) _phaseLabel.text = Data.Phase.Value.ToString();
			if (_potLabel) _potLabel.text = Data.Pot.Value.ToString();
			if (_stageLabel) _stageLabel.text = Data.StageId.Value.ToString();

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
