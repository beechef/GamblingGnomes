using System;
using System.Collections.Generic;
using System.Linq;
using Game.Runtime.Controller;
using Game.Runtime.UI.Button;
using Localization;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.UI.Settings
{
	// The player's settings: window mode, resolution, frame rate cap, VSync and language, each applied the
	// moment it is stepped. Opened from the main menu and the pause menu; whoever opened it hears OnClosed.
	public class UISettingsScreen : MonoBehaviour
	{
		private static readonly FullScreenMode[] WindowModes =
			{ FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };

		// The common 16:9 sizes, offered up to what the display can show, rather than every mode it reports.
		private static readonly Vector2Int[] ResolutionPresets =
			{ new(1280, 720), new(1600, 900), new(1920, 1080), new(2560, 1440), new(3840, 2160) };

		private static readonly int[] FrameRates = { 30, 60, 120, 144, 165, 240, GameSettings.UncappedFrameRate };

		[Header("Graphics")]
		[Required] [SerializeField] private UIOptionStepper _windowMode;
		[Required] [SerializeField] private UIOptionStepper _resolution;
		[Required] [SerializeField] private UIOptionStepper _frameRate;
		[Required] [SerializeField] private UIOptionStepper _vSync;

		[Header("Language")]
		[Required] [SerializeField] private UIOptionStepper _language;

		[SerializeField] private UIButton _closeButton;

		private readonly List<Vector2Int> _resolutions = new();

		public event Action OnClosed;

		public void Open() => gameObject.SetActive(true);

		public void Close()
		{
			if (!gameObject.activeSelf) return;

			gameObject.SetActive(false);
			OnClosed?.Invoke();
		}

		private void OnEnable()
		{
			_windowMode.OnIndexChanged += ApplyWindowMode;
			_resolution.OnIndexChanged += ApplyResolution;
			_frameRate.OnIndexChanged += ApplyFrameRate;
			_vSync.OnIndexChanged += ApplyVSync;
			_language.OnIndexChanged += ApplyLanguage;
			if (_closeButton) _closeButton.OnClick += Close;
			Localizer.OnLocaleChanged += Refresh;
			UIEscapeStack.Push(Close);

			Refresh();
		}

		private void OnDisable()
		{
			UIEscapeStack.Remove(Close);
			Localizer.OnLocaleChanged -= Refresh;
			if (_closeButton) _closeButton.OnClick -= Close;
			_language.OnIndexChanged -= ApplyLanguage;
			_vSync.OnIndexChanged -= ApplyVSync;
			_frameRate.OnIndexChanged -= ApplyFrameRate;
			_resolution.OnIndexChanged -= ApplyResolution;
			_windowMode.OnIndexChanged -= ApplyWindowMode;
		}

		private void Refresh()
		{
			_windowMode.SetOptions(
				new[]
				{
					Localizer.Get(LocalizationKeys.Settings.WindowMode.Fullscreen),
					Localizer.Get(LocalizationKeys.Settings.WindowMode.Borderless),
					Localizer.Get(LocalizationKeys.Settings.WindowMode.Windowed)
				},
				Math.Max(0, Array.IndexOf(WindowModes, GameSettings.WindowMode)));

			_resolutions.Clear();
			var display = Screen.currentResolution;
			_resolutions.AddRange(ResolutionPresets.Where(r => r.x <= display.width && r.y <= display.height));

			var current = new Vector2Int(Screen.width, Screen.height);
			// A window dragged to another size still shows what it is.
			if (!_resolutions.Contains(current)) _resolutions.Add(current);
			_resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

			_resolution.SetOptions(_resolutions.Select(r => $"{r.x} × {r.y}"), _resolutions.IndexOf(current));

			var vSync = GameSettings.VSync;
			_frameRate.SetOptions(
				FrameRates.Select(rate => rate == GameSettings.UncappedFrameRate ? Localizer.Get(LocalizationKeys.Settings.Fps.Unlimited) : rate.ToString()),
				NearestFrameRateIndex(GameSettings.TargetFrameRate));

			// VSync overrides the cap, so the cap is dimmed while it rules.
			_frameRate.IsInteractable = !vSync;

			_vSync.SetOptions(new[] { Localizer.Get(LocalizationKeys.Common.Off), Localizer.Get(LocalizationKeys.Common.On) }, vSync ? 1 : 0);

			var locales = Localizer.Locales;
			_language.SetOptions(locales.Select(locale => locale.DisplayName),
				locales.ToList().FindIndex(locale => locale.LocaleCode == Localizer.CurrentLocaleCode));
		}

		private static int NearestFrameRateIndex(int target)
		{
			if (target <= 0) return FrameRates.Length - 1;

			var best = 0;
			for (var i = 0; i < FrameRates.Length; i++)
			{
				if (FrameRates[i] > 0 && Mathf.Abs(FrameRates[i] - target) < Mathf.Abs(FrameRates[best] - target)) best = i;
			}

			return best;
		}

		private void ApplyWindowMode(int index) => GameSettings.WindowMode = WindowModes[index];

		private void ApplyResolution(int index) => GameSettings.SetResolution(_resolutions[index].x, _resolutions[index].y);

		private void ApplyFrameRate(int index) => GameSettings.TargetFrameRate = FrameRates[index];

		private void ApplyVSync(int index)
		{
			GameSettings.VSync = index == 1;
			_frameRate.IsInteractable = index == 0;
		}

		private void ApplyLanguage(int index) => GameSettings.SetLanguage(Localizer.Locales[index].LocaleCode);
	}
}
