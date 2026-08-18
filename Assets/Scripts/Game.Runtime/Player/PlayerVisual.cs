using System;
using System.Collections.Generic;
using Game.Runtime.Utility;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.Player
{
	public class PlayerVisual : NetworkBehaviour
	{
		[Serializable]
		public struct PlayerSkin
		{
			public string SkinName;
			public Material BodyMaterial;
			public Material OutfitMaterial;
			public Material HatMaterial;
		}

		[Header("Skins")]
		[SerializeField] private List<PlayerSkin> _skins = new();

		// A rig is however many meshes the artist cut it into — the gnome arrives as a body, a head and a
		// piece per finger. Hiding "the body" therefore means hiding all of them: one renderer left behind
		// is a head floating where the owner should see nothing.
		[Header("Full Body")]
		[SerializeField] private SkinnedMeshRenderer[] _bodyMeshRenderers;

		[SerializeField] private SkinnedMeshRenderer _outfitMeshRenderer;
		[SerializeField] private MeshRenderer _hatRenderer;

		[Header("Hand Only")]
		[SerializeField] private SkinnedMeshRenderer[] _handOnlyBodyMeshRenderers;

		[SerializeField] private SkinnedMeshRenderer _handOnlyOutfitMeshRenderer;

		private readonly NetworkVariable<int> _skinIndex = new(0,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		private void Awake()
		{
			// Outfits ship with their own skeletons; rebinding onto the body rig lets one
			// animated rig drive both meshes.
			BoneRemapper.RemapBoneRenderer(_outfitMeshRenderer, RootBoneOf(_bodyMeshRenderers));
			BoneRemapper.RemapBoneRenderer(_handOnlyOutfitMeshRenderer, RootBoneOf(_handOnlyBodyMeshRenderers));
		}

		protected override void OnNetworkPostSpawn()
		{
			SetEnabled(_bodyMeshRenderers, !IsOwner);
			if (_outfitMeshRenderer) _outfitMeshRenderer.enabled = !IsOwner;
			if (_hatRenderer) _hatRenderer.enabled = !IsOwner;

			SetEnabled(_handOnlyBodyMeshRenderers, IsOwner);
			if (_handOnlyOutfitMeshRenderer) _handOnlyOutfitMeshRenderer.enabled = IsOwner;

			ApplySkin(_skinIndex.Value);
			_skinIndex.OnValueChanged += HandleSkinIndexChanged;
		}

		public override void OnNetworkDespawn()
		{
			_skinIndex.OnValueChanged -= HandleSkinIndexChanged;
		}

		public void SetSkin(int skinIndex)
		{
			if (!IsServer) return;
			if (skinIndex < 0 || skinIndex >= _skins.Count) return;

			_skinIndex.Value = skinIndex;
		}

		private void HandleSkinIndexChanged(int previous, int current)
		{
			ApplySkin(current);
		}

		private void ApplySkin(int skinIndex)
		{
			if (skinIndex < 0 || skinIndex >= _skins.Count) return;

			var skin = _skins[skinIndex];

			ApplyMaterial(_bodyMeshRenderers, skin.BodyMaterial);
			ApplyMaterial(_handOnlyBodyMeshRenderers, skin.BodyMaterial);
			ApplyMaterial(_outfitMeshRenderer, skin.OutfitMaterial);
			ApplyMaterial(_handOnlyOutfitMeshRenderer, skin.OutfitMaterial);
			ApplyMaterial(_hatRenderer, skin.HatMaterial);
		}

		private static Transform RootBoneOf(SkinnedMeshRenderer[] renderers)
		{
			if (renderers == null) return null;

			foreach (var renderer in renderers)
			{
				if (renderer && renderer.rootBone) return renderer.rootBone;
			}

			return null;
		}

		private static void SetEnabled(SkinnedMeshRenderer[] renderers, bool enabled)
		{
			if (renderers == null) return;

			foreach (var renderer in renderers)
			{
				if (renderer) renderer.enabled = enabled;
			}
		}

		private static void ApplyMaterial(SkinnedMeshRenderer[] renderers, Material material)
		{
			if (renderers == null) return;

			foreach (var renderer in renderers) ApplyMaterial(renderer, material);
		}

		private static void ApplyMaterial(Renderer renderer, Material material)
		{
			if (!renderer || !material) return;
			renderer.material = material;
		}
	}
}
