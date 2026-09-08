using System.Collections.Generic;
using Game.Runtime.Props;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	public class PokerHallucinationVariantBehaviour : PokerHallucinationEffectBehaviour<PokerHallucinationVariantEffect>
	{
		private readonly List<Transform> _resolved = new();
		private readonly List<PropVariantController> _bound = new();

		protected override void OnBegin()
		{
			Config.Target?.Subscribe(Viewer, Reapply);

			Reapply();
		}

		protected override void OnEnd()
		{
			Config.Target?.Unsubscribe(Viewer, Reapply);

			Release();
		}

		// Every prop the target names is swept again rather than only the one that arrived: the event says
		// the set moved, not what moved, and a cap staked while this was running has to be caught up.
		private void Reapply()
		{
			Release();

			if (!Config || !Config.Target) return;

			Config.Target.Collect(Viewer, _resolved);

			foreach (var found in _resolved)
			{
				if (!found) continue;

				// Searched upward: a target names the thing being pointed at — a cap, which carries its own
				// controller, or a bone, whose controller is on the body it belongs to. One lookup covers both
				// because a prop is always at or below whatever owns its looks.
				var controller = found.GetComponentInParent<PropVariantController>(true);
				if (!controller || _bound.Contains(controller)) continue;

				_bound.Add(controller);
				controller.Set(this, Config.Variant);
			}
		}

		private void Release()
		{
			foreach (var controller in _bound)
			{
				if (controller) controller.Clear(this);
			}

			_bound.Clear();
			_resolved.Clear();
		}
	}
}
