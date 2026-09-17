using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.UI
{
	// A 3D model standing on the UI, scaled into a box of its own. The canvas is drawn by the overlay UICamera, so
	// anything on the UI layer under it is rendered with the HUD — this puts a prefab there, fits its
	// bounds into that box whatever size the model was authored at, and brings its root just in front of the
	// canvas plane so the flat art behind it cannot cover it.
	//
	// root (turned, brought forward) -> pivot (the lean) -> model, centred on the pivot in all three axes.
	// It knows nothing about what the model is; a button visual spins or outlines the root, and a view only
	// says which prefab to show.
	[RequireComponent(typeof(RectTransform))]
	public class UIModelView : MonoBehaviour
	{
		[Header("References")]
		[Tooltip("What the model hangs under — a tilt, if the model should lean. The model is centred on this, so whatever turns a parent of it turns the model about its own middle.")]
		[Required]
		[SerializeField] private Transform _pivot;

		[Tooltip("What is brought forward in front of the canvas and what button visuals turn: the pivot itself, or a parent of it. Keeping the lean on the pivot and the spin on this root is what lets a tilted model turn about the upright axis, so the spin can be seen. Empty uses the pivot. Visuals point here, never at the model, because the model is replaced whenever a different prefab is shown.")]
		[SerializeField] private Transform _root;

		private Transform Root => _root ? _root : _pivot;

		[Header("Fit")]
		[Tooltip("The box every model is scaled into, in UI units, centred on the pivot (drawn when selected). One box for all of them, so prefabs authored at different sizes all come out the size this says. A model is scaled uniformly until it touches the box; its width and depth are taken together, because turning about the pivot's Y swaps one for the other.")]
		[SerializeField] private Vector3 _bounds = new(100f, 100f, 100f);

		public GameObject Model { get; private set; }

		public event Action<GameObject> OnModelChanged;

		private GameObject _prefab;

		// The same prefab again keeps the instance it already has, so re-binding a list never flashes.
		public void Show(GameObject prefab)
		{
			if (prefab == _prefab && Model) return;

			Clear();
			if (!prefab || !_pivot) return;

			_prefab = prefab;
			Model = Instantiate(prefab, _pivot, false);
			Model.name = prefab.name;

			SetLayer(Model.transform, gameObject.layer);
			Fit();

			OnModelChanged?.Invoke(Model);
		}

		public void Clear()
		{
			_prefab = null;
			if (!Model) return;

			Destroy(Model);
			Model = null;

			OnModelChanged?.Invoke(null);
		}

		// Measured in the pivot's own space with the model at unit scale, so the answer depends neither on how the
		// canvas is scaled on this screen nor on the lean or the spin above it.
		private void Fit()
		{
			var modelTransform = Model.transform;
			modelTransform.localPosition = Vector3.zero;
			modelTransform.localRotation = Quaternion.identity;
			modelTransform.localScale = Vector3.one;

			// Only what is drawn: a look switched off (a variant's eyes) would pull the centre off the axis it spins on.
			var renderers = Model.GetComponentsInChildren<Renderer>(false);
			var bounds = renderers.Length > 0 ? LocalBounds(renderers) : new Bounds();
			var scale = FitScale(bounds.size);

			if (scale > 0f)
			{
				modelTransform.localScale = Vector3.one * scale;
				modelTransform.localPosition = -bounds.center * scale;
			}

			PlaceRoot();
		}

		private float FitScale(Vector3 size)
		{
			var across = Mathf.Max(size.x, size.z);
			var room = Mathf.Min(_bounds.x, _bounds.z);

			var scale = float.MaxValue;
			if (across > 0f) scale = Mathf.Min(scale, room / across);
			if (size.y > 0f) scale = Mathf.Min(scale, _bounds.y / size.y);

			return scale == float.MaxValue ? 0f : scale;
		}

		// The root, not the model, comes forward by half the box (-Z is towards the camera): every model then
		// sits at the same depth in front of the canvas, and a model pushed off what turns it would swing round
		// it rather than turning in place.
		private void PlaceRoot()
		{
			var root = Root;
			var position = root.localPosition;
			position.z = -_bounds.z * 0.5f;
			root.localPosition = position;
		}

		private void OnDrawGizmosSelected()
		{
			if (!_pivot) return;

			Gizmos.matrix = transform.localToWorldMatrix;
			Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.8f);
			Gizmos.DrawWireCube(new Vector3(Root.localPosition.x, Root.localPosition.y, -_bounds.z * 0.5f), _bounds);
		}

		// Built from each mesh's own bounds through the local transforms between the renderer and the pivot,
		// never through world space. UICamera is parked 5000 units up, and a model at unit scale under a canvas
		// is a few thousandths of a unit across there — within float precision of nothing, so a box measured
		// in world space came out several percent off and different for every button (measured: 1600, 1458
		// and 1482 for one cap whose true fit is 1466.667).
		private Bounds LocalBounds(Renderer[] renderers)
		{
			var bounds = new Bounds();
			var initialised = false;

			foreach (var renderer in renderers)
			{
				var mesh = MeshBounds(renderer);
				var matrix = ToPivot(renderer.transform);

				for (var i = 0; i < 8; i++)
				{
					var corner = mesh.center + Vector3.Scale(mesh.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
					var local = matrix.MultiplyPoint3x4(corner);

					if (!initialised)
					{
						bounds = new Bounds(local, Vector3.zero);
						initialised = true;
					}
					else
					{
						bounds.Encapsulate(local);
					}
				}
			}

			return bounds;
		}

		private Matrix4x4 ToPivot(Transform from)
		{
			var matrix = Matrix4x4.identity;

			for (var t = from; t && t != _pivot; t = t.parent)
			{
				matrix = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale) * matrix;
			}

			return matrix;
		}

		// The mesh asset's own box, so nothing measured in world space is involved at all.
		private static Bounds MeshBounds(Renderer renderer)
		{
			if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh) return skinned.sharedMesh.bounds;

			var filter = renderer.GetComponent<MeshFilter>();
			return filter && filter.sharedMesh ? filter.sharedMesh.bounds : renderer.localBounds;
		}

		private static void SetLayer(Transform root, int layer)
		{
			root.gameObject.layer = layer;
			foreach (Transform child in root) SetLayer(child, layer);
		}
	}
}
