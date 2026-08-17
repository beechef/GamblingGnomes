using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The corner that answers "how am I doing" at a glance: the money this player can stake and the
	// health they have left. Both are read straight off the local player's data — this panel computes
	// nothing and stays up for the whole session, because those two numbers never stop mattering.
	public class UIPokerVitalsPanel : UIPokerView
	{
		[Header("Panel")]
		[SerializeField] private GameObject _panel;

		[Header("Labels")]
		[SerializeField] private TextMeshProUGUI _moneyLabel;
		[SerializeField] private TextMeshProUGUI _healthLabel;

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnBind()
		{
			LocalData.OnStateChanged += Refresh;

			Refresh();
		}

		protected override void OnUnbind()
		{
			LocalData.OnStateChanged -= Refresh;

			if (_panel) _panel.SetActive(false);
		}

		private void Refresh()
		{
			if (_panel && !_panel.activeSelf) _panel.SetActive(true);

			if (_moneyLabel) _moneyLabel.text = LocalData.Chips.ToString();
			if (_healthLabel) _healthLabel.text = LocalData.Health.Value.ToString();
		}
	}
}
