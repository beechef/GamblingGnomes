using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Player
{
	// Holds the request to repaint a body and hands the winner to PlayerVisual, which is the one thing that
	// writes a renderer's materials. Two systems assigning sharedMaterials is the bug this exists to avoid:
	// the outline is hung and dropped mid-hand, and it replaces the whole array each time, so a repaint
	// written straight onto the renderer would be wiped by the next accusation.
	//
	// Stacking is resolved here rather than in PlayerVisual because it is a question about callers, and
	// PlayerVisual has no idea how many there are.
	[DefaultExecutionOrder(50)]
	public class PlayerMaterialOverrideController : MonoBehaviour
	{
		private class Entry
		{
			public object Handle;
			public Material Material;
		}

		[SerializeField] private PlayerVisual _visual;

		private readonly List<Entry> _entries = new();

		public int Count => _entries.Count;

		private void Awake()
		{
			if (!_visual) _visual = GetComponentInParent<PlayerVisual>();
		}

		// Last one in wins, which is the same answer the bone scaler gives and for the same reason: two
		// paints on one body is a conflict somebody authored, not a blend anybody asked for.
		public void Set(object handle, Material material)
		{
			if (handle == null) return;

			foreach (var entry in _entries)
			{
				if (entry.Handle != handle) continue;

				entry.Material = material;
				Apply();
				return;
			}

			_entries.Add(new Entry { Handle = handle, Material = material });
			Apply();
		}

		public void Clear(object handle)
		{
			if (handle == null) return;

			for (var i = _entries.Count - 1; i >= 0; i--)
			{
				if (_entries[i].Handle == handle) _entries.RemoveAt(i);
			}

			Apply();
		}

		private void OnDisable()
		{
			if (_entries.Count == 0) return;

			_entries.Clear();
			Apply();
		}

		private void Apply()
		{
			if (!_visual) return;

			Material winner = null;
			for (var i = _entries.Count - 1; i >= 0; i--)
			{
				if (!_entries[i].Material) continue;

				winner = _entries[i].Material;
				break;
			}

			_visual.SetMaterialOverride(winner);
		}
	}
}
