using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Runtime.AnimationVfx
{
	// One effect fired at one frame of one clip, hung off one bone. The game and the editor preview both
	// spawn it through Spawn, so what was lined up while scrubbing is exactly what plays.
	[Serializable]
	public class AnimationVfxCue
	{
		[Tooltip("Frame of the clip the effect starts on, counted at the clip's own frame rate.")]
		[MinValue(0)]
		public int Frame;

		[Tooltip("What is spawned. A VisualEffect inside it plays on enable.")]
		[Required]
		public GameObject Prefab;

		[Tooltip("Bone it hangs off, by name, so one cue lands on whichever rig is drawn — both carry the same skeleton. Empty uses the rig's root.")]
		public string Bone;

		[Tooltip("Offset from the bone, in the bone's own space.")]
		public Vector3 Position;

		public Vector3 Rotation;

		[Tooltip("Keeps riding the bone. Off leaves it in the world where it was spawned, for something that should stay behind as the body moves on.")]
		public bool FollowBone = true;

		[Tooltip("Seconds the spawned effect lives before it is destroyed.")]
		[MinValue(0.01f)]
		public float Lifetime = 2f;

		public float TimeIn(AnimationClip clip) => clip && clip.frameRate > 0f ? Frame / clip.frameRate : 0f;

		public GameObject Spawn(Transform bone)
		{
			if (!Prefab || !bone) return null;

			var instance = Object.Instantiate(Prefab, bone, false);
			instance.transform.SetLocalPositionAndRotation(Position, Quaternion.Euler(Rotation));

			if (!FollowBone) instance.transform.SetParent(null, true);

			return instance;
		}

		// Null when a named bone is not there, so the caller can say so rather than quietly using the root.
		public static Transform FindBone(Transform root, string name)
		{
			if (!root) return null;
			if (string.IsNullOrEmpty(name)) return root;

			return FindChild(root, name);
		}

		private static Transform FindChild(Transform parent, string name)
		{
			if (parent.name == name) return parent;

			for (var i = 0; i < parent.childCount; i++)
			{
				var found = FindChild(parent.GetChild(i), name);
				if (found) return found;
			}

			return null;
		}
	}
}
