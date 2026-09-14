using System.Collections.Generic;
using DG.Tweening;
using Game.Runtime.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	public class PokerHallucinationScaleBehaviour : PokerHallucinationEffectBehaviour<PokerHallucinationScaleEffect>
	{
		// The modifiers this effect holds, one per bone it reached. Held rather than looked up: the tween
		// pushes every frame, and both a GetComponentInParent and a search by handle in that path are the
		// lookups the no-polling rule exists to keep out of it.
		private readonly List<PlayerBoneScaleModifier> _bound = new();
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

			var neutral = Neutral;

			foreach (var bone in _resolved)
			{
				if (!bone) continue;

				var controller = PlayerRigController.FindOnBody<PlayerBoneScaleController>(bone);

				// Loud, because a body with no scale controller is a setup that cannot possibly work and
				// skipping it quietly is what let this run for weeks resolving bones and moving nothing.
				if (!controller)
				{
					Debug.LogWarning($"[{name}] '{bone.name}' belongs to no body carrying a {nameof(PlayerBoneScaleController)}; nothing will be scaled.", bone);
					continue;
				}

				// Added at its own neutral: a modifier that exists but has not grown yet must leave the bone
				// exactly as it was, or every effect would jump to full strength the instant it resolved.
				_bound.Add(controller.Add(bone, neutral, Config.Mode));
			}

			Push();
		}

		// What this effect leaves the bone alone at. A multiply that has not started is 1 and an add that
		// has not started is 0, so a mode carries its own neutral — travelling from the wrong one means an
		// effect at zero progress is already doing something.
		private Vector3 Neutral => Config.Mode == PlayerBoneScaleMode.Multiply ? Vector3.one : Vector3.zero;

		// A field write per bone. Nothing is searched for and nothing is allocated, which is what the tween
		// calling this every frame wants.
		private void Push()
		{
			var scale = Vector3.Lerp(Neutral, Config.Scale, _progress);

			foreach (var modifier in _bound)
			{
				if (modifier != null) modifier.Value = scale;
			}
		}

		private void Release()
		{
			foreach (var modifier in _bound)
			{
				modifier?.Remove();
			}

			_bound.Clear();
			_resolved.Clear();
		}
	}
}
