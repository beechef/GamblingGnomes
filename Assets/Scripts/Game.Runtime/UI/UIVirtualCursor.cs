using System;
using System.Threading;
using Game.Runtime.Controller;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Game.Runtime.UI
{
	// The pad's pointer. `VirtualMouseInput` does the work — it is the Input System's own answer and it
	// creates a real virtual Mouse device, so anything reading `Pointer.current` (the card picker, uGUI's
	// own raycasting) is pointed at by a stick without knowing it.
	//
	// This only decides *when* it runs: on a keyboard the hardware cursor is the pointer and a second one
	// drawn beside it would be two arrows answering the same hand. A software cursor is also why
	// CursorController may go on hiding the hardware one for a pad — the arrow the player sees is this
	// Image, not the OS's.
	//
	// Switching it off is the awkward part, and the reason for most of what follows. `VirtualMouseInput`
	// is handed actions from the project-wide asset, and its own OnDisable removes the virtual device
	// *before* it stops listening to them — so the disable cancels an action that is still bound to the
	// device it just deleted, and the package reads a control that is no longer there. It also calls
	// Disable() on those actions, which are shared by the whole process. Both are fixed the same way: the
	// actions are taken off the component before it is switched off, and given back before it is switched
	// on.
	[RequireComponent(typeof(VirtualMouseInput))]
	public class UIVirtualCursor : MonoBehaviour
	{
		[Header("References")]
		[Tooltip("The arrow itself. Switched off with the device, so a player who puts the pad down is not left with a pointer nothing is driving.")]
		[SerializeField] private RectTransform _cursor;

		private VirtualMouseInput _input;

		// The cursor's own click, made here rather than taken from the actions asset — and being unshared
		// is the entire point. Anything in the asset that clicks is bound to <Mouse>/leftButton, which the
		// virtual mouse itself satisfies, so feeding one back in latches the button down: the pad presses it,
		// the pressed button keeps the action actuated, releasing the pad raises no cancel, and it never comes
		// back up. Hover works and nothing can ever be clicked. An action nothing else binds cannot close it.
		private InputAction _click;
		private bool _applyQueued;

		private bool _actionsHeld;
		private InputActionProperty _stick;
		private InputActionProperty _left;
		private InputActionProperty _right;
		private InputActionProperty _middle;
		private InputActionProperty _forward;
		private InputActionProperty _back;
		private InputActionProperty _scroll;

		private void Awake()
		{
			_input = GetComponent<VirtualMouseInput>();

			_click = new InputAction("VirtualCursorClick", InputActionType.Button, "<Gamepad>/buttonSouth");
			_click.Enable();
			_input.leftButtonAction = new InputActionProperty(_click);
		}

		// Start rather than OnEnable: InputSchemeController is another object's static, and objects in one
		// scene wake in no guaranteed order — an OnEnable reading it can subscribe to nothing and stay
		// deaf for the session.
		private void Start()
		{
			InputSchemeController.OnSchemeChanged += HandleSchemeChanged;
			CursorController.OnPointerWantedChanged += Refresh;

			Refresh();
		}

		private void OnDestroy()
		{
			InputSchemeController.OnSchemeChanged -= HandleSchemeChanged;
			CursorController.OnPointerWantedChanged -= Refresh;

			_click?.Disable();
			_click?.Dispose();
		}

		private void HandleSchemeChanged(InputScheme scheme) => Refresh();

		// Deferred by a frame. The scheme changes from inside InputSystem.onEvent, and switching the
		// component adds or removes a real InputDevice, which is not something to do part-way through the
		// update that is delivering events.
		//
		// Collapsed rather than queued: Apply reads the scheme at the moment it runs, so several changes in
		// one frame settle on the last one and a queue would only spend frames repeating it.
		private void Refresh()
		{
			if (_applyQueued) return;

			_applyQueued = true;
			_ = ApplyNextFrame(destroyCancellationToken);
		}

		private async Awaitable ApplyNextFrame(CancellationToken token)
		{
			try
			{
				await Awaitable.NextFrameAsync(token);

				Apply();
			}
			catch (OperationCanceledException)
			{
				// Gone before the frame turned, so there is nothing left to apply it to.
			}
			finally
			{
				_applyQueued = false;
			}
		}

		private void Apply()
		{
			// A pad only gets an arrow where there is something in the world to point at. A menu is driven by
			// uGUI's own focus, and an arrow drawn beside a moving selection is two pointers answering one
			// hand — the same argument that keeps it off a keyboard.
			var wanted = InputSchemeController.IsGamepad && CursorController.IsPointerWanted;

			if (_input && _input.enabled != wanted)
			{
				// Both orders put the actions in place before the flag moves: taking them off while the
				// component is still enabled is what unhooks its callbacks, and giving them back while it
				// is still disabled leaves OnEnable to hook them the way it normally would.
				if (wanted) GiveActionsBack();
				else TakeActionsOff();

				_input.enabled = wanted;
			}

			if (_cursor && _cursor.gameObject.activeSelf != wanted) _cursor.gameObject.SetActive(wanted);
		}

		private void TakeActionsOff()
		{
			if (_actionsHeld || !_input) return;

			_stick = _input.stickAction;
			_left = _input.leftButtonAction;
			_right = _input.rightButtonAction;
			_middle = _input.middleButtonAction;
			_forward = _input.forwardButtonAction;
			_back = _input.backButtonAction;
			_scroll = _input.scrollWheelAction;

			_input.stickAction = default;
			_input.leftButtonAction = default;
			_input.rightButtonAction = default;
			_input.middleButtonAction = default;
			_input.forwardButtonAction = default;
			_input.backButtonAction = default;
			_input.scrollWheelAction = default;

			_actionsHeld = true;
		}

		private void GiveActionsBack()
		{
			if (!_actionsHeld || !_input) return;

			_input.stickAction = _stick;
			_input.leftButtonAction = _left;
			_input.rightButtonAction = _right;
			_input.middleButtonAction = _middle;
			_input.forwardButtonAction = _forward;
			_input.backButtonAction = _back;
			_input.scrollWheelAction = _scroll;

			_actionsHeld = false;
		}
	}
}
