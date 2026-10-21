using System;
using Game.Runtime.GameMode;
using Game.Runtime.UI.Button;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.MainMenu
{
	// The second half of hosting: one mode at a time, its picture, name and description, stepped between
	// two arrows. Create Room opens the room with what the room panel holds and the mode shown here.
	public class UIGameModeSelect : MonoBehaviour
	{
		[SerializeField] private GameModeDatabase _gameModeDatabase;

		[Tooltip("The panel before this one, which holds the room's name, privacy and timer.")]
		[SerializeField] private UIRoomSetting _roomSetting;

		[Header("Mode")]
		[SerializeField] private Image _preview;
		[SerializeField] private TMP_Text _nameLabel;
		[SerializeField] private TMP_Text _descriptionLabel;
		[SerializeField] private UIButton _previousButton;
		[SerializeField] private UIButton _nextButton;

		[Header("Buttons")]
		[SerializeField] private UIButton _createButton;

		[Tooltip("The close cross: out of the whole play menu.")]
		[SerializeField] private UIButton _cancelButton;

		[Tooltip("Back to the room panel.")]
		[SerializeField] private UIButton _backButton;

		public event Action OnCloseRequested;
		public event Action OnBackRequested;

		private int _index;
		private bool _creatingRoom;

		private void Awake()
		{
			_previousButton.OnClick += HandlePrevious;
			_nextButton.OnClick += HandleNext;
			_createButton.OnClick += HandleCreate;
			_cancelButton.OnClick += HandleClose;
			_backButton.OnClick += HandleBack;
		}

		private void OnDestroy()
		{
			_backButton.OnClick -= HandleBack;
			_cancelButton.OnClick -= HandleClose;
			_createButton.OnClick -= HandleCreate;
			_nextButton.OnClick -= HandleNext;
			_previousButton.OnClick -= HandlePrevious;
		}

		private void OnEnable() => Refresh();

		private int ModeCount => _gameModeDatabase.Entries.Count;

		private void HandlePrevious() => Step(-1);

		private void HandleNext() => Step(1);

		private void Step(int direction)
		{
			if (ModeCount < 2) return;

			_index = (_index + direction + ModeCount) % ModeCount;
			Refresh();
		}

		private void Refresh()
		{
			var steppable = ModeCount > 1;
			_previousButton.IsInteractable = steppable;
			_nextButton.IsInteractable = steppable;
			_createButton.IsInteractable = ModeCount > 0;

			if (ModeCount == 0) return;

			_index = Mathf.Clamp(_index, 0, ModeCount - 1);
			var entry = _gameModeDatabase.Entries[_index];

			_nameLabel.text = entry.DisplayName;
			_descriptionLabel.text = entry.Description;
			_preview.sprite = entry.Preview;
			_preview.enabled = entry.Preview;
		}

		private async void HandleCreate()
		{
			if (_creatingRoom || ModeCount == 0) return;
			_creatingRoom = true;

			try
			{
				await _roomSetting.CreateRoomAsync(_gameModeDatabase.Entries[_index].GameModeType, destroyCancellationToken);
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
			}
			finally
			{
				_creatingRoom = false;
			}
		}

		private void HandleClose() => OnCloseRequested?.Invoke();

		private void HandleBack() => OnBackRequested?.Invoke();
	}
}
