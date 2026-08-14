using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Player
{
	// A model's bones under the names the game uses for them, so anything that wants a head, a hand or a
	// foot asks for that rather than knowing what this particular skeleton calls it. One of these sits on
	// each model; two rigs built on the same skeleton can be given the same setup.
	public class PlayerBoneRig : MonoBehaviour
	{
		[Serializable]
		public struct BoneBinding
		{
			public PlayerBone Bone;

			[Tooltip("Left empty, the bone is looked up by name under the root instead.")]
			public Transform Transform;

			public string BoneName;
		}

		// The skeleton this project ships with, filled in when the component is added. A model built on
		// anything else overrides the names, or has its bones dropped in by hand.
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
			(PlayerBone.ToeRight, "Toes_R")
		};

		[Header("Rig")]
		[Tooltip("Where bones are searched for by name. Empty searches from this object down.")]
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

			Dictionary<string, Transform> byName = null;

			foreach (var binding in _bones)
			{
				var boneTransform = binding.Transform;

				if (!boneTransform && !string.IsNullOrEmpty(binding.BoneName))
				{
					byName ??= CollectByName();
					byName.TryGetValue(binding.BoneName, out boneTransform);
				}

				if (boneTransform) _resolved[binding.Bone] = boneTransform;
			}
		}

		private Dictionary<string, Transform> CollectByName()
		{
			var byName = new Dictionary<string, Transform>();

			foreach (var child in Root.GetComponentsInChildren<Transform>(true)) byName[child.name] = child;

			return byName;
		}

		private void Reset()
		{
			_bones.Clear();

			foreach (var (bone, boneName) in DefaultBoneNames)
			{
				_bones.Add(new BoneBinding { Bone = bone, BoneName = boneName });
			}
		}
	}
}
