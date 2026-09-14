using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// The Volume sits on the rung's own object rather than one made beside it, so what a player is seeing
	// is one line in the hierarchy with the profile and the live weight on it.
	public class PokerHallucinationVolumeBehaviour : PokerHallucinationEffectBehaviour<PokerHallucinationVolumeEffect>
	{
		private Volume _volume;

		// The fade out is the effect leaving; killing the object at the top of it would snap the room back.
		protected override float LingerSeconds => Config ? Config.FadeDuration : 0f;

		protected override void OnBegin()
		{
			if (!Config.Profile) return;

			_volume = gameObject.AddComponent<Volume>();
			_volume.isGlobal = true;
			_volume.priority = Config.Priority;
			_volume.profile = Config.Profile;
			_volume.weight = 0f;

			Fade(Config.Weight);
		}

		protected override void OnEnd() => Fade(0f);

		protected override void OnDisposed()
		{
			if (_volume) DOTween.Kill(_volume);
		}

		private void Fade(float target)
		{
			if (!_volume) return;

			DOTween.Kill(_volume);
			DOTween.To(() => _volume.weight, w => _volume.weight = w, target, Config.FadeDuration)
				.SetTarget(_volume)
				.SetUpdate(true);
		}
	}
}
