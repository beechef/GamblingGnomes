using System.Collections.Generic;
using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Items;
using Game.Runtime.GameMode.Poker.Stages;
using Localization;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// Fills a rules line's {0}, {1}, … with the running mode's own numbers. Read on every enable, since the
	// host may change a match setting between two looks at the page.
	[RequireComponent(typeof(LocalizedText))]
	public class UIPokerRuleArgs : MonoBehaviour
	{
		[Tooltip("Order matters: the first fills {0}, the second {1}.")]
		[SerializeField] private List<PokerRuleFact> _facts = new();

		private LocalizedText _text;
		private PokerGameMode _mode;

		private void Awake() => _text = GetComponent<LocalizedText>();

		private void OnEnable() => Apply();

		public void Initialize(PokerGameMode mode)
		{
			_mode = mode;
			Apply();
		}

		private void Apply()
		{
			if (!_mode || !_text) return;

			var args = new object[_facts.Count];
			for (var i = 0; i < _facts.Count; i++) args[i] = Read(_facts[i]);
			_text.SetArgs(args);
		}

		private object Read(PokerRuleFact fact)
		{
			var items = _mode.FindModule<PokerItemModule>();

			switch (fact)
			{
				case PokerRuleFact.HoleCards:
					var deal = FirstStage<PokerDealStage>();
					return deal ? deal.HoleCardsPerPlayer : 0;
				case PokerRuleFact.FirstHandItems:
					return items ? items.ItemsPerMatch + items.ItemsPerHand : 0;
				case PokerRuleFact.ItemUsesPerStreet:
					return items ? items.UsesPerStreet : 0;
				case PokerRuleFact.CommunityCardReveals:
					return CommunityCardReveals();
				default:
					return 0;
			}
		}

		// Each street's reveal in order ("3 - 1 - 1"); whatever the deal laid that no street turns is
		// turned last, at the reveal.
		private string CommunityCardReveals()
		{
			var counts = new List<int>();
			var revealed = 0;
			var deal = FirstStage<PokerDealStage>();

			foreach (var stage in Stages)
			{
				if (stage is not PokerStreetStage street || street.CommunityCardsToReveal == 0) continue;

				counts.Add(street.CommunityCardsToReveal);
				revealed += street.CommunityCardsToReveal;
			}

			if (deal && deal.CommunityCardCount > revealed) counts.Add(deal.CommunityCardCount - revealed);
			return string.Join(" - ", counts);
		}

		private T FirstStage<T>() where T : PokerStage
		{
			foreach (var stage in Stages)
			{
				if (stage is T match) return match;
			}

			return null;
		}

		private IReadOnlyList<PokerStage> Stages => _mode.Sequence ? _mode.Sequence.Stages : System.Array.Empty<PokerStage>();
	}
}
