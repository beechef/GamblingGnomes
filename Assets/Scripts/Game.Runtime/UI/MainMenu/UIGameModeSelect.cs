using System;
using Game.Runtime.UI.Button;
using UnityEngine;

namespace Game.Runtime.UI.MainMenu
{
	// The second half of hosting: the mode picker, then Create Room opens the room with what the room panel
	// holds and the mode picked here.
	public class UIGameModeSelect : MonoBehaviour
	{
		[Tooltip("The panel before this one, which holds the room's name, privacy and timer.")]
		[SerializeField] private UIRoomSetting _roomSetting;

		[SerializeField] private UIGameModePicker _picker;

		[Header("Buttons")]
		[SerializeField] private UIButton _createButton;

		[Tooltip("The close cross: out of the whole play menu.")]
		[SerializeField] private UIButton _cancelButton;

		[Tooltip("Back to the room panel.")]
		[SerializeField] private UIButton _backButton;

		public event Action OnCloseRequested;
		public event Action OnBackRequested;

		private bool _creatingRoom;

		private void Awake()
		{
			_createButton.OnClick += HandleCreate;
			_cancelButton.OnClick += HandleClose;
			_backButton.OnClick += HandleBack;
		}

		private void OnDestroy()
		{
			_backButton.OnClick -= HandleBack;
			_cancelButton.OnClick -= HandleClose;
			_createButton.OnClick -= HandleCreate;
		}

		private void OnEnable() => _createButton.IsInteractable = _picker.HasMode;

		private async void HandleCreate()
		{
			if (_creatingRoom || !_picker.HasMode) return;
			_creatingRoom = true;

			try
			{
				await _roomSetting.CreateRoomAsync(_picker.Selected, destroyCancellationToken);
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
