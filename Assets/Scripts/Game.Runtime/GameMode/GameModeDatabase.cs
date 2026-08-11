using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode
{
	[CreateAssetMenu(fileName = "GameModeDatabase", menuName = "Game/Game Mode Database")]
	public class GameModeDatabase : ScriptableObject
	{
		[Serializable]
		public struct GameModeEntry
		{
			public GameModeType GameModeType;
			public string SceneName;
			public string DisplayName;
		}

		[SerializeField] private List<GameModeEntry> _entries = new();

		public IReadOnlyList<GameModeEntry> Entries => _entries;

		public bool TryGetEntry(GameModeType gameModeType, out GameModeEntry entry)
		{
			foreach (var candidate in _entries)
			{
				if (candidate.GameModeType != gameModeType) continue;

				entry = candidate;
				return true;
			}

			entry = default;
			return false;
		}
	}
}
