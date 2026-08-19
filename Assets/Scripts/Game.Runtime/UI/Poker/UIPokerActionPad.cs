using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Modules;
using Game.Runtime.GameMode.Poker.Stages;
using Game.Runtime.UI.Button;
using TMPro;
using Unity.Collections;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The seat's four verbs on one pad — report, call, check the cards in hand, fold — each carrying its
	// own key. It replaces the call bar and stays up for the whole hand rather than only on our turn,
	// because the petals are also where the bindings live, and a key that only exists while it is your
	// turn can never teach anyone it exists. What this moment does not allow is dimmed, not hidden: the
	// pad is the mode's fixed control scheme, and a cluster that reshapes itself cannot be learned.
	public class UIPokerActionPad : UIPokerView
	{
		[Header("Panel")]
		[SerializeField] private GameObject _panel;

		[Header("Buttons")]
		[Tooltip("Hidden entirely in a mode without the ability game — not here, rather than not now.")]
		[SerializeField] private UIButton _reportButton;

		[SerializeField] private UIButton _callButton;

		[Tooltip("Shares the call petal's slot: a call the player cannot fully cover is a different move with a different name, so it is a different button rather than the same one wearing another word. Exactly one of the two is ever up.")]
		[SerializeField] private UIButton _allInButton;

		[SerializeField] private UIButton _checkButton;
		[SerializeField] private UIButton _foldButton;

		[Header("Labels")]
		[SerializeField] private TextMeshProUGUI _callLabel;
		[SerializeField] private TextMeshProUGUI _allInLabel;

		private PokerAbilityModule _module;

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnBind()
		{
			_module = GameMode.FindModule<PokerAbilityModule>();

			if (_reportButton) _reportButton.OnClick += HandleReport;
			if (_callButton) _callButton.OnClick += HandleCall;
			if (_allInButton) _allInButton.OnClick += HandleAllIn;
			if (_checkButton) _checkButton.OnClick += HandleCheckCards;
			if (_foldButton) _foldButton.OnClick += HandleFold;

			if (LocalPlayer.HandPeek) LocalPlayer.HandPeek.IsPeeking.OnValueChanged += HandlePeekingChanged;

			Data.CurrentTurnClientId.OnValueChanged += HandleTurnChanged;
			Data.CurrentBet.OnValueChanged += HandleBetChanged;
			Data.StageId.OnValueChanged += HandleStageChanged;
			Data.OverlayStageId.OnValueChanged += HandleStageChanged;
			LocalData.OnStateChanged += Refresh;
			GameMode.OnSeatedPlayersChanged += Refresh;

			if (_module != null)
			{
				_module.Enabled.OnValueChanged += HandleModuleToggled;
				_module.ReportWindowOpen.OnValueChanged += HandleModuleToggled;
			}

			Refresh();
		}

		protected override void OnUnbind()
		{
			if (_module != null)
			{
				_module.ReportWindowOpen.OnValueChanged -= HandleModuleToggled;
				_module.Enabled.OnValueChanged -= HandleModuleToggled;
			}

			GameMode.OnSeatedPlayersChanged -= Refresh;
			LocalData.OnStateChanged -= Refresh;
			Data.OverlayStageId.OnValueChanged -= HandleStageChanged;
			Data.StageId.OnValueChanged -= HandleStageChanged;
			Data.CurrentBet.OnValueChanged -= HandleBetChanged;
			Data.CurrentTurnClientId.OnValueChanged -= HandleTurnChanged;

			if (LocalPlayer.HandPeek) LocalPlayer.HandPeek.IsPeeking.OnValueChanged -= HandlePeekingChanged;

			if (_foldButton) _foldButton.OnClick -= HandleFold;
			if (_checkButton) _checkButton.OnClick -= HandleCheckCards;
			if (_allInButton) _allInButton.OnClick -= HandleAllIn;
			if (_callButton) _callButton.OnClick -= HandleCall;
			if (_reportButton) _reportButton.OnClick -= HandleReport;

			_module = null;

			if (_panel) _panel.SetActive(false);
		}

		private void HandlePeekingChanged(bool previous, bool current) => RefreshCheck();
		private void HandleTurnChanged(ulong previous, ulong current) => Refresh();
		private void HandleBetChanged(int previous, int current) => Refresh();
		private void HandleStageChanged(FixedString32Bytes previous, FixedString32Bytes current) => Refresh();
		private void HandleModuleToggled(bool previous, bool current) => Refresh();

		private void Refresh()
		{
			// An overlay asks a different question in a different currency and brings its own pad, so this
			// one leaves rather than sitting underneath it greyed out — two pads up at once is two control
			// schemes up at once, and neither of them is learnable.
			var visible = LocalData.IsSeated && LocalData.IsInHand && Data.OverlayStageId.Value.IsEmpty;
			if (_panel && _panel.activeSelf != visible) _panel.SetActive(visible);
			if (!visible) return;

			RefreshStreet();
			RefreshReport();
			RefreshCheck();
		}

		// Call and fold answer the street's question, so they light only on our call-only turn — on any
		// other street the full action bar owns those verbs, and two lit folds would race each other.
		private void RefreshStreet()
		{
			var stage = GameMode.FindStage(Data.StageId.Value.ToString()) as PokerBettingStage;
			var ourTurn = stage && stage.IsCallOnly && IsLocalTurn && LocalData.CanAct && Data.OverlayStageId.Value.IsEmpty;

			var owed = ourTurn ? Mathf.Max(0, Data.CurrentBet.Value - LocalData.Bet.Value) : 0;
			var chips = LocalData.Chips;

			// One petal or the other, never one petal meaning two things. Off our turn there is no price to
			// be short of, so the slot rests on Call — the pad reads the same when nothing is happening.
			var allIn = chips > 0 && owed >= chips;

			ShowPetal(_callButton, !allIn, ourTurn && owed > 0);
			ShowPetal(_allInButton, allIn, ourTurn);

			if (_callLabel) _callLabel.text = owed > 0 ? $"Call +{owed}" : "Call";
			if (_allInLabel) _allInLabel.text = $"All In {chips}";

			if (_foldButton) _foldButton.IsInteractable = ourTurn;
		}

		// Present decides which move this slot is offering; interactable decides whether it can be made
		// now. Kept apart because they are different sentences — "not this move" and "not this moment".
		private static void ShowPetal(UIButton button, bool present, bool interactable)
		{
			if (!button) return;

			if (button.gameObject.activeSelf != present) button.gameObject.SetActive(present);
			if (present) button.IsInteractable = interactable;
		}

		private void RefreshReport()
		{
			var exists = _module != null;
			if (_reportButton && _reportButton.gameObject.activeSelf != exists) _reportButton.gameObject.SetActive(exists);
			if (!exists) return;

			// Folding is not a gag order — a player out of the hand watched the same table everyone else
			// did, and is still owed the chance to say what they saw.
			var open = _module.Enabled.Value && _module.ReportWindowOpen.Value &&
				LocalData.IsSeated && LocalData.ReportsLeft.Value > 0;

			if (_reportButton) _reportButton.IsInteractable = open;
		}

		// Peeking is free — it costs nothing and asks the server for nothing but the pose — so the only
		// gate is having cards to look at. Selected while the cards are up, so the petal itself says
		// which way the next press flips them.
		private void RefreshCheck()
		{
			if (!_checkButton) return;

			var peek = LocalPlayer.HandPeek;

			_checkButton.IsInteractable = peek && LocalData.IsInHand;
			// _checkButton.IsSelected = peek && peek.IsPeeking.Value;
		}

		// The tap names nobody. It stands the accuser up with an arm out and hands them the table to look
		// at — who it lands on is decided by where they look, not by a list they picked off a menu.
		private void HandleReport()
		{
			if (_module != null) _module.ReportRPC();
		}

		private void HandleCheckCards()
		{
			if (LocalPlayer.HandPeek) LocalPlayer.HandPeek.TogglePeekRPC();
		}

		private void HandleCall()
		{
			if (GameMode) GameMode.SubmitActionRPC(PokerActionType.Call, 0);
		}

		private void HandleAllIn()
		{
			// Priced in rather than choosing to shove, which is why the street's ban on raising does not
			// refuse it — the price was already standing and this player simply cannot cover it. Asked for as
			// AllIn rather than Call so the table's notice announces the move that actually happened.
			if (GameMode) GameMode.SubmitActionRPC(PokerActionType.AllIn, 0);
		}

		private void HandleFold()
		{
			if (GameMode) GameMode.SubmitActionRPC(PokerActionType.Fold, 0);
		}
	}
}
