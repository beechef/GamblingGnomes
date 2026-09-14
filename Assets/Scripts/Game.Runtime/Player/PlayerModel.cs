using System;
using System.Collections.Generic;
using System.Text;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.Player
{
	// One body a player can be drawn as, and everything that decides what goes in each slot of it: the
	// meshes the body is cut into, its outfits, and a look per version for both. A new character is a new
	// asset — nothing in the prefab changes, because every model is exported against the same skeleton
	// and only the meshes change hands.
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

			[Tooltip("Must be skinned to the shared skeleton, in its bone order — it is dropped onto a renderer already bound to those bones.")]
			public Mesh Mesh;
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

		// The body's own parts come first, so an outfit cannot take a slot the body already fills. Materials
		// come back one per submesh; a submesh the version leaves empty takes Cartoon's, and one neither
		// names is null so the caller can tell.
		public void Resolve(PlayerSlot slot, PlayerOutfitId outfitId, PlayerLookVersion version, out Mesh mesh, List<Material> materials)
		{
			materials.Clear();

			if (TryFind(_parts, slot, out mesh))
			{
				Pick(_looks, slot, version, mesh.subMeshCount, materials);
				return;
			}

			if (TryGetOutfit(outfitId, out var outfit) && TryFind(outfit.Parts, slot, out mesh))
			{
				Pick(outfit.Looks, slot, version, mesh.subMeshCount, materials);
				return;
			}

			mesh = null;
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

		private static bool TryFind(List<Part> parts, PlayerSlot slot, out Mesh mesh)
		{
			if (parts != null)
			{
				foreach (var part in parts)
				{
					if (part.Slot != slot || !part.Mesh) continue;

					mesh = part.Mesh;
					return true;
				}
			}

			mesh = null;
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
					if (!TryFind(parts, paint.Slot, out var mesh)) continue;

					var count = paint.Materials?.Count ?? 0;
					if (count == mesh.subMeshCount) continue;

					report.AppendLine($"{owner} / {look.Version} / {paint.Slot}: {count} material(s) for {mesh.name}, which has {mesh.subMeshCount} submesh(es).");
				}
			}
		}
	}
}
