using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Stages;
using Game.Runtime.UI.Button;
using TMPro;
using Unity.Collections;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The winner naming themselves as the one who eats the Colorful cap. Everybody else is picked by pointing
	// at them across the table (PokerColorfulPickController), and their name lights up over their head as
	// they are pointed at; your own body is under your own eye, so your own name is put where your own meter
	// already is — over the hallucination bar — and pointing at it is the same act. Up only while this
	// client is the one choosing and may choose themselves.
	public class UIPokerColorfulSelfPick : UIPokerView
	{
		[Header("References")]
		[Tooltip("The name, as a button. Switched on only while this client is choosing.")]
		[SerializeField] private UIButton _button;

		[SerializeField] private TMP_Text _label;

		private void Awake()
		{
			if (_button) _button.gameObject.SetActive(false);
		}

		protected override void OnBind()
		{
			Data.CurrentTurnClientId.OnValueChanged += HandleTurnChanged;
			Data.StageId.OnValueChanged += HandleStageChanged;
			if (_button) _button.OnClick += HandlePick;

			Refresh();
		}

		protected override void OnUnbind()
		{
			if (_button) _button.OnClick -= HandlePick;
			Data.StageId.OnValueChanged -= HandleStageChanged;
			Data.CurrentTurnClientId.OnValueChanged -= HandleTurnChanged;

			if (_button) _button.gameObject.SetActive(false);
		}

		private void HandleTurnChanged(ulong previous, ulong current) => Refresh();
		private void HandleStageChanged(FixedString32Bytes previous, FixedString32Bytes current) => Refresh();

		// Resolved from the replicated stage id: GameMode.CurrentStage is null on a client for the session.
		// Asked of the stage's own CanBeFed, the test the server refuses with.
		private void Refresh()
		{
			var stage = GameMode ? GameMode.FindStage(Data.StageId.Value.ToString()) as PokerColorfulPickStage : null;
			var show = stage != null && IsLocalTurn && stage.CanBeFed(LocalPlayer);

			if (show && _label) _label.text = LocalPlayer.DisplayName;
			if (_button && _button.gameObject.activeSelf != show) _button.gameObject.SetActive(show);
		}

		// The amount carries an identity rather than a size: a seat index, the same trick the wager plays
		// with the mushroom kind.
		private void HandlePick()
		{
			if (GameMode && LocalData) GameMode.SubmitActionRPC(PokerActionType.Target, LocalData.SeatIndex.Value);
		}
	}
}
