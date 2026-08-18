using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// What this client has been shown of somebody else's hand, laid out over their head where it can
	// actually be read. The cards in a gnome's fist are edge on to everyone but its owner, so a peek that
	// only turned them face up granted sight of something still unreadable — this is where the sight is
	// spent.
	//
	// Driven by the visibility rule rather than by whichever ability granted it, so anything that ever
	// hands out a look at a hand gets this for free and never has to know the display exists. It follows
	// that a hand made public at a showdown does not appear here: everyone can see those, and the point of
	// this is the thing you were not owed.
	[RequireComponent(typeof(PokerPlayerData))]
	public class PokerRevealedCardsVisual : NetworkBehaviour
	{
		[Header("Placement")]
		[Tooltip("Where the cards are laid out — a real transform in the prefab, so it is dragged into place in the scene view rather than guessed at as a height. The same spot a stretched neck comes to look at.")]
		[Required]
		[SerializeField] private Transform _anchor;

		[SerializeField] private float _cardSpacing = 0.09f;

		[Tooltip("Gap along the anchor's forward. Coplanar cards z-fight.")]
		[SerializeField] private float _depthStep = 0.001f;

		[Header("References")]
		[SerializeField] private PokerPlayerData _data;
		[SerializeField] private PokerCardVisual _cardPrefab;
		[SerializeField] private PokerCardDatabase _database;

		private readonly List<PokerCardVisual> _cards = new();

		private bool _shown;

		public override void OnNetworkSpawn()
		{
			if (!_data) _data = GetComponent<PokerPlayerData>();
			if (!_data) return;

			_data.OnStateChanged += Refresh;
			_data.OnHoleCardsChanged += HandleHoleCardsChanged;

			// A client arriving mid peek is already inside the grant, so the state is read as it stands
			// rather than waited on.
			Refresh();
		}

		public override void OnNetworkDespawn()
		{
			if (!_data) return;

			_data.OnHoleCardsChanged -= HandleHoleCardsChanged;
			_data.OnStateChanged -= Refresh;

			Hide();
		}

		private void HandleHoleCardsChanged(NetworkListEvent<CardData> change) => Refresh();

		private void Refresh()
		{
			var shown = _data.IsHandVisibleByGrant && _data.CardCount > 0;

			if (shown == _shown && !shown) return;

			_shown = shown;

			if (!shown)
			{
				Hide();
				return;
			}

			// Rebuilt rather than diffed: the whole point of a grant is that it arrives and leaves as one
			// thing, and a hand that changes under it is being redealt rather than edited.
			Show();
		}

		private void Show()
		{
			Hide();

			if (!_cardPrefab || !_anchor) return;

			for (var i = 0; i < _data.HoleCards.Count; i++)
			{
				var visual = Instantiate(_cardPrefab, _anchor);
				_cards.Add(visual);

				visual.SetCard(_data.HoleCards[i], true, _database);

				var offset = (i - (_data.HoleCards.Count - 1) * 0.5f) * _cardSpacing;
				visual.transform.localPosition = new Vector3(offset, 0f, -i * _depthStep);
				visual.transform.localRotation = Quaternion.identity;
			}
		}

		private void Hide()
		{
			foreach (var card in _cards)
			{
				if (card) Destroy(card.gameObject);
			}

			_cards.Clear();
		}
	}
}
