using System;
using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// A named part of the room. The scene is what owns those objects, so this only names the group and
	// asks PokerScenery for it — which is also why an effect pointed at a room in a scene that has none
	// draws nothing rather than throwing.
	[CreateAssetMenu(fileName = "HallucinationTarget_Scenery", menuName = "Game/Poker/Hallucination/Target/Scenery")]
	public class PokerHallucinationSceneryTarget : PokerHallucinationTarget
	{
		[Tooltip("Which part of the room. The scene decides what is in it.")]
		[SerializeField] private PokerSceneryGroup _group = PokerSceneryGroup.Room;

		protected override void OnCollect(PokerPlayer viewer, List<Transform> into)
		{
			var scenery = PokerScenery.Instance;
			if (!scenery) return;

			scenery.Collect(_group, into);
		}

		// The room can arrive after an effect has begun, since gameplay is loaded additively and a client
		// can be part-way through it. Handler shape is the same as every other target's, so an effect does
		// not care which of them it is holding.
		public override void Subscribe(PokerPlayer viewer, Action onChanged) => PokerScenery.OnInstanceChanged += onChanged;
		public override void Unsubscribe(PokerPlayer viewer, Action onChanged) => PokerScenery.OnInstanceChanged -= onChanged;
	}
}
