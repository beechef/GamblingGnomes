using System;
using System.Collections.Generic;
using Game.Runtime.Controller;
using Game.Runtime.GameMode;
using Game.Runtime.UI.Button;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.MainMenu
{
	public class UIRoomSetting : MonoBehaviour
	{
		[SerializeField] private GameModeDatabase _gameModeDatabase;

		[Header("References")]
		[SerializeField] private UIMainMenu _mainMenuUI;
		[SerializeField] private TMP_InputField _maxPlayersField;
		[SerializeField] private Toggle _isPrivateToggle;
		[SerializeField] private TMP_Dropdown _gameModeDropdown;
		[SerializeField] private UIButton _confirmButton;
		[SerializeField] private UIButton _cancelButton;

		private readonly List<GameModeType> _dropdownGameModes = new();
		private bool _creatingLobby;

		private void Awake()
		{
			_confirmButton.OnClick += OnConfirmClicked;
			_cancelButton.OnClick += Close;
		}

		private void OnDestroy()
		{
			_confirmButton.OnClick -= OnConfirmClicked;
			_cancelButton.OnClick -= Close;
		}

		private void OnEnable()
		{
			PopulateGameModeDropdown();
		}

		private void PopulateGameModeDropdown()
		{
			_dropdownGameModes.Clear();
			_gameModeDropdown.ClearOptions();

			var options = new List<TMP_Dropdown.OptionData>();
			foreach (var entry in _gameModeDatabase.Entries)
			{
				_dropdownGameModes.Add(entry.GameModeType);
				options.Add(new TMP_Dropdown.OptionData(entry.DisplayName));
			}

			_gameModeDropdown.AddOptions(options);
		}

		private async void OnConfirmClicked()
		{
			if (_creatingLobby) return;
			_creatingLobby = true;

			var maxPlayers = int.TryParse(_maxPlayersField.text, out var parsed) ? Mathf.Max(2, parsed) : 6;
			var isPrivate = _isPrivateToggle.isOn;
			var gameMode = _dropdownGameModes.Count > 0
				? _dropdownGameModes[Mathf.Clamp(_gameModeDropdown.value, 0, _dropdownGameModes.Count - 1)]
				: GameModeType.Sandbox;

			try
			{
				GameNetworkManager.Instance.ConfigureLobby(maxPlayers, isPrivate, gameMode);
				await GameNetworkManager.Instance.StartHost(destroyCancellationToken);

				gameObject.SetActive(false);
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
				_creatingLobby = false;
			}
		}

		public void Close()
		{
			gameObject.SetActive(false);
			_mainMenuUI.Show();
		}
	}
}
