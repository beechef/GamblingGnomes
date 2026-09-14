using System;
using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.GameMode.Poker.Visual;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// The cards on the table. Whose cards is worth choosing for the same reason whose body is: a hand that
	// will not hold still is a different problem from a room where everybody else's cards are crawling.
	[CreateAssetMenu(fileName = "HallucinationTarget_Cards", menuName = "Game/Poker/Hallucination/Target/Cards")]
	public class PokerHallucinationCardTarget : PokerHallucinationTarget
	{
		public enum Scope
		{
			Everyone,
			Others,
			Self
		}

		[Tooltip("Whose cards. Self is the viewer's own five, which are the ones they are trying to read.")]
		[SerializeField] private Scope _scope = Scope.Everyone;

		protected override void OnCollect(PokerPlayer viewer, List<Transform> into)
		{
			foreach (var player in PokerPlayer.All)
			{
				if (!player || !InScope(viewer, player)) continue;

				var hand = player.GetComponentInChildren<PokerHandVisual>(true);
				if (!hand) continue;

				foreach (var card in hand.Cards)
				{
					if (card) into.Add(card.transform);
				}
			}
		}

		// A hand is emptied and dealt again every round, so anything painted on a card comes off on its own
		// while the hallucination has not moved at all. This is the event that says so.
		public override void Subscribe(PokerPlayer viewer, Action onChanged) => PokerHandVisual.OnAnyHandChanged += onChanged;

		public override void Unsubscribe(PokerPlayer viewer, Action onChanged) => PokerHandVisual.OnAnyHandChanged -= onChanged;

		private bool InScope(PokerPlayer viewer, PokerPlayer player)
		{
			return _scope switch
			{
				Scope.Everyone => true,
				Scope.Self => player == viewer,
				_ => player != viewer
			};
		}
	}
}
