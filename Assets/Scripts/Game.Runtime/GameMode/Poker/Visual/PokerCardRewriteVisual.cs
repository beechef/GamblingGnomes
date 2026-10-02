using System.Collections.Generic;
using DG.Tweening;
using Game.Runtime.GameMode.Poker.Items;
using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// A card rewritten in place flickering through faces. The real card is hidden and a stand-in riding it
	// flickers until the server writes the new face, then the real card comes back wearing it. Only a face
	// this screen can see flickers; a card shown face down has nothing to show changing.
	public class PokerCardRewriteVisual : PokerVisual
	{
		[Required]
		[SerializeField] private PokerCardVisual _cardPrefab;

		[Required]
		[SerializeField] private PokerCardDatabase _database;

		private sealed class Rewrite
		{
			public PokerCardVisual Card;
			public PokerCardVisual Stand;
			public PokerPlayer Holder;
			public Tween Flicker;
		}

		private const int SuitCount = 4;

		private readonly List<Rewrite> _rewrites = new();
		private readonly List<CardData> _faces = new();
		private PokerItemModule _module;

		protected override void OnBind()
		{
			_module = GameMode.FindModule<PokerItemModule>();
			if (_module) _module.OnCardRewriting += HandleCardRewriting;
		}

		protected override void OnUnbind()
		{
			if (_module) _module.OnCardRewriting -= HandleCardRewriting;
			_module = null;

			for (var i = _rewrites.Count - 1; i >= 0; i--) Settle(_rewrites[i]);
		}

		private void HandleCardRewriting(PokerCardPlace place, PokerCardFlickerFaces faces)
		{
			var pacing = _module ? _module.ExchangePacing : null;
			var player = PokerPlayer.Find(place.HolderClientId);
			if (!pacing || !_cardPrefab || place.IsBoard || !player || !player.HandVisual || !player.Data) return;

			var cards = player.HandVisual.Cards;
			if (place.Slot < 0 || place.Slot >= cards.Count) return;

			var card = cards[place.Slot];
			if (!card || !card.FaceUp || !card.Card.IsValid) return;

			CollectFaces(player.Data, place.Slot, card.Card, faces);
			if (_faces.Count == 0) return;

			var stand = Instantiate(_cardPrefab, card.transform);
			stand.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			stand.transform.localScale = Vector3.one;
			foreach (var hit in stand.GetComponentsInChildren<Collider>(true)) hit.enabled = false;
			stand.SetCard(card.Card, true, _database);

			var rewrite = new Rewrite { Card = card, Stand = stand, Holder = player };
			var shown = new List<CardData>(_faces);
			var step = 0;

			rewrite.Flicker = DOTween.Sequence()
				.AppendInterval(pacing.RewriteFlickerInterval)
				.AppendCallback(() => stand.SetCard(shown[step++ % shown.Count], true, _database))
				.SetLoops(-1)
				.SetLink(stand.gameObject);

			card.SetConcealed(true);

			if (!_rewrites.Exists(r => r.Holder == player)) player.Data.OnHoleCardsChanged += HandleHoleCardsChanged;
			_rewrites.Add(rewrite);

			// A failed gamble writes nothing, so the flicker ends on the backstop and the old face comes back.
			DOVirtual.DelayedCall(pacing.RewriteDuration + pacing.LandingBackstop, () => Settle(rewrite), false).SetLink(stand.gameObject);
		}

		private void CollectFaces(PokerPlayerData holder, int slot, CardData card, PokerCardFlickerFaces faces)
		{
			_faces.Clear();

			if (faces == PokerCardFlickerFaces.Suits)
			{
				for (var suit = 0; suit < SuitCount; suit++)
				{
					if (suit != card.Suit) _faces.Add(new CardData(card.Rank, (CardSuit)suit));
				}

				_faces.Add(card);
				return;
			}

			for (var other = 0; other < holder.CardCount; other++)
			{
				var face = holder.HoleCards[other];
				if (other != slot && face.IsValid && holder.IsHoleCardVisible(other)) _faces.Add(face);
			}

			if (_faces.Count > 0) _faces.Add(card);
		}

		private void HandleHoleCardsChanged(NetworkListEvent<CardData> change)
		{
			for (var i = _rewrites.Count - 1; i >= 0; i--)
			{
				if (_rewrites[i].Holder && IndexOf(_rewrites[i]) == change.Index) Settle(_rewrites[i]);
			}
		}

		private void Settle(Rewrite rewrite)
		{
			if (!_rewrites.Remove(rewrite)) return;

			rewrite.Flicker?.Kill();
			if (rewrite.Stand) Destroy(rewrite.Stand.gameObject);
			if (rewrite.Card) rewrite.Card.SetConcealed(false);

			if (rewrite.Holder && rewrite.Holder.Data && !_rewrites.Exists(r => r.Holder == rewrite.Holder)) rewrite.Holder.Data.OnHoleCardsChanged -= HandleHoleCardsChanged;
		}

		private static int IndexOf(Rewrite rewrite)
		{
			if (!rewrite.Holder || !rewrite.Holder.HandVisual) return -1;

			var cards = rewrite.Holder.HandVisual.Cards;
			for (var i = 0; i < cards.Count; i++)
			{
				if (cards[i] == rewrite.Card) return i;
			}

			return -1;
		}
	}
}
