using System;
using System.Collections.Generic;
using Game.Runtime.Controller;
using Game.Runtime.GameMode;
using Game.Runtime.GameMode.Config;
using Game.Runtime.UI.Button;
using Game.Runtime.UI.Config;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.MainMenu
{
	// Also the pre-scene surface of the match config: the selected mode's prefab is asked for its
	// tunables and the host's choices land in PendingMatchConfig, which the mode consumes when it
	// spawns. Nothing here ever writes the prefab or the stage assets the entries were built from.
	public class UIRoomSetting : MonoBehaviour, IMatchConfigValueAccess
	{
		[SerializeField] private GameModeDatabase _gameModeDatabase;

		[Header("References")]
		[SerializeField] private UIMainMenu _mainMenuUI;
		[SerializeField] private TMP_InputField _maxPlayersField;
		[SerializeField] private Toggle _isPrivateToggle;
		[SerializeField] private TMP_Dropdown _gameModeDropdown;
		[SerializeField] private UIMatchConfigList _configList;
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

			_gameModeDropdown.onValueChanged.AddListener(HandleGameModeChanged);

			RebuildConfigList();
		}

		private void OnDisable()
		{
			_gameModeDropdown.onValueChanged.RemoveListener(HandleGameModeChanged);
		}

		public float GetValue(MatchConfigEntry entry) =>
			PendingMatchConfig.TryGet(entry.Id, out var value) ? value : entry.ReadValue();

		public void SetValue(MatchConfigEntry entry, float value) =>
			PendingMatchConfig.Set(entry.Id, entry.ClampValue(value));

		private void HandleGameModeChanged(int index) => RebuildConfigList();

		private void RebuildConfigList()
		{
			if (!_configList) return;

			// A different mode is a different rulebook — nothing chosen for the last one carries over.
			PendingMatchConfig.Clear();

			var entries = new List<MatchConfigEntry>();
			var selected = SelectedGameMode();

			foreach (var entry in _gameModeDatabase.Entries)
			{
				if (entry.GameModeType != selected || !entry.ModePrefab) continue;

				if (entry.ModePrefab.TryGetComponent<IMatchConfigProvider>(out var provider))
				{
					provider.CollectAuthoredConfigEntries(entries);
				}

				break;
			}

			_configList.Build(entries, this);
			_configList.SetEditable(true);
		}

		private GameModeType SelectedGameMode() => _dropdownGameModes.Count > 0
			? _dropdownGameModes[Mathf.Clamp(_gameModeDropdown.value, 0, _dropdownGameModes.Count - 1)]
			: GameModeType.Sandbox;

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
			var gameMode = SelectedGameMode();

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
