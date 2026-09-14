using DG.Tweening;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	public class PokerHallucinationFullscreenBehaviour : PokerHallucinationEffectBehaviour<PokerHallucinationFullscreenEffect>
	{
		private Material _instance;
		private Material _restore;
		private int _property;

		// The fade out is the effect leaving, so the pass has to keep drawing until it is over.
		protected override float LingerSeconds => Config ? Config.FadeDuration : 0f;

		protected override void OnBegin()
		{
			if (!Config.Feature || !Config.Material) return;

			// Cloned, because a renderer feature holds its material by reference and writing to the asset
			// would leave the strength baked into the project the next time anybody opened it.
			_instance = Instantiate(Config.Material);
			_instance.name = Config.Material.name;
			_property = Shader.PropertyToID(Config.StrengthProperty);
			_instance.SetFloat(_property, 0f);

			if (Config.Feature is UnityEngine.Rendering.Universal.FullScreenPassRendererFeature pass)
			{
				_restore = pass.passMaterial;
				pass.passMaterial = _instance;
			}

			Config.Feature.SetActive(true);

			Fade(Config.Strength);
		}

		protected override void OnEnd() => Fade(0f);

		protected override void OnDisposed()
		{
			if (_instance) DOTween.Kill(_instance);

			if (Config && Config.Feature)
			{
				Config.Feature.SetActive(false);

				// Put the asset back, or the feature is left holding a material that is about to be destroyed
				// and the next effect to run finds a hole where its pass used to be.
				if (Config.Feature is UnityEngine.Rendering.Universal.FullScreenPassRendererFeature pass) pass.passMaterial = _restore;
			}

			if (_instance) Destroy(_instance);
		}

		private void Fade(float target)
		{
			if (!_instance) return;

			DOTween.Kill(_instance);
			DOTween.To(() => _instance.GetFloat(_property), value => _instance.SetFloat(_property, value), target, Config.FadeDuration)
				.SetTarget(_instance)
				.SetUpdate(true);
		}
	}
}
