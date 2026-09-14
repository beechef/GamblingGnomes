using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Props
{
	// A prop that can be asked to look like something else, and answers for itself. What Smiling means is
	// authored on the prop — a mushroom grows a face, a card curls, and neither of them is described by
	// whatever asked for the change. That is the whole point of naming the variant rather than handing the
	// prop a prefab to wear: one hallucination can be pointed at everything on the table and each thing
	// knows its own version of it.
	//
	// Requests are counted the way the bone scale and material overrides are: several effects may be
	// looking at the same prop, the last one to ask wins, and dropping a request puts back whatever the
	// caller before it had asked for rather than the default.
	[DisallowMultipleComponent]
	public class PropVariantController : MonoBehaviour
	{
		[Serializable]
		private class Look
		{
			[SerializeField] private PropVariant _variant;

			[Tooltip("Switched on while this variant is worn, and off for every other one.")]
			[SerializeField] private List<GameObject> _objects = new();

			[Tooltip("Meshes worn instead of the ones authored. This is the shape a finished variant takes: the artist builds the piece against the same skeleton and only the mesh changes hands.")]
			[SerializeField] private List<MeshSwap> _meshes = new();

			public PropVariant Variant => _variant;
			public IReadOnlyList<GameObject> Objects => _objects;
			public IReadOnlyList<MeshSwap> Meshes => _meshes;
		}

		[Serializable]
		public struct MeshSwap
		{
			[Tooltip("What is re-meshed. A SkinnedMeshRenderer or a MeshFilter, whichever this object carries.")]
			public GameObject Target;

			public Mesh Mesh;
		}

		[Tooltip("What this prop can be. A variant nobody authored here is simply not one this prop has, and asking for it leaves it as it was.")]
		[SerializeField] private List<Look> _looks = new();

		private readonly List<(object Handle, PropVariant Variant)> _requests = new();

		private readonly Dictionary<GameObject, Mesh> _restMeshes = new();

		private PropVariant _current = PropVariant.Default;

		// Raised after the objects have been switched, for a prop whose variant is more than a change of
		// which children are on — an animator state, a tween, a sound.
		public event Action<PropVariant> OnVariantChanged;

		public PropVariant Current => _current;

		private void Awake() => Apply();

		public void Set(object handle, PropVariant variant)
		{
			if (handle == null) return;

			Remove(handle);
			_requests.Add((handle, variant));

			Apply();
		}

		public void Clear(object handle)
		{
			if (handle == null || !Remove(handle)) return;

			Apply();
		}

		private bool Remove(object handle)
		{
			for (var i = _requests.Count - 1; i >= 0; i--)
			{
				if (!ReferenceEquals(_requests[i].Handle, handle)) continue;

				_requests.RemoveAt(i);
				return true;
			}

			return false;
		}

		private void Apply()
		{
			var wanted = _requests.Count > 0 ? _requests[^1].Variant : PropVariant.Default;

			// Everything any variant owns goes off first, so a prop whose looks share an object cannot end up
			// wearing half of each.
			foreach (var look in _looks)
			{
				foreach (var go in look.Objects)
				{
					if (go) go.SetActive(false);
				}
			}

			// Meshes go back to what the model was authored with rather than to the previous variant's, so a
			// variant dropped while another is still standing cannot leave its own mesh behind.
			foreach (var pair in _restMeshes)
			{
				WriteMesh(pair.Key, pair.Value);
			}

			foreach (var look in _looks)
			{
				if (look.Variant != wanted) continue;

				foreach (var go in look.Objects)
				{
					if (go) go.SetActive(true);
				}

				foreach (var swap in look.Meshes)
				{
					if (!swap.Target || !swap.Mesh) continue;

					// Captured the first time a mesh is taken over, which is the only moment the authored one is
					// still on the renderer to read.
					if (!_restMeshes.ContainsKey(swap.Target)) _restMeshes[swap.Target] = ReadMesh(swap.Target);

					WriteMesh(swap.Target, swap.Mesh);
				}
			}

			if (_current == wanted) return;

			_current = wanted;
			OnVariantChanged?.Invoke(wanted);
		}

		private static Mesh ReadMesh(GameObject target)
		{
			if (target.TryGetComponent<SkinnedMeshRenderer>(out var skinned)) return skinned.sharedMesh;

			return target.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
		}

		private static void WriteMesh(GameObject target, Mesh mesh)
		{
			if (!target) return;

			if (target.TryGetComponent<SkinnedMeshRenderer>(out var skinned))
			{
				skinned.sharedMesh = mesh;
				return;
			}

			if (target.TryGetComponent<MeshFilter>(out var filter)) filter.sharedMesh = mesh;
		}
	}
}
