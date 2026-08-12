using Game.Runtime.Controller;
using Game.Runtime.Player;
using Game.Runtime.UI.Button;
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

		[Header("Buttons")]
		[SerializeField] private UIButton _resumeButton;
		[SerializeField] private UIButton _leaveButton;
		[SerializeField] private UIButton _quitButton;

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

			if (_resumeButton) _resumeButton.OnClick += Close;
			if (_leaveButton) _leaveButton.OnClick += HandleLeaveClicked;
			if (_quitButton) _quitButton.OnClick += HandleQuitClicked;
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

			if (_resumeButton) _resumeButton.OnClick -= Close;
			if (_leaveButton) _leaveButton.OnClick -= HandleLeaveClicked;
			if (_quitButton) _quitButton.OnClick -= HandleQuitClicked;

			ClosePanel();
		}

		private void HandlePausePressed(InputAction.CallbackContext ctx)
		{
			if (IsOpen) Close();
			else Open();
		}

		public void Open()
		{
			if (IsOpen || _leaving) return;

			var network = GameNetworkManager.Instance;
			if (!network || !network.IsInGame) return;

			IsOpen = true;
			if (_panel) _panel.SetActive(true);

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

			IsOpen = false;
			if (_panel) _panel.SetActive(false);
		}

		// The table is already gone by now, and so is the player we would have restored — closing the
		// panel is all that is left to do.
		private void HandleGameLeft() => ClosePanel();

		private async void HandleLeaveClicked()
		{
			if (_leaving) return;
			_leaving = true;

			if (_leaveButton) _leaveButton.IsInteractable = false;

			var network = GameNetworkManager.Instance;
			if (network) await network.LeaveGame();

			ClosePanel();

			if (_leaveButton) _leaveButton.IsInteractable = true;
			_leaving = false;
		}

		private void HandleQuitClicked()
		{
			var network = GameNetworkManager.Instance;
			if (network) network.Shutdown();

#if UNITY_EDITOR
			UnityEditor.EditorApplication.isPlaying = false;
#else
			Application.Quit();
#endif
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
