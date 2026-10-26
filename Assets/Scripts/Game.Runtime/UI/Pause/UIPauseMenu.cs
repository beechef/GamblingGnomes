using System;
using Game.Runtime.Controller;
using Game.Runtime.Player;
using Game.Runtime.UI.Button;
using Game.Runtime.UI.Settings;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Runtime.UI.Pause
{
	// The way back out of a running table. It only answers while a game is actually up, so the key does
	// nothing in the menu, and it frees the cursor itself rather than leaning on the cursor toggle —
	// that one belongs to the HUD, which players still need to click mid-hand.
	public class UIPauseMenu : MonoBehaviour
	{
		[Header("Panel")]
		[SerializeField] private GameObject _panel;

		[Header("Input")]
		[SerializeField] private InputActionReference _pauseAction;

		[Tooltip("UI/Cancel. A pad's Back steps out of the newest thing open, then out of this menu; it never opens it. Not enabled or disabled here: the UI input module shares it.")]
		[SerializeField] private InputActionReference _cancelAction;

		[Header("Buttons")]
		[SerializeField] private UIButton _resumeButton;
		[SerializeField] private UIButton _closeButton;
		[SerializeField] private UIButton _settingsButton;
		[SerializeField] private UIButton _leaveButton;

		[Tooltip("Opened over the table by the settings button; the pause panel comes back when it closes.")]
		[SerializeField] private UISettingsScreen _settingsScreen;

		public bool IsOpen { get; private set; }

		private bool _restoreMovement;
		private bool _leaving;

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
		}

		private void OnEnable()
		{
			if (_pauseAction)
			{
				_pauseAction.action.Enable();
				_pauseAction.action.performed += HandlePausePressed;
			}

			if (_cancelAction) _cancelAction.action.performed += HandleCancelPressed;

			if (_resumeButton) _resumeButton.OnClick += Close;
			if (_closeButton) _closeButton.OnClick += Close;
			if (_settingsButton) _settingsButton.OnClick += HandleSettingsClicked;
			if (_leaveButton) _leaveButton.OnClick += HandleLeaveClicked;
		}

		// The network manager wakes in the same scene as this screen, in no guaranteed order, so the
		// teardown is subscribed once everything is awake rather than from OnEnable.
		private void Start()
		{
			if (GameNetworkManager.Instance) GameNetworkManager.Instance.OnGameLeft += HandleGameLeft;
		}

		private void OnDestroy()
		{
			if (GameNetworkManager.Instance) GameNetworkManager.Instance.OnGameLeft -= HandleGameLeft;
		}

		private void OnDisable()
		{
			if (_pauseAction)
			{
				_pauseAction.action.performed -= HandlePausePressed;
				_pauseAction.action.Disable();
			}

			if (_cancelAction) _cancelAction.action.performed -= HandleCancelPressed;

			if (_resumeButton) _resumeButton.OnClick -= Close;
			if (_closeButton) _closeButton.OnClick -= Close;
			if (_settingsButton) _settingsButton.OnClick -= HandleSettingsClicked;
			if (_leaveButton) _leaveButton.OnClick -= HandleLeaveClicked;

			ClosePanel();
		}

		private void HandlePausePressed(InputAction.CallbackContext ctx)
		{
			// The pad has Back for stepping out, so its Start only pauses: whatever is open underneath (a kick
			// being picked) waits under the menu and carries on when it closes.
			if (ctx.control != null && ctx.control.device is Gamepad)
			{
				if (IsOpen) Close();
				else Open();
				return;
			}

			// Escape closes the newest thing that is open before it opens or closes anything of its own — this
			// menu is what the key falls through to, not what it always means.
			if (UIEscapeStack.DismissTop()) return;

			if (IsOpen) Close();
			else Open();
		}

		// The keyboard's Escape is both Pause and Cancel and is already handled above, so only a pad answers here.
		private void HandleCancelPressed(InputAction.CallbackContext ctx)
		{
			if (ctx.control == null || ctx.control.device is not Gamepad) return;

			if (UIEscapeStack.DismissTop()) return;

			if (IsOpen) Close();
		}

		public void Open()
		{
			if (IsOpen || _leaving) return;

			var network = GameNetworkManager.Instance;
			if (!network || !network.IsInGame) return;

			IsOpen = true;
			if (_panel) _panel.SetActive(true);

			// On the stack while open, so Back and Escape close it before anything opened underneath it.
			UIEscapeStack.Push(Close);

			// One release among however many are out — the poker HUD may be holding its own, and
			// resuming must not take the cursor back from it.
			CursorController.RequestUnlock();

			var player = FindLocalPlayer();
			if (!player) return;

			// Remembered rather than assumed: a seated player is already frozen, and resuming must not
			// hand their legs back while they are still in the chair.
			_restoreMovement = player.MovementEnabled;
			player.SetMovementEnabled(false);
		}

		public void Close()
		{
			if (!IsOpen) return;

			var player = FindLocalPlayer();
			if (player) player.SetMovementEnabled(_restoreMovement);

			ClosePanel();
		}

		private void ClosePanel()
		{
			// Every way out runs through here, so the release is handed back exactly once however the
			// panel closed — resumed, left the table, or switched off.
			if (IsOpen) CursorController.ReleaseUnlock();

			UIEscapeStack.Remove(Close);

			IsOpen = false;
			if (_panel) _panel.SetActive(false);

			if (_settingsScreen)
			{
				_settingsScreen.OnClosed -= HandleSettingsClosed;
				_settingsScreen.gameObject.SetActive(false);
			}
		}

		// The table is already gone by now, and so is the player we would have restored — closing the
		// panel is all that is left to do.
		private void HandleGameLeft() => ClosePanel();

		private void HandleSettingsClicked()
		{
			if (!_settingsScreen) return;

			if (_panel) _panel.SetActive(false);
			_settingsScreen.OnClosed += HandleSettingsClosed;
			_settingsScreen.Open();
		}

		private void HandleSettingsClosed()
		{
			_settingsScreen.OnClosed -= HandleSettingsClosed;
			if (IsOpen && _panel) _panel.SetActive(true);
		}

		private async void HandleLeaveClicked()
		{
			if (_leaving) return;
			_leaving = true;

			if (_leaveButton) _leaveButton.IsInteractable = false;

			try
			{
				var network = GameNetworkManager.Instance;
				if (network) await network.LeaveGame(destroyCancellationToken);

				ClosePanel();
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
			}
			finally
			{
				// Handed back whatever happened — a leave that threw must not leave the way out greyed
				// out with the table still up.
				if (_leaveButton) _leaveButton.IsInteractable = true;
				_leaving = false;
			}
		}

		private PlayerController FindLocalPlayer()
		{
			var manager = NetworkManager.Singleton;
			if (!manager || !manager.IsClient || manager.SpawnManager == null) return null;

			var playerObject = manager.SpawnManager.GetLocalPlayerObject();
			return playerObject ? playerObject.GetComponent<PlayerController>() : null;
		}
	}
}
