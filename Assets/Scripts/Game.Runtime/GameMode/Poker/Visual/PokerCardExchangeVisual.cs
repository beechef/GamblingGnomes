using System.Collections.Generic;
using DG.Tweening;
using Game.Runtime.GameMode.Poker.Items;
using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// Two cards changing places, crossing in the air. The cards on the table are left where their groups put
	// them and only hidden; a stand-in wearing each one's face as this screen sees it flies to the other's
	// place and follows it if it moves. A landed card comes back once its slot holds the card that flew
	// there, which the server writes only after the flight, so no face changes in plain sight.
	public class PokerCardExchangeVisual : PokerVisual
	{
		[Required]
		[SerializeField] private PokerCardVisual _cardPrefab;

		[Required]
		[SerializeField] private PokerCardDatabase _database;

		[Tooltip("The second card's arc as a share of the first's, so the two pass one over the other rather than through it.")]
		[Range(0f, 1f)]
		[SerializeField] private float _lowerArcShare = 0.35f;

		private sealed class Landing
		{
			public PokerCardPlace Place;
			public PokerCardVisual Card;
			public PokerCardVisual Stand;
			public CardData Before;
			public PokerPlayerData Holder;
			public Tween Flight;
			public Tween Backstop;
			public bool Watching;
		}

		private readonly List<Landing> _landings = new();
		private PokerItemModule _module;

		protected override void OnBind()
		{
			_module = GameMode.FindModule<PokerItemModule>();
			if (_module) _module.OnCardsExchanging += HandleCardsExchanging;
		}

		protected override void OnUnbind()
		{
			if (_module) _module.OnCardsExchanging -= HandleCardsExchanging;
			_module = null;

			for (var i = _landings.Count - 1; i >= 0; i--) Reveal(_landings[i]);
		}

		private void HandleCardsExchanging(PokerCardPlace first, PokerCardPlace second)
		{
			var pacing = _module ? _module.ExchangePacing : null;
			var firstCard = VisualAt(first);
			var secondCard = VisualAt(second);
			if (!pacing || !firstCard || !secondCard || !_cardPrefab) return;

			Launch(firstCard, secondCard, second, pacing, pacing.Arc);
			Launch(secondCard, firstCard, first, pacing, pacing.Arc * _lowerArcShare);

			firstCard.SetConcealed(true);
			secondCard.SetConcealed(true);
		}

		private void Launch(PokerCardVisual from, PokerCardVisual to, PokerCardPlace toPlace, PokerCardExchangePacing pacing, float arc)
		{
			var stand = Instantiate(_cardPrefab, from.transform.position, from.transform.rotation);
			stand.transform.localScale = from.transform.lossyScale;

			// A stand-in is looked at, never pointed at.
			foreach (var hit in stand.GetComponentsInChildren<Collider>(true)) hit.enabled = false;

			stand.SetCard(from.Card, from.FaceUp, _database);

			var landing = new Landing
			{
				Place = toPlace,
				Card = to,
				Stand = stand,
				Before = ReadCard(toPlace),
				Holder = toPlace.IsBoard ? null : FindData(toPlace.HolderClientId)
			};

			landing.Flight = DOTween.Sequence()
				.Join(PokerCardVisual.ArcTween(stand.transform, to.transform, Vector3.zero, Quaternion.identity, pacing.FlightDuration, pacing.Ease, arc))
				.Join(stand.transform.DOScale(to.transform.lossyScale, pacing.FlightDuration).SetEase(pacing.Ease))
				.OnComplete(() => Land(landing, pacing))
				.SetLink(stand.gameObject);

			_landings.Add(landing);
		}

		// The stand-in stays on the spot, riding the hidden card, until the real one can take over, so the
		// place is never empty while the new face is still on its way.
		private void Land(Landing landing, PokerCardExchangePacing pacing)
		{
			if (landing.Stand && landing.Card)
			{
				landing.Stand.transform.SetParent(landing.Card.transform, true);
				landing.Stand.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
				landing.Stand.transform.localScale = Vector3.one;
			}

			if (HasArrived(landing))
			{
				Reveal(landing);
				return;
			}

			Watch(landing, true);
			landing.Backstop = DOVirtual.DelayedCall(pacing.LandingBackstop, () => Reveal(landing), false).SetLink(gameObject);
		}

		private void Reveal(Landing landing)
		{
			Watch(landing, false);

			landing.Flight?.Kill();
			landing.Backstop?.Kill();

			if (landing.Stand) Destroy(landing.Stand.gameObject);
			if (landing.Card) landing.Card.SetConcealed(false);

			_landings.Remove(landing);
		}

		private void Watch(Landing landing, bool watch)
		{
			if (landing.Watching == watch) return;

			landing.Watching = watch;

			if (landing.Place.IsBoard)
			{
				if (watch) Data.OnCommunityCardsChanged += HandleCommunityCardsChanged;
				else if (Data) Data.OnCommunityCardsChanged -= HandleCommunityCardsChanged;
				return;
			}

			if (!landing.Holder) return;

			if (watch) landing.Holder.OnHoleCardsChanged += HandleHoleCardsChanged;
			else landing.Holder.OnHoleCardsChanged -= HandleHoleCardsChanged;
		}

		private void HandleCommunityCardsChanged(NetworkListEvent<CardData> change) => RevealArrived();
		private void HandleHoleCardsChanged(NetworkListEvent<CardData> change) => RevealArrived();

		private void RevealArrived()
		{
			for (var i = _landings.Count - 1; i >= 0; i--)
			{
				var landing = _landings[i];
				if (landing.Watching && HasArrived(landing)) Reveal(landing);
			}
		}

		// The slot holds something other than what it held when the card set off: the swap has been written.
		private bool HasArrived(Landing landing) => !ReadCard(landing.Place).Equals(landing.Before);

		private CardData ReadCard(PokerCardPlace place)
		{
			if (place.IsBoard)
			{
				return Data && place.Slot >= 0 && place.Slot < Data.CommunityCards.Count ? Data.CommunityCards[place.Slot] : CardData.None;
			}

			var holder = FindData(place.HolderClientId);
			return holder && place.Slot >= 0 && place.Slot < holder.CardCount ? holder.HoleCards[place.Slot] : CardData.None;
		}

		private static PokerCardVisual VisualAt(PokerCardPlace place)
		{
			IReadOnlyList<PokerCardVisual> cards = null;

			if (place.IsBoard)
			{
				if (PokerBoardVisual.Instance) cards = PokerBoardVisual.Instance.Cards;
			}
			else
			{
				var holder = PokerPlayer.Find(place.HolderClientId);
				if (holder && holder.HandVisual) cards = holder.HandVisual.Cards;
			}

			return cards != null && place.Slot >= 0 && place.Slot < cards.Count ? cards[place.Slot] : null;
		}

		private static PokerPlayerData FindData(ulong clientId)
		{
			var player = PokerPlayer.Find(clientId);
			return player ? player.Data : null;
		}
	}
}
