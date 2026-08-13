using System.Collections.Generic;
using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Abilities;
using Game.Runtime.GameMode.Poker.Modules;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.UI.Button;
using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The ability game's corner of the HUD: the card this player was dealt, the button that plays it,
	// and the report that calls somebody else's. Everything it shows comes off the module — this bar
	// never decides anything, it only asks.
	public class UIPokerAbilityBar : UIPokerView
	{
		[Header("Panel")]
		[SerializeField] private GameObject _panel;

		[Header("Ability")]
		[SerializeField] private TextMeshProUGUI _abilityNameLabel;

		[Tooltip("Only the holder ever sees the kind — knowing your own card is a cheat is what makes playing it a decision.")]
		[SerializeField] private TextMeshProUGUI _abilityKindLabel;

		[SerializeField] private UIButton _useButton;

		[Header("Report")]
		[SerializeField] private TMP_Dropdown _targetDropdown;
		[SerializeField] private UIButton _reportButton;
		[SerializeField] private TextMeshProUGUI _reportsLeftLabel;

		private readonly List<ulong> _targetClientIds = new();

		private PokerAbilityModule _module;

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnBind()
		{
			_module = FindModule();
			if (_module == null) return;

			if (_useButton) _useButton.OnClick += HandleUseClicked;
			if (_reportButton) _reportButton.OnClick += HandleReportClicked;

			_module.OnLocalStateChanged += Refresh;
			_module.Enabled.OnValueChanged += HandleEnabledChanged;
			GameMode.OnSeatedPlayersChanged += Refresh;

			Refresh();
		}

		protected override void OnUnbind()
		{
			if (_module != null)
			{
				if (_useButton) _useButton.OnClick -= HandleUseClicked;
				if (_reportButton) _reportButton.OnClick -= HandleReportClicked;

				GameMode.OnSeatedPlayersChanged -= Refresh;
				_module.Enabled.OnValueChanged -= HandleEnabledChanged;
				_module.OnLocalStateChanged -= Refresh;
			}

			_module = null;

			if (_panel) _panel.SetActive(false);
		}

		private PokerAbilityModule FindModule()
		{
			foreach (var module in GameMode.Modules)
			{
				if (module is PokerAbilityModule abilityModule) return abilityModule;
			}

			return null;
		}

		private void HandleEnabledChanged(bool previous, bool current) => Refresh();

		private void Refresh()
		{
			var visible = _module != null && _module.Enabled.Value && LocalData.IsSeated;

			if (_panel && _panel.activeSelf != visible) _panel.SetActive(visible);
			if (!visible) return;

			RefreshAbility();
			RefreshReport();
		}

		private void RefreshAbility()
		{
			var holds = _module.HasLocalAbility && !_module.LocalAbilityUsed;

			if (_abilityNameLabel) _abilityNameLabel.text = holds ? _module.LocalAbilityName : "-";

			if (_abilityKindLabel)
			{
				_abilityKindLabel.text = holds
					? _module.LocalAbilityKind == PokerAbilityKind.Cheat ? "Cheat" : "Normal"
					: string.Empty;
			}

			if (_useButton) _useButton.IsInteractable = holds && LocalData.IsInHand;
		}

		private void RefreshReport()
		{
			RebuildTargets();

			var canReport = _module.LocalReportsLeft > 0 && LocalData.IsInHand && _targetClientIds.Count > 0;

			if (_reportButton) _reportButton.IsInteractable = canReport;
			if (_targetDropdown) _targetDropdown.interactable = canReport;
			if (_reportsLeftLabel) _reportsLeftLabel.text = _module.LocalReportsLeft.ToString();
		}

		// Anyone dealt into the hand can be accused, folded players included — folding out does not
		// launder a cheat.
		private void RebuildTargets()
		{
			if (!_targetDropdown) return;

			var previous = SelectedTarget();

			_targetClientIds.Clear();
			_targetDropdown.ClearOptions();

			var options = new List<string>();

			foreach (var player in GameMode.SeatedPlayers)
			{
				if (!player || player.ClientId == LocalClientId) continue;
				if (!player.Data.IsInHand && player.Data.Status.Value != PokerPlayerStatus.Folded) continue;

				_targetClientIds.Add(player.ClientId);
				options.Add(player.DisplayName);
			}

			_targetDropdown.AddOptions(options);

			var keep = _targetClientIds.IndexOf(previous);
			if (keep >= 0) _targetDropdown.SetValueWithoutNotify(keep);
		}

		private ulong SelectedTarget()
		{
			if (!_targetDropdown) return ulong.MaxValue;

			var index = _targetDropdown.value;
			return index >= 0 && index < _targetClientIds.Count ? _targetClientIds[index] : ulong.MaxValue;
		}

		private void HandleUseClicked()
		{
			if (_module != null) _module.UseAbilityRPC();
		}

		private void HandleReportClicked()
		{
			var target = SelectedTarget();
			if (_module != null && target != ulong.MaxValue) _module.ReportRPC(target);
		}
	}
}
