using System;
using Game.Runtime.Controller;
using Game.Runtime.GameMode;
using Game.Runtime.Lobby;
using Game.Runtime.UI.Button;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.FindLobby
{
	public class UIFindLobbyItem : MonoBehaviour
	{
		public event Action<ILobby> OnJoinLobbyRequested;

		[SerializeField] private GameModeDatabase _gameModeDatabase;

		[SerializeField] private TMP_Text _nameText;

		[Tooltip("Optional: a row without a mode column leaves it empty.")]
		[SerializeField] private TMP_Text _gameModeText;

		[SerializeField] private TMP_Text _playerCountText;

		[Tooltip("Colour of the member count while the room still has a free seat.")]
		[SerializeField] private Color _openCountColor = new(0.788f, 0.243f, 0.251f);

		[SerializeField] private UIButton _joinButton;

		[Tooltip("The whole row, the pad's one stop: pressing it presses Join.")]
		[SerializeField] private UIButton _rowButton;

		private ILobby _currentLobby;

		public Selectable RowSelectable { get; private set; }

		private void Awake()
		{
			_joinButton.OnClick += OnJoinButtonClicked;
			if (_rowButton)
			{
				RowSelectable = _rowButton.GetComponent<Selectable>();
				_rowButton.OnClick += HandleRowClicked;
			}
		}

		private void OnDestroy()
		{
			if (_rowButton) _rowButton.OnClick -= HandleRowClicked;
			_joinButton.OnClick -= OnJoinButtonClicked;
		}

		// Through the Join button itself, so a press on the row sounds and looks like a click on it.
		private void HandleRowClicked()
		{
			_joinButton.Flash();
			_joinButton.Submit();
		}

		public void SetData(ILobby lobby)
		{
			_currentLobby = lobby;

			_nameText.text = lobby.GetData(LobbyConstant.RoomNameKey);
			var players = lobby.PlayerCount;
			var open = players < lobby.MaxMembers;
			var count = open
				? $"<color=#{ColorUtility.ToHtmlStringRGB(_openCountColor)}>{players}</color>"
				: players.ToString();
			_playerCountText.text = $"{count}/{lobby.MaxMembers}";
			_joinButton.IsInteractable = open;

			if (!_gameModeText) return;

			var gameModeString = lobby.GetData(LobbyConstant.GameModeKey);
			if (Enum.TryParse<GameModeType>(gameModeString, out var gameMode) &&
				_gameModeDatabase.TryGetEntry(gameMode, out var entry))
			{
				_gameModeText.text = entry.DisplayName;
			}
			else
			{
				_gameModeText.text = gameModeString;
			}
		}

		private void OnJoinButtonClicked()
		{
			OnJoinLobbyRequested?.Invoke(_currentLobby);
		}
	}
}
