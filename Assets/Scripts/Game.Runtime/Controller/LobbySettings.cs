using System;
using System.Collections.Generic;
using Game.Runtime.GameMode;
using Game.Runtime.Lobby;

namespace Game.Runtime.Controller
{
	[Serializable]
	public struct LobbySettings
	{
		public int MaxPlayers;
		public bool IsPrivate;
		public GameModeType SelectedGameMode;
		public List<LobbyData> GameSearchStrings;
		public List<LobbyData> LobbyData;

		public LobbySettings(int maxPlayers, bool isPrivate, GameModeType selectedGameMode, List<LobbyData> gameSearchStrings, List<LobbyData> lobbyData)
		{
			MaxPlayers = maxPlayers;
			IsPrivate = isPrivate;
			SelectedGameMode = selectedGameMode;
			GameSearchStrings = gameSearchStrings;
			LobbyData = lobbyData;
		}
	}
}
