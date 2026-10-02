using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Items;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.GameMode.Poker.Stages;
using Game.Runtime.UI.Button;
using TMPro;
using Unity.Collections;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// Naming yourself when the table asks you to point at a player. Everybody else is picked by pointing at
	// them across the table, and their name lights up over their head as they are pointed at; your own body
	// is under your own eye, so your own name is put where your own meter already is — over the
	// hallucination bar — and pointing at it is the same act.
	//
	// Two askers: the winner naming who eats the Colorful cap (PokerColorfulPickStage), and an item whose
	// player step may name its user (PokerItemTargetingController.CanPickSelf). Up only while one of them is
	// asking this client and would accept the answer.
	public class UIPokerSelfPick : UIPokerView
	{
		[Header("References")]
		[Tooltip("The name, as a button. Switched on only while this client may name themselves.")]
		[SerializeField] private UIButton _button;

		[SerializeField] private TMP_Text _label;

		[Tooltip("Space around the name the button answers the pointer in. The button is sized to the name plus this, so only the name is hit.")]
		[SerializeField] private Vector2 _hitPadding = new(24f, 8f);

		[Header("Colorful Reward")]
		[Tooltip("The items you would be handed for surviving the Colorful (PokerItemModule.ColorfulSurvivorItems), whether or not your hand has room for them, shown over your name while you may name yourself. Hidden for an item's pick and at tables that hand none.")]
		[SerializeField] private GameObject _reward;

		[SerializeField] private TMP_Text _rewardLabel;

		[Tooltip("{0} is how many items.")]
		[SerializeField] private string _rewardFormat = "+{0}";

		private PokerItemTargetingController _targeting;

		private void Awake()
		{
			if (_button) _button.gameObject.SetActive(false);
		}

		protected override void OnBind()
		{
			Data.CurrentTurnClientId.OnValueChanged += HandleTurnChanged;
			Data.StageId.OnValueChanged += HandleStageChanged;

			_targeting = LocalPlayer.ItemTargeting;
			if (_targeting) _targeting.OnTargetingChanged += Refresh;

			if (_button) _button.OnClick += HandlePick;
			if (_button) _button.OnStateChanged += HandleButtonStateChanged;

			Refresh();
		}

		protected override void OnUnbind()
		{
			SetBodyLit(false);

			if (_button) _button.OnStateChanged -= HandleButtonStateChanged;
			if (_button) _button.OnClick -= HandlePick;

			if (_targeting) _targeting.OnTargetingChanged -= Refresh;
			_targeting = null;

			Data.StageId.OnValueChanged -= HandleStageChanged;
			Data.CurrentTurnClientId.OnValueChanged -= HandleTurnChanged;

			if (_button) _button.gameObject.SetActive(false);
		}

		private void HandleTurnChanged(ulong previous, ulong current) => Refresh();
		private void HandleStageChanged(FixedString32Bytes previous, FixedString32Bytes current) => Refresh();

		private bool ItemAsksForSelf => _targeting && _targeting.CanPickSelf;

		// Resolved from the replicated stage id: GameMode.CurrentStage is null on a client for the session.
		// Asked of the stage's own CanBeFed, the test the server refuses with.
		private bool ColorfulAsksForSelf
		{
			get
			{
				var stage = GameMode ? GameMode.FindStage(Data.StageId.Value.ToString()) as PokerColorfulPickStage : null;
				return stage != null && IsLocalTurn && stage.CanBeFed(LocalPlayer);
			}
		}

		private void Refresh()
		{
			var item = LocalPlayer && ItemAsksForSelf;
			var colorful = LocalPlayer && !item && ColorfulAsksForSelf;
			var show = item || colorful;

			if (show) FillName(LocalPlayer.ColoredName);
			if (!show) SetBodyLit(false);
			if (_button && _button.gameObject.activeSelf != show) _button.gameObject.SetActive(show);

			var reward = colorful ? ColorfulReward() : 0;
			if (reward > 0 && _rewardLabel) _rewardLabel.text = string.Format(_rewardFormat, reward);
			if (_reward && _reward.activeSelf != reward > 0) _reward.SetActive(reward > 0);
		}

		// The button is as big as the name it shows, so the pointer has to be on the name to pick it.
		private void FillName(string displayName)
		{
			if (!_label) return;

			_label.text = displayName;

			if (!_button || _button.transform is not RectTransform hit) return;

			var size = _label.GetPreferredValues(displayName);
			hit.sizeDelta = new Vector2(size.x + _hitPadding.x * 2f, size.y + _hitPadding.y * 2f);
		}

		// Pointing at your own name lights your own body, as pointing at anybody else lights theirs; on your
		// screen that is the hand rig.
		private void HandleButtonStateChanged(UIButtonState previous, UIButtonState current) =>
			SetBodyLit(current is UIButtonState.Hovered or UIButtonState.Selected or UIButtonState.Pressed);

		private void SetBodyLit(bool lit)
		{
			var visual = LocalPlayer ? LocalPlayer.Visual : null;
			if (visual) visual.SetLocalOutlined(lit);
		}

		private int ColorfulReward()
		{
			var module = GameMode ? GameMode.FindModule<PokerItemModule>() : null;
			return module ? module.ColorfulSurvivorItems : 0;
		}

		// The Colorful answer's amount carries an identity rather than a size: a seat index, the same trick
		// the bet plays with the mushroom kind.
		private void HandlePick()
		{
			if (ItemAsksForSelf)
			{
				_targeting.PickSelf();
				return;
			}

			if (GameMode && LocalData) GameMode.SubmitActionRPC(PokerActionType.Target, LocalData.SeatIndex.Value);
		}
	}
}
