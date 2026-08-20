using DG.Tweening;
using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// Being drunk, drawn on the drinker's own screen and nowhere else. The server says who is swimming and
	// for how long; what that looks like is entirely this view's business, so it can be restyled without a
	// word of it reaching the table.
	//
	// Drawn on the canvas rather than as a post-process, for the same reason the blackout is: the canvas is
	// Screen Space Overlay and is drawn after the pipeline has finished, so a volume could not touch the
	// HUD at all — and half of what a player is reading is on the HUD. Bending the world as well wants a
	// local volume beside this, which is a second component and a render pipeline reference this assembly
	// does not carry today.
	public class UIPokerDrunkScreen : UIPokerView
	{
		[Header("Screen")]
		[Required]
		[Tooltip("Faded rather than switched, and never deactivated: a tween cannot run on a disabled object, so sobering up would happen by teleporting.")]
		[SerializeField] private CanvasGroup _screen;

		[Tooltip("How strong the tint gets over the HUD. Short of one leaves everything readable, which is the point — drunk is meant to be a handicap, not a blindfold.")]
		[Range(0f, 1f)]
		[SerializeField] private float _tint = 0.55f;

		[Header("Fade")]
		[Tooltip("How fast it comes on. Quick, because it is a swallow rather than a slow evening.")]
		[SerializeField] private float _fadeInSeconds = 0.25f;

		[SerializeField] private Ease _fadeInEase = Ease.OutQuad;

		[Tooltip("How fast it wears off. Slower than it came on, the way it actually goes.")]
		[SerializeField] private float _fadeOutSeconds = 0.8f;

		[SerializeField] private Ease _fadeOutEase = Ease.InOutSine;

		private PokerDrinkController _drink;
		private Tween _fadeTween;
		private bool _drunk;

		// The clock runs out without anybody writing a value, so this is the rare thing a view has to watch
		// rather than subscribe to.
		protected override bool WantsTick => _drink;

		private void Awake() => Apply(false, true);

		private void OnDestroy() => _fadeTween?.Kill();

		protected override void OnBind()
		{
			_drink = LocalPlayer ? LocalPlayer.GetComponentInChildren<PokerDrinkController>() : null;

			// A client arriving mid-round is already inside it, so the state is read as it stands rather
			// than waited on — and it snaps rather than fading, because it has nothing to fade from.
			_drunk = _drink && _drink.IsDrunk;

			Apply(_drunk, true);
		}

		protected override void OnUnbind()
		{
			_drink = null;
			_drunk = false;

			Apply(false, true);
		}

		protected override void OnTick()
		{
			var drunk = _drink && _drink.IsDrunk;
			if (drunk == _drunk) return;

			_drunk = drunk;

			Apply(drunk, false);
		}

		private void Apply(bool drunk, bool immediate)
		{
			_fadeTween?.Kill();
			_fadeTween = null;

			var target = drunk ? Mathf.Clamp01(_tint) : 0f;

			if (_screen)
			{
				// It never takes the pointer: the hand carries on underneath and the pad is still being
				// pressed, by players who are simply having trouble aiming at it.
				_screen.blocksRaycasts = false;
				_screen.interactable = false;
			}

			var duration = immediate ? 0f : drunk ? _fadeInSeconds : _fadeOutSeconds;

			if (duration <= 0f)
			{
				SetWeight(target);
				return;
			}

			var from = _screen ? _screen.alpha : target;

			// DOTween's UI module is not in the project, so the group is driven by value the way every other
			// fade here is.
			_fadeTween = DOTween.To(() => from, value =>
				{
					from = value;
					SetWeight(value);
				}, target, duration)
				.SetEase(drunk ? _fadeInEase : _fadeOutEase)
				.SetUpdate(true)
				.OnComplete(() => _fadeTween = null);
		}

		private void SetWeight(float value)
		{
			if (_screen) _screen.alpha = value;
		}
	}
}
