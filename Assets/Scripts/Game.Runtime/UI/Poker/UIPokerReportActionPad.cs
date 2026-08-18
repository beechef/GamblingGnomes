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
	// everything either of them has. There is no fold here — an accusation cannot be waited out.
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

		[Header("Always")]
		[Tooltip("Looking at your own cards costs nothing and asks the server for nothing, so it stays lit through an accusation like it does through a street.")]
		[SerializeField] private UIButton _checkButton;

		[Tooltip("Kept in its slot and permanently dimmed. The petal exists so the four positions never move under the player's thumb; it is dimmed because an accusation cannot be waited out.")]
		[SerializeField] private UIButton _foldButton;

		[Header("Turn Timer")]
		[SerializeField] private UITimerBar _timerBar;

		[Tooltip("What this player has left to answer with — the number both sides of this are measured against.")]
		[SerializeField] private TextMeshProUGUI _bloodLabel;

		// Only the clock moves on its own, and only while this bar is the one being answered.
		protected override bool WantsTick => _panel && _panel.activeSelf;

		private PokerAbilityModule _module;

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnBind()
		{
			_module = GameMode.FindModule<PokerAbilityModule>();
			if (_module == null) return;

			if (_callButton) _callButton.OnClick += HandleCall;
			if (_allInButton) _allInButton.OnClick += HandleAllIn;
			if (_checkButton) _checkButton.OnClick += HandleCheckCards;

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

				if (_checkButton) _checkButton.OnClick -= HandleCheckCards;
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
			var accusation = _module.Accusation.Value;

			// Whose move it is comes off the turn, as everywhere else at this table.
			var visible = _module.ReportPhase.Value == PokerReportPhase.Response && IsLocalTurn;

			if (_panel && _panel.activeSelf != visible) _panel.SetActive(visible);
			if (!visible) return;

			if (_bloodLabel) _bloodLabel.text = LocalData.Health.Value.ToString();

			// The same ceiling the server clamps to, read off the same module: neither of them can be
			// offered a number the other could not cover.
			var stake = _module.ReportStake.Value;
			var accused = LocalClientId == accusation.TargetClientId;
			var allIn = _module.AllInStake(PokerPlayer.Find(accusation.AccuserClientId), LocalPlayer);

			if (_callButton) _callButton.IsInteractable = true;
			if (_callLabel) _callLabel.text = $"Call {stake}";

			// Shoving is the accused's answer to being named. Whoever did the naming has already said what
			// they think it is worth, so all that is left for them is to be matched — and a stack with
			// nothing more in it than the stake has no shove in it either.
			if (_allInButton) _allInButton.IsInteractable = accused && allIn > stake;
			if (_allInLabel) _allInLabel.text = $"All In {allIn}";

			// Never available, never moved. See the field's own note: the slot is the control scheme.
			if (_foldButton) _foldButton.IsInteractable = false;

			RefreshCheck();
			RefreshTimer();
		}

		private void RefreshCheck()
		{
			if (!_checkButton) return;

			var peek = LocalPlayer.HandPeek;
			_checkButton.IsInteractable = peek && LocalData.IsInHand;
		}

		private void HandleCheckCards()
		{
			if (LocalPlayer.HandPeek) LocalPlayer.HandPeek.TogglePeekRPC();
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
	}
}
