using Localization;
using UnityEngine;

namespace Game.Runtime.Controller
{
	// The player's video and language settings, kept in SettingController. Frame rate and VSync are ours to
	// save and apply; window mode and resolution are the engine's (Screen), which a player build saves itself.
	public static class GameSettings
	{
		public const int UncappedFrameRate = -1;

		public static int TargetFrameRate
		{
			get => SettingController.Data.TargetFrameRate;
			set
			{
				SettingController.Data.TargetFrameRate = value;
				SettingController.Save();
				ApplyFrameRate();
			}
		}

		// With VSync on the display's refresh rate decides and the target frame rate is ignored.
		public static bool VSync
		{
			get => SettingController.Data.VSync;
			set
			{
				SettingController.Data.VSync = value;
				SettingController.Save();
				ApplyFrameRate();
			}
		}

		public static FullScreenMode WindowMode
		{
			get => Screen.fullScreenMode == FullScreenMode.MaximizedWindow ? FullScreenMode.Windowed : Screen.fullScreenMode;
			set => Screen.fullScreenMode = value;
		}

		public static void SetResolution(int width, int height) => Screen.SetResolution(width, height, Screen.fullScreenMode);

		public static bool SetLanguage(string localeCode) => Localizer.TrySetLocale(localeCode);

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void ApplyFrameRate()
		{
			QualitySettings.vSyncCount = VSync ? 1 : 0;
			Application.targetFrameRate = TargetFrameRate;
		}

		// After the plugin resets its statics, before the bootstrap picks a language: the plugin's own
		// PlayerPrefs key is replaced by the settings document.
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
		private static void StoreLanguageInSettings()
		{
			Localizer.LoadLocale = () => SettingController.Data.Language;
			Localizer.SaveLocale = code =>
			{
				SettingController.Data.Language = code ?? string.Empty;
				SettingController.Save();
			};
		}
	}
}
