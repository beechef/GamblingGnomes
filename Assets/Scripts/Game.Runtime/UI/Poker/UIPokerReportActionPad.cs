using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Abilities;
using Game.Runtime.GameMode.Poker.Modules;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.UI.Button;
using Game.Runtime.UI.Progress;
using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// What the accused is asked, which is nothing a street ever asks: somebody has put blood on the table
	// against them and the only question left is how much this answer is worth. Match it, or shove
	// everything either of them has; there is no waiting an accusation out. The same pad then asks the
	// accuser whether they will stand behind what they said, once a shove has handed the question back —
	// and that is the one moment fold is on offer here.
	//
	// Its own pad rather than a second mode on the street's. Both the verbs and the currency change — this
	// one is counted in blood — and a pad that quietly means something else under the same four petals is
	// worse than a different pad: the whole point of a fixed control scheme is that it can be learned. The
	// street pad stands down for as long as an overlay is up.
	public class UIPokerReportActionPad : UIPokerView
	{
		[Header("Panel")]
		[SerializeField] private GameObject _panel;

		[Header("Answer")]
		[SerializeField] private UIButton _callButton;
		[SerializeField] private TextMeshProUGUI _callLabel;

		[Tooltip("The accused's alone. Whoever called the report has already put their number up and can only be matched — offering them a shove would be offering to raise their own accusation.")]
		[SerializeField] private UIButton _allInButton;

		[SerializeField] private TextMeshProUGUI _allInLabel;

		[Tooltip("Kept in its slot whether or not it is offered. The accused can never use it — an accusation cannot be waited out — but the accuser can, to walk away from a shove and leave what they staked.")]
		[SerializeField] private UIButton _foldButton;

		[Header("Turn Timer")]
		[SerializeField] private UITimerBar _timerBar;

		[Tooltip("What this player has left to answer with — the number both sides of this are measured against.")]
		[SerializeField] private TextMeshProUGUI _bloodLabel;

		// Only the clock moves on its own, and only while this bar is the one being answered.
		protected override bool WantsTick => _panel && _panel.activeSelf;

		private PokerReportModule _module;

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnBind()
		{
			_module = GameMode.FindModule<PokerReportModule>();
			if (_module == null) return;

			if (_callButton) _callButton.OnClick += HandleCall;
			if (_allInButton) _allInButton.OnClick += HandleAllIn;
			if (_foldButton) _foldButton.OnClick += HandleFold;

			Data.CurrentTurnClientId.OnValueChanged += HandleTurnChanged;
			_module.ReportPhase.OnValueChanged += HandlePhaseChanged;
			_module.Accusation.OnValueChanged += HandleAccusationChanged;
			_module.ReportStake.OnValueChanged += HandleStakeChanged;
			LocalData.OnStateChanged += Refresh;

			Refresh();
		}

		protected override void OnUnbind()
		{
			if (_module != null)
			{
				LocalData.OnStateChanged -= Refresh;
				_module.ReportStake.OnValueChanged -= HandleStakeChanged;
				_module.Accusation.OnValueChanged -= HandleAccusationChanged;
				_module.ReportPhase.OnValueChanged -= HandlePhaseChanged;
				Data.CurrentTurnClientId.OnValueChanged -= HandleTurnChanged;

				if (_foldButton) _foldButton.OnClick -= HandleFold;
				if (_allInButton) _allInButton.OnClick -= HandleAllIn;
				if (_callButton) _callButton.OnClick -= HandleCall;
			}

			_module = null;

			if (_panel) _panel.SetActive(false);
		}

		private void HandleTurnChanged(ulong previous, ulong current) => Refresh();
		private void HandlePhaseChanged(PokerReportPhase previous, PokerReportPhase current) => Refresh();
		private void HandleAccusationChanged(PokerReportAccusation previous, PokerReportAccusation current) => Refresh();
		private void HandleStakeChanged(int previous, int current) => Refresh();

		private void Refresh()
		{
			// Whose move it is comes off the turn, as everywhere else at this table.
			var visible = _module.ReportPhase.Value == PokerReportPhase.Response && IsLocalTurn;

			if (_panel && _panel.activeSelf != visible) _panel.SetActive(visible);
			if (!visible) return;

			if (_bloodLabel) _bloodLabel.text = LocalData.Health.Value.ToString();

			// Every number and every permission comes off the module — the same code the server settles
			// with — so what this pad lights and what the RPC accepts cannot drift apart.
			var stake = _module.ReportStake.Value;
			var allIn = _module.CurrentAllInStake();

			if (_callButton) _callButton.IsInteractable = true;
			if (_callLabel) _callLabel.text = $"Call {stake}";

			if (_allInButton) _allInButton.IsInteractable = _module.CanReportAllIn(LocalClientId);
			if (_allInLabel) _allInLabel.text = $"All In {allIn}";

			if (_foldButton) _foldButton.IsInteractable = _module.CanReportFold(LocalClientId);

			RefreshTimer();
		}



		private void RefreshTimer()
		{
			if (_timerBar) _timerBar.SetTime(Data.TurnRemaining, Data.TurnNormalized);
		}

		protected override void OnTick() => RefreshTimer();

		private void Submit(PokerActionType action)
		{
			if (GameMode) GameMode.SubmitActionRPC(action, 0);
		}

		private void HandleCall() => Submit(PokerActionType.Call);
		private void HandleAllIn() => Submit(PokerActionType.AllIn);
		private void HandleFold() => Submit(PokerActionType.Fold);
	}
}
