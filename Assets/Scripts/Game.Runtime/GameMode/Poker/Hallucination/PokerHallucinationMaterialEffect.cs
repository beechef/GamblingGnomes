using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// Repaints whatever the target names. Covers both the deck's player shader and its card shader, because
	// the difference between them is which target the asset points at rather than anything this does.
	//
	// A body goes through PlayerMaterialOverrideController, which is the only route that survives the
	// outline being hung and dropped mid-hand. Anything else has nothing to compose with, so its renderers
	// are written directly and their materials put back at the end.
	[CreateAssetMenu(fileName = "Hallucination_Material", menuName = "Game/Poker/Hallucination/Material")]
	public class PokerHallucinationMaterialEffect : PokerHallucinationEffect
	{
		[Required]
		[SerializeField] private PokerHallucinationTarget _target;

		[Tooltip("What everything the target names is painted with.")]
		[Required]
		[SerializeField] private Material _material;

		private readonly List<Transform> _resolved = new();
		private readonly List<PlayerMaterialOverrideController> _bodies = new();
		private readonly Dictionary<Renderer, Material[]> _restored = new();

		private PokerPlayer _viewer;

		protected override void OnBegin(PokerPlayer viewer)
		{
			_viewer = viewer;
			_target?.Subscribe(viewer, Reapply);

			Reapply();
		}

		protected override void OnEnd(PokerPlayer viewer)
		{
			_target?.Unsubscribe(viewer, Reapply);

			Release();
			_viewer = null;
		}

		// Cards are destroyed and dealt again every round, so a paint resolved once would come off on its
		// own while the bar had not moved at all. The target says when its set changed and this runs again.
		private void Reapply()
		{
			Release();

			if (!_target || !_material || !_viewer) return;

			_target.Collect(_viewer, _resolved);

			foreach (var found in _resolved)
			{
				if (!found) continue;

				var body = found.GetComponentInParent<PlayerMaterialOverrideController>(true);
				if (body)
				{
					if (!_bodies.Contains(body))
					{
						_bodies.Add(body);
						body.Set(this, _material);
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
				for (var i = 0; i < next.Length; i++) next[i] = _material;

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
