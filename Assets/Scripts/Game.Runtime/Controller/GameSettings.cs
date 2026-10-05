using Localization;
using UnityEngine;

namespace Game.Runtime.Controller
{
	// The player's own settings, read and written in one place. Frame rate and VSync are ours to save and
	// apply; window mode and resolution are the engine's (Screen), which a player build saves itself; the
	// language is the localization plugin's, which keeps its own key. Keys are saved data: never rename one.
	public static class GameSettings
	{
		public const int UncappedFrameRate = -1;

		private const int DefaultTargetFrameRate = 60;
		private const string TargetFrameRateKey = "settings.video.targetFrameRate";
		private const string VSyncKey = "settings.video.vSync";
		private const string LanguageKey = "localization.locale";

		public static int TargetFrameRate
		{
			get => PlayerPrefs.GetInt(TargetFrameRateKey, DefaultTargetFrameRate);
			set
			{
				PlayerPrefs.SetInt(TargetFrameRateKey, value);
				ApplyFrameRate();
			}
		}

		// With VSync on the display's refresh rate decides and the target frame rate is ignored.
		public static bool VSync
		{
			get => PlayerPrefs.GetInt(VSyncKey, 0) != 0;
			set
			{
				PlayerPrefs.SetInt(VSyncKey, value ? 1 : 0);
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

		// After the plugin resets its statics, before the bootstrap picks a language: a first launch on a
		// Vietnamese system starts in Vietnamese, every later one in whatever the player chose.
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
		private static void DefaultLanguageToSystem() =>
			Localizer.LoadLocale = () =>
				PlayerPrefs.GetString(LanguageKey, Application.systemLanguage == SystemLanguage.Vietnamese ? "vi" : string.Empty);
	}
}
