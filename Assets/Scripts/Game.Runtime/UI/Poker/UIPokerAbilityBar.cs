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
	// and the report that calls somebody else's. Every gate it shows — which stage, what window — is
	// the module's replicated decision; this bar never works the config out for itself.
	public class UIPokerAbilityBar : UIPokerView
	{
		private const string NoTargetOption = "Select player...";

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
			if (_targetDropdown) _targetDropdown.onValueChanged.AddListener(HandleTargetChanged);

			_module.Enabled.OnValueChanged += HandleToggleChanged;
			_module.AbilityWindowOpen.OnValueChanged += HandleToggleChanged;
			_module.ReportWindowOpen.OnValueChanged += HandleToggleChanged;
			LocalData.OnStateChanged += Refresh;
			GameMode.OnSeatedPlayersChanged += Refresh;

			Refresh();
		}

		protected override void OnUnbind()
		{
			if (_module != null)
			{
				GameMode.OnSeatedPlayersChanged -= Refresh;
				LocalData.OnStateChanged -= Refresh;
				_module.ReportWindowOpen.OnValueChanged -= HandleToggleChanged;
				_module.AbilityWindowOpen.OnValueChanged -= HandleToggleChanged;
				_module.Enabled.OnValueChanged -= HandleToggleChanged;

				if (_targetDropdown) _targetDropdown.onValueChanged.RemoveListener(HandleTargetChanged);
				if (_reportButton) _reportButton.OnClick -= HandleReportClicked;
				if (_useButton) _useButton.OnClick -= HandleUseClicked;
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

		private void HandleToggleChanged(bool previous, bool current) => Refresh();
		private void HandleTargetChanged(int index) => Refresh();

		private void Refresh()
		{
			var active = _module != null && _module.Enabled.Value && LocalData.IsSeated;

			var holdsAbility = active && !LocalData.AbilityId.Value.IsEmpty && !LocalData.AbilityUsed.Value;
			var abilityOpen = holdsAbility && _module.AbilityWindowOpen.Value && LocalData.IsInHand;
			var reportOpen = active && _module.ReportWindowOpen.Value && LocalData.IsInHand;

			var visible = abilityOpen || reportOpen;
			if (_panel && _panel.activeSelf != visible) _panel.SetActive(visible);
			if (!visible) return;

			RefreshAbility(abilityOpen);
			RefreshReport(reportOpen);
		}

		private void RefreshAbility(bool open)
		{
			SetSectionActive(open, _abilityNameLabel ? _abilityNameLabel.gameObject : null,
				_abilityKindLabel ? _abilityKindLabel.gameObject : null,
				_useButton ? _useButton.gameObject : null);

			if (!open) return;

			var ability = _module.Pool ? _module.Pool.FindById(LocalData.AbilityId.Value.ToString()) : null;

			if (_abilityNameLabel) _abilityNameLabel.text = ability ? ability.DisplayName : LocalData.AbilityId.Value.ToString();
			if (_abilityKindLabel) _abilityKindLabel.text = ability && ability.Kind == PokerAbilityKind.Cheat ? "Cheat" : "Normal";
			if (_useButton) _useButton.IsInteractable = true;
		}

		private void RefreshReport(bool open)
		{
			SetSectionActive(open, _targetDropdown ? _targetDropdown.gameObject : null,
				_reportButton ? _reportButton.gameObject : null,
				_reportsLeftLabel ? _reportsLeftLabel.gameObject : null);

			if (!open) return;

			RebuildTargets();

			var reportsLeft = LocalData.ReportsLeft.Value;
			var hasTarget = SelectedTarget() != ulong.MaxValue;

			// Reporting takes an explicit choice — the placeholder never fires at whoever happened to
			// sort first.
			if (_reportButton) _reportButton.IsInteractable = reportsLeft > 0 && hasTarget;
			if (_targetDropdown) _targetDropdown.interactable = reportsLeft > 0;
			if (_reportsLeftLabel) _reportsLeftLabel.text = reportsLeft.ToString();
		}

		private static void SetSectionActive(bool active, params GameObject[] gameObjects)
		{
			foreach (var gameObject in gameObjects)
			{
				if (gameObject && gameObject.activeSelf != active) gameObject.SetActive(active);
			}
		}

		// Anyone dealt into the hand can be accused, folded players included — folding out does not
		// launder a cheat.
		private void RebuildTargets()
		{
			if (!_targetDropdown) return;

			var previous = SelectedTarget();

			_targetClientIds.Clear();
			_targetDropdown.ClearOptions();

			var options = new List<string> { NoTargetOption };

			foreach (var player in GameMode.SeatedPlayers)
			{
				if (!player || player.ClientId == LocalClientId) continue;
				if (!player.Data.IsInHand && player.Data.Status.Value != PokerPlayerStatus.Folded) continue;

				_targetClientIds.Add(player.ClientId);
				options.Add(player.DisplayName);
			}

			_targetDropdown.AddOptions(options);

			// An explicit earlier choice survives the rebuild; anything else lands on the placeholder.
			var keep = previous == ulong.MaxValue ? -1 : _targetClientIds.IndexOf(previous);
			_targetDropdown.SetValueWithoutNotify(keep >= 0 ? keep + 1 : 0);
		}

		private ulong SelectedTarget()
		{
			if (!_targetDropdown) return ulong.MaxValue;

			// Slot zero is the placeholder, so the real targets sit one below their option index.
			var index = _targetDropdown.value - 1;
			return index >= 0 && index < _targetClientIds.Count ? _targetClientIds[index] : ulong.MaxValue;
		}

		private void HandleUseClicked()
		{
			if (_module != null) _module.UseAbilityRPC();
		}

		private void HandleReportClicked()
		{
			var target = SelectedTarget();
			if (_module == null || target == ulong.MaxValue) return;

			_module.ReportRPC(target);

			if (_targetDropdown) _targetDropdown.SetValueWithoutNotify(0);
		}
	}
}
