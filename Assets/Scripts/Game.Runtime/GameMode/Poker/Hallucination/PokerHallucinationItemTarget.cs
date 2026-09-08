using System;
using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.GameMode.Poker.Visual;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// The caps staked on the table. What somebody put up is the read of the round, so a mushroom that has
	// started smiling back is the cheapest way to make a player doubt what they are looking at.
	//
	// The plate rather than the scenery: a cap is spawned by PokerItemPotVisual when it is wagered and
	// destroyed when the pot clears, so it can never be named by a scenery group the scene authored.
	[CreateAssetMenu(fileName = "HallucinationTarget_Items", menuName = "Game/Poker/Hallucination/Target/Items")]
	public class PokerHallucinationItemTarget : PokerHallucinationTarget
	{
		protected override void OnCollect(PokerPlayer viewer, List<Transform> into)
		{
			var pot = PokerItemPotVisual.Instance;
			if (!pot) return;

			foreach (var cap in pot.Caps)
			{
				if (cap) into.Add(cap.transform);
			}
		}

		public override void Subscribe(PokerPlayer viewer, Action onChanged) => PokerItemPotVisual.OnAnyPotChanged += onChanged;

		public override void Unsubscribe(PokerPlayer viewer, Action onChanged) => PokerItemPotVisual.OnAnyPotChanged -= onChanged;
	}
}
