using System;
using System.Collections.Generic;
using Game.Runtime.Controller;
using Game.Runtime.Lobby;
using Game.Runtime.GameMode;
using Game.Runtime.UI.MainMenu;
using Game.Runtime.UI.Button;
using Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

		[Tooltip("Which mode's rooms the list shows.")]
		[SerializeField] private UIGameModePicker _modeFilter;

		[Tooltip("Shown in place of the rows while searching, and when the search finds no room.")]
		[SerializeField] private TMP_Text _statusLabel;

		public event Action OnCloseRequested;
		public event Action OnBackRequested;

		private bool _isRefreshing;
		private bool _refreshAgain;
		private bool _joiningLobby;
		private readonly List<UIFindLobbyItem> _items = new();

		private void OnEnable()
		{
			if (_closeButton) _closeButton.OnClick += HandleClose;
			if (_backButton) _backButton.OnClick += HandleBack;
			if (_refreshButton) _refreshButton.OnClick += Refresh;
			_modeFilter.OnModeChanged += HandleModeChanged;

			Refresh();
		}

		private void OnDisable()
		{
			_modeFilter.OnModeChanged -= HandleModeChanged;
			if (_refreshButton) _refreshButton.OnClick -= Refresh;
			if (_backButton) _backButton.OnClick -= HandleBack;
			if (_closeButton) _closeButton.OnClick -= HandleClose;
		}

		// async void because a UI callback has nowhere to hand a task back to — so it catches its own
		// exceptions, and the guard flag comes off on every path. Left standing it would make the
		// button dead for the rest of the session.
		private void HandleModeChanged(GameModeType gameMode) => Refresh();

		// A refresh asked for mid-search runs once that search lands, so the list always answers the latest
		// filter rather than the one the slow search started with.
		public async void Refresh()
		{
			if (_isRefreshing)
			{
				_refreshAgain = true;
				return;
			}

			_isRefreshing = true;
			_refreshAgain = false;

			ClearItems();
			ShowStatus(LocalizationKeys.Lobby.Searching);

			var found = 0;

			try
			{
				var lobbies = await GameNetworkManager.Instance.SearchLobby(_modeFilter.Selected, destroyCancellationToken);
				foreach (var lobby in lobbies)
				{
					AddItem(lobby);
					found++;
				}

				LinkNavigation();

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

			if (_refreshAgain && isActiveAndEnabled) Refresh();
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

			_items.Clear();
			LinkNavigation();
		}

		private void AddItem(ILobby lobby)
		{
			var item = Instantiate(_itemPrefab, _itemContainer);
			item.OnJoinLobbyRequested += OnJoinLobbyRequested;
			item.SetData(lobby);
			_items.Add(item);
		}

		// Automatic navigation sends a sideways move from the filter's arrows to whichever row is nearer than the
		// next arrow, so the stops are linked by hand into one column: filter (left/right step its mode), Refresh,
		// then the rows, up/down only. A full room is skipped: a pad cannot press it.
		private void LinkNavigation()
		{
			var filter = _modeFilter.GetComponent<Selectable>();
			var refresh = _refreshButton ? _refreshButton.GetComponent<Selectable>() : null;
			var back = _backButton ? _backButton.GetComponent<Selectable>() : null;
			var joinable = _items.FindAll(item => item.RowSelectable && item.RowSelectable.interactable);
			var firstRow = joinable.Count > 0 ? joinable[0].RowSelectable : back;
			var aboveRows = refresh ? refresh : filter;

			filter.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = refresh ? refresh : firstRow };
			if (refresh) refresh.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = filter, selectOnLeft = filter, selectOnDown = firstRow };

			for (var i = 0; i < joinable.Count; i++)
			{
				joinable[i].RowSelectable.navigation = new Navigation
				{
					mode = Navigation.Mode.Explicit,
					selectOnUp = i > 0 ? joinable[i - 1].RowSelectable : aboveRows,
					selectOnDown = i < joinable.Count - 1 ? joinable[i + 1].RowSelectable : back,
				};
			}
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
