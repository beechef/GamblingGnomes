using System;
using System.Collections.Generic;
using System.Threading;
using Game.Runtime.GameMode.Poker.BetItems;
using Game.Runtime.GameMode.Poker.Items;
using Game.Runtime.GameMode.Poker.Stages;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Player
{
	// Plays this bot's part on the server: stakes a drawn kind on each of its turns and answers whatever
	// the table would otherwise wait on it for. No strategy, only enough to keep a solo playtest moving.
	public class PokerBotController : NetworkBehaviour
	{
		[Required]
		[SerializeField] private PokerPlayer _player;

		[Tooltip("Seconds the bot waits before answering, so its move reads on screen.")]
		[MinValue(0f)]
		[SerializeField] private float _answerDelay = 1f;

		[Tooltip("Chance the bot plays one of its items, picked at random, before staking on its turn.")]
		[PropertyRange(0f, 1f)]
		[SerializeField] private float _itemUseChance = 0.5f;

		private PokerGameMode _gameMode;
		private PokerItemModule _itemModule;

		public override void OnNetworkSpawn()
		{
			if (!IsServer) return;

			PokerGameMode.OnInstanceChanged += Bind;
			Bind(PokerGameMode.Instance);
		}

		public override void OnNetworkDespawn()
		{
			if (!IsServer) return;

			Bind(null);
			PokerGameMode.OnInstanceChanged -= Bind;
		}

		private void Bind(PokerGameMode gameMode)
		{
			if (_gameMode == gameMode) return;

			if (_itemModule) _itemModule.OnPendingResponseChanged -= HandlePendingResponseChanged;

			if (_gameMode)
			{
				_gameMode.OnStageChanged -= HandleStageChanged;
				_gameMode.Data.TurnEndTime.OnValueChanged -= HandleTurnBegan;
			}

			_gameMode = gameMode;
			_itemModule = _gameMode ? _gameMode.FindModule<PokerItemModule>() : null;

			if (_gameMode)
			{
				_gameMode.Data.TurnEndTime.OnValueChanged += HandleTurnBegan;
				_gameMode.OnStageChanged += HandleStageChanged;
			}

			if (_itemModule) _itemModule.OnPendingResponseChanged += HandlePendingResponseChanged;
		}

		// Written by every BeginTurn, even one handing the turn to the same player again, which the turn id
		// alone would not report.
		private void HandleTurnBegan(double previous, double current)
		{
			if (_gameMode.Data.CurrentTurnClientId.Value == _player.ClientId) _ = AnswerTurnAsync(_gameMode, destroyCancellationToken);
		}

		// Stages the whole table answers at once give no turn.
		private void HandleStageChanged(PokerStage stage)
		{
			if (stage is PokerCardLookStage or PokerAllInStage) _ = AnswerStageAsync(_gameMode, stage, destroyCancellationToken);
		}

		private async Awaitable AnswerTurnAsync(PokerGameMode gameMode, CancellationToken ct)
		{
			if (!await WaitAnswerDelayAsync(ct)) return;
			if (gameMode != _gameMode || gameMode.Data.CurrentTurnClientId.Value != _player.ClientId) return;

			if (_itemModule && UnityEngine.Random.value < _itemUseChance && TryBuildItemRequest(out var request))
			{
				await _itemModule.ServerUseItemAsync(_player.ClientId, request, ct);
				if (ct.IsCancellationRequested || gameMode != _gameMode || gameMode.Data.CurrentTurnClientId.Value != _player.ClientId) return;
			}

			var clientId = _player.ClientId;
			if (gameMode.ServerSubmitAction(clientId, PokerActionType.Bet, DrawBetKind(gameMode))) return;
			if (gameMode.ServerSubmitAction(clientId, PokerActionType.AllIn, 0)) return;
			if (TryTargetAnyone(gameMode)) return;

			gameMode.ServerSubmitAction(clientId, PokerActionType.Fold, 0);
		}

		private async Awaitable AnswerStageAsync(PokerGameMode gameMode, PokerStage stage, CancellationToken ct)
		{
			if (!await WaitAnswerDelayAsync(ct)) return;
			if (gameMode != _gameMode || gameMode.CurrentStage != stage) return;

			if (stage is PokerCardLookStage) _player.Data.ServerLookAtEveryHoleCard();
			else gameMode.ServerSubmitAction(_player.ClientId, PokerActionType.AllIn, 0);
		}

		// An item somebody played asks this bot to put a card forward (a Swap): any card it may give will do.
		private void HandlePendingResponseChanged()
		{
			var pending = _itemModule.PendingResponse.Value;
			if (pending.IsPending && pending.ResponderClientId == _player.ClientId) _ = AnswerCardAsync(_itemModule, destroyCancellationToken);
		}

		private async Awaitable AnswerCardAsync(PokerItemModule module, CancellationToken ct)
		{
			if (!await WaitAnswerDelayAsync(ct) || module != _itemModule) return;

			var count = _player.Data.CardCount;
			var start = UnityEngine.Random.Range(0, Mathf.Max(1, count));

			for (var i = 0; i < count; i++)
			{
				if (module.ServerAnswerCard(_player, (start + i) % count)) return;
			}
		}

		// Any held item the table lets it play now, aimed at whatever its steps accept, picked at random.
		private bool TryBuildItemRequest(out PokerItemUseRequest request)
		{
			request = default;
			var inventory = _player.ItemInventory;
			if (!inventory || inventory.Items.Count == 0) return false;

			var context = _itemModule.ContextFor(_player);
			var steps = new List<PokerItemTargetKind>();
			var start = UnityEngine.Random.Range(0, inventory.Items.Count);

			for (var i = 0; i < inventory.Items.Count; i++)
			{
				PokerItemType type = inventory.Items[(start + i) % inventory.Items.Count];
				if (!_itemModule.GetAvailability(_player, type).IsUsable || !_itemModule.TryGetItem(type, out var item)) continue;

				steps.Clear();
				item.CollectTargetSteps(steps);
				if (TryAim(item, context, steps, new PokerItemUseRequest(type), out request)) return true;
			}

			return false;
		}

		private bool TryAim(PokerItem item, PokerItemContext context, List<PokerItemTargetKind> steps, PokerItemUseRequest request, out PokerItemUseRequest aimed)
		{
			aimed = request;

			if (steps.Contains(PokerItemTargetKind.OwnCard))
			{
				var slot = PokerItemModule.PickRandomSlot(_player.Data.CardCount, s => item.AcceptsOwnCard(context, s));
				if (slot < 0) return false;
				aimed = aimed.WithOwnCard(slot);
			}

			if (steps.Contains(PokerItemTargetKind.BoardCard))
			{
				var slot = PokerItemModule.PickRandomSlot(_gameMode.Data.CommunityCards.Count, s => item.AcceptsBoardCard(context, s));
				if (slot < 0) return false;
				aimed = aimed.WithCard(slot);
			}

			var wantsPlayer = steps.Contains(PokerItemTargetKind.Player);
			var wantsCard = steps.Contains(PokerItemTargetKind.OpponentCard);
			if (!wantsPlayer && !wantsCard) return item.IsValidRequest(context, aimed);

			var players = _gameMode.SeatedPlayers;
			var first = UnityEngine.Random.Range(0, Mathf.Max(1, players.Count));

			for (var i = 0; i < players.Count; i++)
			{
				var target = players[(first + i) % players.Count];
				if (!target || (wantsPlayer && !item.AcceptsPlayer(context, target))) continue;

				var candidate = aimed.WithTarget(target.Data.SeatIndex.Value);
				if (wantsCard)
				{
					var slot = PokerItemModule.PickRandomSlot(target.Data.CardCount, s => item.AcceptsOpponentCard(context, target, s));
					if (slot < 0) continue;
					candidate = candidate.WithCard(slot);
				}

				if (!item.IsValidRequest(context, candidate)) continue;

				aimed = candidate;
				return true;
			}

			return false;
		}

		private async Awaitable<bool> WaitAnswerDelayAsync(CancellationToken ct)
		{
			try
			{
				await Awaitable.WaitForSecondsAsync(_answerDelay, ct);
				return true;
			}
			catch (OperationCanceledException)
			{
				return false;
			}
		}

		private static int DrawBetKind(PokerGameMode gameMode)
		{
			var database = gameMode.BetItemDatabase;
			return (int)(database ? database.DrawBetItemType() : PokerBetItemDatabase.PlainChip);
		}

		// A pick that names a player (the Colorful one); the stage refuses whoever it won't take.
		private bool TryTargetAnyone(PokerGameMode gameMode)
		{
			var players = gameMode.SeatedPlayers;
			var start = UnityEngine.Random.Range(0, Mathf.Max(1, players.Count));

			for (var i = 0; i < players.Count; i++)
			{
				var player = players[(start + i) % players.Count];
				if (player && gameMode.ServerSubmitAction(_player.ClientId, PokerActionType.Target, player.Data.SeatIndex.Value)) return true;
			}

			return false;
		}
	}
}
