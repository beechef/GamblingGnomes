using System.Collections.Generic;
using DG.Tweening;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// A head that swells, or a body that shrinks. Written through PlayerBoneScaleController rather than
	// onto the bone, because every clip carries a constant scale curve for the whole skeleton and the
	// Animator would erase a direct write on the next frame.
	[CreateAssetMenu(fileName = "Hallucination_Scale", menuName = "Game/Poker/Hallucination/Scale")]
	public class PokerHallucinationScaleEffect : PokerHallucinationEffect
	{
		[Tooltip("Whose bone. A player target with a bone picked is what makes this one part rather than a whole body.")]
		[Required]
		[SerializeField] private PokerHallucinationTarget _target;

		[Tooltip("Multiplied onto the bone's authored scale. Two is twice the size, half is half.")]
		[SerializeField] private Vector3 _multiplier = new(2f, 2f, 2f);

		[Tooltip("Seconds it takes to grow. Snapping reads as a bug rather than as a symptom.")]
		[SerializeField] private float _duration = 1.2f;

		[SerializeField] private Ease _ease = Ease.OutBack;

		private struct Bound
		{
			public Transform Bone;
			public PlayerBoneScaleController Controller;
		}

		// Bone and its controller cached together at resolve time. The tween pushes every frame, and a
		// GetComponentInParent in that path is the lookup the no-polling rule exists to keep out of it.
		private readonly List<Bound> _bound = new();
		private readonly List<Transform> _resolved = new();

		private PokerPlayer _viewer;
		private Tween _tween;
		private float _progress;

		protected override void OnBegin(PokerPlayer viewer)
		{
			_viewer = viewer;
			_target?.Subscribe(viewer, Reapply);

			Reapply();

			_tween?.Kill();
			_tween = DOVirtual.Float(0f, 1f, _duration, value =>
				{
					_progress = value;
					Push();
				})
				.SetEase(_ease)
				.SetUpdate(true);
		}

		protected override void OnEnd(PokerPlayer viewer)
		{
			_target?.Unsubscribe(viewer, Reapply);

			_tween?.Kill();
			_tween = DOVirtual.Float(_progress, 0f, _duration, value =>
				{
					_progress = value;
					Push();
				})
				.SetEase(_ease)
				.SetUpdate(true)
				.OnComplete(Release);
		}

		// Re-resolved rather than resolved once: a player who sits down after this began has a body that
		// should be swelling too, and the target is what knows when that happened.
		private void Reapply()
		{
			Release();

			if (!_target || !_viewer) return;

			_target.Collect(_viewer, _resolved);

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
			var scale = Vector3.Lerp(Vector3.one, _multiplier, _progress);

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
