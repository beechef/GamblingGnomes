using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Player;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// Lives on the player so the cards travel with the gnome holding them. The owner sees faces —
	// their hole card list is the only one their client receives — everyone else sees backs until a
	// showdown copies the hand into the revealed list.
	[RequireComponent(typeof(PokerPlayerData))]
	public class PokerHandVisual : NetworkBehaviour
	{
		[Header("Layout")]
		[SerializeField] private Transform _cardAnchor;
		[SerializeField] private float _cardSpacing = 0.03f;
		[SerializeField] private float _fanAngle = 8f;

		[Header("References")]
		[SerializeField] private PokerPlayerData _data;
		[SerializeField] private PokerCardVisual _cardPrefab;
		[SerializeField] private PokerCardDatabase _database;

		private readonly List<PokerCardVisual> _spawnedCards = new();
		private readonly List<CardData> _cardBuffer = new();

		public override void OnNetworkSpawn()
		{
			if (!_data) _data = GetComponent<PokerPlayerData>();
			if (!_data) return;

			_data.OnHoleCardsChanged += Refresh;
			_data.OnStateChanged += Refresh;

			Refresh();
		}

		public override void OnNetworkDespawn()
		{
			if (!_data) return;

			_data.OnHoleCardsChanged -= Refresh;
			_data.OnStateChanged -= Refresh;
		}

		private void Refresh()
		{
			if (!_cardPrefab || !_data) return;

			var faceUp = ResolveCards(_cardBuffer);
			var anchor = _cardAnchor ? _cardAnchor : transform;

			while (_spawnedCards.Count < _cardBuffer.Count)
			{
				_spawnedCards.Add(Instantiate(_cardPrefab, anchor));
			}

			for (var i = 0; i < _spawnedCards.Count; i++)
			{
				var visual = _spawnedCards[i];
				var active = i < _cardBuffer.Count;
				visual.gameObject.SetActive(active);

				if (!active) continue;

				var offset = (i - (_cardBuffer.Count - 1) * 0.5f) * _cardSpacing;
				visual.transform.localPosition = new Vector3(offset, 0f, 0f);
				visual.transform.localRotation = Quaternion.Euler(0f, 0f, -offset / Mathf.Max(_cardSpacing, 0.0001f) * _fanAngle);
				visual.SetCard(_cardBuffer[i], faceUp, _database);
			}
		}

		// The cards are here either way — IsHandVisible is the only thing deciding whether this client
		// is allowed to look at them, which is exactly the switch an ability needs to flip.
		private bool ResolveCards(List<CardData> buffer)
		{
			buffer.Clear();

			var visible = _data.IsHandVisible;

			foreach (var card in _data.HoleCards) buffer.Add(visible ? card : CardData.None);

			return visible;
		}
	}
}
