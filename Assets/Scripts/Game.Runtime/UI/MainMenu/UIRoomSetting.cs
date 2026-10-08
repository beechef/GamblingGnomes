using Sirenix.OdinInspector;
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
		[SerializeField] private TMP_InputField _maxPlayersField;

		[Tooltip("Optional. Left blank by the player, the room is named after the host.")]
		[SerializeField] private TMP_InputField _roomNameField;

		[SerializeField] private Toggle _isPrivateToggle;

		[Tooltip("Optional. Left empty, the room hosts the database's first mode.")]
		[SerializeField] private TMP_Dropdown _gameModeDropdown;
		[SerializeField] private UIMatchConfigList _configList;

		[Header("Room")]
		[Tooltip("How many the room admits. Fixed rather than typed: the table is laid with a set number of chairs, and a room that lets more people in than there are seats is a room where somebody stands.")]
		[MinValue(2)]
		[SerializeField] private int _fixedMaxPlayers = 4;

		[Header("Match Settings")]
		[Tooltip("Off, the host is not asked to tune the mode before starting: every stage, module and starting stat plays at whatever the assets say. The list is switched off rather than torn out, so turning it back on is one checkbox.")]
		[SerializeField] private bool _showModeSettings;
		[SerializeField] private UIButton _confirmButton;
		[Tooltip("The close cross: out of the whole play menu.")]
		[SerializeField] private UIButton _cancelButton;

		[Tooltip("Optional. Back to the play choices.")]
		[SerializeField] private UIButton _backButton;

		public event Action OnCloseRequested;
		public event Action OnBackRequested;

		private readonly List<GameModeType> _dropdownGameModes = new();
		private bool _creatingLobby;

		private void Awake()
		{
			_confirmButton.OnClick += OnConfirmClicked;
			_cancelButton.OnClick += HandleClose;
			if (_backButton) _backButton.OnClick += HandleBack;

			// The room size is fixed, so the field that used to set it is taken off screen rather than left
			// accepting a number nothing reads — a control that ignores what you type into it is worse than
			// no control at all.
			if (_maxPlayersField) _maxPlayersField.gameObject.SetActive(false);
		}

		private void OnDestroy()
		{
			if (_backButton) _backButton.OnClick -= HandleBack;
			_confirmButton.OnClick -= OnConfirmClicked;
			_cancelButton.OnClick -= HandleClose;
		}

		private void OnEnable()
		{
			PopulateGameModeDropdown();

			if (_gameModeDropdown) _gameModeDropdown.onValueChanged.AddListener(HandleGameModeChanged);

			// Escape steps back the way Back does.
			UIEscapeStack.Push(HandleBack);

			RebuildConfigList();
		}

		private void OnDisable()
		{
			UIEscapeStack.Remove(HandleBack);
			if (_gameModeDropdown) _gameModeDropdown.onValueChanged.RemoveListener(HandleGameModeChanged);
		}

		public float GetValue(MatchConfigEntry entry) =>
			PendingMatchConfig.TryGet(entry.Id, out var value) ? value : entry.ReadValue();

		public void SetValue(MatchConfigEntry entry, float value) =>
			PendingMatchConfig.Set(entry.Id, entry.ClampValue(value));

		private void HandleGameModeChanged(int index) => RebuildConfigList();

		private void RebuildConfigList()
		{
			if (!_configList) return;

			// A different mode is a different rulebook, and nothing chosen for the last one carries over.
			// Cleared even when the list is hidden: a stale choice from a previous session would otherwise
			// be applied by a mode nobody was offered the chance to tune.
			PendingMatchConfig.Clear();

			if (!_showModeSettings)
			{
				_configList.gameObject.SetActive(false);
				return;
			}

			_configList.gameObject.SetActive(true);

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
			? _dropdownGameModes[Mathf.Clamp(_gameModeDropdown ? _gameModeDropdown.value : 0, 0, _dropdownGameModes.Count - 1)]
			: GameModeType.Main;

		private void PopulateGameModeDropdown()
		{
			_dropdownGameModes.Clear();
			if (_gameModeDropdown) _gameModeDropdown.ClearOptions();

			var options = new List<TMP_Dropdown.OptionData>();
			foreach (var entry in _gameModeDatabase.Entries)
			{
				_dropdownGameModes.Add(entry.GameModeType);
				options.Add(new TMP_Dropdown.OptionData(entry.DisplayName));
			}

			if (_gameModeDropdown) _gameModeDropdown.AddOptions(options);
		}

		private async void OnConfirmClicked()
		{
			if (_creatingLobby) return;
			_creatingLobby = true;

			var maxPlayers = Mathf.Max(2, _fixedMaxPlayers);
			var isPrivate = _isPrivateToggle.isOn;
			var gameMode = SelectedGameMode();

			try
			{
				GameNetworkManager.Instance.ConfigureLobby(maxPlayers, isPrivate, gameMode, _roomNameField ? _roomNameField.text : null);
				await GameNetworkManager.Instance.StartHost(destroyCancellationToken);
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

		private void HandleClose() => OnCloseRequested?.Invoke();

		private void HandleBack() => OnBackRequested?.Invoke();
	}
}
