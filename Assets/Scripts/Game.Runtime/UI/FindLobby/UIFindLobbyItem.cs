using System;
using Game.Runtime.Controller;
using Game.Runtime.GameMode;
using Game.Runtime.UI.Button;
using Steamworks.Data;
using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.FindLobby
{
	public class UIFindLobbyItem : MonoBehaviour
	{
		public event Action<Lobby> OnJoinLobbyRequested;

		[SerializeField] private GameModeDatabase _gameModeDatabase;

		[SerializeField] private TMP_Text _nameText;
		[SerializeField] private TMP_Text _gameModeText;
		[SerializeField] private TMP_Text _playerCountText;
		[SerializeField] private UIButton _joinButton;

		private Lobby _currentLobby;

		private void Awake()
		{
			_joinButton.OnClick += OnJoinButtonClicked;
		}

		private void OnDestroy()
		{
			_joinButton.OnClick -= OnJoinButtonClicked;
		}

		public void SetData(Lobby lobby)
		{
			_currentLobby = lobby;

			_nameText.text = lobby.GetData(LobbyConstant.RoomNameKey);
			_playerCountText.text = $"{lobby.MemberCount}/{lobby.MaxMembers}";

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
