using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Player
{
	// The gnome avatar is Generic, so a clip binds to bones by transform path rather than through a humanoid
	// mapping. The model update renamed Neck7_M to Neck_M and Head_M to Head1_M while the eight animation
	// FBXs under Anim/ were left exported against the old skeleton — so every curve from the neck up bound to
	// nothing, and the Animator stopped writing the head. That is not a missing animation you can see: it is
	// a bone nobody rewrites, and PlayerController composes the look onto whatever is already on the bone, so
	// the view accumulated its own turn every frame (measured: 30 degrees written twice read back as 60) and
	// stayed wherever it had got to once the player stood up.
	//
	// Renaming the paths as the clips are imported keeps the fix in one place, duplicates no asset, and turns
	// itself off the moment the animations are re-exported against the current skeleton — which is the real
	// repair, and what should eventually make this class deletable.
	public class GnomeAnimationBoneRemapper : AssetPostprocessor
	{
		private const string AnimationFolder = "Assets/Art/Character/Gnome/Anim/";

		// Matched a whole path segment at a time: a substring replace would also rewrite any longer bone name
		// that happens to contain one of these.
		private static readonly (string From, string To)[] RenamedBones =
		{
			("Neck7_M", "Neck_M"),
			("Head_M", "Head1_M")
		};

		private void OnPostprocessAnimation(GameObject root, AnimationClip clip)
		{
			if (!assetPath.StartsWith(AnimationFolder)) return;

			var bindings = AnimationUtility.GetCurveBindings(clip);
			var rewritten = 0;

			foreach (var binding in bindings)
			{
				var path = RenamedPath(binding.path);
				if (path == binding.path) continue;

				var curve = AnimationUtility.GetEditorCurve(clip, binding);
				if (curve == null) continue;

				AnimationUtility.SetEditorCurve(clip, binding, null);
				AnimationUtility.SetEditorCurve(clip, new EditorCurveBinding { path = path, type = binding.type, propertyName = binding.propertyName }, curve);
				rewritten++;
			}

			if (rewritten > 0) Debug.Log($"{clip.name}: bound {rewritten} curve(s) to the renamed gnome bones.");
		}

		private static string RenamedPath(string path)
		{
			if (string.IsNullOrEmpty(path)) return path;

			return string.Join("/", path.Split('/').Select(Renamed));
		}

		private static string Renamed(string segment)
		{
			foreach (var renamed in RenamedBones)
			{
				if (segment == renamed.From) return renamed.To;
			}

			return segment;
		}
	}
}
