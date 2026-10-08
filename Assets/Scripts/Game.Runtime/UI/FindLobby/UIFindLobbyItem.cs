using System;
using Game.Runtime.Controller;
using Game.Runtime.GameMode;
using Game.Runtime.Lobby;
using Game.Runtime.UI.Button;
using TMPro;
using UnityEngine;

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

		private ILobby _currentLobby;

		private void Awake()
		{
			_joinButton.OnClick += OnJoinButtonClicked;
		}

		private void OnDestroy()
		{
			_joinButton.OnClick -= OnJoinButtonClicked;
		}

		public void SetData(ILobby lobby)
		{
			_currentLobby = lobby;

			_nameText.text = lobby.GetData(LobbyConstant.RoomNameKey);
			var open = lobby.MemberCount < lobby.MaxMembers;
			var count = open
				? $"<color=#{ColorUtility.ToHtmlStringRGB(_openCountColor)}>{lobby.MemberCount}</color>"
				: lobby.MemberCount.ToString();
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
