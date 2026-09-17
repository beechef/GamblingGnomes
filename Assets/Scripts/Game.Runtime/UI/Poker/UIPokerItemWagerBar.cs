using Unity.Collections;
using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Stages;
using Game.Runtime.UI.Button;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// What the player can do on their own wager turn: bet, use an item, or fold. Betting does not put a cap
	// up by itself — it opens the picker, because which kind is the actual decision — and the two are panels
	// of one group, so the menu and the picker are never up at once and neither is up off the turn.
	//
	// This component stays on an object that is always active and only switches the panels, or it would
	// switch itself off with the menu and never hear the turn come round again.
	public class UIPokerItemWagerBar : UIPokerView
	{
		[Header("Panels")]
		[SerializeField] private UIPanelStateGroup _panels;
		[SerializeField] private GameObject _menuPanel;
		[SerializeField] private GameObject _pickerPanel;
		[SerializeField] private UIPokerBetPicker _picker;

		[Header("Menu")]
		[SerializeField] private UIButton _betButton;

		[Tooltip("Shown only on the wager that allows it. What the rules forbid is hidden, not greyed.")]
		[SerializeField] private UIButton _foldButton;

		[Header("Overlays")]
		[Tooltip("Optional. While the hand board is open the bar steps aside, and the turn comes back to the menu when it closes.")]
		[SerializeField] private UIPokerHandHelper _handHelper;

		private PokerItemWagerStage _stage;

		private void Awake()
		{
			if (_panels) _panels.HideAll();
		}

		protected override void OnBind()
		{
			if (_betButton) _betButton.OnClick += HandleBet;
			if (_foldButton) _foldButton.OnClick += HandleFold;
			if (_handHelper) _handHelper.OnOpenChanged += HandleHandHelperOpenChanged;

			Data.CurrentTurnClientId.OnValueChanged += HandleTurnChanged;
			Data.StageId.OnValueChanged += HandleStageChanged;

			Refresh();
		}

		protected override void OnUnbind()
		{
			if (_handHelper) _handHelper.OnOpenChanged -= HandleHandHelperOpenChanged;
			Data.StageId.OnValueChanged -= HandleStageChanged;
			Data.CurrentTurnClientId.OnValueChanged -= HandleTurnChanged;

			if (_foldButton) _foldButton.OnClick -= HandleFold;
			if (_betButton) _betButton.OnClick -= HandleBet;

			CloseAll();
		}

		private void HandleTurnChanged(ulong previous, ulong current) => Refresh();
		private void HandleStageChanged(FixedString32Bytes previous, FixedString32Bytes current) => Refresh();
		private void HandleHandHelperOpenChanged(bool open) => Refresh();

		private bool IsHandHelperOpen => _handHelper && _handHelper.IsOpen;

		private void Refresh()
		{
			// Resolved from the replicated stage id, never from GameMode.CurrentStage: that is written only by
			// the server's own stage machine, so on a client it is null forever.
			_stage = GameMode ? GameMode.FindStage(Data.StageId.Value.ToString()) as PokerItemWagerStage : null;

			// The hand board covers the same moment, so the bar steps aside while it is up; closing it lands back
			// on the menu, since the picker was put away with everything else.
			if (_stage == null || !IsLocalTurn || IsHandHelperOpen)
			{
				CloseAll();
				return;
			}

			// A turn that is still ours keeps whichever panel the player is on; only a fresh turn opens the menu.
			if (_panels && !_panels.IsShowing(_pickerPanel)) ShowMenu();
		}

		private void ShowMenu()
		{
			if (_picker) _picker.Close();
			if (_panels) _panels.Show(_menuPanel);

			if (_foldButton && _stage != null) _foldButton.gameObject.SetActive(_stage.AllowFold);
		}

		private void HandleBet()
		{
			if (_stage == null || !IsLocalTurn || IsHandHelperOpen) return;

			if (_panels) _panels.Show(_pickerPanel);
			if (_picker) _picker.Open(GameMode, LocalPlayer, _stage, ShowMenu);
		}

		private void HandleFold()
		{
			if (GameMode) GameMode.SubmitActionRPC(PokerActionType.Fold, 0);
		}

		private void CloseAll()
		{
			if (_picker) _picker.Close();
			if (_panels) _panels.HideAll();
		}
	}
}
