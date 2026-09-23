using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Stages;
using Game.Runtime.UI.Button;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// Somebody went all in and this player is being asked to match it or fold. Everybody answers at once and
	// in secret, so the bar goes away the moment this player answers and says nothing about anybody else.
	// The clock is the table's stage clock, drawn by the turn panel like any other.
	//
	// Stays on an object that is always active and switches only its panel, through the same panel group the
	// bet bar uses, so it pops in and fades out the same way.
	public class UIPokerAllInBar : UIPokerView
	{
		[SerializeField] private UIPanelStateGroup _panels;
		[SerializeField] private GameObject _panel;
		[SerializeField] private UIButton _allInButton;
		[SerializeField] private UIButton _foldButton;

		private PokerAllInStage _stage;
		private bool _answered;

		protected override void OnBind()
		{
			if (_allInButton) _allInButton.OnClick += HandleAllIn;
			if (_foldButton) _foldButton.OnClick += HandleFold;

			Data.StageId.OnValueChanged += HandleStageChanged;
			Data.Phase.OnValueChanged += HandlePhaseChanged;
			Data.OnPotEntriesChanged += HandlePotEntriesChanged;
			LocalData.OnStateChanged += Refresh;

			_answered = false;
			Refresh();
		}

		protected override void OnUnbind()
		{
			LocalData.OnStateChanged -= Refresh;
			Data.OnPotEntriesChanged -= HandlePotEntriesChanged;
			Data.Phase.OnValueChanged -= HandlePhaseChanged;
			Data.StageId.OnValueChanged -= HandleStageChanged;

			if (_foldButton) _foldButton.OnClick -= HandleFold;
			if (_allInButton) _allInButton.OnClick -= HandleAllIn;

			if (_panels) _panels.HideAll();
		}

		// A new stage is a new question, so an answer given to the last one no longer hides the bar.
		private void HandleStageChanged(FixedString32Bytes previous, FixedString32Bytes current)
		{
			_answered = false;
			Refresh();
		}

		private void HandlePhaseChanged(PokerPhase previous, PokerPhase current) => Refresh();
		private void HandlePotEntriesChanged(NetworkListEvent<PokerPotEntry> change) => Refresh();

		private void Refresh()
		{
			// Resolved from the replicated stage id: GameMode.CurrentStage is only ever set on the server.
			_stage = GameMode.FindStage(Data.StageId.Value.ToString()) as PokerAllInStage;

			var show = _stage && Data.Phase.Value == PokerPhase.AllIn && !_answered && _stage.IsAsked(LocalPlayer);
			if (!_panels || _panels.IsShowing(_panel) == show) return;

			if (show) _panels.Show(_panel);
			else _panels.HideAll();
		}

		private void HandleAllIn() => Answer(PokerActionType.AllIn);
		private void HandleFold() => Answer(PokerActionType.Fold);

		private void Answer(PokerActionType action)
		{
			if (!_stage || _answered) return;

			_answered = true;
			GameMode.SubmitActionRPC(action, 0);
			Refresh();
		}
	}
}
