using System;
using System.Collections.Generic;
using Game.Runtime.UI.Button;
using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.Settings
{
	// One choice out of a list, stepped between two buttons with the chosen option's words between them.
	// Stepping past either end wraps round to the other.
	public class UIOptionStepper : MonoBehaviour
	{
		[SerializeField] private UIButton _previousButton;
		[SerializeField] private UIButton _nextButton;
		[SerializeField] private TMP_Text _valueLabel;

		[Tooltip("The row whose left/right presses these arrows; empty for a stepper standing alone.")]
		[SerializeField] private UISettingRow _row;

		private readonly List<string> _options = new();
		private bool _isInteractable = true;

		public int Index { get; private set; }

		public event Action<int> OnIndexChanged;

		public bool IsInteractable
		{
			get => _isInteractable;
			set
			{
				_isInteractable = value;
				Refresh();
			}
		}

		private void Awake()
		{
			if (_previousButton) _previousButton.OnClick += HandlePrevious;
			if (_nextButton) _nextButton.OnClick += HandleNext;
			if (_row) _row.OnStepped += HandleRowStepped;
		}

		private void OnDestroy()
		{
			if (_row) _row.OnStepped -= HandleRowStepped;
			if (_previousButton) _previousButton.OnClick -= HandlePrevious;
			if (_nextButton) _nextButton.OnClick -= HandleNext;
		}

		public void SetOptions(IEnumerable<string> options, int index)
		{
			_options.Clear();
			_options.AddRange(options);
			Index = Mathf.Clamp(index, 0, Mathf.Max(0, _options.Count - 1));
			Refresh();
		}

		private void HandlePrevious() => Step(-1);

		private void HandleNext() => Step(1);

		// Through the arrow itself, so a step from the row sounds and looks like a click on it.
		private void HandleRowStepped(int direction)
		{
			var button = direction < 0 ? _previousButton : _nextButton;
			if (!button) return;

			button.Flash();
			button.Submit();
		}

		private void Step(int direction)
		{
			if (!_isInteractable || _options.Count < 2) return;

			Index = (Index + direction + _options.Count) % _options.Count;
			Refresh();
			OnIndexChanged?.Invoke(Index);
		}

		private void Refresh()
		{
			if (_valueLabel) _valueLabel.text = Index < _options.Count ? _options[Index] : string.Empty;
			var steppable = _isInteractable && _options.Count > 1;
			if (_previousButton) _previousButton.IsInteractable = steppable;
			if (_nextButton) _nextButton.IsInteractable = steppable;
		}
	}
}
