using System.Collections.Generic;
using Game.Runtime.Controller;
using Game.Runtime.GameMode.Poker;
using Game.Runtime.UI.Button;
using Game.Runtime.UI.Selection;
using Localization;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Runtime.UI.Poker
{
	// The host's Kick button in the waiting room, for a player with no pointer to aim at the crosses over heads.
	// Pressed, it starts picking: left and right step between the offered crosses in screen order (the picked one
	// pops and its player is outlined), Submit kicks, Back stops. While picking, the empty, confining
	// _picking root is the newest focus root, so the pad navigates no button; the pause menu stacks over it and
	// picking carries on when it closes. With nobody left to kick, picking stops as if Back were pressed.
	public class UIPokerKickPicker : UIPokerView
	{
		[Header("References")]
		[Required]
		[SerializeField] private UIButton _kickButton;

		[Tooltip("The Kick button's label: Kick at rest, Choose... while picking.")]
		[Required]
		[SerializeField] private LocalizedText _label;

		[Tooltip("Empty, with a confining UIFocusRoot; switched on only while picking.")]
		[Required]
		[SerializeField] private UIFocusRoot _picking;

		[Header("Input")]
		[Tooltip("UI/Navigate. Read, never enabled or disabled here: the UI input module shares it.")]
		[Required]
		[SerializeField] private InputActionReference _navigateAction;

		[Tooltip("UI/Submit. Read, never enabled or disabled here: the UI input module shares it.")]
		[Required]
		[SerializeField] private InputActionReference _submitAction;

		[Tooltip("How far the stick must lean sideways to step once; it must come back under this before the next step.")]
		[PropertyRange(0.1f, 0.9f)]
		[SerializeField] private float _stepThreshold = 0.5f;

		private readonly List<UIPokerKickButton> _ordered = new();

		private UIPokerKickButton _picked;
		private int _pickedIndex;
		private UINavigationPicker _navigation;

		public bool IsPicking => _navigation != null && _navigation.IsActive;

		private void Awake()
		{
			_navigation = new UINavigationPicker(_picking, _navigateAction, _submitAction, _stepThreshold);
		}

		protected override void OnBind()
		{
			_kickButton.OnClick += Begin;
			_navigation.OnStep += HandleStep;
			_navigation.OnSubmit += HandleSubmit;
			UIPokerKickButton.OnOfferedChanged += Refresh;
			Data.Phase.OnValueChanged += HandlePhaseChanged;

			Refresh();
		}

		protected override void OnUnbind()
		{
			End();

			Data.Phase.OnValueChanged -= HandlePhaseChanged;
			UIPokerKickButton.OnOfferedChanged -= Refresh;
			_navigation.OnSubmit -= HandleSubmit;
			_navigation.OnStep -= HandleStep;
			_kickButton.OnClick -= Begin;
		}

		private void HandlePhaseChanged(PokerPhase previous, PokerPhase current) => Refresh();

		private bool IsOffered => GameMode && GameMode.IsHost && GameMode.IsInWaitingRoom;

		private void Refresh()
		{
			if (!IsBound) return;

			var offered = IsOffered;
			_kickButton.gameObject.SetActive(offered);
			_kickButton.IsInteractable = UIPokerKickButton.Offered.Count > 0;

			if (!IsPicking) return;

			if (!offered || UIPokerKickButton.Offered.Count == 0)
			{
				End();
				return;
			}

			// The picked player left (kicked, or quit): the one now standing where they were is picked instead.
			if (!_picked || !Contains(_picked)) PickAt(_pickedIndex);
		}

		private void Begin()
		{
			if (IsPicking || UIPokerKickButton.Offered.Count == 0) return;

			_navigation.Begin();
			UIEscapeStack.Push(End);

			SetLabels(true);
			PickAt(0);
		}

		private void End()
		{
			if (!IsPicking) return;

			UIEscapeStack.Remove(End);

			Pick(null);
			_pickedIndex = 0;
			SetLabels(false);

			// Last, so the root hands focus back to the Kick button.
			_navigation.End();
		}

		private void HandleStep(int direction) => PickAt(_pickedIndex + direction);

		private void HandleSubmit()
		{
			if (_picked) _picked.Kick();
		}

		// Wraps at either end, ordered left to right as the players stand on screen.
		private void PickAt(int index)
		{
			OrderOffered();

			if (_ordered.Count == 0)
			{
				Pick(null);
				return;
			}

			_pickedIndex = (index % _ordered.Count + _ordered.Count) % _ordered.Count;
			Pick(_ordered[_pickedIndex]);
		}

		private void Pick(UIPokerKickButton target)
		{
			if (_picked == target) return;

			if (_picked) _picked.SetPicked(false);
			_picked = target;
			if (_picked) _picked.SetPicked(true);
		}

		private void OrderOffered()
		{
			_ordered.Clear();

			foreach (var button in UIPokerKickButton.Offered)
			{
				if (button && button.Player) _ordered.Add(button);
			}

			var camera = GameCamera.Main;
			if (!camera) return;

			_ordered.Sort((a, b) => ScreenX(camera, a).CompareTo(ScreenX(camera, b)));
		}

		private static float ScreenX(Camera camera, UIPokerKickButton button) =>
			camera.WorldToScreenPoint(button.Player.transform.position).x;

		private static bool Contains(UIPokerKickButton button)
		{
			foreach (var offered in UIPokerKickButton.Offered)
			{
				if (offered == button) return true;
			}

			return false;
		}

		private void SetLabels(bool picking) =>
			_label.Key = picking ? LocalizationKeys.Poker.Kick.Choosing : LocalizationKeys.Poker.Kick.Button;
	}
}
