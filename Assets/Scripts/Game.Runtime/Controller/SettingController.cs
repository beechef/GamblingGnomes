using System;
using UnityEngine;

namespace Game.Runtime.Controller
{
	// The one path every setting is loaded and saved through: a SettingData kept as a single JSON document.
	// Today it lives under one PlayerPrefs key; moving it to a file (for Steam Cloud) changes only Load/Write.
	public static class SettingController
	{
		private const string PrefsKey = "settings";

		private static SettingData _data;

		public static event Action OnChanged;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			_data = null;
			OnChanged = null;
		}

		public static SettingData Data => _data ??= Load();

		// Call after changing Data.
		public static void Save()
		{
			Write(JsonUtility.ToJson(Data));
			OnChanged?.Invoke();
		}

		private static SettingData Load()
		{
			var json = PlayerPrefs.GetString(PrefsKey, string.Empty);
			if (!string.IsNullOrEmpty(json))
			{
				try
				{
					return JsonUtility.FromJson<SettingData>(json) ?? CreateDefault();
				}
				catch (Exception exception)
				{
					Debug.LogWarning($"[{nameof(SettingController)}] Saved settings are unreadable, using defaults: {exception.Message}");
				}
			}

			return CreateDefault();
		}

		private static void Write(string json) => PlayerPrefs.SetString(PrefsKey, json);

		// A first launch on a Vietnamese system starts in Vietnamese, every later one in whatever the player chose.
		private static SettingData CreateDefault() => new()
		{
			Language = Application.systemLanguage == SystemLanguage.Vietnamese ? "vi" : string.Empty
		};
	}
}
