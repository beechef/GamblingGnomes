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

		// Picking this player out of the room. Replicated because the whole point of an outline here is
		// that everybody watches an accuser's finger settle on somebody — one only the accuser could see
		// would say nothing to anyone else. Cosmetic, and carrying no reason: whatever put it there is the
		// one that takes it away.
		[Header("Outline")]
		[Tooltip("Second pass hung on the full body rig. The owner is never outlined to themselves — a glow on your own hands marks you to nobody.")]
		[SerializeField] private Material _outlineMaterial;

		[SerializeField] private Color _outlineColor = new(1f, 0.2f, 0.2f, 1f);
		[SerializeField] private float _outlineWidth = 0.02f;

		private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
		private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

		[HideInInspector] public NetworkVariable<bool> Outlined = new(false,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		private readonly NetworkVariable<int> _skinIndex = new(0,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		private Material _runtimeOutlineMaterial;

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

			// Late join: whoever is already lit up stays lit up, and the skin underneath carries the pass.
			ApplySkin(_skinIndex.Value);

			_skinIndex.OnValueChanged += HandleSkinIndexChanged;
			Outlined.OnValueChanged += HandleOutlinedChanged;
		}

		public override void OnNetworkDespawn()
		{
			_skinIndex.OnValueChanged -= HandleSkinIndexChanged;
			Outlined.OnValueChanged -= HandleOutlinedChanged;
		}

		public override void OnDestroy()
		{
			base.OnDestroy();

			if (_runtimeOutlineMaterial) Destroy(_runtimeOutlineMaterial);
		}

		public void ServerSetOutlined(bool outlined)
		{
			if (!IsServer) return;

			Outlined.Value = outlined;
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

		private void HandleOutlinedChanged(bool previous, bool current)
		{
			ApplyOutline(current);
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

			// Assigning a material replaces the whole list, so the pass sitting on top of it is hung again.
			ApplyOutline(Outlined.Value);
		}

		// Added to and taken off whatever a renderer is already wearing, rather than rebuilt out of a skin.
		// Hanging it as part of the skin looked tidier and was the bug: a slot the skin leaves empty is a
		// renderer the outline can never reach, and on a rig cut into ten meshes one unlit piece reads as
		// the whole outline being broken.
		//
		// Only the body rig is lit: the hand-only pair is what the owner sees of themselves, and a glow on
		// your own hands tells you something the rest of the table already knew.
		private void ApplyOutline(bool outlined)
		{
			if (_bodyMeshRenderers != null)
			{
				foreach (var renderer in _bodyMeshRenderers) ApplyOutline(renderer, outlined);
			}

			ApplyOutline(_outfitMeshRenderer, outlined);
			ApplyOutline(_hatRenderer, outlined);
		}

		private void ApplyOutline(Renderer renderer, bool outlined)
		{
			if (!renderer) return;

			var outline = ResolveOutlineMaterial();
			if (!outline) return;

			var current = renderer.sharedMaterials;
			var wearing = current.Length > 0 && current[^1] == outline;
			if (wearing == outlined) return;

			var next = new Material[outlined ? current.Length + 1 : current.Length - 1];
			for (var i = 0; i < next.Length && i < current.Length; i++) next[i] = current[i];
			if (outlined) next[^1] = outline;

			renderer.sharedMaterials = next;
		}

		// One instance shared by every renderer on this player, made on first use and destroyed with the
		// object — the colour and width are this player's, not the asset's.
		private Material ResolveOutlineMaterial()
		{
			if (_runtimeOutlineMaterial || !_outlineMaterial) return _runtimeOutlineMaterial;

			_runtimeOutlineMaterial = new Material(_outlineMaterial);
			_runtimeOutlineMaterial.SetColor(OutlineColorId, _outlineColor);
			_runtimeOutlineMaterial.SetFloat(OutlineWidthId, _outlineWidth);

			return _runtimeOutlineMaterial;
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
