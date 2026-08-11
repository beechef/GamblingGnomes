using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	public class UIPokerPlayerList : UIPokerView
	{
		[Header("References")]
		[SerializeField] private UIPokerPlayerSlot _slotPrefab;
		[SerializeField] private RectTransform _slotContainer;

		private readonly List<UIPokerPlayerSlot> _slots = new();

		protected override void OnTick()
		{
			if (!_slotPrefab || !_slotContainer) return;

			var players = GameMode.SeatedPlayers;

			while (_slots.Count < players.Count)
			{
				_slots.Add(Instantiate(_slotPrefab, _slotContainer));
			}

			for (var i = 0; i < _slots.Count; i++)
			{
				var slot = _slots[i];
				var active = i < players.Count;
				slot.gameObject.SetActive(active);

				if (!active) continue;

				var player = players[i];
				var isTurn = Data.CurrentTurnClientId.Value == player.ClientId;

				slot.SetData(player,
					player.ClientId == LocalClientId,
					isTurn,
					player.Data.SeatIndex.Value == Data.DealerSeatIndex.Value,
					isTurn ? Data.TurnNormalized : 0f);
			}
		}
	}
}
