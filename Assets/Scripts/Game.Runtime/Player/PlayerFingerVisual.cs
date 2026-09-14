using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.Player
{
	// Fingers that come off one at a time, in an order somebody authored. Domain-agnostic on purpose: what
	// makes a finger fall is never this component's business — it is handed a count and draws it, so
	// anything that ever costs a player a finger gets the display for nothing.
	//
	// A finger is taken by collapsing a bone, because the model is one skinned mesh per rig: a joint scaled
	// to zero pulls every vertex it carries, and every bone below it, into a point, leaving the segment
	// before it as the stump. The scale goes through PlayerBoneScaleController rather than being written
	// here, because the Animator rewrites every bone's scale each frame and that controller is the one
	// writer that runs after it — and a hallucination resizing the same hand then composes with a lost
	// finger instead of fighting it.
	public class PlayerFingerVisual : MonoBehaviour
	{
		[Serializable]
		public struct Finger
		{
			[Tooltip("Read the list by, not used at runtime.")]
			public string Name;

			[Tooltip("The joint this finger comes off at, on every rig — the owner's hand-only rig included, or a player keeps a finger only they can see.")]
			public Transform[] Bones;
		}

		[Header("Fingers")]
		[Tooltip("Lost in list order: the first entry is the first to go. A reorder is a behaviour change no compiler catches.")]
		[SerializeField] private List<Finger> _fingers = new();

		[Header("References")]
		[Required]
		[SerializeField] private PlayerBoneScaleController _boneScale;

		private readonly Dictionary<Transform, PlayerBoneScaleModifier> _collapsed = new();

		public int FingerCount => _fingers.Count;

		public int LostFingerCount => Mathf.Max(0, _lostCount);

		// Nothing has said how many are gone yet, so the first answer always draws.
		private int _lostCount = -1;

		private void Awake() => SetLostFingerCount(0);

		public void SetLostFingerCount(int lostCount)
		{
			var lost = Mathf.Clamp(lostCount, 0, _fingers.Count);
			if (lost == _lostCount) return;

			_lostCount = lost;

			for (var i = 0; i < _fingers.Count; i++) SetAttached(_fingers[i], i >= lost);
		}

		private void SetAttached(Finger finger, bool attached)
		{
			if (finger.Bones == null) return;

			foreach (var bone in finger.Bones)
			{
				if (!bone) continue;

				if (attached)
				{
					if (!_collapsed.TryGetValue(bone, out var modifier)) continue;

					modifier.Remove();
					_collapsed.Remove(bone);
				}
				else if (_boneScale && !_collapsed.ContainsKey(bone))
				{
					var modifier = _boneScale.Add(bone, Vector3.zero);
					if (modifier != null) _collapsed[bone] = modifier;
				}
			}
		}
	}
}
