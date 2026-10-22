using System;
using System.IO;
using UnityEngine;

namespace Game.Runtime.Controller
{
	// The one path every setting is loaded and saved through: a SettingData kept as a single JSON document.
	// It lives in persistentDataPath/settings.json, which Steam Auto-Cloud syncs before launch (configured on
	// the Steamworks site: root WinAppDataLocalLow, path <company>/<product>, pattern settings.json).
	public static class SettingController
	{
		private const string FileName = "settings.json";
		private const string LegacyPrefsKey = "settings";

		private static SettingData _data;

		public static event Action OnChanged;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			_data = null;
			OnChanged = null;
		}

		public static SettingData Data => _data ??= Load();

		private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

		// Call after changing Data.
		public static void Save()
		{
			Write(JsonUtility.ToJson(Data));
			OnChanged?.Invoke();
		}

		private static SettingData Load()
		{
			try
			{
				var json = File.Exists(FilePath) ? File.ReadAllText(FilePath) : PlayerPrefs.GetString(LegacyPrefsKey, string.Empty);
				if (!string.IsNullOrEmpty(json))
					return JsonUtility.FromJson<SettingData>(json) ?? CreateDefault();
			}
			catch (Exception exception)
			{
				Debug.LogWarning($"[{nameof(SettingController)}] Saved settings are unreadable, using defaults: {exception.Message}");
			}

			return CreateDefault();
		}

		// Written beside the file and swapped in, so a crash mid-write never leaves Steam a torn document to sync.
		private static void Write(string json)
		{
			try
			{
				var temp = FilePath + ".tmp";
				File.WriteAllText(temp, json);
				if (File.Exists(FilePath))
					File.Replace(temp, FilePath, null);
				else
					File.Move(temp, FilePath);

				PlayerPrefs.DeleteKey(LegacyPrefsKey);
			}
			catch (Exception exception)
			{
				Debug.LogWarning($"[{nameof(SettingController)}] Could not save settings: {exception.Message}");
			}
		}

		// A first launch starts in the system's language when it ships (Spanish as Spain's: the system does not
		// tell the regions apart), every later one in whatever the player chose.
		private static SettingData CreateDefault() => new()
		{
			Language = Application.systemLanguage switch
			{
				SystemLanguage.French => "fr",
				SystemLanguage.Italian => "it",
				SystemLanguage.German => "de",
				SystemLanguage.Spanish => "es",
				SystemLanguage.Portuguese => "pt-br",
				SystemLanguage.Russian => "ru",
				SystemLanguage.ChineseSimplified or SystemLanguage.Chinese => "zh-hans",
				SystemLanguage.ChineseTraditional => "zh-hant",
				SystemLanguage.Japanese => "ja",
				SystemLanguage.Korean => "ko",
				SystemLanguage.Polish => "pl",
				SystemLanguage.Turkish => "tr",
				SystemLanguage.Ukrainian => "uk",
				SystemLanguage.Vietnamese => "vi",
				_ => string.Empty
			}
		};
	}
}
