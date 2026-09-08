using DG.Tweening;
using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// A hallucination drawn as post-processing. The profile is the whole effect, so a new one is an
	// artist making a Volume Profile rather than anybody writing code — which is the point of the rungs
	// being pools: twelve of these and the ladder is full.
	//
	// The Volume is created rather than placed: an effect belongs to one viewer's screen and there is
	// nothing in the scene it could sensibly be authored on.
	[CreateAssetMenu(fileName = "Hallucination_Volume", menuName = "Game/Poker/Hallucination/Volume")]
	public class PokerHallucinationVolumeEffect : PokerHallucinationEffect
	{
		[Tooltip("What the world looks like at this rung.")]
		[Required]
		[SerializeField] private VolumeProfile _profile;

		[Tooltip("How strong it gets. Below one, several rungs can stack without the screen going white.")]
		[PropertyRange(0f, 1f)]
		[SerializeField] private float _weight = 0.6f;

		[Tooltip("Seconds it takes to arrive. A hallucination that snaps on reads as a bug rather than as a symptom.")]
		[SerializeField] private float _fadeDuration = 1.5f;

		[Tooltip("Where this rung sits against the others. Higher rungs should sit above lower ones.")]
		[SerializeField] private int _priority = 10;

		// One field, because the controller clones this asset per rung: a clone is already one effect on
		// one screen, so there is nothing left to key by.
		private Volume _volume;

		protected override void OnBegin(PokerPlayer viewer)
		{
			if (!_profile || _volume) return;

			var host = new GameObject($"Hallucination_{name}");
			var volume = host.AddComponent<Volume>();
			volume.isGlobal = true;
			volume.priority = _priority;
			volume.profile = _profile;
			volume.weight = 0f;

			_volume = volume;

			DOTween.To(() => volume.weight, w => volume.weight = w, _weight, _fadeDuration)
				.SetTarget(volume)
				.SetUpdate(true);
		}

		protected override void OnEnd(PokerPlayer viewer)
		{
			var volume = _volume;
			_volume = null;
			if (!volume) return;

			DOTween.Kill(volume);
			DOTween.To(() => volume.weight, w => volume.weight = w, 0f, _fadeDuration)
				.SetTarget(volume)
				.SetUpdate(true)
				.OnComplete(() => { if (volume) Destroy(volume.gameObject); });
		}
	}
}
