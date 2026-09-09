using System;
using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.GameMode.Poker.Visual;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// Every cap on the table, wherever it is sitting. What somebody put up is the read of the round, so a
	// mushroom that has started smiling back is the cheapest way to make a player doubt what they are
	// looking at — and one being handed to them to swallow is the moment they are looking hardest.
	//
	// The registry rather than the pot: a cap is spawned by whichever visual is drawing it and destroyed
	// when that visual is done, so it can never be named by a scenery group the scene authored, and
	// naming only the pot's caps left everything served at the settlement wearing its plain face.
	[CreateAssetMenu(fileName = "HallucinationTarget_Items", menuName = "Game/Poker/Hallucination/Target/Items")]
	public class PokerHallucinationItemTarget : PokerHallucinationTarget
	{
		protected override void OnCollect(PokerPlayer viewer, List<Transform> into)
		{
			foreach (var cap in PokerItemCapRegistry.All)
			{
				if (cap) into.Add(cap.transform);
			}
		}

		public override void Subscribe(PokerPlayer viewer, Action onChanged) => PokerItemCapRegistry.OnChanged += onChanged;

		public override void Unsubscribe(PokerPlayer viewer, Action onChanged) => PokerItemCapRegistry.OnChanged -= onChanged;
	}
}
