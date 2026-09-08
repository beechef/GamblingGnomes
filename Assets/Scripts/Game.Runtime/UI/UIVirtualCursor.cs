using System;
using System.Threading;
using Game.Runtime.Controller;
using UnityEngine;
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
	[RequireComponent(typeof(VirtualMouseInput))]
	public class UIVirtualCursor : MonoBehaviour
	{
		[Header("References")]
		[Tooltip("The arrow itself. Switched off with the device, so a player who puts the pad down is not left with a pointer nothing is driving.")]
		[SerializeField] private RectTransform _cursor;

		private VirtualMouseInput _input;
		private bool _applyQueued;

		private void Awake()
		{
			_input = GetComponent<VirtualMouseInput>();
		}

		// Start rather than OnEnable: InputSchemeController is another object's static, and objects in one
		// scene wake in no guaranteed order — an OnEnable reading it can subscribe to nothing and stay
		// deaf for the session.
		private void Start()
		{
			InputSchemeController.OnSchemeChanged += HandleSchemeChanged;

			Refresh();
		}

		private void OnDestroy()
		{
			InputSchemeController.OnSchemeChanged -= HandleSchemeChanged;
		}

		private void HandleSchemeChanged(InputScheme scheme) => Refresh();

		// Always deferred by a frame, and that is the whole point of it. Enabling or disabling
		// VirtualMouseInput adds or removes a real InputDevice, while the scheme changes from inside
		// InputSystem.onEvent — so applying it here tears the VirtualMouse out from under an action
		// callback still running against it, and the InvalidOperationException that throws takes the rest
		// of that callback list with it. The symptom is not an error anybody connects to input devices:
		// it is the UI quietly refusing clicks.
		//
		// Collapsed rather than queued: Apply reads the scheme at the moment it runs, so several changes in
		// one frame settle on the last one and a queue would only be spending frames repeating it.
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
			var wanted = InputSchemeController.IsGamepad;

			if (_input && _input.enabled != wanted) _input.enabled = wanted;
			if (_cursor && _cursor.gameObject.activeSelf != wanted) _cursor.gameObject.SetActive(wanted);
		}
	}
}
