using System.Collections.Generic;
using Game.Runtime.Player;
using Game.Runtime.Props;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// A body goes through PlayerMaterialOverrideController, which is the only route that survives the
	// outline being hung and dropped mid-hand. Anything else goes through the renderer's own
	// PropMaterialOverrideController, which owns what it was authored with — this never writes
	// sharedMaterials itself, because two callers each capturing and restoring is how a renderer ends up
	// wearing paint nobody can take off.
	public class PokerHallucinationMaterialBehaviour : PokerHallucinationEffectBehaviour<PokerHallucinationMaterialEffect>
	{
		private readonly List<Transform> _resolved = new();
		private readonly List<PlayerMaterialOverrideController> _bodies = new();
		private readonly List<PropMaterialOverrideController> _props = new();

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

		// Cards are destroyed and dealt again every round, so a paint resolved once would come off on its
		// own while the bar had not moved at all. The target says when its set changed and this runs again.
		private void Reapply()
		{
			Release();

			if (!Config || !Config.Target || !Config.Material) return;

			Config.Target.Collect(Viewer, _resolved);

			foreach (var found in _resolved)
			{
				if (!found) continue;

				// Up to the body and then down, never straight up: the override controller sits on a named
				// child of the player root and the thing being painted is off under the rig, so a walk up
				// from it passes the root and finds nothing. Painting the renderers directly is the
				// deliberate fallback for a prop that is not a body at all, and it was quietly swallowing
				// every player too.
				//
				// Only for a repaint. The override controller resolves one winning material and hands it to
				// PlayerVisual, which has no way to express "and this one as well" — so an added pass goes
				// the prop route whatever it landed on, rather than silently becoming a replacement.
				//
				// And only when the target says it handed over bodies. A card lies under the player holding
				// it, so the walk up from a card finds that player's body too: Card_Wave repainted the
				// whole gnome instead of the card it was aimed at.
				var body = Config.Mode == PropPaintMode.Replace && Config.Target.ResolvesBodies
					? PlayerRigController.FindOnBody<PlayerMaterialOverrideController>(found)
					: null;

				if (body)
				{
					if (!_bodies.Contains(body))
					{
						_bodies.Add(body);
						body.Set(this, Config.Material);
					}

					continue;
				}

				Paint(found);
			}
		}

		private void Paint(Transform root)
		{
			foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
			{
				var controller = PropMaterialOverrideController.Claim(renderer);
				if (!controller || _props.Contains(controller)) continue;

				_props.Add(controller);
				controller.Set(this, Config.Material, Config.Mode);
			}
		}

		private void Release()
		{
			foreach (var body in _bodies)
			{
				if (body) body.Clear(this);
			}

			_bodies.Clear();

			// A card painted last round is gone by now, and its controller with it — the reference is what
			// is left in this list, so it is asked whether it still exists rather than trusted.
			foreach (var prop in _props)
			{
				if (prop) prop.Clear(this);
			}

			_props.Clear();
			_resolved.Clear();
		}
	}
}
