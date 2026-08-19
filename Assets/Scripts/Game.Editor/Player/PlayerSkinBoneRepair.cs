using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Player
{
	// A skinned mesh deforms from its bone array *in index order*, because index i is what the mesh's
	// bindposes[i] belongs to. Re-parenting a rig and rebuilding that array from a fresh hierarchy walk
	// keeps every bone and loses the order, and the mesh then melts — the bones themselves look perfect in
	// the inspector, which is why this costs an afternoon to find by eye. The model the mesh was imported
	// from is the only record of the right order, so the repair reorders the renderer's own bones to match
	// its source renderer and writes nothing else.
	public static class PlayerSkinBoneRepair
	{
		[MenuItem("Assets/Player/Repair Skin Bones", true)]
		private static bool ValidateRepair() =>
			Selection.activeObject is GameObject prefab && PrefabUtility.IsPartOfPrefabAsset(prefab);

		[MenuItem("Assets/Player/Repair Skin Bones")]
		private static void Repair()
		{
			var path = AssetDatabase.GetAssetPath(Selection.activeObject);
			var root = PrefabUtility.LoadPrefabContents(path);

			try
			{
				var report = new StringBuilder();
				var repaired = 0;

				foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
				{
					if (!TryRepair(renderer, report, out var changed)) return;
					if (changed) repaired++;
				}

				if (repaired == 0)
				{
					Debug.Log($"Every skinned mesh in {path} already matches its source order.\n{report}");
					return;
				}

				PrefabUtility.SaveAsPrefabAsset(root, path, out var saved);

				if (!saved) Debug.LogError($"Repairing {path} wrote nothing. Check the asset database is not read only.");
				else Debug.Log($"Reordered bones on {repaired} renderer(s) in {path}.\n{report}");
			}
			finally
			{
				PrefabUtility.UnloadPrefabContents(root);
			}
		}

		private static bool TryRepair(SkinnedMeshRenderer renderer, StringBuilder report, out bool changed)
		{
			changed = false;

			if (!renderer.sharedMesh)
			{
				report.AppendLine($"{renderer.name}: no mesh, skipped");
				return true;
			}

			var source = FindSourceRenderer(renderer.sharedMesh);

			if (!source)
			{
				report.AppendLine($"{renderer.name}: no source renderer for {renderer.sharedMesh.name}, skipped");
				return true;
			}

			var current = renderer.bones;
			var wanted = source.bones;

			if (current.Length != wanted.Length)
			{
				Debug.LogError($"{renderer.name} has {current.Length} bones where {source.name} has {wanted.Length}. The rig no longer matches the model it was skinned to — nothing was written.");
				return false;
			}

			// The transforms are already the right ones; only their order is lost. Reordering what is there
			// rather than searching the hierarchy is what keeps this from binding to the wrong skeleton on a
			// prefab that carries two of them under the same bone names.
			var byName = new Dictionary<string, Transform>();
			foreach (var bone in current)
			{
				if (bone) byName[bone.name] = bone;
			}

			var reordered = new Transform[wanted.Length];
			var wrongOrder = 0;

			for (var i = 0; i < wanted.Length; i++)
			{
				var wantedName = wanted[i] ? wanted[i].name : null;

				if (wantedName == null || !byName.TryGetValue(wantedName, out var bone))
				{
					Debug.LogError($"{renderer.name} has no bone named '{wantedName}' that {source.name} expects at index {i} — nothing was written.");
					return false;
				}

				reordered[i] = bone;
				if (current[i] != bone) wrongOrder++;
			}

			if (wrongOrder == 0)
			{
				report.AppendLine($"{renderer.name}: already in order");
				return true;
			}

			renderer.bones = reordered;

			if (source.rootBone && byName.TryGetValue(source.rootBone.name, out var rootBone)) renderer.rootBone = rootBone;

			report.AppendLine($"{renderer.name}: reordered {wrongOrder} of {wanted.Length} bones");
			changed = true;
			return true;
		}

		// The model asset the mesh was imported from still holds the order its bindposes were baked against.
		// Matched on the mesh asset itself rather than on a name: two rigs here ship meshes called "body".
		private static SkinnedMeshRenderer FindSourceRenderer(Mesh mesh)
		{
			var modelPath = AssetDatabase.GetAssetPath(mesh);
			if (string.IsNullOrEmpty(modelPath)) return null;

			var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
			if (!model) return null;

			foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
			{
				if (renderer.sharedMesh == mesh) return renderer;
			}

			return null;
		}
	}
}
