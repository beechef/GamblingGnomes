using System;
using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// Bodies at the table, or one bone of each. Whose body is the choice worth making: a room where
	// everyone else has grown a mushroom for a head is a different hallucination from one where you look
	// down and find that you have.
	[CreateAssetMenu(fileName = "HallucinationTarget_Players", menuName = "Game/Poker/Hallucination/Target/Players")]
	public class PokerHallucinationPlayerTarget : PokerHallucinationTarget
	{
		public enum Scope
		{
			Everyone,
			Others,
			Self
		}

		[Tooltip("Whose body. Others is the usual answer: the viewer renders their own rig as hands alone, so nearly everything they can see of a body belongs to somebody else.")]
		[SerializeField] private Scope _scope = Scope.Others;

		[Tooltip("Which bone to hand over. Root is the body itself.")]
		[SerializeField] private PlayerBone _bone = PlayerBone.Root;

		protected override void OnCollect(PokerPlayer viewer, List<Transform> into)
		{
			foreach (var player in PokerPlayer.All)
			{
				if (!player || !InScope(viewer, player)) continue;

				var found = Resolve(player);
				if (found) into.Add(found);
			}
		}

		public override void Subscribe(PokerPlayer viewer, Action onChanged) => PokerPlayer.OnRegistryChanged += onChanged;

		public override void Unsubscribe(PokerPlayer viewer, Action onChanged) => PokerPlayer.OnRegistryChanged -= onChanged;

		public override bool ResolvesBodies => true;

		private bool InScope(PokerPlayer viewer, PokerPlayer player)
		{
			return _scope switch
			{
				Scope.Everyone => true,
				Scope.Self => player == viewer,
				_ => player != viewer
			};
		}

		// The rig is what knows which of the two skeletons this client is drawing, so a bone asked for here
		// is always one that is actually on screen. A body with no rig yet falls back to its own transform
		// rather than dropping out of the set.
		private Transform Resolve(PokerPlayer player)
		{
			if (!player.Rig) return player.transform;

			return player.Rig.TryGetBone(_bone, out var bone) ? bone : player.transform;
		}
	}
}
