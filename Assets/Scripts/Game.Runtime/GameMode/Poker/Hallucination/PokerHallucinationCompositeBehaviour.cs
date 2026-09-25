using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	public class PokerHallucinationCompositeBehaviour : PokerHallucinationEffectBehaviour<PokerHallucinationCompositeEffect>
	{
		private readonly List<PokerHallucinationEffectBehaviour> _running = new();

		// The members hang under this object, so it stays alive for the longest of their ease-outs.
		protected override float LingerSeconds
		{
			get
			{
				var linger = 0f;

				foreach (var member in _running)
				{
					if (member) linger = Mathf.Max(linger, member.Linger);
				}

				return linger;
			}
		}

		protected override void OnBegin()
		{
			foreach (var member in Config.Members)
			{
				if (!member || member == Config) continue;

				var behaviour = member.Run(transform, Viewer, Pacing);
				if (behaviour) _running.Add(behaviour);
			}
		}

		protected override void OnEnd()
		{
			foreach (var member in _running)
			{
				if (member) member.Stop();
			}
		}
	}
}
