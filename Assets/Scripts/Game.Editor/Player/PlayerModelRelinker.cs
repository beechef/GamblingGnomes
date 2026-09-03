using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Runtime.Player;
using UnityEditor;
using UnityEditorInternal;
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

			// Where the model now keeps a mesh node the prefab carries somewhere else. A pair is written as
			// the path the node has *in the prefab today* against the path the model gives it, so a chain of
			// them reads top down without any entry describing an intermediate state; the longest matching
			// From wins, which is what lets a node and its children each name their own destination.
			public readonly (string From, string To)[] MovedNodes;

			public ModelLink(string nodeName, string modelPath, params (string From, string To)[] movedNodes)
			{
				NodeName = nodeName;
				ModelPath = modelPath;
				MovedNodes = movedNodes;
			}
		}

		// A mesh node left unmatched is not dropped — it is kept as an override and the model's own copy is
		// added beside it, so the rig renders both. That reads as a broken skin rather than as two meshes,
		// which is why the hand-only pair is mapped here rather than left to name matching.
		private static readonly ModelLink[] Links =
		{
			new("Gnome_rig", "Assets/Art/Character/Gnome/Model_rig/Gnome_Model_Rig.fbx"),
			new("Gnome_Outfit_1", "Assets/Art/Character/Gnome/Model_rig/Gnome_Outfit_Rig.fbx"),
			new("Gnome_HandOnly", "Assets/Art/Character/Gnome/Model_rig/Gnome_HandOnly.fbx",
				("Body", "Hand"),
				("Body/FullBody", "Hand/Hand_only"),
				("Body/FullBody/body", "Hand/Hand_only/Hand")),
			new("Gnome_Outfit_OnlyHand_1", "Assets/Art/Character/Gnome/Model_rig/Gnome_HandOutFit.fbx",
				("OutFit/polySurface27", "HandOutFit"))
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
			var beforeOwners = OwnerPaths(AssetDatabase.LoadAssetAtPath<GameObject>(path));
			var variantPaths = VariantPaths(path).ToList();
			var variantsBefore = variantPaths.ToDictionary(p => p, p =>
			{
				var asset = AssetDatabase.LoadAssetAtPath<GameObject>(p);
				return (Nulls: NullReferences(asset), Owners: OwnerPaths(asset));
			});
			var variantSites = variantPaths.ToDictionary(p => p, p => CaptureReferencesInto(AssetDatabase.LoadAssetAtPath<GameObject>(p), "Models"));
			var root = PrefabUtility.LoadPrefabContents(path);
			Dictionary<string, string> movedPaths;

			try
			{
				if (!Rebuild(root, apply, report, out movedPaths))
				{
					Debug.LogError($"Relinking {path} stopped before writing anything.\n{report}");
					return;
				}

				ReportNewNulls(path, before, NullReferences(root), movedPaths, beforeOwners, report);

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

				report.Insert(0, $"Relinked the models in {path}.\n");
			}
			finally
			{
				PrefabUtility.UnloadPrefabContents(root);
			}

			RestoreVariantReferences(variantSites, movedPaths, report);
			Debug.Log(report.ToString());
			VerifyVariants(variantsBefore, movedPaths);
		}

		// Everything that happens to the loaded copy, so a caller can run it and inspect the result without a
		// save standing in the way. Answers false when it refused to go on. `movedPaths` maps every path under
		// Models as it was before the pass to the path it has afterwards, which is what lets a reference held
		// somewhere else — in a variant — be found again after a bone was renamed or a mesh node re-homed.
		public static bool Rebuild(GameObject root, bool apply, StringBuilder report, out Dictionary<string, string> movedPaths)
		{
			movedPaths = new Dictionary<string, string>();

			var models = root.transform.Find("Models");

			if (!models)
			{
				report.AppendLine("no Models node — nothing to relink");
				return false;
			}

			// Captured before anything moves, because these are exactly the references the conversion drops:
			// a camera parked on the head bone, the bone rig, the glasses. Restoring them by path afterwards is
			// what makes "the GameObjects survive" mean the wiring survives too.
			var references = apply ? CaptureReferencesInto(root, "Models") : new List<ReferenceSite>();
			var startPaths = models.GetComponentsInChildren<Transform>(true)
				.ToDictionary(t => t, t => AnimationUtility.CalculateTransformPath(t, root.transform));
			var matched = new List<ModelLink>();

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
				MatchHierarchyToModel(node, model, link, apply, report);
				matched.Add(link);
			}

			if (!apply) return true;

			foreach (var pair in startPaths)
			{
				if (!pair.Key) continue;

				movedPaths[pair.Value] = AnimationUtility.CalculateTransformPath(pair.Key, root.transform);
			}

			var contentsBefore = Contents(models, root.transform);

			// Every hierarchy is matched before any of them is converted, so the paths captured above describe
			// one settled shape rather than a half-converted one.
			foreach (var link in matched)
			{
				var node = models.Find(link.NodeName);
				var model = AssetDatabase.LoadAssetAtPath<GameObject>(link.ModelPath);
				var parked = ParkAddedObjects(node, model, models, report);
				var lifted = LiftAddedComponents(node, model, models, report);

				PrefabUtility.ConvertToPrefabInstance(node.gameObject, model, ConversionSettings(), InteractionMode.AutomatedAction);

				var converted = models.Find(link.NodeName);
				Unpark(parked, converted, report);
				DropAddedComponents(lifted, converted, report);
				RebindSkins(converted, report);
			}

			if (!ReportDroppedContents(contentsBefore, Contents(models, root.transform), report)) return false;

			RestoreReferences(root, references, movedPaths, report);
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
		private static void MatchHierarchyToModel(Transform node, GameObject model, ModelLink link, bool apply, StringBuilder report)
		{
			MoveNodes(node, link, apply, report);

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
				.Where(t => !modelPaths.Contains(PathUnderRename(t, node, link, apply)))
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
				.Select(t => PathUnderRename(t, node, link, apply)));

			foreach (var added in modelPaths.Where(p => !string.IsNullOrEmpty(p) && !present.Contains(p)).OrderBy(p => p))
			{
				report.AppendLine($"   the model adds: {added}");
			}
		}

		// Puts the mesh nodes where the model keeps them, before anything is matched by name. Sources are
		// looked up through a snapshot of the paths the nodes started at, so an entry always names where the
		// node is in the prefab rather than where an earlier entry left it, while the destination's parent is
		// resolved live — which is what lets a parent be renamed and its child re-homed under the new name in
		// the same list.
		private static void MoveNodes(Transform node, ModelLink link, bool apply, StringBuilder report)
		{
			if (link.MovedNodes == null || link.MovedNodes.Length == 0) return;

			var byStartPath = node.GetComponentsInChildren<Transform>(true)
				.ToDictionary(t => AnimationUtility.CalculateTransformPath(t, node));

			foreach (var move in link.MovedNodes)
			{
				if (!byStartPath.TryGetValue(move.From, out var source) || !source)
				{
					report.AppendLine($"   move {move.From} -> {move.To}: nothing at {move.From}, skipped");
					continue;
				}

				report.AppendLine($"   move mesh node {move.From} -> {move.To}");

				if (!apply) continue;

				var cut = move.To.LastIndexOf('/');
				var parent = cut < 0 ? node : node.Find(move.To[..cut]);

				if (!parent)
				{
					report.AppendLine($"   nowhere to put {move.To} — the list is out of order");
					continue;
				}

				source.SetParent(parent, false);
				source.name = cut < 0 ? move.To : move.To[(cut + 1)..];
			}
		}

		// On a dry run the moves and renames have not been written, so a path still reads with the old names
		// and would compare as a leftover against every bone below the neck. Applying the map to the string is
		// what makes the report say the same thing the real pass will do. The longest matching move wins, so a
		// node and its own children each land on the destination that was written for them.
		private static string PathUnderRename(Transform t, Transform root, ModelLink link, bool renamesApplied)
		{
			var path = AnimationUtility.CalculateTransformPath(t, root);

			if (renamesApplied) return path;

			var moved = link.MovedNodes?
				.Where(m => path == m.From || path.StartsWith(m.From + "/"))
				.OrderByDescending(m => m.From.Length)
				.Select(m => m.To + path[m.From.Length..])
				.FirstOrDefault();

			if (moved != null) path = moved;

			foreach (var renamed in RenamedBones) path = path.Replace(renamed.From, renamed.To);

			return path;
		}

		private static bool CarriesAnything(Transform t) =>
			t.GetComponentsInChildren<Component>(true).Any(c => c && c is not Transform);

		// `componentsNotMatchedBecomesOverride` and `gameObjectsNotMatchedBecomesOverride` do not hold: measured
		// on Player.prefab, ConvertToPrefabInstance drops every added object and component inside the hierarchy
		// it converts — the camera on the head bone, both Glasses nodes, AimTarget, the PlayerBoneRig on each
		// rig root, the hand-only Animator — and drops them *in memory*, before any save, in a prefab stage as
		// well as in LoadPrefabContents. Nothing is logged. So the shape is compared either side of the
		// conversion and the pass refuses rather than writing a prefab with its attach points quietly gone.
		private static HashSet<string> Contents(Transform scope, Transform root)
		{
			var contents = new HashSet<string>();

			foreach (var component in scope.GetComponentsInChildren<Component>(true))
			{
				if (!component) continue;

				contents.Add($"{AnimationUtility.CalculateTransformPath(component.transform, root)} [{component.GetType().Name}]");
			}

			return contents;
		}

		private static bool ReportDroppedContents(HashSet<string> before, HashSet<string> after, StringBuilder report)
		{
			var dropped = before.Where(entry => !after.Contains(entry)).OrderBy(entry => entry).ToList();

			if (dropped.Count == 0) return true;

			report.AppendLine($"the conversion dropped {dropped.Count} object(s) or component(s) — nothing was written:");

			foreach (var entry in dropped) report.AppendLine($"   {entry}");

			return false;
		}

		// Everything the prefab hung on the model that the model does not have of its own — the camera on the
		// head bone, both pairs of glasses, the aim collider, the hat. The conversion destroys all of it, so it
		// is lifted clear beforehand and hung back on the same bone after, which keeps the objects themselves
		// and every reference into them rather than building lookalikes.
		private readonly struct ParkedObject
		{
			public readonly Transform Object;
			public readonly string ParentPath;
			public readonly int SiblingIndex;

			public ParkedObject(Transform target, string parentPath, int siblingIndex)
			{
				Object = target;
				ParentPath = parentPath;
				SiblingIndex = siblingIndex;
			}
		}

		private static List<ParkedObject> ParkAddedObjects(Transform node, GameObject model, Transform holder, StringBuilder report)
		{
			var parked = new List<ParkedObject>();
			var modelPaths = new HashSet<string>(model.GetComponentsInChildren<Transform>(true)
				.Select(t => AnimationUtility.CalculateTransformPath(t, model.transform)));

			// Outermost first, and anything inside something already parked travels with it.
			foreach (var candidate in node.GetComponentsInChildren<Transform>(true))
			{
				if (candidate == node || modelPaths.Contains(AnimationUtility.CalculateTransformPath(candidate, node))) continue;
				if (parked.Any(p => candidate.IsChildOf(p.Object))) continue;

				parked.Add(new ParkedObject(candidate, AnimationUtility.CalculateTransformPath(candidate.parent, node), candidate.GetSiblingIndex()));
			}

			foreach (var entry in parked)
			{
				report.AppendLine($"   lift {entry.Object.name} off {entry.ParentPath} for the conversion");
				entry.Object.SetParent(holder, false);
			}

			return parked;
		}

		private static void Unpark(List<ParkedObject> parked, Transform node, StringBuilder report)
		{
			foreach (var entry in parked)
			{
				var parent = string.IsNullOrEmpty(entry.ParentPath) ? node : node.Find(entry.ParentPath);

				if (!parent)
				{
					report.AppendLine($"   {entry.Object.name} has nowhere to go back to — {entry.ParentPath} is gone");
					continue;
				}

				entry.Object.SetParent(parent, false);
				entry.Object.SetSiblingIndex(entry.SiblingIndex);
			}
		}

		// A component added to a bone the model *does* have cannot be parked — the bone is what the conversion
		// re-uses, and a component has no life apart from the GameObject carrying it. So it is copied onto a
		// throwaway holder of its own and pasted back afterwards. That does hand out a new component, which is
		// the one thing parking avoids, so it is kept to the few that need it: the bone rig on each rig root,
		// the scene-view BoneRenderer, and the Animator on the rig the model ships without one. Whatever
		// pointed at the old component is re-found by path and type in RestoreReferences.
		private readonly struct LiftedComponent
		{
			public readonly string OwnerPath;
			public readonly GameObject Holder;
			public readonly string TypeName;

			public LiftedComponent(string ownerPath, GameObject holder, string typeName)
			{
				OwnerPath = ownerPath;
				Holder = holder;
				TypeName = typeName;
			}
		}

		private static List<LiftedComponent> LiftAddedComponents(Transform node, GameObject model, Transform holderParent, StringBuilder report)
		{
			var lifted = new List<LiftedComponent>();

			foreach (var owner in node.GetComponentsInChildren<Transform>(true))
			{
				var ownerPath = AnimationUtility.CalculateTransformPath(owner, node);
				var counterpart = string.IsNullOrEmpty(ownerPath) ? model.transform : model.transform.Find(ownerPath);

				// No counterpart means the whole object is parked, and its components go with it.
				if (!counterpart) continue;

				foreach (var group in owner.GetComponents<Component>().Where(c => c && c is not Transform).GroupBy(c => c.GetType()))
				{
					var kept = counterpart.GetComponents(group.Key).Length;

					foreach (var component in group.Skip(kept))
					{
						var holder = new GameObject($"{owner.name} ({component.GetType().Name})");
						holder.transform.SetParent(holderParent, false);

						if (!ComponentUtility.CopyComponent(component) || !ComponentUtility.PasteComponentAsNew(holder))
						{
							report.AppendLine($"   could not lift {component.GetType().Name} off {ownerPath}");
							Object.DestroyImmediate(holder);
							continue;
						}

						report.AppendLine($"   lift the {component.GetType().Name} on {(ownerPath.Length == 0 ? node.name : ownerPath)} for the conversion");
						lifted.Add(new LiftedComponent(ownerPath, holder, component.GetType().Name));
					}
				}
			}

			return lifted;
		}

		private static void DropAddedComponents(List<LiftedComponent> lifted, Transform node, StringBuilder report)
		{
			foreach (var entry in lifted)
			{
				var owner = string.IsNullOrEmpty(entry.OwnerPath) ? node : node.Find(entry.OwnerPath);
				var source = entry.Holder.GetComponents<Component>().FirstOrDefault(c => c && c is not Transform);

				if (!owner || !source || !ComponentUtility.CopyComponent(source) || !ComponentUtility.PasteComponentAsNew(owner.gameObject))
					report.AppendLine($"   could not put the {entry.TypeName} back on {entry.OwnerPath}");

				Object.DestroyImmediate(entry.Holder);
			}
		}

		// A reference is addressed by where it is written and what it points at, both as a path plus the type
		// and the position among same-typed components on that transform — never as an object, because the
		// whole point is to survive a pass that hands the objects new identities.
		private readonly struct ReferenceSite
		{
			public readonly string OwnerPath;
			public readonly string OwnerType;
			public readonly int OwnerIndex;
			public readonly string PropertyPath;
			public readonly string TargetPath;
			public readonly string TargetType;
			public readonly int TargetIndex;

			public ReferenceSite(string ownerPath, string ownerType, int ownerIndex, string propertyPath, string targetPath, string targetType, int targetIndex)
			{
				OwnerPath = ownerPath;
				OwnerType = ownerType;
				OwnerIndex = ownerIndex;
				PropertyPath = propertyPath;
				TargetPath = targetPath;
				TargetType = targetType;
				TargetIndex = targetIndex;
			}
		}

		// Converting a hierarchy into a prefab instance drops every reference held from outside it — the
		// camera on the head bone, the bone rig, the glasses, a variant's finger pieces — and drops them
		// silently, as a fileID of zero that only reads as a rig that does nothing. So they are written down
		// first and put back afterwards.
		private static List<ReferenceSite> CaptureReferencesInto(GameObject root, string scopeName)
		{
			var sites = new List<ReferenceSite>();

			if (!root) return sites;

			var scope = root.transform.Find(scopeName);

			if (!scope) return sites;

			// A Transform's own parent and children are the hierarchy itself, which the conversion rebuilds on
			// purpose — putting the old ones back would be undoing the work.
			foreach (var owner in root.GetComponentsInChildren<Component>(true))
			{
				if (!owner || owner is Transform) continue;

				var iterator = new SerializedObject(owner).GetIterator();

				while (iterator.NextVisible(true))
				{
					if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;

					var target = TargetTransform(iterator.objectReferenceValue);

					if (!target || !target.IsChildOf(scope)) continue;

					sites.Add(new ReferenceSite(
						AnimationUtility.CalculateTransformPath(owner.transform, root.transform),
						owner.GetType().Name,
						IndexAmongSameType(owner),
						iterator.propertyPath,
						AnimationUtility.CalculateTransformPath(target, root.transform),
						iterator.objectReferenceValue is GameObject ? null : iterator.objectReferenceValue.GetType().Name,
						iterator.objectReferenceValue is Component component ? IndexAmongSameType(component) : 0));
				}
			}

			return sites;
		}

		private static void RestoreReferences(GameObject root, List<ReferenceSite> sites, Dictionary<string, string> movedPaths, StringBuilder report)
		{
			var restored = 0;

			foreach (var site in sites)
			{
				var owner = FindComponent(root, site.OwnerPath, site.OwnerType, site.OwnerIndex);

				if (!owner) continue;

				var serialized = new SerializedObject(owner);
				var property = serialized.FindProperty(site.PropertyPath);

				// Only what the pass actually dropped: a reference somebody deliberately re-pointed since must
				// not be quietly overwritten with what it used to be.
				if (property == null || property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue) continue;

				var targetPath = movedPaths != null && movedPaths.TryGetValue(site.TargetPath, out var moved) ? moved : site.TargetPath;
				var target = root.transform.Find(targetPath);

				if (!target)
				{
					report.AppendLine($"   {site.OwnerPath} [{site.OwnerType}].{site.PropertyPath} pointed at {targetPath}, which is gone");
					continue;
				}

				var value = site.TargetType == null
					? target.gameObject
					: (Object)FindComponent(root, targetPath, site.TargetType, site.TargetIndex);

				if (!value)
				{
					report.AppendLine($"   {site.OwnerPath} [{site.OwnerType}].{site.PropertyPath} pointed at a {site.TargetType} on {targetPath}, which is gone");
					continue;
				}

				property.objectReferenceValue = value;
				serialized.ApplyModifiedPropertiesWithoutUndo();
				restored++;
			}

			if (restored > 0) report.AppendLine($"   put back {restored} reference(s) the conversion dropped");
		}

		// A variant addresses the base's objects from its own file, so the base being repaired only fixes what
		// the variant inherits — anything the variant wires itself, like the finger pieces, has to be put back
		// in the variant. Run after the base is saved, so an inherited property is already right and is left
		// alone rather than being turned into an override that says the same thing.
		private static void RestoreVariantReferences(Dictionary<string, List<ReferenceSite>> sitesByPath, Dictionary<string, string> movedPaths, StringBuilder report)
		{
			foreach (var (path, sites) in sitesByPath)
			{
				var root = PrefabUtility.LoadPrefabContents(path);

				try
				{
					var before = report.Length;
					RestoreReferences(root, sites, movedPaths, report);

					if (report.Length == before) continue;

					report.Insert(before, $"== {path}\n");
					PrefabUtility.SaveAsPrefabAsset(root, path, out var saved);

					if (!saved) report.AppendLine($"   {path} wrote nothing");
				}
				finally
				{
					PrefabUtility.UnloadPrefabContents(root);
				}
			}
		}

		private static Transform TargetTransform(Object value) => value switch
		{
			GameObject go => go.transform,
			Component component => component.transform,
			_ => null
		};

		private static int IndexAmongSameType(Component component) =>
			System.Array.IndexOf(component.GetComponents(component.GetType()), component);

		private static Component FindComponent(GameObject root, string path, string typeName, int index)
		{
			var transform = string.IsNullOrEmpty(path) ? root.transform : root.transform.Find(path);

			if (!transform) return null;

			var candidates = transform.GetComponents<Component>().Where(c => c && c.GetType().Name == typeName).ToArray();
			return index >= 0 && index < candidates.Length ? candidates[index] : candidates.FirstOrDefault();
		}

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
		private static void VerifyVariants(Dictionary<string, (HashSet<string> Nulls, HashSet<string> Owners)> before, Dictionary<string, string> movedPaths)
		{
			foreach (var (path, was) in before)
			{
				var report = new StringBuilder();
				ReportNewNulls(path, was.Nulls, NullReferences(AssetDatabase.LoadAssetAtPath<GameObject>(path)), movedPaths, was.Owners, report);

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

		// An object the model brought with it — a new finger piece, a beard joint — arrives with the empty
		// fields every renderer has, and a bone the artist renamed arrives under a path nothing was recorded
		// against. Neither is a broken reference, and reporting them buries the handful that are: the first
		// run of this reported 70 where the real answer was none. So a null is only news when the thing
		// carrying it was already there, addressed by the path it had before the pass.
		private static void ReportNewNulls(string path, HashSet<string> before, HashSet<string> after, Dictionary<string, string> movedPaths, HashSet<string> knownOwners, StringBuilder report)
		{
			var backFromMove = movedPaths?
				.Where(pair => pair.Key != pair.Value)
				.GroupBy(pair => pair.Value)
				.ToDictionary(group => group.Key, group => group.First().Key);

			var broken = after
				.Select(entry => AsItWasBefore(entry, backFromMove))
				.Where(entry => !before.Contains(entry))
				.Where(entry => knownOwners == null || knownOwners.Contains(OwnerOf(entry)))
				.OrderBy(entry => entry)
				.ToList();

			if (broken.Count == 0) return;

			report.AppendLine($"{path}: {broken.Count} reference(s) the relink broke:");

			foreach (var reference in broken) report.AppendLine($"   {reference}");
		}

		private static string AsItWasBefore(string entry, Dictionary<string, string> backFromMove)
		{
			if (backFromMove == null) return entry;

			var owner = OwnerOf(entry);
			var moved = backFromMove
				.Where(pair => owner == pair.Key || owner.StartsWith(pair.Key + "/"))
				.OrderByDescending(pair => pair.Key.Length)
				.Select(pair => pair.Value + owner[pair.Key.Length..])
				.FirstOrDefault();

			return moved == null ? entry : moved + entry[owner.Length..];
		}

		private static string OwnerOf(string entry)
		{
			var cut = entry.LastIndexOf(" [", System.StringComparison.Ordinal);
			return cut < 0 ? entry : entry[..cut];
		}

		private static HashSet<string> OwnerPaths(GameObject asset)
		{
			var paths = new HashSet<string>();

			if (!asset) return paths;

			foreach (var t in asset.GetComponentsInChildren<Transform>(true))
			{
				paths.Add(AnimationUtility.CalculateTransformPath(t, asset.transform));
			}

			return paths;
		}
	}
}
