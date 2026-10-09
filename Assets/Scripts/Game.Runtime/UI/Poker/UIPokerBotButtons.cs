using Game.Runtime.Controller;
using Game.Runtime.GameMode.Poker;
using Game.Runtime.UI.Button;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The host's Add Bot and Clear Bot. Shown only to the host at a table that allows bots; Add Bot greys
	// once every chair has a body, Clear Bot while there is no bot to clear.
	public class UIPokerBotButtons : UIPokerView
	{
		[Tooltip("Switched off for anyone but the host. A child, so this keeps listening while hidden.")]
		[Required]
		[SerializeField] private GameObject _content;

		[Required]
		[SerializeField] private UIButton _addButton;

		[Required]
		[SerializeField] private UIButton _clearButton;

		private PokerBotSpawner _spawner;
		private PlayerManager _players;

		private void Awake()
		{
			if (_content) _content.SetActive(false);
		}

		protected override void OnBind()
		{
			_spawner = GameMode.GetComponent<PokerBotSpawner>();
			_players = PlayerManager.Instance;

			if (_players) _players.Players.OnListChanged += HandlePlayersChanged;
			Data.ActiveSeatCount.OnValueChanged += HandleSeatCountChanged;

			_addButton.OnClick += HandleAdd;
			_clearButton.OnClick += HandleClear;

			Refresh();
		}

		protected override void OnUnbind()
		{
			_clearButton.OnClick -= HandleClear;
			_addButton.OnClick -= HandleAdd;

			Data.ActiveSeatCount.OnValueChanged -= HandleSeatCountChanged;
			if (_players) _players.Players.OnListChanged -= HandlePlayersChanged;

			_players = null;
			_spawner = null;

			if (_content) _content.SetActive(false);
		}

		private void HandlePlayersChanged(NetworkListEvent<NetworkObjectReference> change) => Refresh();
		private void HandleSeatCountChanged(int previous, int current) => Refresh();

		private void Refresh()
		{
			var shown = _spawner && _spawner.IsHost && _spawner.AllowsBots;
			if (_content) _content.SetActive(shown);
			if (!shown) return;

			_addButton.IsInteractable = _spawner.CanAddBot;
			_clearButton.IsInteractable = _spawner.HasBots;
		}

		private void HandleAdd()
		{
			if (_spawner) _spawner.ServerAddBot();
		}

		private void HandleClear()
		{
			if (_spawner) _spawner.ServerClearBots();
		}
	}
}
