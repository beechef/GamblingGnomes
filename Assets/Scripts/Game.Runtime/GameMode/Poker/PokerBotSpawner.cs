using Game.Runtime.Controller;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Runtime.GameMode.Poker
{
	// Solo playtesting: host-only keys [ and ] add and remove bots, never more bodies than the table lays
	// chairs. The keys are built here rather than in the action asset so they never show up for rebinding.
	public class PokerBotSpawner : NetworkBehaviour
	{
		[Tooltip("On, the host can add bots with [ and remove them with ].")]
		[SerializeField] private bool _allowBots;

		[Tooltip("Body a bot is spawned as; carries the bot's own logic.")]
		[Required, ShowIf(nameof(_allowBots))]
		[SerializeField] private NetworkObject _botPrefab;

		private PokerGameMode _gameMode;
		private InputAction _addBotAction;
		private InputAction _removeBotAction;

		private void Awake()
		{
			_gameMode = GetComponent<PokerGameMode>();
		}

		public override void OnNetworkSpawn()
		{
			if (!IsHost || !_allowBots) return;

			_addBotAction = new InputAction("AddBot", binding: "<Keyboard>/leftBracket");
			_removeBotAction = new InputAction("RemoveBot", binding: "<Keyboard>/rightBracket");

			_addBotAction.performed += HandleAddBotPerformed;
			_removeBotAction.performed += HandleRemoveBotPerformed;

			_addBotAction.Enable();
			_removeBotAction.Enable();
		}

		public override void OnNetworkDespawn()
		{
			if (_addBotAction == null) return;

			_removeBotAction.performed -= HandleRemoveBotPerformed;
			_addBotAction.performed -= HandleAddBotPerformed;

			_removeBotAction.Dispose();
			_addBotAction.Dispose();

			_removeBotAction = null;
			_addBotAction = null;
		}

		private void HandleAddBotPerformed(InputAction.CallbackContext context)
		{
			var players = PlayerManager.Instance;
			if (!players)
			{
				Debug.LogWarning("Bot not added: no PlayerManager in the loaded gameplay scene.");
				return;
			}

			// A body with no chair would stand around the table for the whole match.
			var seats = _gameMode && _gameMode.Data ? _gameMode.Data.ActiveSeatCount.Value : 0;
			if (players.Players.Count >= seats)
			{
				Debug.LogWarning($"Bot not added: all {seats} chairs are taken.");
				return;
			}

			players.ServerAddBot(_botPrefab);
		}

		private void HandleRemoveBotPerformed(InputAction.CallbackContext context)
		{
			var players = PlayerManager.Instance;
			if (players) players.ServerRemoveBot();
		}
	}
}
