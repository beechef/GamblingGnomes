using System.Collections.Generic;
using DG.Tweening;
using Game.Runtime.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	public class PokerHallucinationGhostBehaviour : PokerHallucinationEffectBehaviour<PokerHallucinationGhostEffect>
	{
		private static readonly int EchoEventId = Shader.PropertyToID("Echo");
		private static readonly int PositionId = Shader.PropertyToID("position");
		private static readonly int MeshIndexId = Shader.PropertyToID("meshIndex");
		private static readonly int DriftId = Shader.PropertyToID("Drift");
		private static readonly int LifetimeId = Shader.PropertyToID("Lifetime");
		private static readonly int TintId = Shader.PropertyToID("Tint");
		private static readonly int AlphaId = Shader.PropertyToID("Alpha");
		private static readonly int AlphaOverLifeId = Shader.PropertyToID("AlphaOverLife");

		private static readonly int[] SnapshotIds =
		{
			Shader.PropertyToID("Mesh0"),
			Shader.PropertyToID("Mesh1"),
			Shader.PropertyToID("Mesh2"),
			Shader.PropertyToID("Mesh3"),
		};

		// One body, the meshes it is drawn with, their baked poses, the snapshots they are combined into in turn,
		// and the effect drawing them.
		private sealed class Source
		{
			public Transform Root;
			public SkinnedMeshRenderer[] Skins;
			public Transform[] Bones;
			public Vector3[] EchoedPose;
			public bool HasEchoedPose;
			public Vector3 HeldPosition;
			public Mesh[] Baked;
			public Mesh[] Snapshots;
			public CombineInstance[] Combine;
			public VisualEffect Effect;
			public VFXEventAttribute Echo;
			public int NextSnapshot;
		}

		private readonly List<Source> _sources = new();
		private readonly List<Transform> _resolved = new();

		private GameObject _container;
		private Tween _tween;
		private float _strength;
		private float _nextEchoTime;

		// Echoes stop at the end; the ones already out live their lifetime and fade on their own.
		protected override float LingerSeconds => EaseDuration + Config.EchoLifetime;

		protected override void OnBegin()
		{
			Config.Target?.Subscribe(Viewer, Rebuild);

			Rebuild();

			_tween?.Kill();
			_tween = DOVirtual.Float(0f, 1f, EaseDuration, value => _strength = value).SetUpdate(true);
		}

		protected override void OnEnd()
		{
			Config.Target?.Unsubscribe(Viewer, Rebuild);

			_tween?.Kill();
			_tween = DOVirtual.Float(_strength, 0f, EaseDuration, value => _strength = value).SetUpdate(true);
		}

		protected override void OnDisposed()
		{
			_tween?.Kill();
			Release();
		}

		// Continuous: every interval, each body that has moved since its last afterimage leaves another. The fade
		// in and out spaces the echoes further apart rather than dimming them, since each one is a whole body.
		private void Update()
		{
			if (_strength <= 0f || Time.unscaledTime < _nextEchoTime) return;

			_nextEchoTime = Time.unscaledTime + Config.EchoInterval / Mathf.Max(_strength, 0.25f);

			foreach (var source in _sources)
			{
				if (!source.Effect || !source.Root) continue;

				if (Config.Pose == PokerHallucinationGhostEffect.EchoPose.Previous) TickPreviousPose(source);
				else TickCurrentPose(source);
			}
		}

		// The pose the body has now, taken the moment it has moved far enough.
		private void TickCurrentPose(Source source)
		{
			if (!source.HasEchoedPose)
			{
				RecordPose(source);
				return;
			}

			if (!HasMoved(source)) return;

			var index = source.NextSnapshot;
			if (!BakeSnapshot(source, source.Snapshots[index])) return;

			RecordPose(source);
			EmitEcho(source, index, source.Root.position);
		}

		// The pose the body has just left: every look bakes the pose it holds into the next snapshot, and that
		// snapshot is only let out once the body has moved away from it, so it stays behind where the body was.
		private void TickPreviousPose(Source source)
		{
			if (source.HasEchoedPose)
			{
				if (!HasMoved(source)) return;

				EmitEcho(source, source.NextSnapshot, source.HeldPosition);
			}

			source.HasEchoedPose = BakeSnapshot(source, source.Snapshots[source.NextSnapshot]);
			if (!source.HasEchoedPose) return;

			source.HeldPosition = source.Root.position;
			RecordPose(source);
		}

		// Measured against the pose the last afterimage was taken in, so slow motion still leaves one once it
		// has gone far enough, and a body at rest leaves none.
		private bool HasMoved(Source source)
		{
			var threshold = Config.MotionThreshold * Config.MotionThreshold;

			for (var i = 0; i < source.Bones.Length; i++)
			{
				if (source.Bones[i] && (source.Bones[i].position - source.EchoedPose[i]).sqrMagnitude > threshold) return true;
			}

			return false;
		}

		private static void RecordPose(Source source)
		{
			for (var i = 0; i < source.Bones.Length; i++)
			{
				if (source.Bones[i]) source.EchoedPose[i] = source.Bones[i].position;
			}

			source.HasEchoedPose = true;
		}

		private void EmitEcho(Source source, int index, Vector3 position)
		{
			source.NextSnapshot = (index + 1) % source.Snapshots.Length;

			source.Effect.SetMesh(SnapshotIds[index], source.Snapshots[index]);

			// A graph that leaves its echoes where they fell has no Drift to set.
			if (source.Effect.HasVector3(DriftId))
			{
				var direction = Random.insideUnitCircle.normalized;
				source.Effect.SetVector3(DriftId, new Vector3(direction.x, 0.25f, direction.y) * Config.DriftSpeed);
			}

			source.Echo.SetVector3(PositionId, position);
			source.Echo.SetUint(MeshIndexId, (uint)index);
			source.Effect.SendEvent(EchoEventId, source.Echo);
		}

		// The pose of whatever of the body is drawn this frame, around the body's root and in world axes, so a
		// hidden piece (a head gone with its owner) leaves no afterimage.
		private bool BakeSnapshot(Source source, Mesh snapshot)
		{
			var fromRoot = Matrix4x4.Translate(-source.Root.position);
			var count = 0;

			for (var i = 0; i < source.Skins.Length; i++)
			{
				var skin = source.Skins[i];
				if (!IsDrawn(skin)) continue;

				skin.BakeMesh(source.Baked[i], true);
				count += source.Baked[i].subMeshCount;
			}

			if (count == 0) return false;

			if (source.Combine == null || source.Combine.Length != count) source.Combine = new CombineInstance[count];

			var slot = 0;
			for (var i = 0; i < source.Skins.Length; i++)
			{
				var skin = source.Skins[i];
				if (!IsDrawn(skin)) continue;

				// BakeMesh(…, true) leaves the renderer's own scale out, so the full matrix puts it back: a hat
				// under a scaled head bone is scaled by its transform, not by its skinning.
				var matrix = fromRoot * skin.transform.localToWorldMatrix;

				for (var subMesh = 0; subMesh < source.Baked[i].subMeshCount; subMesh++)
				{
					source.Combine[slot++] = new CombineInstance { mesh = source.Baked[i], subMeshIndex = subMesh, transform = matrix };
				}
			}

			snapshot.Clear();
			snapshot.CombineMeshes(source.Combine, true, true);
			return true;
		}

		private static bool IsDrawn(SkinnedMeshRenderer skin) =>
			skin && skin.enabled && skin.gameObject.activeInHierarchy && skin.sharedMesh;

		// Re-resolved rather than resolved once: a player who sits down after this began echoes too.
		private void Rebuild()
		{
			Release();

			if (!Config || !Config.Target || !Config.VisualEffect) return;

			Config.Target.Collect(Viewer, _resolved);

			_container = new GameObject("HallucinationGhosts");
			_container.transform.SetParent(transform, false);

			foreach (var bone in _resolved)
			{
				var rig = bone ? bone.GetComponentInParent<PlayerBoneRig>() : null;
				if (!rig) continue;

				var skins = rig.GetComponentsInChildren<SkinnedMeshRenderer>(false);
				if (skins.Length > 0) _sources.Add(CreateSource(rig.transform, skins));
			}
		}

		private Source CreateSource(Transform root, SkinnedMeshRenderer[] skins)
		{
			var go = new GameObject($"Ghost_{root.name}");
			go.transform.SetParent(_container.transform, false);

			var effect = go.AddComponent<VisualEffect>();
			effect.visualEffectAsset = Config.VisualEffect;
			effect.SetVector4(TintId, Config.Tint);
			effect.SetFloat(LifetimeId, Config.EchoLifetime);
			effect.SetFloat(AlphaId, Config.Alpha);
			if (Config.AlphaOverLife != null) effect.SetAnimationCurve(AlphaOverLifeId, Config.AlphaOverLife);

			var baked = new Mesh[skins.Length];
			for (var i = 0; i < baked.Length; i++) baked[i] = new Mesh { name = $"GhostBake_{root.name}_{i}" };

			var snapshots = new Mesh[PokerHallucinationGhostEffect.SnapshotCount];
			for (var i = 0; i < snapshots.Length; i++)
			{
				snapshots[i] = new Mesh { name = $"GhostSnapshot_{root.name}_{i}", indexFormat = IndexFormat.UInt32 };
			}

			var bones = new HashSet<Transform>();
			foreach (var skin in skins)
			{
				foreach (var bone in skin.bones)
				{
					if (bone) bones.Add(bone);
				}
			}

			var boneArray = new Transform[bones.Count];
			bones.CopyTo(boneArray);

			return new Source
			{
				Root = root,
				Skins = skins,
				Bones = boneArray,
				EchoedPose = new Vector3[boneArray.Length],
				Baked = baked,
				Snapshots = snapshots,
				Effect = effect,
				Echo = effect.CreateVFXEventAttribute(),
			};
		}

		private void Release()
		{
			foreach (var source in _sources)
			{
				foreach (var mesh in source.Baked)
				{
					if (mesh) Destroy(mesh);
				}

				foreach (var mesh in source.Snapshots)
				{
					if (mesh) Destroy(mesh);
				}

				source.Echo?.Dispose();
			}

			_sources.Clear();
			_resolved.Clear();

			if (_container) Destroy(_container);
			_container = null;
		}
	}
}
