using Game.Runtime.Controller;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker
{
	// Fills empty chairs with bots for the host, never more bodies than the table lays chairs. Driven by the
	// host's Add Bot and Clear Bot buttons (UIPokerBotButtons).
	public class PokerBotSpawner : NetworkBehaviour
	{
		[Tooltip("On, the host is offered Add Bot and Clear Bot at this table.")]
		[SerializeField] private bool _allowBots;

		[Tooltip("Body a bot is spawned as; carries the bot's own logic.")]
		[Required, ShowIf(nameof(_allowBots))]
		[SerializeField] private NetworkObject _botPrefab;

		private PokerGameMode _gameMode;

		public bool AllowsBots => _allowBots && _botPrefab;

		// A body with no chair would stand around the table for the whole match.
		public bool CanAddBot
		{
			get
			{
				var players = PlayerManager.Instance;
				var seats = _gameMode && _gameMode.Data ? _gameMode.Data.ActiveSeatCount.Value : 0;
				return IsHost && AllowsBots && players && players.Players.Count < seats;
			}
		}

		public bool HasBots => IsHost && PlayerManager.Instance && PlayerManager.Instance.BotCount > 0;

		private void Awake()
		{
			_gameMode = GetComponent<PokerGameMode>();
		}

		public void ServerAddBot()
		{
			if (!CanAddBot) return;

			PlayerManager.Instance.ServerAddBot(_botPrefab);
		}

		public void ServerClearBots()
		{
			var players = PlayerManager.Instance;
			if (!IsHost || !players) return;

			while (players.ServerRemoveBot()) { }
		}
	}
}
