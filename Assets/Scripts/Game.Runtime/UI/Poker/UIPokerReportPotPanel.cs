using Game.Runtime.GameMode.Poker.Abilities;
using Game.Runtime.GameMode.Poker.Modules;
using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The blood on the table for one accusation. Its own panel rather than a mode on the table's pot,
	// because it is a different pot in a different currency between a different set of players: two of
	// them, staking blood, over something the rest of the hand has no share in. Sharing one readout would
	// mean one number quietly meaning two things, and the icon beside it is the only thing that would say
	// which — a distinction that survives exactly until somebody glances at it.
	public class UIPokerReportPotPanel : UIPokerView
	{
		[Header("Panel")]
		[SerializeField] private GameObject _panel;

		[Tooltip("Blood, so the icon beside it is the finger and never the coin.")]
		[SerializeField] private TextMeshProUGUI _potLabel;

		[Header("Wording")]
		[SerializeField] private string _format = "Pot: {0}";

		private PokerAbilityModule _module;

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnBind()
		{
			_module = GameMode.FindModule<PokerAbilityModule>();
			if (_module == null) return;

			_module.ReportPhase.OnValueChanged += HandlePhaseChanged;
			_module.ReportPot.OnValueChanged += HandlePotChanged;

			Refresh();
		}

		protected override void OnUnbind()
		{
			if (_module != null)
			{
				_module.ReportPot.OnValueChanged -= HandlePotChanged;
				_module.ReportPhase.OnValueChanged -= HandlePhaseChanged;
			}

			_module = null;

			if (_panel) _panel.SetActive(false);
		}

		private void HandlePhaseChanged(PokerReportPhase previous, PokerReportPhase current) => Refresh();
		private void HandlePotChanged(int previous, int current) => Refresh();

		private void Refresh()
		{
			// Nothing is staked until a name has been said, so aiming shows no pot at all — the same
			// reasoning that keeps "Pot: 0" off the felt between hands.
			var visible = _module.ReportPhase.Value is PokerReportPhase.Response or PokerReportPhase.Judging or PokerReportPhase.Verdict
				&& _module.ReportPot.Value > 0;

			if (_panel && _panel.activeSelf != visible) _panel.SetActive(visible);
			if (!visible) return;

			if (_potLabel) _potLabel.text = string.Format(_format, _module.ReportPot.Value);
		}
	}
}
