using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Props
{
	// The one writer of a renderer's materials, and the one holder of what it was authored with.
	//
	// Every caller used to capture and restore for itself, and that is wrong the moment two of them want
	// the same renderer — which the hallucination ladder allows on purpose, since an effect may sit in two
	// rungs' pools. The second caller captures the *first one's paint* as though it were the original, and
	// then the order they let go in decides the outcome: release them the wrong way round and the renderer
	// keeps a coat of paint nothing will ever take off. Nothing warns, and it only shows on the rung
	// combination somebody happens to draw.
	//
	// So the authored array is captured once, by whoever claims the renderer first, and it is this that
	// puts it back when the last caller leaves. It composes rather than fighting: the last Replace wins —
	// two repaints on one thing is a conflict somebody authored, not a blend anybody asked for — and every
	// Add is hung on the end afterwards, so extra passes stack in the order they arrived.
	[DefaultExecutionOrder(50)]
	public class PropMaterialOverrideController : MonoBehaviour
	{
		private class Entry
		{
			public object Handle;
			public Material Material;
			public PropPaintMode Mode;
		}

		[SerializeField] private Renderer _renderer;

		private readonly List<Entry> _entries = new();
		private readonly List<Material> _buffer = new();

		private Material[] _authored;

		// Claimed rather than serialized: what gets painted is a card dealt this round or a cap put down a
		// moment ago, and neither exists to be wired up in a prefab.
		public static PropMaterialOverrideController Claim(Renderer renderer)
		{
			if (!renderer) return null;

			var controller = renderer.GetComponent<PropMaterialOverrideController>();
			if (controller) return controller;

			controller = renderer.gameObject.AddComponent<PropMaterialOverrideController>();
			controller.Adopt(renderer);

			return controller;
		}

		private void Adopt(Renderer renderer)
		{
			_renderer = renderer;

			// The only moment the authored array is still there to read.
			_authored = renderer.sharedMaterials;
		}

		public void Set(object handle, Material material, PropPaintMode mode)
		{
			if (handle == null || !_renderer) return;

			foreach (var entry in _entries)
			{
				if (entry.Handle != handle) continue;

				entry.Material = material;
				entry.Mode = mode;
				Apply();
				return;
			}

			_entries.Add(new Entry { Handle = handle, Material = material, Mode = mode });
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

		private void Apply()
		{
			if (!_renderer || _authored == null) return;

			Material replacement = null;
			for (var i = _entries.Count - 1; i >= 0; i--)
			{
				if (_entries[i].Mode != PropPaintMode.Replace || !_entries[i].Material) continue;

				replacement = _entries[i].Material;
				break;
			}

			_buffer.Clear();

			if (replacement)
			{
				for (var i = 0; i < _authored.Length; i++) _buffer.Add(replacement);
			}
			else
			{
				_buffer.AddRange(_authored);
			}

			foreach (var entry in _entries)
			{
				if (entry.Mode == PropPaintMode.Add && entry.Material) _buffer.Add(entry.Material);
			}

			_renderer.sharedMaterials = _buffer.ToArray();
		}
	}
}
