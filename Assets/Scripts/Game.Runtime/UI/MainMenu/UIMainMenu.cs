using Game.Runtime.Controller;
using Game.Runtime.UI.FindLobby;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.UI.MainMenu
{
	public class UIMainMenu : MonoBehaviour
	{
		[SerializeField] private UIRoomSetting _roomSettingUI;
		[SerializeField] private UIFindLobby _findLobbyUI;

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
			_roomSettingUI.gameObject.SetActive(false);
			_findLobbyUI.gameObject.SetActive(false);
		}

		public void CreateLobby()
		{
			gameObject.SetActive(false);
			_roomSettingUI.gameObject.SetActive(true);
		}

		public void FindLobby()
		{
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
	}
}
