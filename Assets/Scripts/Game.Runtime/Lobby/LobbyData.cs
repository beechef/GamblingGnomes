using System;

namespace Game.Runtime.Lobby
{
	[Serializable]
	public struct LobbyData
	{
		public string Key;
		public string Value;

		public LobbyData(string key, string value)
		{
			Key = key;
			Value = value;
		}
	}
}
