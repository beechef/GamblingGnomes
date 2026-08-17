using System;
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

		// async void because a UI callback has nowhere to hand a task back to — so it catches its own
		// exceptions, and the guard flag comes off on every path. Left standing it would make the
		// button dead for the rest of the session.
		public async void Refresh()
		{
			if (_isRefreshing) return;
			_isRefreshing = true;

			ClearItems();

			try
			{
				var lobbies = await GameNetworkManager.Instance.SearchLobby(destroyCancellationToken);
				foreach (var lobby in lobbies)
				{
					AddItem(lobby);
				}
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
				_isRefreshing = false;
			}
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

			try
			{
				await GameNetworkManager.Instance.JoinLobby(lobby, destroyCancellationToken);

				gameObject.SetActive(false);
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
				_joiningLobby = false;
			}
		}
	}
}
