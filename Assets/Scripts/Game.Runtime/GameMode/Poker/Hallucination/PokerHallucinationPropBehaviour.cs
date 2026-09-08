using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	public class PokerHallucinationPropBehaviour : PokerHallucinationEffectBehaviour<PokerHallucinationPropEffect>
	{
		private readonly List<Transform> _resolved = new();
		private readonly List<GameObject> _spawned = new();
		private readonly List<Renderer> _hiddenRenderers = new();
		private readonly List<GameObject> _hiddenObjects = new();

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

		private void Reapply()
		{
			Release();

			if (!Config || !Config.Target) return;

			Config.Target.Collect(Viewer, _resolved);

			foreach (var found in _resolved)
			{
				if (!found) continue;

				HideOne(found);
				Grow(found);
			}
		}

		private void HideOne(Transform root)
		{
			switch (Config.Hide)
			{
				case PokerHallucinationPropEffect.HideMode.Renderers:
					foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
					{
						if (!renderer || !renderer.enabled) continue;

						renderer.enabled = false;
						_hiddenRenderers.Add(renderer);
					}

					break;

				case PokerHallucinationPropEffect.HideMode.GameObject:
					if (!root.gameObject.activeSelf) break;

					root.gameObject.SetActive(false);
					_hiddenObjects.Add(root.gameObject);
					break;
			}
		}

		// Parented to the thing it replaces, so it travels with a bone that is being animated and lands in
		// the right place on a chair that the ring moved.
		private void Grow(Transform root)
		{
			if (!Config.Prefab) return;

			var instance = Instantiate(Config.Prefab, root, false);
			instance.transform.localPosition = Config.LocalPosition;
			instance.transform.localRotation = Quaternion.Euler(Config.LocalEuler);
			instance.transform.localScale = Config.LocalScale;

			_spawned.Add(instance);
		}

		private void Release()
		{
			foreach (var instance in _spawned)
			{
				if (instance) Destroy(instance);
			}

			// Only what this switched off is switched back on. Anything already hidden when it arrived was
			// somebody else's decision and is left alone.
			foreach (var renderer in _hiddenRenderers)
			{
				if (renderer) renderer.enabled = true;
			}

			foreach (var hidden in _hiddenObjects)
			{
				if (hidden) hidden.SetActive(true);
			}

			_spawned.Clear();
			_hiddenRenderers.Clear();
			_hiddenObjects.Clear();
			_resolved.Clear();
		}
	}
}
