using System;
using System.Collections.Generic;
using System.Linq;
using Game.Runtime.Controller;
using Game.Runtime.UI.Button;
using Game.Runtime.UI.Selection;
using Localization;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Runtime.UI.Settings
{
	// The player's settings: a menu of sections, each its own panel. Graphics are window mode, resolution,
	// frame rate cap and VSync; the language panel lists every locale. Each choice is applied the moment it is
	// made. Opened from the main menu and the pause menu; whoever opened it hears OnClosed.
	public class UISettingsScreen : MonoBehaviour
	{
		private static readonly FullScreenMode[] WindowModes =
			{ FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };

		// The common 16:9 sizes, offered up to what the display can show, rather than every mode it reports.
		private static readonly Vector2Int[] ResolutionPresets =
			{ new(1280, 720), new(1600, 900), new(1920, 1080), new(2560, 1440), new(3840, 2160) };

		private static readonly int[] FrameRates = { 30, 60, 120, 144, 165, 240, GameSettings.UncappedFrameRate };

		[Header("Panels")]
		[Required]
		[SerializeField] private UIPanelStateGroup _panels;

		[Required]
		[SerializeField] private GameObject _menuPanel;

		[Required]
		[SerializeField] private GameObject _graphicPanel;

		[Required]
		[SerializeField] private GameObject _audioPanel;

		[Required]
		[SerializeField] private GameObject _languagePanel;

		[Header("Menu")]
		[Required]
		[SerializeField] private UISelectionGroup _menuGroup;

		[Required]
		[SerializeField] private UISelectionItem _graphicItem;

		[Required]
		[SerializeField] private UISelectionItem _audioItem;

		[Required]
		[SerializeField] private UISelectionItem _languageItem;

		[Tooltip("Every way out of the whole screen: each panel's close cross and the menu's Back.")]
		[SerializeField] private UIButton[] _closeButtons = Array.Empty<UIButton>();

		[Tooltip("Each section's Back, returning to the menu.")]
		[SerializeField] private UIButton[] _backButtons = Array.Empty<UIButton>();

		[Header("Graphics")]
		[Required] [SerializeField] private UIOptionStepper _windowMode;
		[Required] [SerializeField] private UIOptionStepper _resolution;
		[Required] [SerializeField] private UIOptionStepper _frameRate;
		[Required] [SerializeField] private UIOptionStepper _vSync;

		[Header("Language")]
		[Required]
		[SerializeField] private UISelectionGroup _languageGroup;

		[Tooltip("One entry per locale, instantiated under the language group (Button_Text).")]
		[Required]
		[SerializeField] private UISelectionItem _languageItemPrefab;

		private readonly List<Vector2Int> _resolutions = new();
		private readonly List<UISelectionItem> _languageItems = new();

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
			_menuGroup.OnSubmitted += HandleMenuSubmitted;
			_languageGroup.OnSubmitted += HandleLanguageSubmitted;
			foreach (var button in _closeButtons) if (button) button.OnClick += Close;
			foreach (var button in _backButtons) if (button) button.OnClick += ShowMenu;
			Localizer.OnLocaleChanged += Refresh;
			UIEscapeStack.Push(HandleEscape);

			Refresh();
			ShowMenu();
		}

		private void OnDisable()
		{
			UIEscapeStack.Remove(HandleEscape);
			Localizer.OnLocaleChanged -= Refresh;
			foreach (var button in _backButtons) if (button) button.OnClick -= ShowMenu;
			foreach (var button in _closeButtons) if (button) button.OnClick -= Close;
			_languageGroup.OnSubmitted -= HandleLanguageSubmitted;
			_menuGroup.OnSubmitted -= HandleMenuSubmitted;
			_vSync.OnIndexChanged -= ApplyVSync;
			_frameRate.OnIndexChanged -= ApplyFrameRate;
			_resolution.OnIndexChanged -= ApplyResolution;
			_windowMode.OnIndexChanged -= ApplyWindowMode;
		}

		// Escape steps back one panel: out of a section to the menu, out of the menu to whoever opened it.
		private void HandleEscape()
		{
			if (_panels.IsShowing(_menuPanel))
			{
				Close();
				return;
			}

			ShowMenu();
			UIEscapeStack.Push(HandleEscape);
		}

		private void ShowMenu() => _panels.Show(_menuPanel);

		private void HandleMenuSubmitted(UISelectionItem item)
		{
			if (item == _graphicItem) _panels.Show(_graphicPanel);
			else if (item == _audioItem) _panels.Show(_audioPanel);
			else if (item == _languageItem) ShowLanguages();
		}

		private void ShowLanguages()
		{
			_panels.Show(_languagePanel);

			// Opened on the language in use, so the list says which one it is before anything is pointed at.
			var index = Localizer.Locales.ToList().FindIndex(locale => locale.LocaleCode == Localizer.CurrentLocaleCode);
			if (index < 0 || index >= _languageItems.Count) return;

			var current = _languageItems[index];
			_languageGroup.Select(current, true);
			if (EventSystem.current) EventSystem.current.SetSelectedGameObject(current.gameObject);
		}

		private void HandleLanguageSubmitted(UISelectionItem item)
		{
			var index = _languageItems.IndexOf(item);
			if (index >= 0) GameSettings.SetLanguage(Localizer.Locales[index].LocaleCode);
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

			RefreshLanguages();
		}

		// Entries already made are re-labelled rather than rebuilt, so reopening costs nothing new.
		private void RefreshLanguages()
		{
			var locales = Localizer.Locales;

			while (_languageItems.Count < locales.Count)
			{
				_languageItems.Add(Instantiate(_languageItemPrefab, _languageGroup.transform));
			}

			for (var i = 0; i < _languageItems.Count; i++)
			{
				var used = i < locales.Count;
				_languageItems[i].gameObject.SetActive(used);
				if (used) _languageItems[i].GetComponentInChildren<TMP_Text>().text = locales[i].DisplayName;
			}

			_languageGroup.SetItems(_languageItems.Take(locales.Count));
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
	}
}
