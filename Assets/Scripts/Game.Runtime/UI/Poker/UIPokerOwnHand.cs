using System.Collections.Generic;
using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Hands;
using Game.Runtime.GameMode.Poker.Player;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The local player's hand as it stands, beside the ranking board: the cards they hold and the best hand those
	// cards make, numbered as the board numbers it. Scored exactly as the showdown will score it, from what the
	// player can actually see: their own cards they have looked at plus the board the whole table has turned. A
	// card still face down is drawn face down and counts for nothing, so the helper never tells more than the
	// table has.
	public class UIPokerOwnHand : MonoBehaviour
	{
		[Header("References")]
		[Tooltip("The hand the cards make, drawn like a row of the board. Hidden while nothing is held.")]
		[SerializeField] private UIPokerHandRow _handRow;

		[Tooltip("Layout row the held cards are laid out in.")]
		[SerializeField] private RectTransform _cardContainer;

		[SerializeField] private UIPokerCard _cardPrefab;

		[Tooltip("Shown instead of the hand while the player holds no cards.")]
		[SerializeField] private GameObject _emptyState;

		[Header("Cards")]
		[Tooltip("Cards are sized here rather than by the layout group, so a card keeps its shape.")]
		[SerializeField] private Vector2 _cardSize = new(150f, 225f);

		private readonly List<UIPokerCard> _cards = new();
		private readonly List<CardData> _shownCards = new();
		private readonly List<bool> _shownFaceUp = new();
		private readonly List<CardData> _scoredCards = new();

		private PokerGameMode _mode;
		private PokerPlayer _player;

		private void Start()
		{
			PokerGameMode.OnInstanceChanged += HandleGameModeChanged;
			PokerPlayer.OnLocalPlayerChanged += HandleLocalPlayerChanged;
		}

		private void OnDestroy()
		{
			PokerGameMode.OnInstanceChanged -= HandleGameModeChanged;
			PokerPlayer.OnLocalPlayerChanged -= HandleLocalPlayerChanged;
		}

		private void OnEnable()
		{
			Bind(PokerGameMode.Instance, PokerPlayer.Local);
			Refresh();
		}

		private void OnDisable() => Unbind();

		private void HandleGameModeChanged(PokerGameMode mode)
		{
			if (!isActiveAndEnabled) return;

			Bind(mode, _player);
			Refresh();
		}

		private void HandleLocalPlayerChanged(PokerPlayer player)
		{
			if (!isActiveAndEnabled) return;

			Bind(_mode, player);
			Refresh();
		}

		private void Bind(PokerGameMode mode, PokerPlayer player)
		{
			Unbind();

			_mode = mode;
			_player = player;

			if (_mode && _mode.Data)
			{
				_mode.Data.OnCommunityCardsChanged += HandleCardsChanged;
				_mode.Data.OnCommunityRevealChanged += Refresh;
			}

			if (_player && _player.Data)
			{
				_player.Data.OnHoleCardsChanged += HandleCardsChanged;
				_player.Data.OnHoleCardPresentationChanged += Refresh;
			}
		}

		private void Unbind()
		{
			if (_player && _player.Data)
			{
				_player.Data.OnHoleCardPresentationChanged -= Refresh;
				_player.Data.OnHoleCardsChanged -= HandleCardsChanged;
			}

			if (_mode && _mode.Data)
			{
				_mode.Data.OnCommunityRevealChanged -= Refresh;
				_mode.Data.OnCommunityCardsChanged -= HandleCardsChanged;
			}

			_player = null;
			_mode = null;
		}

		private void HandleCardsChanged(NetworkListEvent<CardData> changeEvent) => Refresh();

		private void Refresh()
		{
			CollectCards();
			DrawCards();
			DrawHand();
		}

		private void CollectCards()
		{
			_shownCards.Clear();
			_shownFaceUp.Clear();
			_scoredCards.Clear();

			if (_player && _player.Data)
			{
				var data = _player.Data;

				for (var slot = 0; slot < data.HoleCards.Count; slot++)
				{
					var visible = data.IsHoleCardVisible(slot);
					AddCard(data.HoleCards[slot], visible);
				}
			}

			// Only what the table has turned: the showdown scores nothing else, and a board card one player was
			// shown in private is not one their hand is made with.
			if (_mode && _mode.Data)
			{
				var data = _mode.Data;

				for (var i = 0; i < data.CommunityCards.Count; i++)
				{
					if (data.IsCommunityCardRevealed(i)) AddCard(data.CommunityCards[i], true);
				}
			}
		}

		private void AddCard(CardData card, bool faceUp)
		{
			_shownCards.Add(card);
			_shownFaceUp.Add(faceUp);

			if (faceUp) _scoredCards.Add(card);
		}

		// Cards already made are re-bound rather than rebuilt, so a hand growing a card never draws a frame of both.
		private void DrawCards()
		{
			if (!_cardContainer || !_cardPrefab) return;

			while (_cards.Count < _shownCards.Count)
			{
				var card = Instantiate(_cardPrefab, _cardContainer);
				((RectTransform)card.transform).sizeDelta = _cardSize;
				_cards.Add(card);
			}

			for (var i = 0; i < _cards.Count; i++)
			{
				var used = i < _shownCards.Count;
				if (_cards[i].gameObject.activeSelf != used) _cards[i].gameObject.SetActive(used);
				if (used) _cards[i].SetCard(_shownCards[i], _shownFaceUp[i]);
			}
		}

		private void DrawHand()
		{
			var database = _mode ? _mode.HandDatabase : null;
			var result = database && _scoredCards.Count > 0
				? _mode.HandEvaluator.Evaluate(database, _scoredCards)
				: PokerHandResult.None;

			var hasHand = result.IsValid;

			if (_handRow)
			{
				if (_handRow.gameObject.activeSelf != hasHand) _handRow.gameObject.SetActive(hasHand);
				if (hasHand) _handRow.Bind(database.DisplayRankOf(result.HandType), result.HandType);
			}

			if (_emptyState) _emptyState.SetActive(_shownCards.Count == 0);
		}
	}
}
