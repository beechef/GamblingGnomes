using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Threading;
using Game.Runtime.Controller;
using Game.Runtime.GameMode;
using Game.Runtime.GameMode.Config;
using Game.Runtime.UI.Button;
using Game.Runtime.UI.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.MainMenu
{
	// The room half of hosting: name, privacy and the turn timer. Next hands over to the play mode screen,
	// which picks the mode and asks this panel to open the room with what was chosen here. The timer lands
	// in PendingMatchConfig on every turn clock the chosen mode declares, which the mode consumes when it
	// spawns; nothing here ever writes the prefab or the stage assets the entries were built from.
	public class UIRoomSetting : MonoBehaviour
	{
		[SerializeField] private GameModeDatabase _gameModeDatabase;

		[Header("References")]
		[SerializeField] private TMP_InputField _maxPlayersField;

		[Tooltip("Optional. Left blank by the player, the room is named after the host.")]
		[SerializeField] private TMP_InputField _roomNameField;

		[SerializeField] private Toggle _isPrivateToggle;

		[SerializeField] private UIOptionStepper _timerStepper;

		[Header("Room")]
		[Tooltip("How many the room admits. Fixed rather than typed: the table is laid with a set number of chairs, and a room that lets more people in than there are seats is a room where somebody stands.")]
		[MinValue(2)]
		[SerializeField] private int _fixedMaxPlayers = 4;

		[Header("Timer")]
		[Tooltip("Seconds a turn may last, in the order the stepper walks them.")]
		[SerializeField] private List<int> _timerSeconds = new() { 15, 30, 45, 60, 90 };

		[ValueDropdown(nameof(_timerSeconds))]
		[SerializeField] private int _defaultTimerSeconds = 30;

		[Header("Buttons")]
		[Tooltip("Next: on to the play mode screen.")]
		[SerializeField] private UIButton _confirmButton;

		[Tooltip("The close cross: out of the whole play menu.")]
		[SerializeField] private UIButton _cancelButton;

		[Tooltip("Optional. Back to the play choices.")]
		[SerializeField] private UIButton _backButton;

		public event Action OnNextRequested;
		public event Action OnCloseRequested;
		public event Action OnBackRequested;

		private void Awake()
		{
			_confirmButton.OnClick += HandleNext;
			_cancelButton.OnClick += HandleClose;
			if (_backButton) _backButton.OnClick += HandleBack;

			// The room size is fixed, so the field that used to set it is taken off screen rather than left
			// accepting a number nothing reads — a control that ignores what you type into it is worse than
			// no control at all.
			if (_maxPlayersField) _maxPlayersField.gameObject.SetActive(false);

			if (_timerStepper)
			{
				var labels = new List<string>();
				foreach (var seconds in _timerSeconds) labels.Add(seconds.ToString());

				_timerStepper.SetOptions(labels, Mathf.Max(0, _timerSeconds.IndexOf(_defaultTimerSeconds)));
			}
		}

		private void OnDestroy()
		{
			if (_backButton) _backButton.OnClick -= HandleBack;
			_confirmButton.OnClick -= HandleNext;
			_cancelButton.OnClick -= HandleClose;
		}

		private void OnEnable()
		{
			if (!_roomNameField) return;

			_roomNameField.onValueChanged.AddListener(HandleRoomNameChanged);

			// Offered as the player's own name, the room is never nameless unless the player clears it. Only
			// when empty: coming back from the play mode screen keeps what was typed.
			if (string.IsNullOrEmpty(_roomNameField.text))
			{
				var network = GameNetworkManager.Instance;
				_roomNameField.text = network ? network.ResolveLobbyService().LocalUserName : string.Empty;
			}

			HandleRoomNameChanged(_roomNameField.text);
		}

		private void OnDisable()
		{
			if (_roomNameField) _roomNameField.onValueChanged.RemoveListener(HandleRoomNameChanged);
		}

		private int TimerSeconds => _timerStepper && _timerStepper.Index < _timerSeconds.Count
			? _timerSeconds[_timerStepper.Index]
			: _defaultTimerSeconds;

		private bool HasRoomName => !_roomNameField || !string.IsNullOrWhiteSpace(_roomNameField.text);

		// A room needs a name of at least one letter; Next refuses until it has one.
		private void HandleRoomNameChanged(string name) => _confirmButton.IsInteractable = HasRoomName;

		public async Awaitable CreateRoomAsync(GameModeType gameMode, CancellationToken ct = default)
		{
			ApplyTimer(gameMode);

			var network = GameNetworkManager.Instance;
			network.ConfigureLobby(Mathf.Max(2, _fixedMaxPlayers), _isPrivateToggle.isOn, gameMode, _roomNameField ? _roomNameField.text : null);
			await network.StartHost(ct);
		}

		// Cleared first: a choice left from an earlier room would otherwise be applied by a mode nobody tuned.
		private void ApplyTimer(GameModeType gameMode)
		{
			PendingMatchConfig.Clear();

			if (!_gameModeDatabase.TryGetEntry(gameMode, out var entry) || !entry.ModePrefab) return;
			if (!entry.ModePrefab.TryGetComponent<IMatchConfigProvider>(out var provider)) return;

			var entries = new List<MatchConfigEntry>();
			provider.CollectAuthoredConfigEntries(entries);

			foreach (var config in entries)
			{
				if (config.Key == MatchConfigKeys.TurnDuration) PendingMatchConfig.Set(config.Id, config.ClampValue(TimerSeconds));
			}
		}

		private void HandleNext()
		{
			if (HasRoomName) OnNextRequested?.Invoke();
		}

		private void HandleClose() => OnCloseRequested?.Invoke();

		private void HandleBack() => OnBackRequested?.Invoke();
	}
}
