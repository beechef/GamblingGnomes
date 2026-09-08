using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// Hides what the target names, grows something in its place, or both. That covers four of the deck's
	// categories at once: swapping the room, swapping an object on the table, adding an object to the room
	// and adding a limb to a body are all the same move, because a swap is only ever a hide plus an add.
	//
	// What it spawns is a plain prefab with no NetworkObject on it. The whole point is that it exists on
	// one screen, and a networked object would put it on everybody's.
	[CreateAssetMenu(fileName = "Hallucination_Prop", menuName = "Game/Poker/Hallucination/Prop")]
	public class PokerHallucinationPropEffect : PokerHallucinationEffect
	{
		public enum HideMode
		{
			Nothing,
			Renderers,
			GameObject
		}

		[Required]
		[SerializeField] private PokerHallucinationTarget _target;

		[Tooltip("Renderers leaves the colliders standing, which is what the room needs or the player falls through the floor. GameObject is for a piece of a body, where PlayerVisual owns renderer.enabled and would hand a hidden one straight back.")]
		[SerializeField] private HideMode _hide = HideMode.Renderers;

		[Tooltip("Grown under each thing the target names. Leave empty to only hide.")]
		[SerializeField] private GameObject _prefab;

		[SerializeField] private Vector3 _localPosition;

		[SerializeField] private Vector3 _localEuler;

		[SerializeField] private Vector3 _localScale = Vector3.one;

		private readonly List<Transform> _resolved = new();
		private readonly List<GameObject> _spawned = new();
		private readonly List<Renderer> _hiddenRenderers = new();
		private readonly List<GameObject> _hiddenObjects = new();

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

		private void Reapply()
		{
			Release();

			if (!_target || !_viewer) return;

			_target.Collect(_viewer, _resolved);

			foreach (var found in _resolved)
			{
				if (!found) continue;

				Hide(found);
				Grow(found);
			}
		}

		private void Hide(Transform root)
		{
			switch (_hide)
			{
				case HideMode.Renderers:
					foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
					{
						if (!renderer || !renderer.enabled) continue;

						renderer.enabled = false;
						_hiddenRenderers.Add(renderer);
					}

					break;

				case HideMode.GameObject:
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
			if (!_prefab) return;

			var instance = Instantiate(_prefab, root, false);
			instance.transform.localPosition = _localPosition;
			instance.transform.localRotation = Quaternion.Euler(_localEuler);
			instance.transform.localScale = _localScale;

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
