using Game.Runtime.Controller;
using Game.Runtime.GameMode.Config;
using Game.Runtime.GameMode.Poker;
using Game.Runtime.UI.Button;
using Game.Runtime.UI.Config;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The in-game surface of the match config: a button beside Start while the table waits, host only —
	// the same gate as the start button itself. Values go up through the store's RPC and come back
	// replicated, so what the panel shows is what the server actually holds.
	public class UIPokerConfigPanel : UIPokerView, IMatchConfigValueAccess
	{
		[Header("References")]
		[SerializeField] private UIButton _openButton;

		[Tooltip("A child, never this view's own GameObject — a view that switches itself off can never hear the event that would wake it.")]
		[SerializeField] private GameObject _panel;

		[SerializeField] private UIButton _closeButton;
		[SerializeField] private UIMatchConfigList _configList;

		private bool _cursorHeld;
		private bool _built;

		private bool IsOpen => _panel && _panel.activeSelf;

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnBind()
		{
			if (_openButton) _openButton.OnClick += OpenPanel;
			if (_closeButton) _closeButton.OnClick += ClosePanel;

			Data.Phase.OnValueChanged += HandlePhaseChanged;

			if (GameMode.ConfigData) GameMode.ConfigData.OnValuesChanged += HandleValuesChanged;

			_built = false;

			Refresh();
		}

		protected override void OnUnbind()
		{
			if (_openButton) _openButton.OnClick -= OpenPanel;
			if (_closeButton) _closeButton.OnClick -= ClosePanel;

			if (Data) Data.Phase.OnValueChanged -= HandlePhaseChanged;
			if (GameMode && GameMode.ConfigData) GameMode.ConfigData.OnValuesChanged -= HandleValuesChanged;

			ClosePanel();

			if (_configList) _configList.Clear();

			_built = false;
		}

		public float GetValue(MatchConfigEntry entry)
		{
			if (GameMode && GameMode.ConfigData && GameMode.ConfigData.TryGetValue(entry.Id, out var value)) return value;

			return entry.ReadValue();
		}

		public void SetValue(MatchConfigEntry entry, float value)
		{
			if (!GameMode || !GameMode.ConfigData) return;

			GameMode.ConfigData.SubmitConfigValueRPC(new FixedString64Bytes(entry.Id), value);
		}

		private void HandlePhaseChanged(PokerPhase previous, PokerPhase current) => Refresh();

		private void HandleValuesChanged()
		{
			if (IsOpen && _configList) _configList.RefreshValues();
		}

		private void Refresh()
		{
			var isHost = NetworkManager.Singleton && NetworkManager.Singleton.IsHost;
			var visible = isHost && Data && Data.Phase.Value == PokerPhase.Waiting;

			// if (_openButton && _openButton.gameObject.activeSelf != visible) _openButton.gameObject.SetActive(visible);

			// The deal starting is the table's own way of saying the rules are settled.
			if (!visible && IsOpen) ClosePanel();
			else if (IsOpen) RefreshEditable();
		}

		private void OpenPanel()
		{
			if (IsOpen || !_panel || !_configList) return;
			if (!GameMode || !GameMode.ConfigData) return;

			// Entries are registered before any view binds, so one build per bind is enough — and the
			// rows survive between opens rather than being torn down and redrawn each time.
			if (!_built)
			{
				_configList.Build(GameMode.ConfigData.Entries, this);
				_built = true;
			}

			RefreshEditable();
			_configList.RefreshValues();

			_panel.SetActive(true);

			CursorController.RequestUnlock();
			_cursorHeld = true;
		}

		// The single funnel every close path goes through — the button, the phase leaving Waiting, and
		// the view unbinding — so the cursor is handed back exactly once whichever one fires.
		private void ClosePanel()
		{
			if (_panel && _panel.activeSelf) _panel.SetActive(false);

			if (!_cursorHeld) return;

			_cursorHeld = false;
			CursorController.ReleaseUnlock();
		}

		private void RefreshEditable()
		{
			if (!_configList) return;

			var isHost = NetworkManager.Singleton && NetworkManager.Singleton.IsHost;

			_configList.SetEditable(isHost && Data && Data.Phase.Value == PokerPhase.Waiting);
		}
	}
}
