using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Interaction
{
	public class InteractableVisual : MonoBehaviour
	{
		[Header("Outline")]
		[SerializeField] private Material _outlineMaterial;
		[SerializeField] private Color _outlineColor = new(1f, 0.85f, 0.35f, 1f);
		[SerializeField] private float _outlineWidth = 0.02f;

		[Header("Renderers")]
		[Tooltip("Left empty, every renderer under this object is outlined.")]
		[SerializeField] private List<Renderer> _renderers = new();

		private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
		private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

		private readonly Dictionary<Renderer, Material[]> _originalMaterials = new();
		private Material _runtimeOutlineMaterial;
		private bool _outlineEnabled;

		private void Awake()
		{
			if (_renderers.Count == 0) GetComponentsInChildren(true, _renderers);

			foreach (var renderer in _renderers)
			{
				if (renderer) _originalMaterials[renderer] = renderer.sharedMaterials;
			}
		}

		private void OnDestroy()
		{
			if (_runtimeOutlineMaterial) Destroy(_runtimeOutlineMaterial);
		}

		public void SetOutline(bool enabled)
		{
			if (_outlineEnabled == enabled) return;
			_outlineEnabled = enabled;

			if (enabled)
			{
				ApplyOutlineMaterial();
				return;
			}

			RestoreOriginalMaterials();
		}

		private void ApplyOutlineMaterial()
		{
			if (!_outlineMaterial) return;

			if (!_runtimeOutlineMaterial)
			{
				_runtimeOutlineMaterial = new Material(_outlineMaterial);
				_runtimeOutlineMaterial.SetColor(OutlineColorId, _outlineColor);
				_runtimeOutlineMaterial.SetFloat(OutlineWidthId, _outlineWidth);
			}

			foreach (var pair in _originalMaterials)
			{
				var renderer = pair.Key;
				if (!renderer) continue;

				var original = pair.Value;
				var withOutline = new Material[original.Length + 1];
				for (var i = 0; i < original.Length; i++) withOutline[i] = original[i];
				withOutline[original.Length] = _runtimeOutlineMaterial;

				renderer.sharedMaterials = withOutline;
			}
		}

		private void RestoreOriginalMaterials()
		{
			foreach (var pair in _originalMaterials)
			{
				if (pair.Key) pair.Key.sharedMaterials = pair.Value;
			}
		}
	}
}
