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

		[Header("Full Body")]
		[SerializeField] private SkinnedMeshRenderer _bodyMeshRenderer;
		[SerializeField] private SkinnedMeshRenderer _outfitMeshRenderer;
		[SerializeField] private MeshRenderer _hatRenderer;

		[Header("Hand Only")]
		[SerializeField] private SkinnedMeshRenderer _handOnlyBodyMeshRenderer;
		[SerializeField] private SkinnedMeshRenderer _handOnlyOutfitMeshRenderer;

		private readonly NetworkVariable<int> _skinIndex = new(0,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		private void Awake()
		{
			// Outfits ship with their own skeletons; rebinding onto the body rig lets one
			// animated rig drive both meshes.
			BoneRemapper.RemapBoneRenderer(_outfitMeshRenderer, _bodyMeshRenderer.rootBone);
			BoneRemapper.RemapBoneRenderer(_handOnlyOutfitMeshRenderer, _handOnlyBodyMeshRenderer.rootBone);
		}

		protected override void OnNetworkPostSpawn()
		{
			_bodyMeshRenderer.enabled = !IsOwner;
			_outfitMeshRenderer.enabled = !IsOwner;
			_hatRenderer.enabled = !IsOwner;

			_handOnlyBodyMeshRenderer.enabled = IsOwner;
			_handOnlyOutfitMeshRenderer.enabled = IsOwner;

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

			ApplyMaterial(_bodyMeshRenderer, skin.BodyMaterial);
			ApplyMaterial(_handOnlyBodyMeshRenderer, skin.BodyMaterial);
			ApplyMaterial(_outfitMeshRenderer, skin.OutfitMaterial);
			ApplyMaterial(_handOnlyOutfitMeshRenderer, skin.OutfitMaterial);
			ApplyMaterial(_hatRenderer, skin.HatMaterial);
		}

		private static void ApplyMaterial(Renderer renderer, Material material)
		{
			if (!renderer || !material) return;
			renderer.material = material;
		}
	}
}
