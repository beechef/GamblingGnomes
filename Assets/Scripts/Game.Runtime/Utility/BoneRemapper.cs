using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Utility
{
	public static class BoneRemapper
	{
		public static void RemapBoneRenderer(SkinnedMeshRenderer renderer, Transform targetRoot)
		{
			var targetBoneNames = new Dictionary<string, Transform>();
			foreach (var t in targetRoot.GetComponentsInChildren<Transform>(true))
			{
				targetBoneNames[t.name] = t;
			}

			var rendererBones = renderer.bones;
			var remappedBones = new Transform[rendererBones.Length];

			for (var i = 0; i < rendererBones.Length; i++)
			{
				var originalBone = rendererBones[i];
				if (originalBone == null)
				{
					remappedBones[i] = null;
					continue;
				}

				if (targetBoneNames.TryGetValue(originalBone.name, out var bodyBone))
				{
					remappedBones[i] = bodyBone;
				}
				else
				{
					Debug.LogWarning($"[ClothBoneRemapper] No matching body bone for '{originalBone.name}'. Left unmapped.");
					remappedBones[i] = originalBone;
				}
			}

			renderer.bones = remappedBones;

			if (renderer.rootBone != null && targetBoneNames.TryGetValue(renderer.rootBone.name, out var newRoot))
			{
				renderer.rootBone = newRoot;
			}
		}
	}
}
