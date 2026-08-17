using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
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

			[Tooltip("Scene the mode loads additively. Picked from Build Settings — a scene typed by hand fails at load, long after the typo.")]
			[ValueDropdown(nameof(BuildSettingsScenes))]
			[InfoBox("This scene is not in Build Settings, so the mode will fail to load.", InfoMessageType.Error, nameof(IsSceneMissing))]
			public string SceneName;

			public string DisplayName;

			private bool IsSceneMissing => !string.IsNullOrEmpty(SceneName) && !BuildSettingsScenes.Contains(SceneName);

			private static List<string> BuildSettingsScenes
			{
				get
				{
					var scenes = new List<string>();

#if UNITY_EDITOR
					foreach (var scene in UnityEditor.EditorBuildSettings.scenes)
					{
						if (!scene.enabled) continue;

						scenes.Add(System.IO.Path.GetFileNameWithoutExtension(scene.path));
					}
#endif

					return scenes;
				}
			}
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
