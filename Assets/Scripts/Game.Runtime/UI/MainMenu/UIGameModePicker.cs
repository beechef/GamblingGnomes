using System;
using Game.Runtime.GameMode;
using Game.Runtime.UI.Button;
using Game.Runtime.UI.Settings;
using Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.MainMenu
{
	// One mode of the database at a time, stepped between two arrows: its name, and where the layout has
	// room for them, its picture and description. Shared by hosting, quick match and the room filter.
	public class UIGameModePicker : MonoBehaviour
	{
		[SerializeField] private GameModeDatabase _gameModeDatabase;

		[Tooltip("Optional. The mode's picture.")]
		[SerializeField] private Image _preview;

		[SerializeField] private TMP_Text _nameLabel;

		[Tooltip("Optional. What the mode is, in a line.")]
		[SerializeField] private TMP_Text _descriptionLabel;

		[SerializeField] private UIButton _previousButton;
		[SerializeField] private UIButton _nextButton;

		[Tooltip("Optional. The row whose left/right presses these arrows; empty for a picker standing alone.")]
		[SerializeField] private UISettingRow _row;

		private int _index;

		public event Action<GameModeType> OnModeChanged;

		public bool HasMode => ModeCount > 0;

		public GameModeType Selected => _gameModeDatabase.Entries[Mathf.Clamp(_index, 0, ModeCount - 1)].GameModeType;

		private int ModeCount => _gameModeDatabase.Entries.Count;

		private void Awake()
		{
			_previousButton.OnClick += HandlePrevious;
			_nextButton.OnClick += HandleNext;
			if (_row) _row.OnStepped += HandleRowStepped;
		}

		private void OnDestroy()
		{
			if (_row) _row.OnStepped -= HandleRowStepped;
			_nextButton.OnClick -= HandleNext;
			_previousButton.OnClick -= HandlePrevious;
		}

		private void OnEnable()
		{
			Localizer.OnLocaleChanged += Refresh;
			Refresh();
		}

		private void OnDisable() => Localizer.OnLocaleChanged -= Refresh;

		private void HandlePrevious() => Step(-1);

		private void HandleNext() => Step(1);

		// Through the arrow itself, so a step from the row sounds and looks like a click on it.
		private void HandleRowStepped(int direction)
		{
			var button = direction < 0 ? _previousButton : _nextButton;
			button.Flash();
			button.Submit();
		}

		private void Step(int direction)
		{
			if (ModeCount < 2) return;

			_index = (_index + direction + ModeCount) % ModeCount;
			Refresh();
			OnModeChanged?.Invoke(Selected);
		}

		private void Refresh()
		{
			var steppable = ModeCount > 1;
			_previousButton.IsInteractable = steppable;
			_nextButton.IsInteractable = steppable;

			if (ModeCount == 0) return;

			_index = Mathf.Clamp(_index, 0, ModeCount - 1);
			var entry = _gameModeDatabase.Entries[_index];

			_nameLabel.text = entry.DisplayName;
			if (_descriptionLabel) _descriptionLabel.text = entry.Description;

			if (!_preview) return;

			_preview.sprite = entry.Preview;
			_preview.enabled = entry.Preview;
		}
	}
}
