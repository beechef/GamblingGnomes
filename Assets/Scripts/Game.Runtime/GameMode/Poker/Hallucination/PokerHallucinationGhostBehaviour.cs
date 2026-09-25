using System.Collections.Generic;
using DG.Tweening;
using Game.Runtime.Player;
using UnityEngine;
using UnityEngine.VFX;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	public class PokerHallucinationGhostBehaviour : PokerHallucinationEffectBehaviour<PokerHallucinationGhostEffect>
	{
		private static readonly int CenterId = Shader.PropertyToID("Center");
		private static readonly int SizeId = Shader.PropertyToID("Size");
		private static readonly int RateId = Shader.PropertyToID("Rate");
		private static readonly int TintId = Shader.PropertyToID("Tint");

		// One body, the meshes it is drawn with, and the effect shedding off it.
		private sealed class Source
		{
			public SkinnedMeshRenderer[] Skins;
			public VisualEffect Effect;
		}

		private readonly List<Source> _sources = new();
		private readonly List<Transform> _resolved = new();

		private GameObject _container;
		private Tween _tween;
		private float _strength;

		// Spawning stops at the end; the wisps already out live their lifetime and fade on their own.
		protected override float LingerSeconds => EaseDuration + Config.WispLifetime;

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

		// Continuous: the wisps rise from wherever the body is drawn this frame, at the rate the fade allows.
		private void Update()
		{
			foreach (var source in _sources)
			{
				if (!source.Effect) continue;

				var drawn = TryGetDrawnBounds(source.Skins, out var bounds);
				source.Effect.SetFloat(RateId, drawn ? Config.Rate * _strength : 0f);
				if (!drawn) continue;

				source.Effect.SetVector3(CenterId, bounds.center);
				source.Effect.SetVector3(SizeId, bounds.size);
			}
		}

		// Re-resolved rather than resolved once: a player who sits down after this began sheds too.
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
				if (skins.Length > 0) _sources.Add(CreateSource(rig.name, skins));
			}
		}

		private Source CreateSource(string bodyName, SkinnedMeshRenderer[] skins)
		{
			var go = new GameObject($"Ghost_{bodyName}");
			go.transform.SetParent(_container.transform, false);

			var effect = go.AddComponent<VisualEffect>();
			effect.visualEffectAsset = Config.VisualEffect;
			effect.SetVector4(TintId, Config.Tint);
			effect.SetFloat(RateId, 0f);

			return new Source { Skins = skins, Effect = effect };
		}

		// The box around whatever of the body is drawn, so a hidden piece (a head gone with its owner) sheds nothing.
		private static bool TryGetDrawnBounds(SkinnedMeshRenderer[] skins, out Bounds bounds)
		{
			bounds = default;
			var any = false;

			foreach (var skin in skins)
			{
				if (!skin || !skin.enabled || !skin.gameObject.activeInHierarchy) continue;

				if (any) bounds.Encapsulate(skin.bounds);
				else bounds = skin.bounds;

				any = true;
			}

			return any;
		}

		private void Release()
		{
			_sources.Clear();
			_resolved.Clear();

			if (_container) Destroy(_container);
			_container = null;
		}
	}
}
