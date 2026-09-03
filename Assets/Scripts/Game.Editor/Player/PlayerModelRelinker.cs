using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Runtime.Player;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Player
{
	// The four gnome models under Player.prefab's Models node were plain GameObject hierarchies, not
	// instances of the FBX they were built from, so an updated model reached nothing. Re-linking them is a
	// conversion rather than a rebuild on purpose: ConvertToPrefabInstance re-uses the GameObjects that are
	// already there, so every serialized reference into a bone — the camera's parent, the look transforms,
	// the finger pieces on the Poker variant — survives, where deleting and re-instantiating would null all
	// of them. What it needs first is a hierarchy the model can be matched against by name: bones the model
	// has since renamed are renamed here, and bones it no longer has are deleted, or they would come back as
	// added objects and the rig would carry two of every neck joint.
	public static class PlayerModelRelinker
	{
		private readonly struct ModelLink
		{
			public readonly string NodeName;
			public readonly string ModelPath;

			public ModelLink(string nodeName, string modelPath)
			{
				NodeName = nodeName;
				ModelPath = modelPath;
			}
		}

		private static readonly ModelLink[] Links =
		{
			new("Gnome_rig", "Assets/Art/Character/Gnome/Model_rig/Gnome_Model_Rig.fbx"),
			new("Gnome_Outfit_1", "Assets/Art/Character/Gnome/Model_rig/Gnome_Outfit_Rig.fbx"),
			new("Gnome_HandOnly", "Assets/Art/Character/Gnome/Model_rig/Gnome_HandOnly.fbx"),
			new("Gnome_Outfit_OnlyHand_1", "Assets/Art/Character/Gnome/Model_rig/Gnome_HandOutFit.fbx")
		};

		// Bones the artist renamed in the model. Applied only where the prefab still carries the old name and
		// the model carries the new one, so a link whose model was never renamed is left alone rather than
		// having a rename forced onto it — the hand-only pair still ships Neck7_M/Head_M.
		private static readonly (string From, string To)[] RenamedBones =
		{
			("Neck7_M", "Neck_M"),
			("Head_M", "Head1_M")
		};

		[MenuItem("Assets/Player/Relink Models (Report)", true)]
		private static bool ValidateReport() => TryGetSelectedPrefabPath(out _);

		[MenuItem("Assets/Player/Relink Models (Report)")]
		private static void ReportSelected()
		{
			if (TryGetSelectedPrefabPath(out var path)) Run(path, apply: false);
		}

		[MenuItem("Assets/Player/Relink Models", true)]
		private static bool ValidateRelink() => TryGetSelectedPrefabPath(out _);

		[MenuItem("Assets/Player/Relink Models")]
		private static void RelinkSelected()
		{
			if (TryGetSelectedPrefabPath(out var path)) Run(path, apply: true);
		}

		private static bool TryGetSelectedPrefabPath(out string path)
		{
			path = null;

			if (Selection.activeObject is not GameObject prefab || !PrefabUtility.IsPartOfPrefabAsset(prefab)) return false;

			path = AssetDatabase.GetAssetPath(prefab);
			return !string.IsNullOrEmpty(path);
		}

		// Split from the menu entries so the whole pass can be run against a path, and so the dry run and the
		// real one are the same code with one flag rather than two routines that drift apart.
		public static void Run(string path, bool apply)
		{
			var report = new StringBuilder();
			var before = NullReferences(AssetDatabase.LoadAssetAtPath<GameObject>(path));
			var variantsBefore = VariantPaths(path).ToDictionary(p => p, p => NullReferences(AssetDatabase.LoadAssetAtPath<GameObject>(p)));
			var root = PrefabUtility.LoadPrefabContents(path);

			try
			{
				if (!Rebuild(root, apply, report))
				{
					Debug.LogError($"Relinking {path} stopped before writing anything.\n{report}");
					return;
				}

				ReportNewNulls(path, before, NullReferences(root), report);

				if (!apply)
				{
					Debug.Log($"Relink report for {path} — nothing was written.\n{report}");
					return;
				}

				PrefabUtility.SaveAsPrefabAsset(root, path, out var saved);

				if (!saved)
				{
					Debug.LogError($"Relinking {path} wrote nothing. The asset database is read only — run this from the main editor, not an MPPM virtual player.\n{report}");
					return;
				}

				Debug.Log($"Relinked the models in {path}.\n{report}");
			}
			finally
			{
				PrefabUtility.UnloadPrefabContents(root);
			}

			VerifyVariants(variantsBefore);
		}

		// Everything that happens to the loaded copy, so a caller can run it and inspect the result without a
		// save standing in the way. Answers false when it refused to go on.
		public static bool Rebuild(GameObject root, bool apply, StringBuilder report)
		{
			var models = root.transform.Find("Models");

			if (!models)
			{
				report.AppendLine("no Models node — nothing to relink");
				return false;
			}

			foreach (var link in Links)
			{
				var node = models.Find(link.NodeName);

				if (!node)
				{
					report.AppendLine($"{link.NodeName}: not found under Models, skipped");
					continue;
				}

				var model = AssetDatabase.LoadAssetAtPath<GameObject>(link.ModelPath);

				if (!model)
				{
					report.AppendLine($"{link.ModelPath} did not load");
					return false;
				}

				if (PrefabUtility.IsAnyPrefabInstanceRoot(node.gameObject))
				{
					report.AppendLine($"{link.NodeName}: already a prefab instance, skipped");
					continue;
				}

				report.AppendLine($"== {link.NodeName}  <-  {link.ModelPath}");
				MatchHierarchyToModel(node, model, apply, report);

				if (!apply) continue;

				PrefabUtility.ConvertToPrefabInstance(node.gameObject, model, ConversionSettings(), InteractionMode.AutomatedAction);
				RebindSkins(models.Find(link.NodeName), report);
			}

			if (!apply) return true;

			RepairBoneRigs(models, report);
			RepairBoneRenderers(models, report);
			AdoptUnlistedRenderers(root, models, report);
			return true;
		}

		private static ConvertToPrefabInstanceSettings ConversionSettings() => new()
		{
			objectMatchMode = ObjectMatchMode.ByName,
			componentsNotMatchedBecomesOverride = true,
			gameObjectsNotMatchedBecomesOverride = true,
			recordPropertyOverridesOfMatches = true,
			changeRootNameToAssetName = false
		};

		// Renames what the model renamed and deletes the bones it dropped, so name matching lands on one
		// skeleton instead of merging two. A leftover carrying anything of its own is left alone and reported
		// — that is a prop hung on a bone, not a bone.
		private static void MatchHierarchyToModel(Transform node, GameObject model, bool apply, StringBuilder report)
		{
			var modelBones = model.GetComponentsInChildren<Transform>(true);
			var modelNames = new HashSet<string>(modelBones.Select(t => t.name));
			var nodeNames = new HashSet<string>(node.GetComponentsInChildren<Transform>(true).Select(t => t.name));

			foreach (var renamed in RenamedBones)
			{
				if (!nodeNames.Contains(renamed.From) || nodeNames.Contains(renamed.To)) continue;
				if (!modelNames.Contains(renamed.To) || modelNames.Contains(renamed.From)) continue;

				report.AppendLine($"   rename {renamed.From} -> {renamed.To}");

				if (apply) node.GetComponentsInChildren<Transform>(true).First(t => t.name == renamed.From).name = renamed.To;
			}

			var modelPaths = new HashSet<string>(modelBones.Select(t => AnimationUtility.CalculateTransformPath(t, model.transform)));

			// Deepest first, so destroying a parent cannot orphan a child still queued behind it.
			var leftovers = node.GetComponentsInChildren<Transform>(true)
				.Where(t => t != node)
				.Where(t => !modelPaths.Contains(PathUnderRename(t, node, apply)))
				.OrderByDescending(t => AnimationUtility.CalculateTransformPath(t, node).Count(c => c == '/'))
				.ToList();

			foreach (var leftover in leftovers)
			{
				if (!leftover) continue;

				var leftoverPath = AnimationUtility.CalculateTransformPath(leftover, node);

				if (CarriesAnything(leftover))
				{
					report.AppendLine($"   keep as override: {leftoverPath}");
					continue;
				}

				report.AppendLine($"   delete bone the model dropped: {leftoverPath}");

				if (apply) Object.DestroyImmediate(leftover.gameObject);
			}

			var present = new HashSet<string>(node.GetComponentsInChildren<Transform>(true)
				.Select(t => PathUnderRename(t, node, apply)));

			foreach (var added in modelPaths.Where(p => !string.IsNullOrEmpty(p) && !present.Contains(p)).OrderBy(p => p))
			{
				report.AppendLine($"   the model adds: {added}");
			}
		}

		// On a dry run the renames have not been written, so a path still reads with the old bone names and
		// would compare as a leftover against every bone below the neck. Applying the map to the string is
		// what makes the report say the same thing the real pass will do.
		private static string PathUnderRename(Transform t, Transform root, bool renamesApplied)
		{
			var path = AnimationUtility.CalculateTransformPath(t, root);

			if (renamesApplied) return path;

			foreach (var renamed in RenamedBones) path = path.Replace(renamed.From, renamed.To);

			return path;
		}

		private static bool CarriesAnything(Transform t) =>
			t.GetComponentsInChildren<Component>(true).Any(c => c && c is not Transform);

		// A skin deforms from its bone array in index order, and the conversion records the pre-existing array
		// as an override — an order that belonged to the old skeleton. Handing these three back to the model
		// is what makes the mesh bind to the rig it was actually skinned to.
		private static void RebindSkins(Transform node, StringBuilder report)
		{
			foreach (var renderer in node.GetComponentsInChildren<SkinnedMeshRenderer>(true))
			{
				var serialized = new SerializedObject(renderer);

				foreach (var name in new[] { "m_Mesh", "m_Bones", "m_RootBone" })
				{
					var property = serialized.FindProperty(name);
					if (property == null || !property.prefabOverride) continue;

					PrefabUtility.RevertPropertyOverride(property, InteractionMode.AutomatedAction);
					report.AppendLine($"   {renderer.name}: reverted {name} to the model");
				}
			}
		}

		private static void RepairBoneRigs(Transform models, StringBuilder report)
		{
			foreach (var rig in models.GetComponentsInChildren<PlayerBoneRig>(true))
			{
				var present = new HashSet<string>(rig.Root.GetComponentsInChildren<Transform>(true).Select(t => t.name));
				var serialized = new SerializedObject(rig);
				var bones = serialized.FindProperty("_bones");
				var rewritten = 0;

				for (var i = 0; i < bones.arraySize; i++)
				{
					var entry = bones.GetArrayElementAtIndex(i);
					if (entry.FindPropertyRelative("Transform").objectReferenceValue) continue;

					var boneName = entry.FindPropertyRelative("BoneName");
					if (string.IsNullOrEmpty(boneName.stringValue) || present.Contains(boneName.stringValue)) continue;

					var renamed = RenamedBones.FirstOrDefault(r => r.From == boneName.stringValue);

					if (renamed.To != null && present.Contains(renamed.To))
					{
						report.AppendLine($"   {rig.name}: bone '{boneName.stringValue}' -> '{renamed.To}'");
						boneName.stringValue = renamed.To;
						rewritten++;
						continue;
					}

					report.AppendLine($"   {rig.name}: bone '{boneName.stringValue}' resolves to nothing on this rig");
				}

				if (rewritten > 0) serialized.ApplyModifiedPropertiesWithoutUndo();
			}
		}

		// Scene-view gizmo tooling from the rigging package. Its list holds the bones that were deleted, and a
		// null there draws nothing useful; refilled by name rather than referenced, so this needs no assembly
		// reference on a package that is only ever used in the scene view.
		private static void RepairBoneRenderers(Transform models, StringBuilder report)
		{
			foreach (var component in models.GetComponentsInChildren<Component>(true))
			{
				if (!component || component.GetType().Name != "BoneRenderer") continue;

				var serialized = new SerializedObject(component);
				var list = serialized.FindProperty("m_Transforms");
				if (list == null || !list.isArray) continue;

				var bones = component.GetComponentsInChildren<Transform>(true);
				list.arraySize = bones.Length;

				for (var i = 0; i < bones.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = bones[i];

				serialized.ApplyModifiedPropertiesWithoutUndo();
				report.AppendLine($"   BoneRenderer on {component.name}: refilled with {bones.Length} bones");
			}
		}

		// A renderer the model has gained that PlayerVisual does not hold is one the owner/remote rig split
		// cannot switch off — it reads as a piece of body floating in front of the first person camera.
		// Whether a new piece is also a *finger* is a rule about blood and stays with whoever authored the
		// finger list, so it is named here rather than adopted there.
		private static void AdoptUnlistedRenderers(GameObject root, Transform models, StringBuilder report)
		{
			var visual = root.GetComponent<PlayerVisual>();

			if (!visual)
			{
				report.AppendLine("   no PlayerVisual on the root — new renderers were not adopted");
				return;
			}

			var serialized = new SerializedObject(visual);

			AdoptInto(serialized.FindProperty("_bodyMeshRenderers"), models.Find("Gnome_rig"), report);
			AdoptInto(serialized.FindProperty("_handOnlyBodyMeshRenderers"), models.Find("Gnome_HandOnly"), report);

			serialized.ApplyModifiedPropertiesWithoutUndo();
		}

		private static void AdoptInto(SerializedProperty list, Transform rig, StringBuilder report)
		{
			if (list == null || !rig) return;

			var held = new HashSet<Object>();

			for (var i = 0; i < list.arraySize; i++) held.Add(list.GetArrayElementAtIndex(i).objectReferenceValue);

			// Only the skin: props hung on bones — the hat, the glasses — are drawn by their own controllers.
			foreach (var renderer in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true))
			{
				if (held.Contains(renderer)) continue;

				list.arraySize++;
				list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = renderer;
				report.AppendLine($"   PlayerVisual: adopted {renderer.name} on {rig.name} — decide separately whether it is a finger");
			}
		}

		// The conversion re-uses the objects it matched, but a variant addresses them from its own file — so
		// the only honest check is to load every variant afterwards and look for a reference that went null.
		private static void VerifyVariants(Dictionary<string, HashSet<string>> before)
		{
			foreach (var (path, wasNull) in before)
			{
				var report = new StringBuilder();
				ReportNewNulls(path, wasNull, NullReferences(AssetDatabase.LoadAssetAtPath<GameObject>(path)), report);

				if (report.Length > 0) Debug.LogError(report.ToString());
				else Debug.Log($"{path}: every reference survived the relink.");
			}
		}

		private static IEnumerable<string> VariantPaths(string basePath)
		{
			var baseAsset = AssetDatabase.LoadAssetAtPath<GameObject>(basePath);

			foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);
				var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);

				if (asset && PrefabUtility.GetCorrespondingObjectFromSource(asset) == baseAsset) yield return path;
			}
		}

		// Which references are null is only half the answer — plenty of them are null by design, the way
		// PlayerBoneRig leaves its bone transforms empty and resolves by name. What matters is which ones
		// went null *because of this*, so the set is taken before and after and only the difference is
		// reported. It also catches a component the conversion dropped, since its whole set disappears.
		private static HashSet<string> NullReferences(GameObject asset)
		{
			var nulls = new HashSet<string>();

			if (!asset) return nulls;

			foreach (var component in asset.GetComponentsInChildren<Component>(true))
			{
				if (!component)
				{
					nulls.Add("a component is missing its script");
					continue;
				}

				var path = AnimationUtility.CalculateTransformPath(component.transform, asset.transform);
				var iterator = new SerializedObject(component).GetIterator();

				while (iterator.NextVisible(true))
				{
					if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;
					if (iterator.objectReferenceValue) continue;

					nulls.Add($"{path} [{component.GetType().Name}].{iterator.propertyPath}");
				}
			}

			return nulls;
		}

		private static void ReportNewNulls(string path, HashSet<string> before, HashSet<string> after, StringBuilder report)
		{
			var broken = after.Where(n => !before.Contains(n)).OrderBy(n => n).ToList();

			if (broken.Count == 0) return;

			report.AppendLine($"{path}: {broken.Count} reference(s) the relink broke:");

			foreach (var reference in broken) report.AppendLine($"   {reference}");
		}
	}
}
