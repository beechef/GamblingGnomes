using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Player
{
	// Binds each finger's joint, on every rig that is drawn, into PlayerFingerVisual by the bone names the
	// model ships with. Run again after a rig update: sixteen bones dragged by hand is sixteen chances to
	// drop one, and a finger left out is one a player keeps forever.
	public static class PlayerFingerBinder
	{
		// The order they come off in — ends first and thumbs last, alternating hands so a player loses the
		// use of both together rather than one whole hand and then the other — and the joint each comes off
		// at. The second joint, so the first segment is left as a stump rather than the finger vanishing
		// from the knuckle.
		private static readonly (string Name, string Bone)[] Fingers =
		{
			("Pinky_L", "PinkyFinger2_L"), ("Pinky_R", "PinkyFinger2_R"),
			("Index_L", "IndexFinger2_L"), ("Index_R", "IndexFinger2_R"),
			("Middle_L", "MiddleFinger2_L"), ("Middle_R", "MiddleFinger2_R"),
			("Thumb_L", "ThumbFinger2_L"), ("Thumb_R", "ThumbFinger2_R")
		};

		private const string FingersChildName = "Fingers";

		[MenuItem("Assets/Player/Bind Fingers", true)]
		private static bool ValidateBindFingers() =>
			Selection.activeObject is GameObject prefab && PrefabUtility.IsPartOfPrefabAsset(prefab);

		[MenuItem("Assets/Player/Bind Fingers")]
		private static void BindFingers()
		{
			var path = AssetDatabase.GetAssetPath(Selection.activeObject);
			var root = PrefabUtility.LoadPrefabContents(path);

			try
			{
				if (!BindInto(root, path)) return;

				PrefabUtility.SaveAsPrefabAsset(root, path, out var saved);

				// A save that reports false has changed nothing and says nothing about why — treated as a hard
				// stop rather than something to run again over, because the next read comes back green either way.
				if (!saved) Debug.LogError($"Binding fingers wrote nothing to {path}. Check the asset database is not read only.");
				else Debug.Log($"Bound {Fingers.Length} fingers into {path}.");
			}
			finally
			{
				PrefabUtility.UnloadPrefabContents(root);
			}
		}

		public static bool BindInto(GameObject root, string path)
		{
			var rigs = root.GetComponentsInChildren<PlayerBoneRig>(true);
			var bones = CollectBones(rigs);

			foreach (var finger in Fingers)
			{
				if (bones.TryGetValue(finger.Bone, out var found) && found.Count == rigs.Length) continue;

				Debug.LogError($"{path} does not carry {finger.Bone} on each of its {rigs.Length} rigs. The skeleton is not the one this expects — nothing was bound.");
				return false;
			}

			var boneScale = root.GetComponentInChildren<PlayerBoneScaleController>(true);
			if (!boneScale)
			{
				Debug.LogError($"{path} has no {nameof(PlayerBoneScaleController)}, and a finger is taken by scaling its bone through one — nothing was bound.");
				return false;
			}

			var visual = ResolveFingerVisual(root);

			var serialized = new SerializedObject(visual);
			serialized.FindProperty("_boneScale").objectReferenceValue = boneScale;

			var fingers = serialized.FindProperty("_fingers");
			fingers.arraySize = Fingers.Length;

			for (var i = 0; i < Fingers.Length; i++)
			{
				var entry = fingers.GetArrayElementAtIndex(i);
				entry.FindPropertyRelative("Name").stringValue = Fingers[i].Name;

				var entryBones = entry.FindPropertyRelative("Bones");
				var found = bones[Fingers[i].Bone];
				entryBones.arraySize = found.Count;

				for (var bone = 0; bone < found.Count; bone++)
				{
					entryBones.GetArrayElementAtIndex(bone).objectReferenceValue = found[bone];
				}
			}

			serialized.ApplyModifiedPropertiesWithoutUndo();

			BindBloodReadout(root, visual);

			return true;
		}

		// Only under a rig that is drawn. The outfit models carry their own copy of the skeleton, rebound
		// onto the body's bones at runtime, so a bone found there is one nothing ever renders from.
		private static Dictionary<string, List<Transform>> CollectBones(PlayerBoneRig[] rigs)
		{
			var wanted = new HashSet<string>();
			foreach (var finger in Fingers) wanted.Add(finger.Bone);

			var bones = new Dictionary<string, List<Transform>>();

			foreach (var rig in rigs)
			{
				foreach (var child in rig.GetComponentsInChildren<Transform>(true))
				{
					if (!wanted.Contains(child.name)) continue;

					if (!bones.TryGetValue(child.name, out var found)) bones[child.name] = found = new List<Transform>();

					found.Add(child);
				}
			}

			return bones;
		}

		private static PlayerFingerVisual ResolveFingerVisual(GameObject root)
		{
			var existing = root.GetComponentInChildren<PlayerFingerVisual>(true);
			if (existing) return existing;

			var holder = root.transform.Find(FingersChildName);

			if (!holder)
			{
				var created = new GameObject(FingersChildName);
				created.transform.SetParent(root.transform, false);
				holder = created.transform;
			}

			return holder.gameObject.AddComponent<PlayerFingerVisual>();
		}

		// Only a prefab that plays poker has blood to spend, so the readout is only hung where there is
		// something to read: the generic half stands on its own for any other mode to drive.
		private static void BindBloodReadout(GameObject root, PlayerFingerVisual visual)
		{
			var data = root.GetComponent<PokerPlayerData>();
			if (!data) return;

			var readout = root.GetComponent<PokerBloodFingerVisual>();
			if (!readout) readout = root.AddComponent<PokerBloodFingerVisual>();

			var serialized = new SerializedObject(readout);
			serialized.FindProperty("_data").objectReferenceValue = data;
			serialized.FindProperty("_fingers").objectReferenceValue = visual;
			serialized.ApplyModifiedPropertiesWithoutUndo();
		}
	}
}
