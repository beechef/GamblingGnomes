using System.Collections.Generic;
using DG.Tweening;
using Game.Runtime.GameMode.Poker.Items;
using Game.Runtime.GameMode.Poker.Player;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// A card rewritten in place flickering through faces. The card itself flickers until the server writes the
	// new face (or the backstop passes, for a gamble that wrote nothing), then shows what its slot holds. Only a
	// face this screen can see flickers; a card shown face down has nothing to show changing.
	public class PokerCardRewriteVisual : PokerVisual
	{
		private sealed class Rewrite
		{
			public PokerCardVisual Card;
			public PokerPlayer Holder;
			public Tween Flicker;
			public Tween Backstop;
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
			if (!pacing || place.IsBoard || !player || !player.HandVisual || !player.Data) return;

			var cards = player.HandVisual.Cards;
			if (place.Slot < 0 || place.Slot >= cards.Count) return;

			var card = cards[place.Slot];
			if (!card || !card.FaceUp || !card.Card.IsValid) return;

			CollectFaces(player.Data, place.Slot, card.Card, faces);
			if (_faces.Count == 0) return;

			var rewrite = new Rewrite { Card = card, Holder = player };
			var shown = new List<CardData>(_faces);
			var step = 0;

			rewrite.Flicker = DOTween.Sequence()
				.AppendInterval(pacing.RewriteFlickerInterval)
				.AppendCallback(() => card.SetCard(shown[step++ % shown.Count], true))
				.SetLoops(-1)
				.SetLink(card.gameObject);

			if (!_rewrites.Exists(r => r.Holder == player)) player.Data.OnHoleCardsChanged += HandleHoleCardsChanged;
			_rewrites.Add(rewrite);

			// A failed gamble writes nothing, so the flicker ends on the backstop and the old face comes back.
			rewrite.Backstop = DOVirtual.DelayedCall(pacing.RewriteDuration + pacing.LandingBackstop, () => Settle(rewrite), false)
				.SetLink(card.gameObject);
		}

		private void CollectFaces(PokerPlayerData holder, int slot, CardData card, PokerCardFlickerFaces faces)
		{
			_faces.Clear();

			if (faces == PokerCardFlickerFaces.Joker)
			{
				_faces.Add(CardData.Joker);
				_faces.Add(card);
				return;
			}

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

		// The flicker stops on whatever the slot holds now: the new face, or the old one if nothing was written.
		private void Settle(Rewrite rewrite)
		{
			if (!_rewrites.Remove(rewrite)) return;

			rewrite.Flicker?.Kill();
			rewrite.Backstop?.Kill();

			var data = rewrite.Holder ? rewrite.Holder.Data : null;
			var slot = IndexOf(rewrite);
			if (rewrite.Card && data && slot >= 0 && slot < data.CardCount && data.IsHoleCardVisible(slot))
				rewrite.Card.SetCard(data.HoleCards[slot], true);

			if (data && !_rewrites.Exists(r => r.Holder == rewrite.Holder)) data.OnHoleCardsChanged -= HandleHoleCardsChanged;
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
