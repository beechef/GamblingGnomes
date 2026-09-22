using DG.Tweening;
using Game.Runtime.Player.Camera;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	public class PokerHallucinationFovBehaviour : PokerHallucinationEffectBehaviour<PokerHallucinationFovEffect>
	{
		private PlayerCameraFovController _controller;
		private PlayerCameraFovModifier _modifier;
		private Tween _tween;

		// The ease out is the effect leaving, so the modifier has to stay until the view is back.
		protected override float LingerSeconds => EaseDuration;

		protected override void OnBegin()
		{
			_controller = Viewer ? Viewer.GetComponentInChildren<PlayerCameraFovController>(true) : null;
			if (!_controller)
			{
				Debug.LogWarning($"[{nameof(PokerHallucinationFovBehaviour)}] {Viewer} has no {nameof(PlayerCameraFovController)}; the field of view cannot change.", this);
				return;
			}

			_modifier = _controller.Add(Config.FieldOfView);
			FadeTo(1f);
		}

		protected override void OnEnd() => FadeTo(0f);

		protected override void OnDisposed()
		{
			_tween?.Kill();

			if (_controller && _modifier != null) _controller.Remove(_modifier);
			_modifier = null;
		}

		private void FadeTo(float weight)
		{
			if (_modifier == null) return;

			var modifier = _modifier;
			_tween?.Kill();
			_tween = DOTween.To(() => modifier.Weight, value => modifier.Weight = value, weight, EaseDuration)
				.SetEase(Config.Ease)
				.SetUpdate(true);
		}
	}
}
