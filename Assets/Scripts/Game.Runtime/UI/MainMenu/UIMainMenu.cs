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

		private void Start()
		{
			NetworkManager.Singleton.OnClientStopped += wasHost => Show();
			NetworkManager.Singleton.OnServerStopped += wasHost => Show();

			NetworkManager.Singleton.OnClientDisconnectCallback += clientId =>
			{
				if (clientId != NetworkManager.Singleton.LocalClientId) return;
				Show();
			};

			NetworkManager.Singleton.OnTransportFailure += Show;

			// Walking out of a table never touches those callbacks when nothing was listening yet, so the
			// menu also answers the teardown itself.
			if (GameNetworkManager.Instance) GameNetworkManager.Instance.OnGameLeft += Show;
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
