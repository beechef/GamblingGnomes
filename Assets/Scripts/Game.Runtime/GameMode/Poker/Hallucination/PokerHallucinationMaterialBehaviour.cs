using System.Collections.Generic;
using Game.Runtime.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// A body goes through PlayerMaterialOverrideController, which is the only route that survives the
	// outline being hung and dropped mid-hand. Anything else has nothing to compose with, so its renderers
	// are written directly and their materials put back at the end.
	public class PokerHallucinationMaterialBehaviour : PokerHallucinationEffectBehaviour<PokerHallucinationMaterialEffect>
	{
		private readonly List<Transform> _resolved = new();
		private readonly List<PlayerMaterialOverrideController> _bodies = new();
		private readonly Dictionary<Renderer, Material[]> _restored = new();

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
				// from it passes the root and finds nothing. Painting directly is the deliberate fallback
				// for a prop that is not a body at all, and it was quietly swallowing every player too.
				//
				// Only for a repaint. The override controller resolves one winning material and hands it to
				// PlayerVisual, which has no way to express "and this one as well" — so an added pass goes
				// the direct route whatever it landed on, rather than silently becoming a replacement.
				var body = Config.Mode == PokerHallucinationMaterialEffect.PaintMode.Replace
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

				PaintDirect(found);
			}
		}

		private void PaintDirect(Transform root)
		{
			foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
			{
				if (!renderer || _restored.ContainsKey(renderer)) continue;

				var authored = renderer.sharedMaterials;
				_restored[renderer] = authored;

				renderer.sharedMaterials = Config.Mode == PokerHallucinationMaterialEffect.PaintMode.Add
					? Append(authored)
					: Fill(authored.Length);
			}
		}

		// An extra pass on the end of what the renderer already wears, the same shape the outline takes on
		// a body: the card keeps its face and this draws over it, which is the only way the rank underneath
		// stays readable. Assigning replaces the whole array, so the authored one is copied rather than
		// added to in place — and it is the array captured above that puts the renderer back.
		private Material[] Append(Material[] authored)
		{
			var next = new Material[authored.Length + 1];

			for (var i = 0; i < authored.Length; i++) next[i] = authored[i];

			next[^1] = Config.Material;
			return next;
		}

		private Material[] Fill(int length)
		{
			var next = new Material[length];

			for (var i = 0; i < length; i++) next[i] = Config.Material;

			return next;
		}

		private void Release()
		{
			foreach (var body in _bodies)
			{
				if (body) body.Clear(this);
			}

			_bodies.Clear();

			foreach (var pair in _restored)
			{
				if (pair.Key) pair.Key.sharedMaterials = pair.Value;
			}

			_restored.Clear();
			_resolved.Clear();
		}
	}
}
