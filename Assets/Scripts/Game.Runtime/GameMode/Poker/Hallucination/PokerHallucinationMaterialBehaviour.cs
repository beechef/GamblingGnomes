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

				var body = found.GetComponentInParent<PlayerMaterialOverrideController>(true);
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

				_restored[renderer] = renderer.sharedMaterials;

				var next = new Material[renderer.sharedMaterials.Length];
				for (var i = 0; i < next.Length; i++) next[i] = Config.Material;

				renderer.sharedMaterials = next;
			}
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
