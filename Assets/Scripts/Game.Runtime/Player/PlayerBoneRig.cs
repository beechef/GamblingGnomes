using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Player
{
	// A model's key transforms under the names the game uses for them, so anything that wants a head, a hand
	// or the point a hand holds a prop at asks for that rather than knowing what this particular skeleton
	// calls it. One of these sits on each model; two rigs built on the same skeleton get the same setup.
	//
	// The transforms are references, never found by name at runtime. A name lookup looks resilient and is the
	// opposite: when the art put POT_attach_R between the wrist and the hand, `Wrist_R.Find("CapHold")`
	// stopped finding a hold point that was still sitting right there, and every staked cap was glued to the
	// wrist origin with nothing logged. A reference follows the object wherever the rig moves it.
	public class PlayerBoneRig : MonoBehaviour
	{
		[Serializable]
		public struct BoneBinding
		{
			public PlayerBone Bone;

			public Transform Transform;

			[Tooltip("Editor only: fills in Transform when it is empty. Never read at runtime.")]
			public string BoneName;
		}

		// The skeleton this project ships with, filled in when the component is added and resolved into
		// references on the spot.
		private static readonly (PlayerBone Bone, string Name)[] DefaultBoneNames =
		{
			(PlayerBone.Root, "Root_M"),
			(PlayerBone.Spine, "Spine1_M"),
			(PlayerBone.Chest, "Chest_M"),
			(PlayerBone.Neck, "Neck_M"),
			(PlayerBone.Head, "Head_M"),
			(PlayerBone.HeadTop, "HeadEnd_M"),
			(PlayerBone.Jaw, "Jaw_M"),
			(PlayerBone.ShoulderLeft, "Shoulder_L"),
			(PlayerBone.ShoulderRight, "Shoulder_R"),
			(PlayerBone.ElbowLeft, "Elbow_L"),
			(PlayerBone.ElbowRight, "Elbow_R"),
			(PlayerBone.HandLeft, "Wrist_L"),
			(PlayerBone.HandRight, "Wrist_R"),
			(PlayerBone.HipLeft, "Hip_L"),
			(PlayerBone.HipRight, "Hip_R"),
			(PlayerBone.KneeLeft, "Knee_L"),
			(PlayerBone.KneeRight, "Knee_R"),
			(PlayerBone.FootLeft, "Ankle_L"),
			(PlayerBone.FootRight, "Ankle_R"),
			(PlayerBone.ToeLeft, "Toes_L"),
			(PlayerBone.ToeRight, "Toes_R"),
			(PlayerBone.HoldRight, "CapHold")
		};

		[Header("Rig")]
		[Tooltip("Where the editor searches for a binding's name. Empty searches from this object down.")]
		[SerializeField] private Transform _root;

		[SerializeField] private List<BoneBinding> _bones = new();

		private readonly Dictionary<PlayerBone, Transform> _resolved = new();

		private bool _built;

		public Transform Root => _root ? _root : transform;

		private void Awake() => Build();

		public Transform Get(PlayerBone bone) => TryGet(bone, out var boneTransform) ? boneTransform : null;

		public bool TryGet(PlayerBone bone, out Transform boneTransform)
		{
			// A rig asked for a bone before its own Awake ran resolves there and then rather than
			// answering that it has none.
			if (!_built) Build();

			return _resolved.TryGetValue(bone, out boneTransform) && boneTransform;
		}

		// Falls back to the rig itself: a caller asking where a bone this model has no equivalent for is
		// gets somewhere on the model rather than a point in the middle of the level.
		public Vector3 GetPosition(PlayerBone bone) => TryGet(bone, out var boneTransform) ? boneTransform.position : Root.position;

		public Quaternion GetRotation(PlayerBone bone) => TryGet(bone, out var boneTransform) ? boneTransform.rotation : Root.rotation;

		private void Build()
		{
			_built = true;
			_resolved.Clear();

			foreach (var binding in _bones)
			{
				if (binding.Transform) _resolved[binding.Bone] = binding.Transform;
			}
		}

#if UNITY_EDITOR
		// A binding authored by name is turned into a reference while it is being edited, so a rig that
		// has been looked at in the inspector carries every transform and runtime never searches.
		private void OnValidate() => ResolveNamesToReferences();

		private void Reset()
		{
			_bones.Clear();

			foreach (var (bone, boneName) in DefaultBoneNames)
			{
				_bones.Add(new BoneBinding { Bone = bone, BoneName = boneName });
			}

			ResolveNamesToReferences();
		}

		public void ResolveNamesToReferences()
		{
			Dictionary<string, Transform> byName = null;

			for (var i = 0; i < _bones.Count; i++)
			{
				var binding = _bones[i];
				if (binding.Transform || string.IsNullOrEmpty(binding.BoneName)) continue;

				if (byName == null)
				{
					byName = new Dictionary<string, Transform>();
					foreach (var child in Root.GetComponentsInChildren<Transform>(true)) byName[child.name] = child;
				}

				if (!byName.TryGetValue(binding.BoneName, out var found)) continue;

				binding.Transform = found;
				_bones[i] = binding;
			}

			_built = false;
		}
#endif
	}
}
