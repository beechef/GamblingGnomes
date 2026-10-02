using System;
using System.Collections.Generic;
using System.Text;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Runtime.Player
{
	// One body a player can be drawn as, and everything that decides what goes in each slot of it: the
	// meshes the body is cut into, its outfits, and a look per version for both. A new character is a new
	// asset — nothing in the prefab changes, because every model is skinned to bones of the same skeleton,
	// named, and only the meshes change hands.
	//
	// Asked for by name and answered here: a caller says which outfit and which version, and this decides
	// what that means for this body. That is what lets an outfit or a version be requested of a model that
	// has never heard of it and still come out as something sensible.
	[CreateAssetMenu(fileName = "PlayerModel", menuName = "Game/Player/Model")]
	public class PlayerModel : ScriptableObject
	{
		[Serializable]
		public struct Part
		{
			public PlayerSlot Slot;

			[Tooltip("Skinned to any subset of the shared skeleton's bones, in any order: the renderer is rebound by Bones.")]
			public Mesh Mesh;

			[Tooltip("The mesh's bones by name, in its bindpose order. Read off the model the mesh was imported from whenever this asset changes.")]
			[ReadOnly]
			public string[] Bones;
		}

		[Serializable]
		public struct Paint
		{
			public PlayerSlot Slot;

			[Tooltip("One per submesh of the slot's mesh, in submesh order.")]
			public List<Material> Materials;
		}

		[Serializable]
		public struct Look
		{
			public PlayerLookVersion Version;
			public List<Paint> Paints;
		}

		[Serializable]
		public struct Outfit
		{
			public PlayerOutfitId Id;
			public List<Part> Parts;

			[Tooltip("One per version. A version with no look here, or a submesh a look leaves empty, is painted as in Cartoon.")]
			public List<Look> Looks;
		}

		[Serializable]
		public struct Tint
		{
			public PlayerSlot Slot;

			[Tooltip("Which submesh of the slot's mesh wears the colour.")]
			[MinValue(0)]
			public int SubmeshIndex;

			[Tooltip("Shader property the player's colour is written to. URP's Lit and Simple Lit both call it _BaseColor.")]
			public string ColorProperty;
		}

		[Header("Body")]
		[InfoBox("$MaterialCountReport", InfoMessageType.Warning, nameof(HasMaterialCountMismatch))]
		[Tooltip("A slot no part and no outfit names is hidden.")]
		[SerializeField] private List<Part> _parts = new();

		[Tooltip("One per version. A version with no look here, or a submesh a look leaves empty, is painted as in Cartoon.")]
		[SerializeField] private List<Look> _looks = new();

		[Header("Outfits")]
		[Tooltip("The first is worn when the one asked for is not here. Empty wears nothing: every slot an outfit would fill is hidden.")]
		[SerializeField] private List<Outfit> _outfits = new();

		[Header("Player Colour")]
		[Tooltip("Where this body wears the colour that tells players apart. Empty wears it nowhere.")]
		[SerializeField] private List<Tint> _tints = new();

		public IReadOnlyList<Tint> Tints => _tints;

		[Serializable]
		public struct SubmeshRef
		{
			public PlayerSlot Slot;

			[MinValue(0)]
			public int SubmeshIndex;
		}

		[Header("Eyes")]
		[Tooltip("The eye submeshes, pupils included, cut out on their own so the eyes are painted and tinted without the face. Empty: this body's eyes cannot be swapped or tinted.")]
		[FormerlySerializedAs("_eyes")]
		[SerializeField] private List<SubmeshRef> _eyes = new();

		[Serializable]
		public struct EyeMask
		{
			public PlayerEyeKind Eyes;
			public Material Material;
		}

		[Tooltip("One mask per kind of eyes, painted on Eyes in every version. A kind with none takes Default's; no Default keeps the version's own paint.")]
		[SerializeField] private List<EyeMask> _eyeMasks = new();

		public Material EyeMaskFor(PlayerEyeKind eyes)
		{
			Material fallback = null;
			foreach (var mask in _eyeMasks)
			{
				if (!mask.Material) continue;
				if (mask.Eyes == eyes) return mask.Material;
				if (mask.Eyes == PlayerEyeKind.Default) fallback = mask.Material;
			}

			return fallback;
		}

		public IReadOnlyList<SubmeshRef> Eyes => _eyes;

		// The body's own parts come first, so an outfit cannot take a slot the body already fills. Materials
		// come back one per submesh; a submesh the version leaves empty takes Cartoon's, and one neither
		// names is null so the caller can tell.
		public void Resolve(PlayerSlot slot, PlayerOutfitId outfitId, PlayerLookVersion version, out Part part, List<Material> materials)
		{
			materials.Clear();

			if (TryFind(_parts, slot, out part))
			{
				Pick(_looks, slot, version, part.Mesh.subMeshCount, materials);
				return;
			}

			if (TryGetOutfit(outfitId, out var outfit) && TryFind(outfit.Parts, slot, out part))
			{
				Pick(outfit.Looks, slot, version, part.Mesh.subMeshCount, materials);
				return;
			}

			part = default;
		}

		private bool TryGetOutfit(PlayerOutfitId id, out Outfit outfit)
		{
			foreach (var candidate in _outfits)
			{
				if (candidate.Id != id) continue;

				outfit = candidate;
				return true;
			}

			outfit = _outfits.Count > 0 ? _outfits[0] : default;
			return _outfits.Count > 0;
		}

		private static bool TryFind(List<Part> parts, PlayerSlot slot, out Part found)
		{
			if (parts != null)
			{
				foreach (var part in parts)
				{
					if (part.Slot != slot || !part.Mesh) continue;

					found = part;
					return true;
				}
			}

			found = default;
			return false;
		}

		private static void Pick(List<Look> looks, PlayerSlot slot, PlayerLookVersion version, int submeshCount, List<Material> into)
		{
			var versioned = Find(looks, slot, version);
			var cartoon = Find(looks, slot, PlayerLookVersion.Cartoon);

			for (var i = 0; i < submeshCount; i++)
			{
				var material = At(versioned, i);
				into.Add(material ? material : At(cartoon, i));
			}
		}

		private static Material At(List<Material> materials, int index) => materials != null && index < materials.Count ? materials[index] : null;

		private static List<Material> Find(List<Look> looks, PlayerSlot slot, PlayerLookVersion version)
		{
			if (looks == null) return null;

			foreach (var look in looks)
			{
				if (look.Version != version || look.Paints == null) continue;

				foreach (var paint in look.Paints)
				{
					if (paint.Slot == slot) return paint.Materials;
				}
			}

			return null;
		}

		// Said in the inspector rather than found at runtime: a look naming two materials for a mesh cut
		// into three leaves the third wearing whatever the last model left on it.
		private bool HasMaterialCountMismatch => !string.IsNullOrEmpty(MaterialCountReport);

		private string MaterialCountReport
		{
			get
			{
				var report = new StringBuilder();

				Check(_parts, _looks, "Body", report);

				foreach (var outfit in _outfits) Check(outfit.Parts, outfit.Looks, $"Outfit {outfit.Id}", report);

				return report.ToString().TrimEnd();
			}
		}

		private static void Check(List<Part> parts, List<Look> looks, string owner, StringBuilder report)
		{
			if (parts == null || looks == null) return;

			foreach (var look in looks)
			{
				if (look.Paints == null) continue;

				foreach (var paint in look.Paints)
				{
					if (!TryFind(parts, paint.Slot, out var part)) continue;

					var mesh = part.Mesh;

					var count = paint.Materials?.Count ?? 0;
					if (count == mesh.subMeshCount) continue;

					report.AppendLine($"{owner} / {look.Version} / {paint.Slot}: {count} material(s) for {mesh.name}, which has {mesh.subMeshCount} submesh(es).");
				}
			}
		}

#if UNITY_EDITOR
		private void OnValidate()
		{
			BakeBones(_parts);
			foreach (var outfit in _outfits) BakeBones(outfit.Parts);
		}

		private void BakeBones(List<Part> parts)
		{
			if (parts == null) return;

			for (var i = 0; i < parts.Count; i++)
			{
				var part = parts[i];
				part.Bones = BonesOf(part.Mesh);
				parts[i] = part;
			}
		}

		private string[] BonesOf(Mesh mesh)
		{
			if (!mesh) return Array.Empty<string>();

			var path = UnityEditor.AssetDatabase.GetAssetPath(mesh);
			if (UnityEditor.AssetDatabase.LoadMainAssetAtPath(path) is GameObject source)
			{
				foreach (var renderer in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
				{
					if (renderer.sharedMesh != mesh) continue;

					var bones = new string[renderer.bones.Length];
					for (var i = 0; i < bones.Length; i++) bones[i] = renderer.bones[i] ? renderer.bones[i].name : string.Empty;
					return bones;
				}
			}

			Debug.LogWarning($"{name}: no skinned renderer in {path} draws {mesh.name}, so its bones cannot be named.", this);
			return Array.Empty<string>();
		}
#endif
	}
}
