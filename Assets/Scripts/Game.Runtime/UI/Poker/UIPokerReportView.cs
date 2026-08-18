using Game.Runtime.GameMode.Poker.Abilities;
using Game.Runtime.GameMode.Poker.Modules;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.UI.Progress;
using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The accusation once a name has been said: who is answering for it, and how long they have. Aiming
	// has its own panel because it asks the whole table one question; this one reports a conversation
	// between two people, and the verdict arrives as a notice the way every other announcement does.
	//
	// Up only while the report overlay runs — the stage owns the pacing and the phase, this mirrors them.
	// What the accused actually presses lives on the report action bar.
	public class UIPokerReportView : UIPokerView
	{
		[Header("Panel")]
		[SerializeField] private GameObject _panel;

		[Tooltip("Who is being asked, for everyone who is not one of the two of them.")]
		[SerializeField] private TextMeshProUGUI _promptLabel;

		[SerializeField] private UITimerBar _timerBar;

		[Header("Wording")]
		[Tooltip("{0} is the accused, {1} the accuser, {2} the blood already on the table.")]
		[SerializeField] private string _responsePrompt = "{0} ANSWERS {1} FOR {2}";

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
			_module.Accusation.OnValueChanged += HandleAccusationChanged;
			_module.ReportStake.OnValueChanged += HandleStakeChanged;

			Refresh();
		}

		protected override void OnUnbind()
		{
			if (_module != null)
			{
				_module.ReportStake.OnValueChanged -= HandleStakeChanged;
				_module.Accusation.OnValueChanged -= HandleAccusationChanged;
				_module.ReportPhase.OnValueChanged -= HandlePhaseChanged;
			}

			_module = null;

			if (_panel) _panel.SetActive(false);
		}

		private void HandlePhaseChanged(PokerReportPhase previous, PokerReportPhase current) => Refresh();
		private void HandleAccusationChanged(PokerReportAccusation previous, PokerReportAccusation current) => Refresh();
		private void HandleStakeChanged(int previous, int current) => Refresh();

		private void Refresh()
		{
			var visible = _module.ReportPhase.Value == PokerReportPhase.Response;

			if (_panel && _panel.activeSelf != visible) _panel.SetActive(visible);
			if (!visible) return;

			var accusation = _module.Accusation.Value;

			if (_promptLabel)
			{
				_promptLabel.text = string.Format(_responsePrompt,
					NameOf(accusation.TargetClientId),
					NameOf(accusation.AccuserClientId),
					_module.ReportStake.Value);
			}

			OnTick();
		}

		protected override void OnTick()
		{
			if (_timerBar) _timerBar.SetTime(Data.TurnRemaining, Data.TurnNormalized);
		}

		private string NameOf(ulong clientId)
		{
			var player = PokerPlayer.Find(clientId);
			return player ? player.DisplayName : $"Player {clientId}";
		}
	}
}
