using DG.Tweening;
using Game.Runtime.Audio;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// What a death roll sounds like, beat by beat, off the meter's skull: the sweep plays while it moves with
	// its parameter running 0 to 1 over the sweep, a near miss gets its own sting late in the sweep, and the
	// stop plays the result then the finish.
	public class UIPokerRollSoundSequence : MonoBehaviour
	{
		[Required]
		[SerializeField] private UIPokerHallucinationMeter _meter;

		[Tooltip("Silent for this screen's own player. On over-head meters, whose owner already hears the HUD bar's.")]
		[SerializeField] private bool _skipOwnPlayer;

		[Header("Sweep")]
		[Tooltip("Plays while the skull sweeps.")]
		[SerializeField] private AudioEvent _sweepSound;

		[Tooltip("Driven 0 to 1 by the sweep's normalized time.")]
		[FMODUnity.ParamRef]
		[SerializeField] private string _sweepParameter = "Parameter 2";

		[Header("Near miss")]
		[Tooltip("Played when the roll lands this close to the rate, death or not.")]
		[SerializeField] private AudioEvent _nearMissSound;

		[Tooltip("A roll is close when |rate - roll| is under this.")]
		[Min(0)]
		[SerializeField] private int _closeDistance = 5;

		[PropertyRange(0f, 1f)]
		[SerializeField] private float _nearMissAt = 0.9f;

		[Header("Stop")]
		[Tooltip("The skull stopped on a death.")]
		[SerializeField] private AudioEvent _deathSound;

		[Tooltip("The skull stopped short of a death.")]
		[SerializeField] private AudioEvent _survivedSound;

		[Tooltip("Played after the result, closing the roll.")]
		[SerializeField] private AudioEvent _finishSound;

		private AudioHandle _sweep;
		private Tween _tween;

		private void OnEnable()
		{
			_meter.OnRollSweepStarted += HandleSweepStarted;
			_meter.OnRollSettled += HandleRollSettled;
		}

		private void OnDisable()
		{
			_meter.OnRollSettled -= HandleRollSettled;
			_meter.OnRollSweepStarted -= HandleSweepStarted;

			StopSweep();
		}

		private void HandleSweepStarted(float duration, int roll, int rate)
		{
			StopSweep();

			var audio = AudioManager.Instance;
			if (!audio || IsSkipped) return;

			_sweep = audio.Play(_sweepSound, transform);
			audio.SetParameter(_sweep, _sweepParameter, 0f);

			var nearMiss = _nearMissSound && Mathf.Abs(rate - roll) < _closeDistance;
			var stung = false;

			_tween = DOTween.To(() => 0f, normalized =>
				{
					audio.SetParameter(_sweep, _sweepParameter, normalized);

					if (!nearMiss || stung || normalized < _nearMissAt) return;

					stung = true;
					audio.PlayOneShotAttached(_nearMissSound, transform);
				}, 1f, duration)
				.SetEase(Ease.Linear)
				.SetLink(gameObject);
		}

		private void HandleRollSettled(int roll, bool fatal)
		{
			StopSweep();

			var audio = AudioManager.Instance;
			if (!audio || IsSkipped) return;

			var result = fatal ? _deathSound : _survivedSound;
			if (result) audio.PlayOneShotAttached(result, transform);
			if (_finishSound) audio.PlayOneShotAttached(_finishSound, transform);
		}

		private bool IsSkipped => _skipOwnPlayer && _meter.Player && _meter.Player.IsOwner;

		private void StopSweep()
		{
			_tween?.Kill();
			_tween = null;

			if (AudioManager.Instance) AudioManager.Instance.Stop(_sweep);
			_sweep = default;
		}
	}
}
