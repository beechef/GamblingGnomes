using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Player
{
	// Binds the finger meshes the model already ships cut apart into PlayerFingerVisual, by the names the
	// artist gave them. Run again after a rig is re-cut: sixteen objects dragged by hand is sixteen chances
	// to drop one, and a finger left out is one a player keeps forever.
	public static class PlayerFingerBinder
	{
		// The order they come off in. Ends first and thumbs last, alternating hands so a player loses the use
		// of both together rather than one whole hand and then the other.
		private static readonly string[] FingerNames =
		{
			"Pinky_L", "Pinky_R",
			"Index_L", "Index_R",
			"Middle_L", "Middle_R",
			"Thumb_L", "Thumb_R"
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
				else Debug.Log($"Bound {FingerNames.Length} fingers into {path}.");
			}
			finally
			{
				PrefabUtility.UnloadPrefabContents(root);
			}
		}

		private static bool BindInto(GameObject root, string path)
		{
			var pieces = CollectPieces(root);

			foreach (var fingerName in FingerNames)
			{
				if (pieces.TryGetValue(fingerName, out var found) && found.Count > 0) continue;

				Debug.LogError($"{path} has no mesh named {fingerName}. The rig is not cut the way this expects — nothing was bound.");
				return false;
			}

			var visual = ResolveFingerVisual(root);

			var serialized = new SerializedObject(visual);
			var fingers = serialized.FindProperty("_fingers");
			fingers.arraySize = FingerNames.Length;

			for (var i = 0; i < FingerNames.Length; i++)
			{
				var entry = fingers.GetArrayElementAtIndex(i);
				entry.FindPropertyRelative("Name").stringValue = FingerNames[i];

				var entryPieces = entry.FindPropertyRelative("Pieces");
				var found = pieces[FingerNames[i]];
				entryPieces.arraySize = found.Count;

				for (var piece = 0; piece < found.Count; piece++)
				{
					entryPieces.GetArrayElementAtIndex(piece).objectReferenceValue = found[piece];
				}
			}

			serialized.ApplyModifiedPropertiesWithoutUndo();

			BindBloodReadout(root, visual);

			return true;
		}

		// Every mesh carrying the finger's name, across every rig on the prefab — the owner's hand-only pair
		// included, or a player keeps a finger only they can see. Bones are named FingerN_L and never collide.
		private static Dictionary<string, List<GameObject>> CollectPieces(GameObject root)
		{
			var wanted = new HashSet<string>(FingerNames);
			var pieces = new Dictionary<string, List<GameObject>>();

			foreach (var child in root.GetComponentsInChildren<Transform>(true))
			{
				if (!wanted.Contains(child.name)) continue;
				if (!child.GetComponent<Renderer>()) continue;

				if (!pieces.TryGetValue(child.name, out var found)) pieces[child.name] = found = new List<GameObject>();

				found.Add(child.gameObject);
			}

			return pieces;
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
