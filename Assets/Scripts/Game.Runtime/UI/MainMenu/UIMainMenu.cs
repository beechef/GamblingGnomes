using Game.Runtime.Controller;
using Game.Runtime.UI.FindLobby;
using Game.Runtime.UI.Selection;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.UI.MainMenu
{
	// The front door. Two lists rather than two screens: Play swaps the entries under the logo for the
	// ways into a table and back again, so the title never leaves the screen and there is nothing to
	// fade between.
	public class UIMainMenu : MonoBehaviour
	{
		[Header("Root Menu")]
		[Required]
		[SerializeField] private UISelectionGroup _rootGroup;

		[Required]
		[SerializeField] private UISelectionItem _playItem;

		[SerializeField] private UISelectionItem _optionItem;

		[Required]
		[SerializeField] private UISelectionItem _quitItem;

		[Header("Play Menu")]
		[Required]
		[SerializeField] private UISelectionGroup _playGroup;

		[Required]
		[SerializeField] private UISelectionItem _hostItem;

		[Required]
		[SerializeField] private UISelectionItem _findItem;

		[Required]
		[SerializeField] private UISelectionItem _backItem;

		[Header("Screens")]
		[SerializeField] private UIRoomSetting _roomSettingUI;

		[SerializeField] private UIFindLobby _findLobbyUI;

		[Tooltip("Left empty, the Option entry greys out — an entry that does nothing should look like one.")]
		[SerializeField] private GameObject _optionScreen;

		private void OnEnable()
		{
			_rootGroup.OnSubmitted += HandleRootSubmitted;
			_playGroup.OnSubmitted += HandlePlaySubmitted;

			if (_optionItem) _optionItem.Button.IsInteractable = _optionScreen;

			ShowRootMenu();
		}

		private void OnDisable()
		{
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
			if (GameNetworkManager.Instance) GameNetworkManager.Instance.OnGameLeft += Show;
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

			if (GameNetworkManager.Instance) GameNetworkManager.Instance.OnGameLeft -= Show;
		}

		private void HandleClientStopped(bool wasHost) => Show();
		private void HandleServerStopped(bool wasHost) => Show();

		private void HandleClientDisconnected(ulong clientId)
		{
			if (!NetworkManager.Singleton || clientId != NetworkManager.Singleton.LocalClientId) return;

			Show();
		}

		// Also closes the sub-screens: they replace the menu rather than stacking on it, so their
		// opaque backgrounds never blend together.
		public void Show()
		{
			gameObject.SetActive(true);

			if (_roomSettingUI) _roomSettingUI.gameObject.SetActive(false);
			if (_findLobbyUI) _findLobbyUI.gameObject.SetActive(false);

			ShowRootMenu();
		}

		public void CreateLobby()
		{
			if (!_roomSettingUI) return;

			gameObject.SetActive(false);
			_roomSettingUI.gameObject.SetActive(true);
		}

		public void FindLobby()
		{
			if (!_findLobbyUI) return;

			gameObject.SetActive(false);
			_findLobbyUI.gameObject.SetActive(true);
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
			if (item == _hostItem) CreateLobby();
			else if (item == _findItem) FindLobby();
			else if (item == _backItem) ShowRootMenu();
		}

		private void ShowOptions()
		{
			if (!_optionScreen) return;

			gameObject.SetActive(false);
			_optionScreen.SetActive(true);
		}

		private void ShowRootMenu() => SetMenu(true);
		private void ShowPlayMenu() => SetMenu(false);

		// Switching the object off and on is what re-arms each group: a group picks its first entry as it
		// appears, so the list always opens with something under the pointer.
		private void SetMenu(bool root)
		{
			if (_rootGroup) _rootGroup.gameObject.SetActive(root);
			if (_playGroup) _playGroup.gameObject.SetActive(!root);
		}
	}
}
