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
		[Tooltip("Second pass worn by every part of the full body rig, the hat included. The owner is never outlined to themselves — a glow on your own hands marks you to nobody.")]
		[SerializeField] private Material _outlineMaterial;

		[Tooltip("What the pass is painted while this player is picked out.")]
		[SerializeField] private Color _outlineColor = new(1f, 0.2f, 0.2f, 1f);

		[Tooltip("What it is painted the rest of the time — the stroke every body wears. Black: the pass is always there and its colour is the whole difference between a drawn line and a highlight.")]
		[SerializeField] private Color _idleOutlineColor = new(0f, 0f, 0f, 1f);

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
		private PlayerEyeKind _eyes = PlayerEyeKind.Default;

		// Which slots the current model fills, so visibility can be asked again without resolving again.
		private readonly HashSet<PlayerSlot> _filled = new();

		// Slots switched off on top of everything else — a head gone when its owner goes under. Held here
		// because this is the one writer of renderer.enabled; switched off anywhere else, the next rig or
		// model change would switch it straight back on.
		private readonly Dictionary<PlayerSlot, HashSet<object>> _hidden = new();

		private readonly List<Material> _resolvedMaterials = new();

		// Which rig this client draws for this body: the full body, or the hands alone. Local, because it
		// is a question about this screen — everyone else always sees a body — and it starts as ownership
		// says, the owner looking out through their own hands. A beat that wants the owner to watch
		// themselves from outside (going under) turns it on for as long as it lasts.
		private bool _renderAllBody = true;

		public bool RenderAllBody => _renderAllBody;

		public event Action<bool> OnRenderAllBodyChanged;

		// Lit for this client alone, on top of whatever the replicated flag says. Pointing at somebody before
		// choosing them is not a move the table needs to watch - the choice is, and the server announces
		// that - so a hover that went over the wire would be a message per twitch of the mouse saying nothing.
		// The two are ORed rather than one overwriting the other, so an accusation lighting this body and a
		// cursor passing over it cannot switch each other off.
		private bool _localOutlined;

		private bool IsOutlined => Outlined.Value || _localOutlined;

		private readonly Dictionary<SkinnedMeshRenderer, Dictionary<string, Transform>> _skeletons = new();

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
			// Settled before the first appearance is drawn, so the owner never shows a frame of their own body.
			_renderAllBody = !IsOwner;

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

		// Another kind of eyes: the model's mask for them (PlayerModel.EyeMaskFor) painted on its eye submeshes,
		// whatever the version and outfit.
		public void SetEyes(PlayerEyeKind eyes)
		{
			if (_eyes == eyes) return;

			_eyes = eyes;
			ApplyAppearance();
		}

		private void PaintEyes(PlayerModel model, PlayerSlot slot, List<Material> materials)
		{
			var mask = model.EyeMaskFor(_eyes);
			if (!mask) return;

			foreach (var eye in model.Eyes)
			{
				if (eye.Slot == slot && eye.SubmeshIndex < materials.Count) materials[eye.SubmeshIndex] = mask;
			}
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

				model.Resolve(slot.Slot, _outfit.Value, _version, out var part, _resolvedMaterials);
				var mesh = part.Mesh;
				if (!mesh) continue;

				_filled.Add(slot.Slot);
				PaintEyes(model, slot.Slot, _resolvedMaterials);
				WriteMesh(slot.Renderer, part);
				WriteMaterials(slot.Renderer, _resolvedMaterials);
			}

			RefreshVisibility();

			// Assigning a material replaces the whole list, so the pass sitting on top of it is hung again.
			ApplyOutline(IsOutlined);

			OnAppearanceChanged?.Invoke();
		}

		public void SetRenderAllBody(bool renderAllBody)
		{
			if (_renderAllBody == renderAllBody) return;

			_renderAllBody = renderAllBody;

			RefreshVisibility();
			OnRenderAllBodyChanged?.Invoke(_renderAllBody);
		}

		// Counted per caller, so a head gone under and a hat swapped for something else never switch each
		// other's slot back on.
		public void SetSlotHidden(object handle, PlayerSlot slot, bool hidden)
		{
			if (handle == null) return;

			if (!_hidden.TryGetValue(slot, out var holders))
			{
				if (!hidden) return;

				holders = new HashSet<object>();
				_hidden.Add(slot, holders);
			}

			var changed = hidden ? holders.Add(handle) : holders.Remove(handle);
			if (changed) RefreshVisibility();
		}

		private bool IsHidden(PlayerSlot slot) => _hidden.TryGetValue(slot, out var holders) && holders.Count > 0;

		// A renderer is drawn when its model fills it and it is on the rig this client renders: the full
		// body while RenderAllBody is on, the hand-only rig otherwise. Before spawn nobody owns anything yet,
		// so the prefab stays as authored.
		private void RefreshVisibility()
		{
			foreach (var slot in _slots)
			{
				if (!slot.Renderer) continue;

				var onRenderedRig = !IsSpawned || IsHandOnly(slot.Slot) != _renderAllBody;
				slot.Renderer.enabled = onRenderedRig && _filled.Contains(slot.Slot) && !IsHidden(slot.Slot);
			}
		}

		private static bool IsHandOnly(PlayerSlot slot) => slot is PlayerSlot.HandBody or PlayerSlot.HandOutfit;

		// Added to whatever a renderer is already wearing, rather than rebuilt out of a model. Hanging it as
		// part of the model looked tidier and was the bug: a slot the model leaves empty is a renderer the
		// outline can never reach, and on a rig cut into ten meshes one unlit piece reads as the whole
		// outline being broken.
		//
		// The pass is worn all the time — every slot, both rigs, the hat included — and it is the black
		// stroke the character is drawn with, so being picked out is a change of its colour rather than a
		// pass appearing. Adding and removing the material rebuilds every renderer's array on a beat
		// that only wanted a different colour, and leaves the lit look depending on a pass that has to be
		// re-hung after each repaint to exist at all.
		//
		// Every slot rather than the body rig alone, so what the prefab is authored with and what runs are
		// the same list: the owner's own hands light up with the rest of them, which is a player being told
		// the table is pointing at them rather than the useless glow the body-only rule was written about.
		private void ApplyOutline(bool outlined)
		{
			var outline = ResolveOutlineMaterial();
			if (!outline) return;

			outline.SetColor(OutlineColorId, outlined ? _outlineColor : _idleOutlineColor);

			foreach (var slot in _slots) WearOutline(slot.Renderer, outline);
		}

		private static void WearOutline(Renderer renderer, Material outline)
		{
			if (!renderer) return;

			var current = renderer.sharedMaterials;
			if (current.Length > 0 && current[^1] == outline) return;

			var next = new Material[current.Length + 1];
			for (var i = 0; i < current.Length; i++) next[i] = current[i];
			next[^1] = outline;

			renderer.sharedMaterials = next;
		}

		// One instance shared by every renderer on this player, made on first use and destroyed with the
		// object — the colour and width are this player's, not the asset's.
		private Material ResolveOutlineMaterial()
		{
			if (_runtimeOutlineMaterial || !_outlineMaterial) return _runtimeOutlineMaterial;

			_runtimeOutlineMaterial = new Material(_outlineMaterial);
			_runtimeOutlineMaterial.SetColor(OutlineColorId, _idleOutlineColor);
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

		// A mesh may be skinned to any subset of the skeleton in any order, so the renderer is rebound by the
		// part's bone names onto the skeleton of the rig it sits in. A name the rig lacks is said out loud,
		// because it would otherwise deform into a spike with nothing logged.
		private void WriteMesh(Renderer renderer, PlayerModel.Part part)
		{
			var mesh = part.Mesh;

			if (renderer is SkinnedMeshRenderer skinned)
			{
				if (skinned.sharedMesh == mesh) return;

				if (part.Bones != null && part.Bones.Length == mesh.bindposes.Length) skinned.bones = BindBones(skinned, part.Bones, mesh);
				else if (mesh.bindposes.Length != skinned.bones.Length)
				{
					Debug.LogWarning($"{mesh.name} is skinned to {mesh.bindposes.Length} bones, {skinned.name} holds {skinned.bones.Length}, and its model has no bone names to rebind by. Open the model asset to bake them.", skinned);
				}

				skinned.sharedMesh = mesh;
				return;
			}

			if (renderer.TryGetComponent<MeshFilter>(out var filter)) filter.sharedMesh = mesh;
		}

		private Transform[] BindBones(SkinnedMeshRenderer skinned, string[] names, Mesh mesh)
		{
			var skeleton = SkeletonOf(skinned);
			var bones = new Transform[names.Length];

			for (var i = 0; i < names.Length; i++)
			{
				if (skeleton.TryGetValue(names[i], out var bone)) bones[i] = bone;
				else
				{
					bones[i] = skinned.rootBone;
					Debug.LogWarning($"{mesh.name} is skinned to {names[i]}, which {skinned.name}'s rig does not have. Its vertices follow the root.", skinned);
				}
			}

			return bones;
		}

		// Every bone of the rig the renderer is animated by, by name: the two rigs share names, so each
		// renderer looks only inside the Animator over its root bone (outfits are rebound onto the body in Awake).
		private Dictionary<string, Transform> SkeletonOf(SkinnedMeshRenderer skinned)
		{
			if (_skeletons.TryGetValue(skinned, out var skeleton)) return skeleton;

			skeleton = new Dictionary<string, Transform>();
			var rig = skinned.rootBone ? skinned.rootBone.GetComponentInParent<Animator>(true) : null;
			var root = rig ? rig.transform : skinned.rootBone;

			if (root)
			{
				foreach (var bone in root.GetComponentsInChildren<Transform>(true)) skeleton.TryAdd(bone.name, bone);
			}

			_skeletons[skinned] = skeleton;
			return skeleton;
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
