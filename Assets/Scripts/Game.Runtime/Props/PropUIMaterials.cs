using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.Props
{
	// The paint a prop wears when it is drawn on the UI instead of in the world. One prefab serves both, so
	// everything that happens to the prop — a variant, a hallucination, a script — happens to the copy on
	// the HUD too; only the materials differ, and they are authored here on the prop itself because every
	// kind of item will want its own.
	//
	// Switched once, as the prop is put on the UI and before anything claims a renderer: whatever paints it
	// afterwards (an outline, a hallucination) then takes the UI paint as what the prop was authored with and
	// layers onto that. Renderers that start switched off are listed too — a look turned on later is part of
	// the same prop.
	public class PropUIMaterials : MonoBehaviour
	{
		[Serializable]
		private struct Entry
		{
			[Required] public Renderer Renderer;

			[Tooltip("One per material slot, in slot order. An empty slot keeps the world material.")]
			public Material[] Materials;
		}

		[SerializeField] private List<Entry> _entries = new();

		private readonly Dictionary<Renderer, Material[]> _world = new();

		public bool IsUI { get; private set; }

		public void SetIsUI(bool isUI)
		{
			if (IsUI == isUI) return;

			IsUI = isUI;

			foreach (var entry in _entries)
			{
				if (!entry.Renderer) continue;

				if (!_world.TryGetValue(entry.Renderer, out var world))
				{
					world = entry.Renderer.sharedMaterials;
					_world.Add(entry.Renderer, world);
				}

				entry.Renderer.sharedMaterials = isUI ? Compose(world, entry.Materials) : world;
			}
		}

		private static Material[] Compose(Material[] world, Material[] ui)
		{
			var result = (Material[])world.Clone();

			if (ui == null) return result;

			for (var i = 0; i < result.Length && i < ui.Length; i++)
			{
				if (ui[i]) result[i] = ui[i];
			}

			return result;
		}

#if UNITY_EDITOR
		// Lists every renderer under the prop with one empty slot per material, keeping whatever is already
		// filled in, so authoring is dragging the UI materials in rather than building the list by hand.
		[Button("Collect Renderers")]
		private void CollectRenderers()
		{
			UnityEditor.Undo.RecordObject(this, "Collect Renderers");

			foreach (var renderer in GetComponentsInChildren<Renderer>(true))
			{
				var index = _entries.FindIndex(e => e.Renderer == renderer);
				var slots = renderer.sharedMaterials.Length;

				if (index < 0)
				{
					_entries.Add(new Entry { Renderer = renderer, Materials = new Material[slots] });
					continue;
				}

				var entry = _entries[index];
				if (entry.Materials == null || entry.Materials.Length != slots)
				{
					var resized = new Material[slots];
					if (entry.Materials != null) Array.Copy(entry.Materials, resized, Mathf.Min(slots, entry.Materials.Length));
					entry.Materials = resized;
					_entries[index] = entry;
				}
			}

			UnityEditor.EditorUtility.SetDirty(this);
		}
#endif
	}
}
