using System;
using Game.Runtime.Controller;
using Game.Runtime.UI.Button;
using Game.Runtime.UI.FindLobby;
using Game.Runtime.UI.Matchmaking;
using Game.Runtime.UI.Selection;
using Game.Runtime.UI.Settings;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.UI.MainMenu
{
	// The front door. One screen: the logo and root entries, and over the same backdrop the play panels — the
	// ways into a table (Start), hosting, the room list and the quick-match wait — swapped through one panel
	// group, so moving between them is a panel changing on paper rather than one screen replacing another.
	public class UIMainMenu : MonoBehaviour
	{
		[Header("Root Menu")]
		[Required]
		[SerializeField] private UISelectionGroup _rootGroup;

		[Required]
		[SerializeField] private UISelectionItem _playItem;

		[SerializeField] private UISelectionItem _optionItem;

		[Tooltip("Greyed until there is a credits screen to open.")]
		[SerializeField] private UISelectionItem _creditItem;

		[Required]
		[SerializeField] private UISelectionItem _quitItem;

		[Tooltip("What the play panels cover: the logo and the root entries.")]
		[Required]
		[SerializeField] private GameObject _rootMenu;

		[Header("Play Menu")]
		[Tooltip("Start, Create Room and Join Room. Quick match is the popup's own panel, shown beside the group.")]
		[Required]
		[SerializeField] private UIPanelStateGroup _playPanels;

		[Required]
		[SerializeField] private GameObject _startPanel;

		[Required]
		[SerializeField] private UISelectionGroup _playGroup;

		[Tooltip("Left empty, or with no matchmaking popup, the entry greys out.")]
		[SerializeField] private UISelectionItem _quickMatchItem;

		[Required]
		[SerializeField] private UISelectionItem _hostItem;

		[Required]
		[SerializeField] private UISelectionItem _findItem;

		[Tooltip("The Start panel's close cross: back to the root menu.")]
		[SerializeField] private UIButton _playCloseButton;

		[Required]
		[SerializeField] private UIRoomSetting _roomSettingUI;

		[Required]
		[SerializeField] private UIFindLobby _findLobbyUI;

		[SerializeField] private UIMatchmakingPopup _matchmakingPopup;

		[Header("Screens")]
		[Tooltip("Left empty, the Option entry greys out — an entry that does nothing should look like one.")]
		[SerializeField] private UISettingsScreen _settingsScreen;

		[Header("Quick Start")]
		[Tooltip("On, Host goes straight in on the settings the network manager already carries. A shortcut for getting to a table while it is being built — turn it off to get the room setup panel back. Join always goes through its list, which refreshes and filters on its own.")]
		[SerializeField] private bool _hostWithoutSetup;

		// One way in at a time. Released in finally, or a throw would leave the menu refusing every later
		// attempt with nothing on screen to explain why.
		private bool _connecting;

		private void OnEnable()
		{
			_rootGroup.OnSubmitted += HandleRootSubmitted;
			_playGroup.OnSubmitted += HandlePlaySubmitted;
			if (_playCloseButton) _playCloseButton.OnClick += ShowRootMenu;
			_roomSettingUI.OnCloseRequested += ShowRootMenu;
			_roomSettingUI.OnBackRequested += ShowPlayMenu;
			_findLobbyUI.OnCloseRequested += ShowRootMenu;
			_findLobbyUI.OnBackRequested += ShowPlayMenu;

			if (_optionItem) _optionItem.Button.IsInteractable = _settingsScreen;
			if (_creditItem) _creditItem.Button.IsInteractable = false;
			if (_quickMatchItem) _quickMatchItem.Button.IsInteractable = _matchmakingPopup;

			ShowRootMenu();
		}

		private void OnDisable()
		{
			_findLobbyUI.OnBackRequested -= ShowPlayMenu;
			_findLobbyUI.OnCloseRequested -= ShowRootMenu;
			_roomSettingUI.OnBackRequested -= ShowPlayMenu;
			_roomSettingUI.OnCloseRequested -= ShowRootMenu;
			if (_playCloseButton) _playCloseButton.OnClick -= ShowRootMenu;
			_rootGroup.OnSubmitted -= HandleRootSubmitted;
			_playGroup.OnSubmitted -= HandlePlaySubmitted;
		}

		// Subscribed once everything is awake rather than from OnEnable: the menu hides itself by going
		// inactive while a table runs, and a disabled object that stopped listening would never hear the
		// disconnect that is supposed to bring it back.
		private void Start()
		{
			var network = NetworkManager.Singleton;
			if (network)
			{
				network.OnClientStopped += HandleClientStopped;
				network.OnServerStopped += HandleServerStopped;
				network.OnClientDisconnectCallback += HandleClientDisconnected;
				network.OnTransportFailure += Show;
			}

			// Walking out of a table never touches those callbacks when nothing was listening yet, so the
			// menu also answers the teardown itself.
			if (GameNetworkManager.Instance)
			{
				GameNetworkManager.Instance.OnGameLeft += Show;

				// A way into a table can start from outside the menu (a Steam invite), with any of its panels up.
				GameNetworkManager.Instance.OnConnectStarted += HideAll;

				// A failed attempt does not always tear anything down, so OnGameLeft can never arrive — and
				// the menu hid itself on the way in. Without this the player is left looking at nothing.
				GameNetworkManager.Instance.OnConnectFailed += HandleConnectFailed;
			}

			// A matchmaking failure can arrive after the menu stepped aside for the loading screen; the popup
			// sits inside the menu, so the menu comes back around it.
			if (_matchmakingPopup)
			{
				_matchmakingPopup.OnOpened += HandleMatchmakingOpened;
				_matchmakingPopup.OnClosed += ShowPlayMenu;
			}
		}

		private void OnDestroy()
		{
			var network = NetworkManager.Singleton;
			if (network)
			{
				network.OnClientStopped -= HandleClientStopped;
				network.OnServerStopped -= HandleServerStopped;
				network.OnClientDisconnectCallback -= HandleClientDisconnected;
				network.OnTransportFailure -= Show;
			}

			if (GameNetworkManager.Instance)
			{
				GameNetworkManager.Instance.OnConnectStarted -= HideAll;
				GameNetworkManager.Instance.OnGameLeft -= Show;
				GameNetworkManager.Instance.OnConnectFailed -= HandleConnectFailed;
			}

			if (_matchmakingPopup)
			{
				_matchmakingPopup.OnClosed -= ShowPlayMenu;
				_matchmakingPopup.OnOpened -= HandleMatchmakingOpened;
			}
		}

		private void HandleConnectFailed(string reason) => Show();

		private void HandleClientStopped(bool wasHost) => Show();
		private void HandleServerStopped(bool wasHost) => Show();

		private void HandleClientDisconnected(ulong clientId)
		{
			if (!NetworkManager.Singleton || clientId != NetworkManager.Singleton.LocalClientId) return;

			Show();
		}

		// Back to the root entries — unless the quick-match popup is up with something to say, which keeps the
		// screen until it is closed.
		public void Show()
		{
			gameObject.SetActive(true);
			CloseSettings();

			if (_matchmakingPopup && _matchmakingPopup.IsOpen)
			{
				HandleMatchmakingOpened();
				return;
			}

			ShowRootMenu();
		}

		private void QuickMatch()
		{
			if (!_matchmakingPopup || !_matchmakingPopup.CanOpen) return;

			_matchmakingPopup.Open();
		}

		private void HandleMatchmakingOpened()
		{
			gameObject.SetActive(true);
			_rootMenu.SetActive(false);
			_playPanels.HideAll();
		}

		private void HideAll()
		{
			CloseSettings();
			gameObject.SetActive(false);
		}

		public void CreateLobby()
		{
			if (_hostWithoutSetup)
			{
				StartHostDirectly();
				return;
			}

			ShowPanel(_roomSettingUI.gameObject);
		}

		public void FindLobby() => ShowPanel(_findLobbyUI.gameObject);

		// Hosts on whatever the network manager is already carrying — the same settings the room panel
		// would have opened with, rather than a second set of defaults written here to drift from them.
		private async void StartHostDirectly()
		{
			if (_connecting) return;
			_connecting = true;

			try
			{
				gameObject.SetActive(false);

				var network = GameNetworkManager.Instance;
				if (network) await network.StartHost(destroyCancellationToken);
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
				Show();
			}
			finally
			{
				_connecting = false;
			}
		}

		public void Quit()
		{
#if UNITY_EDITOR
			UnityEditor.EditorApplication.isPlaying = false;
#else
			Application.Quit();
#endif
		}

		private void HandleRootSubmitted(UISelectionItem item)
		{
			if (item == _playItem) ShowPlayMenu();
			else if (item == _optionItem) ShowOptions();
			else if (item == _quitItem) Quit();
		}

		private void HandlePlaySubmitted(UISelectionItem item)
		{
			if (item == _quickMatchItem) QuickMatch();
			else if (item == _hostItem) CreateLobby();
			else if (item == _findItem) FindLobby();
		}

		// Laid over the menu rather than replacing it: the backdrop stays, the logo and the models step aside so
		// the settings paper sits alone on it.
		private void ShowOptions()
		{
			if (!_settingsScreen) return;

			_rootMenu.SetActive(false);
			_playPanels.HideAll();
			_settingsScreen.OnClosed += HandleSettingsClosed;
			_settingsScreen.Open();
		}

		private void HandleSettingsClosed()
		{
			_settingsScreen.OnClosed -= HandleSettingsClosed;
			Show();
		}

		// Shut without raising OnClosed: whoever shuts it this way is already putting up what comes next.
		private void CloseSettings()
		{
			if (!_settingsScreen) return;

			_settingsScreen.OnClosed -= HandleSettingsClosed;
			_settingsScreen.gameObject.SetActive(false);
		}

		private void ShowRootMenu()
		{
			_playPanels.HideAll();
			_rootMenu.SetActive(true);
		}

		private void ShowPlayMenu() => ShowPanel(_startPanel);

		// Switching a panel on is what re-arms its list: a group picks its first entry as it appears.
		private void ShowPanel(GameObject panel)
		{
			_rootMenu.SetActive(false);
			_playPanels.Show(panel);
		}
	}
}
