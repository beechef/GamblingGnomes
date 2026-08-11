using Game.Runtime.Controller;
using Game.Runtime.UI.MainMenu;
using Steamworks.Data;
using UnityEngine;

namespace Game.Runtime.UI.FindLobby
{
	public class UIFindLobby : MonoBehaviour
	{
		[SerializeField] private UIFindLobbyItem _itemPrefab;
		[SerializeField] private RectTransform _itemContainer;
		[SerializeField] private UIMainMenu _mainMenuUI;

		private bool _isRefreshing;
		private bool _joiningLobby;

		private void OnEnable()
		{
			Refresh();
		}

		public async void Refresh()
		{
			if (_isRefreshing) return;
			_isRefreshing = true;

			ClearItems();

			var lobbies = await GameNetworkManager.Instance.SearchLobby();
			foreach (var lobby in lobbies)
			{
				AddItem(lobby);
			}

			_isRefreshing = false;
		}

		public void Close()
		{
			gameObject.SetActive(false);
			_mainMenuUI.Show();
		}

		private void ClearItems()
		{
			var items = _itemContainer.GetComponentsInChildren<UIFindLobbyItem>();
			foreach (var item in items)
			{
				item.OnJoinLobbyRequested -= OnJoinLobbyRequested;
				Destroy(item.gameObject);
			}
		}

		private void AddItem(Lobby lobby)
		{
			var item = Instantiate(_itemPrefab, _itemContainer);
			item.OnJoinLobbyRequested += OnJoinLobbyRequested;
			item.SetData(lobby);
		}

		private async void OnJoinLobbyRequested(Lobby lobby)
		{
			if (_joiningLobby) return;
			_joiningLobby = true;

			await GameNetworkManager.Instance.JoinLobby(lobby);

			_joiningLobby = false;
			gameObject.SetActive(false);
		}
	}
}
