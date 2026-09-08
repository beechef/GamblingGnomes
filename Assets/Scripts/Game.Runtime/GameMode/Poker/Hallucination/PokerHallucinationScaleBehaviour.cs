using System.Collections.Generic;
using DG.Tweening;
using Game.Runtime.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	public class PokerHallucinationScaleBehaviour : PokerHallucinationEffectBehaviour<PokerHallucinationScaleEffect>
	{
		private struct Bound
		{
			public Transform Bone;
			public PlayerBoneScaleController Controller;
		}

		// Bone and its controller cached together at resolve time. The tween pushes every frame, and a
		// GetComponentInParent in that path is the lookup the no-polling rule exists to keep out of it.
		private readonly List<Bound> _bound = new();
		private readonly List<Transform> _resolved = new();

		private Tween _tween;
		private float _progress;

		// The shrink back is the whole reason a rung coming off is watchable, so the host waits it out.
		protected override float LingerSeconds => Config ? Config.Duration : 0f;

		protected override void OnBegin()
		{
			Config.Target?.Subscribe(Viewer, Reapply);

			Reapply();

			_tween?.Kill();
			_tween = DOVirtual.Float(0f, 1f, Config.Duration, value =>
				{
					_progress = value;
					Push();
				})
				.SetEase(Config.Ease)
				.SetUpdate(true);
		}

		protected override void OnEnd()
		{
			Config.Target?.Unsubscribe(Viewer, Reapply);

			_tween?.Kill();
			_tween = DOVirtual.Float(_progress, 0f, Config.Duration, value =>
				{
					_progress = value;
					Push();
				})
				.SetEase(Config.Ease)
				.SetUpdate(true)
				.OnComplete(Release);
		}

		// A bone left part-way through a shrink is a body that never comes home.
		protected override void OnDisposed()
		{
			_tween?.Kill();
			Release();
		}

		// Re-resolved rather than resolved once: a player who sits down after this began has a body that
		// should be swelling too, and the target is what knows when that happened.
		private void Reapply()
		{
			Release();

			if (!Config || !Config.Target) return;

			Config.Target.Collect(Viewer, _resolved);

			foreach (var bone in _resolved)
			{
				if (!bone) continue;

				var controller = bone.GetComponentInParent<PlayerBoneScaleController>(true);
				if (!controller) continue;

				_bound.Add(new Bound { Bone = bone, Controller = controller });
			}

			Push();
		}

		private void Push()
		{
			var scale = Vector3.Lerp(Vector3.one, Config.Multiplier, _progress);

			foreach (var bound in _bound)
			{
				if (!bound.Bone || !bound.Controller) continue;

				bound.Controller.Set(this, bound.Bone, scale);
			}
		}

		private void Release()
		{
			foreach (var bound in _bound)
			{
				if (bound.Controller) bound.Controller.Clear(this);
			}

			_bound.Clear();
			_resolved.Clear();
		}
	}
}
