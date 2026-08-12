using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Player;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// Slots follow the seated players and each one listens to the player it shows, so a chip count
	// changing redraws one row instead of the whole table.
	public class UIPokerPlayerList : UIPokerView
	{
		[Header("References")]
		[SerializeField] private UIPokerPlayerSlot _slotPrefab;
		[SerializeField] private RectTransform _slotContainer;

		private readonly List<UIPokerPlayerSlot> _slots = new();
		private readonly List<PokerPlayer> _boundPlayers = new();

		// Only the timer ring is continuous, and only while somebody is on the clock.
		protected override bool WantsTick => Data && Data.HasTurn;

		protected override void OnBind()
		{
			GameMode.OnSeatedPlayersChanged += RebuildSlots;
			Data.CurrentTurnClientId.OnValueChanged += HandleTurnChanged;
			Data.DealerSeatIndex.OnValueChanged += HandleDealerChanged;

			RebuildSlots();
		}

		protected override void OnUnbind()
		{
			GameMode.OnSeatedPlayersChanged -= RebuildSlots;
			Data.CurrentTurnClientId.OnValueChanged -= HandleTurnChanged;
			Data.DealerSeatIndex.OnValueChanged -= HandleDealerChanged;

			UnbindPlayers();
		}

		private void HandleTurnChanged(ulong previous, ulong current) => RefreshAll();
		private void HandleDealerChanged(int previous, int current) => RefreshAll();
		private void HandlePlayerStateChanged() => RefreshAll();

		private void RebuildSlots()
		{
			if (!_slotPrefab || !_slotContainer) return;

			UnbindPlayers();

			var players = GameMode.SeatedPlayers;

			while (_slots.Count < players.Count) _slots.Add(Instantiate(_slotPrefab, _slotContainer));

			for (var i = 0; i < _slots.Count; i++) _slots[i].gameObject.SetActive(i < players.Count);

			foreach (var player in players)
			{
				if (!player || !player.Data) continue;

				player.Data.OnStateChanged += HandlePlayerStateChanged;
				_boundPlayers.Add(player);
			}

			RefreshAll();
		}

		private void UnbindPlayers()
		{
			foreach (var player in _boundPlayers)
			{
				if (player && player.Data) player.Data.OnStateChanged -= HandlePlayerStateChanged;
			}

			_boundPlayers.Clear();
		}

		private void RefreshAll()
		{
			var players = GameMode.SeatedPlayers;

			for (var i = 0; i < _slots.Count && i < players.Count; i++)
			{
				var player = players[i];
				var isTurn = Data.CurrentTurnClientId.Value == player.ClientId;

				_slots[i].SetData(player,
					player.ClientId == LocalClientId,
					isTurn,
					player.Data.SeatIndex.Value == Data.DealerSeatIndex.Value,
					isTurn ? Data.TurnNormalized : 0f);
			}
		}

		protected override void OnTick()
		{
			var players = GameMode.SeatedPlayers;

			for (var i = 0; i < _slots.Count && i < players.Count; i++)
			{
				if (Data.CurrentTurnClientId.Value == players[i].ClientId) _slots[i].SetTurnProgress(Data.TurnNormalized);
			}
		}
	}
}
