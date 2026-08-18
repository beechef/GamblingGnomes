using Game.Runtime.GameMode.Poker.Abilities;
using Game.Runtime.GameMode.Poker.Modules;
using Game.Runtime.UI.Progress;
using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The ten seconds the whole table spends waiting on one person to look somebody in the face. Its own
	// panel rather than a second wording on the report view: this beat asks everyone the same question at
	// once and puts a clock on it, where the rest of the accusation is a conversation between two people.
	// The question is fixed and the clock is the drama, so nothing here is read off the accusation — there
	// is not one yet.
	public class UIPokerReportAimPanel : UIPokerView
	{
		[Header("Panel")]
		[SerializeField] private GameObject _panel;

		[Tooltip("The question, on its own line. The clock underneath keeps its own countdown — a title written into a timer's label leaves the timer with nowhere to put the seconds.")]
		[SerializeField] private TextMeshProUGUI _titleLabel;

		[SerializeField] private UITimerBar _timerBar;

		[Header("Wording")]
		[SerializeField] private string _title = "WHO IS THE CHEATER?";

		private PokerAbilityModule _module;

		protected override bool WantsTick => _panel && _panel.activeSelf;

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnBind()
		{
			_module = GameMode.FindModule<PokerAbilityModule>();
			if (_module == null) return;

			_module.ReportPhase.OnValueChanged += HandlePhaseChanged;

			Refresh();
		}

		protected override void OnUnbind()
		{
			if (_module != null) _module.ReportPhase.OnValueChanged -= HandlePhaseChanged;

			_module = null;

			if (_panel) _panel.SetActive(false);
		}

		private void HandlePhaseChanged(PokerReportPhase previous, PokerReportPhase current) => Refresh();

		private void Refresh()
		{
			var visible = _module.ReportPhase.Value == PokerReportPhase.Aiming;

			if (_panel && _panel.activeSelf != visible) _panel.SetActive(visible);
			if (!visible) return;

			if (_titleLabel) _titleLabel.text = _title;

			OnTick();
		}

		protected override void OnTick()
		{
			// The accuser's own clock, which every other seat is watching run down on them.
			if (_timerBar) _timerBar.SetTime(Data.TurnRemaining, Data.TurnNormalized);
		}
	}
}
