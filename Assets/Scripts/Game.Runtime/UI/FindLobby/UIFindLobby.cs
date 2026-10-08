using System;
using Game.Runtime.Controller;
using Game.Runtime.Lobby;
using Game.Runtime.UI.Button;
using Localization;
using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.FindLobby
{
	public class UIFindLobby : MonoBehaviour
	{
		[SerializeField] private UIFindLobbyItem _itemPrefab;
		[SerializeField] private RectTransform _itemContainer;
		[Tooltip("The close cross: out of the whole play menu.")]
		[SerializeField] private UIButton _closeButton;

		[Tooltip("Back to the play choices.")]
		[SerializeField] private UIButton _backButton;

		[Tooltip("Searches again.")]
		[SerializeField] private UIButton _refreshButton;

		[Tooltip("Shown in place of the rows while searching, and when the search finds no room.")]
		[SerializeField] private TMP_Text _statusLabel;

		public event Action OnCloseRequested;
		public event Action OnBackRequested;

		private bool _isRefreshing;
		private bool _joiningLobby;

		private void OnEnable()
		{
			if (_closeButton) _closeButton.OnClick += HandleClose;
			if (_backButton) _backButton.OnClick += HandleBack;
			if (_refreshButton) _refreshButton.OnClick += Refresh;

			Refresh();
		}

		private void OnDisable()
		{
			if (_refreshButton) _refreshButton.OnClick -= Refresh;
			if (_backButton) _backButton.OnClick -= HandleBack;
			if (_closeButton) _closeButton.OnClick -= HandleClose;
		}

		// async void because a UI callback has nowhere to hand a task back to — so it catches its own
		// exceptions, and the guard flag comes off on every path. Left standing it would make the
		// button dead for the rest of the session.
		public async void Refresh()
		{
			if (_isRefreshing) return;
			_isRefreshing = true;

			ClearItems();
			ShowStatus(LocalizationKeys.Lobby.Searching);

			var found = 0;

			try
			{
				var lobbies = await GameNetworkManager.Instance.SearchLobby(destroyCancellationToken);
				foreach (var lobby in lobbies)
				{
					AddItem(lobby);
					found++;
				}

				ShowStatus(found == 0 ? LocalizationKeys.Lobby.Empty : null);
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
				ShowStatus(found == 0 ? LocalizationKeys.Lobby.Empty : null);
			}
			finally
			{
				_isRefreshing = false;
			}
		}

		// The list's empty state: what the search is doing, or that it found nothing. Null hides it.
		private void ShowStatus(string key)
		{
			if (!_statusLabel) return;

			_statusLabel.gameObject.SetActive(key != null);
			if (key != null) _statusLabel.text = Localizer.Get(key);
		}

		private void HandleClose() => OnCloseRequested?.Invoke();

		private void HandleBack() => OnBackRequested?.Invoke();

		private void ClearItems()
		{
			var items = _itemContainer.GetComponentsInChildren<UIFindLobbyItem>();
			foreach (var item in items)
			{
				item.OnJoinLobbyRequested -= OnJoinLobbyRequested;
				Destroy(item.gameObject);
			}
		}

		private void AddItem(ILobby lobby)
		{
			var item = Instantiate(_itemPrefab, _itemContainer);
			item.OnJoinLobbyRequested += OnJoinLobbyRequested;
			item.SetData(lobby);
		}

		private async void OnJoinLobbyRequested(ILobby lobby)
		{
			if (_joiningLobby) return;
			_joiningLobby = true;

			try
			{
				await GameNetworkManager.Instance.JoinLobby(lobby, destroyCancellationToken);
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
