using System.Collections.Generic;
using System.Linq;
using Game.Runtime.Platform;
using Localization;
using UnityEngine;

namespace Game.Runtime.Controller
{
	// The player's video and language settings, kept in SettingController and applied before the first scene.
	// Window mode and resolution override what the engine saved for itself. Steam Auto-Cloud carries the document
	// between machines, so both are checked against this one as they are read.
	public static class GameSettings
	{
		public const int UncappedFrameRate = -1;

		public static bool FirstTime
		{
			get => SettingController.Data.FirstTime;
			set
			{
				SettingController.Data.FirstTime = value;
				SettingController.Save();
			}
		}

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
			get
			{
				var saved = SettingController.Data.WindowMode;
				return saved == FullScreenMode.ExclusiveFullScreen && !ExclusiveFullScreenSupported ? FullScreenMode.FullScreenWindow : saved;
			}
			set
			{
				SettingController.Data.WindowMode = value;
				SettingController.Save();
				ApplyDisplay();
			}
		}

		public static Vector2Int Resolution => FitToDisplay(
			new Vector2Int(SettingController.Data.ResolutionWidth, SettingController.Data.ResolutionHeight), NativeResolution);

		// Under Proton a refused exclusive mode change crashes the player inside d3d12core.
		public static bool ExclusiveFullScreenSupported => !WineRuntime.IsRunning;

		public static Vector2Int NativeResolution => LargestLandscape(Screen.resolutions.Append(Screen.currentResolution));

		// Long edge first: Steam Deck's panel is portrait, and under Proton its modes are reported that way.
		public static Vector2Int LargestLandscape(IEnumerable<Resolution> modes)
		{
			var largest = Vector2Int.zero;

			foreach (var mode in modes)
			{
				largest.x = Mathf.Max(largest.x, Mathf.Max(mode.width, mode.height));
				largest.y = Mathf.Max(largest.y, Mathf.Min(mode.width, mode.height));
			}

			return largest;
		}

		public static Vector2Int FitToDisplay(Vector2Int saved, Vector2Int native) =>
			saved.x <= 0 || saved.y <= 0 || saved.x > native.x || saved.y > native.y ? native : saved;

		public static void SetResolution(int width, int height)
		{
			SettingController.Data.ResolutionWidth = width;
			SettingController.Data.ResolutionHeight = height;
			SettingController.Save();
			ApplyDisplay();
		}

		private static void ApplyDisplay()
		{
			var resolution = Resolution;
			Screen.SetResolution(resolution.x, resolution.y, WindowMode);
		}

		public static bool SetLanguage(string localeCode) => Localizer.TrySetLocale(localeCode);

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void ApplyFrameRate()
		{
			QualitySettings.vSyncCount = VSync ? 1 : 0;
			Application.targetFrameRate = TargetFrameRate;
		}

		// The engine restores its own saved mode first, so a player that once went exclusive under Wine would
		// otherwise crash on every launch. The editor's Game view is not ours to resize.
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void ApplySavedDisplay()
		{
			if (Application.isEditor) return;

			if (!ExclusiveFullScreenSupported)
			{
				var native = NativeResolution;
				Debug.Log($"[GameSettings] Wine: mode {Screen.fullScreenMode}, window {Screen.width}x{Screen.height}, display {Screen.currentResolution.width}x{Screen.currentResolution.height}, native {native.x}x{native.y}, {Screen.resolutions.Length} modes");
			}

			ApplyDisplay();
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
