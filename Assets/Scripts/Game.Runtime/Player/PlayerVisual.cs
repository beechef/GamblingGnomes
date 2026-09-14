using System;
using System.Collections.Generic;
using Game.Runtime.Utility;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.Player
{
	// The one writer of what this player's renderers draw: which mesh sits in each slot and what it is
	// painted with. Three answers decide it — the model (a body, local, overridden by whatever this client
	// is being made to see), the outfit (replicated, by id) and the version (local) — and the model
	// resolves all three into a mesh and a material per slot. Every model shares one skeleton, so a switch
	// only hands renderers new meshes: the camera, the props and every system holding a bone never notice.
	public class PlayerVisual : NetworkBehaviour
	{
		[Serializable]
		public struct SlotRenderer
		{
			public PlayerSlot Slot;

			[Tooltip("A SkinnedMeshRenderer on the shared skeleton, or a MeshRenderer whose MeshFilter is re-meshed.")]
			public Renderer Renderer;
		}

		[Header("Appearance")]
		[Tooltip("What this player is drawn as when nothing asks otherwise.")]
		[Required]
		[SerializeField] private PlayerModel _defaultModel;

		[Tooltip("Every slot a model can fill, one renderer each. A slot the model leaves empty is hidden.")]
		[SerializeField] private List<SlotRenderer> _slots = new();

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

		private readonly NetworkVariable<PlayerOutfitId> _outfit = new(PlayerOutfitId.Classic,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		private Material _runtimeOutlineMaterial;

		// What a hallucination is painting this body with, or null. Kept here because this is the one
		// place a renderer's materials are written, and it has to survive a model change and an outline.
		private Material _materialOverride;

		// Local for the same reason: what this client is being made to see, handed down by
		// PlayerAppearanceController, which resolves whose request wins.
		private PlayerModel _modelOverride;
		private PlayerLookVersion _version = PlayerLookVersion.Cartoon;

		// Which slots the current model fills, so visibility can be asked again without resolving again.
		private readonly HashSet<PlayerSlot> _filled = new();

		private readonly List<Material> _resolvedMaterials = new();

		// Lit for this client alone, on top of whatever the replicated flag says. Pointing at somebody before
		// choosing them is not a move the table needs to watch - the choice is, and the server announces
		// that - so a hover that went over the wire would be a message per twitch of the mouse saying nothing.
		// The two are ORed rather than one overwriting the other, so an accusation lighting this body and a
		// cursor passing over it cannot switch each other off.
		private bool _localOutlined;

		private bool IsOutlined => Outlined.Value || _localOutlined;

		public PlayerModel Model => _modelOverride ? _modelOverride : _defaultModel;

		// Raised after every slot has been re-meshed and repainted, for anything drawing on top of them —
		// the player's colour goes wherever the model now says it is worn.
		public event Action OnAppearanceChanged;

		private void Awake()
		{
			// Outfits ship with their own skeletons; rebinding onto the body rig lets one animated rig drive
			// both meshes.
			BoneRemapper.RemapBoneRenderer(Find(PlayerSlot.Outfit) as SkinnedMeshRenderer, RootBoneOf(PlayerSlot.Body));
			BoneRemapper.RemapBoneRenderer(Find(PlayerSlot.HandOutfit) as SkinnedMeshRenderer, RootBoneOf(PlayerSlot.HandBody));
		}

		protected override void OnNetworkPostSpawn()
		{
			// Late join: whoever is already lit up stays lit up, and the model underneath carries the pass.
			ApplyAppearance();

			_outfit.OnValueChanged += HandleOutfitChanged;
			Outlined.OnValueChanged += HandleOutlinedChanged;
		}

		public override void OnNetworkDespawn()
		{
			_outfit.OnValueChanged -= HandleOutfitChanged;
			Outlined.OnValueChanged -= HandleOutlinedChanged;
		}

		public override void OnDestroy()
		{
			base.OnDestroy();

			if (_runtimeOutlineMaterial) Destroy(_runtimeOutlineMaterial);
		}

		public bool TryGetRenderer(PlayerSlot slot, out Renderer renderer)
		{
			renderer = Find(slot);
			return renderer;
		}

		public void ServerSetOutlined(bool outlined)
		{
			if (!IsServer) return;

			Outlined.Value = outlined;
		}

		public void SetLocalOutlined(bool outlined)
		{
			if (_localOutlined == outlined) return;

			_localOutlined = outlined;
			ApplyOutline(IsOutlined);
		}

		public void ServerSetOutfit(PlayerOutfitId outfit)
		{
			if (!IsServer) return;

			_outfit.Value = outfit;
		}

		// One repaint for the whole body, composed here rather than written onto the renderers by
		// whoever asked. Assigning materials replaces the array outright, so a second writer would
		// silently undo the first — which is exactly what the outline does every time it is hung.
		public void SetMaterialOverride(Material material)
		{
			if (_materialOverride == material) return;

			_materialOverride = material;
			ApplyAppearance();
		}

		public void SetModelOverride(PlayerModel model)
		{
			if (_modelOverride == model) return;

			_modelOverride = model;
			ApplyAppearance();
		}

		public void SetVersion(PlayerLookVersion version)
		{
			if (_version == version) return;

			_version = version;
			ApplyAppearance();
		}

		private void HandleOutfitChanged(PlayerOutfitId previous, PlayerOutfitId current) => ApplyAppearance();

		private void HandleOutlinedChanged(bool previous, bool current) => ApplyOutline(IsOutlined);

		private void ApplyAppearance()
		{
			var model = Model;
			if (!model) return;

			_filled.Clear();

			foreach (var slot in _slots)
			{
				if (!slot.Renderer) continue;

				model.Resolve(slot.Slot, _outfit.Value, _version, out var mesh, _resolvedMaterials);
				if (!mesh) continue;

				_filled.Add(slot.Slot);
				WriteMesh(slot.Renderer, mesh);
				WriteMaterials(slot.Renderer, _resolvedMaterials);
			}

			RefreshVisibility();

			// Assigning a material replaces the whole list, so the pass sitting on top of it is hung again.
			ApplyOutline(IsOutlined);

			OnAppearanceChanged?.Invoke();
		}

		// A renderer is drawn when its model fills it and it is on the rig this client renders: the owner
		// sees the hand-only rig, everyone else the full body. Before spawn nobody owns anything yet, so the
		// prefab stays as authored.
		private void RefreshVisibility()
		{
			foreach (var slot in _slots)
			{
				if (!slot.Renderer) continue;

				var onRenderedRig = !IsSpawned || IsHandOnly(slot.Slot) == IsOwner;
				slot.Renderer.enabled = onRenderedRig && _filled.Contains(slot.Slot);
			}
		}

		private static bool IsHandOnly(PlayerSlot slot) => slot is PlayerSlot.HandBody or PlayerSlot.HandOutfit;

		// Added to and taken off whatever a renderer is already wearing, rather than rebuilt out of a model.
		// Hanging it as part of the model looked tidier and was the bug: a slot the model leaves empty is a
		// renderer the outline can never reach, and on a rig cut into ten meshes one unlit piece reads as
		// the whole outline being broken.
		//
		// Only the body rig is lit: the hand-only pair is what the owner sees of themselves, and a glow on
		// your own hands tells you something the rest of the table already knew.
		private void ApplyOutline(bool outlined)
		{
			foreach (var slot in _slots)
			{
				if (!IsHandOnly(slot.Slot)) ApplyOutline(slot.Renderer, outlined);
			}
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

		private Renderer Find(PlayerSlot slot)
		{
			foreach (var entry in _slots)
			{
				if (entry.Slot == slot) return entry.Renderer;
			}

			return null;
		}

		private Transform RootBoneOf(PlayerSlot slot) => Find(slot) is SkinnedMeshRenderer skinned ? skinned.rootBone : null;

		// Only the mesh changes hands: the renderer keeps the bones it was bound to, which is why every model
		// is exported against the same skeleton in the same bone order. A mesh that was not is said out loud,
		// because it would otherwise deform into a spike with nothing logged.
		private static void WriteMesh(Renderer renderer, Mesh mesh)
		{
			if (renderer is SkinnedMeshRenderer skinned)
			{
				if (skinned.sharedMesh == mesh) return;

				if (mesh.bindposes.Length != skinned.bones.Length)
				{
					Debug.LogWarning($"{mesh.name} is skinned to {mesh.bindposes.Length} bones and {skinned.name} holds {skinned.bones.Length}. It was not exported against the shared skeleton and will deform wrong.", skinned);
				}

				skinned.sharedMesh = mesh;
				return;
			}

			if (renderer.TryGetComponent<MeshFilter>(out var filter)) filter.sharedMesh = mesh;
		}

		// One material per submesh, so the list is rebuilt to exactly the mesh's size: a model with fewer
		// submeshes than the last one must not keep its leftovers. The override stands in for every one of
		// them, so a hallucination covers the whole piece rather than its first submesh. A submesh the model
		// names nothing for keeps what it wore, which PlayerModel's inspector flags as a mistake. The outline
		// is gone from the new list and is hung again by the caller.
		private void WriteMaterials(Renderer renderer, List<Material> materials)
		{
			var current = renderer.sharedMaterials;
			var next = new Material[materials.Count];

			for (var i = 0; i < next.Length; i++)
			{
				var material = _materialOverride ? _materialOverride : materials[i];
				next[i] = material ? material : i < current.Length ? current[i] : null;
			}

			renderer.sharedMaterials = next;
		}
	}
}
