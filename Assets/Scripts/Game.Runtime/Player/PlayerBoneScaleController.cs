using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Player
{
	// Resizes bones without asking the Animator's permission. Every gnome clip carries a scale curve for
	// all 83 animated bones, all of them constant 1, so the Animator writes scale over the whole skeleton
	// every frame: a resize done once is gone by the next one, the same trap already written down for bone
	// positions. This writes in LateUpdate, after the Animator and before the Cinemachine brain samples,
	// because a scaled head moves the camera hanging off it.
	//
	// Rest is captured once and every frame writes rest times the multiplier, never the value read back —
	// so a bone the clips do not animate cannot compound its own last write into the next one.
	//
	// It carries no game meaning. What a swollen head means is the caller's business.
	[DefaultExecutionOrder(50)]
	public class PlayerBoneScaleController : MonoBehaviour
	{
		private class Entry
		{
			public object Handle;
			public Transform Bone;
			public Vector3 Multiplier;
		}

		private readonly List<Entry> _entries = new();
		private readonly Dictionary<Transform, Vector3> _rest = new();

		public int Count => _entries.Count;

		// Last one in wins where two callers name the same bone, which is the simplest answer that is still
		// a decision: the alternative is multiplying them together, and two effects that each double a head
		// would quadruple it without anybody having asked for that.
		public void Set(object handle, Transform bone, Vector3 multiplier)
		{
			if (handle == null || !bone) return;

			if (!_rest.ContainsKey(bone)) _rest[bone] = bone.localScale;

			foreach (var entry in _entries)
			{
				if (entry.Handle != handle || entry.Bone != bone) continue;

				entry.Multiplier = multiplier;
				return;
			}

			_entries.Add(new Entry { Handle = handle, Bone = bone, Multiplier = multiplier });
		}

		public void Clear(object handle)
		{
			if (handle == null) return;

			for (var i = _entries.Count - 1; i >= 0; i--)
			{
				if (_entries[i].Handle != handle) continue;

				var bone = _entries[i].Bone;
				_entries.RemoveAt(i);
				RestoreIfUnclaimed(bone);
			}
		}

		private void OnDisable()
		{
			foreach (var pair in _rest)
			{
				if (pair.Key) pair.Key.localScale = pair.Value;
			}

			_entries.Clear();
			_rest.Clear();
		}

		private void RestoreIfUnclaimed(Transform bone)
		{
			if (!bone) return;

			foreach (var entry in _entries)
			{
				if (entry.Bone == bone) return;
			}

			if (_rest.TryGetValue(bone, out var rest)) bone.localScale = rest;
			_rest.Remove(bone);
		}

		private void LateUpdate()
		{
			if (_entries.Count == 0) return;

			foreach (var entry in _entries)
			{
				if (!entry.Bone || !_rest.TryGetValue(entry.Bone, out var rest)) continue;

				entry.Bone.localScale = Vector3.Scale(rest, entry.Multiplier);
			}
		}
	}
}
