using DG.Tweening;
using Game.Runtime.GameMode.Poker.Modules;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The dark itself. The module decides when the lights go and for how long; this only draws the answer,
	// so the event can be restyled — a slower fade, a hint of light left, a different colour — without a
	// word of it reaching the server or the other clients.
	//
	// A panel over everything rather than the scene's lights switched off: it covers the HUD as well as the
	// table, which is what "everything goes dark" has to mean when half of what a player is reading is
	// drawn on the canvas. It also lands identically whatever the lighting setup grows into.
	public class UIPokerBlackoutScreen : UIPokerView
	{
		[Header("Screen")]
		[Required]
		[Tooltip("Faded rather than switched, and never deactivated: a tween cannot run on a disabled object, so the lights would come back on by teleporting.")]
		[SerializeField] private CanvasGroup _screen;

		[Header("Fade")]
		[Tooltip("How fast the lights go. Quick, but not instant — a cut to black reads as a dropped frame rather than as a power cut.")]
		[SerializeField] private float _fadeOutSeconds = 0.18f;

		[SerializeField] private Ease _fadeOutEase = Ease.InQuad;

		[Tooltip("How fast they come back. Slower than they went, the way eyes and filaments both work.")]
		[SerializeField] private float _fadeInSeconds = 0.45f;

		[SerializeField] private Ease _fadeInEase = Ease.OutQuad;

		[Tooltip("How dark it gets. Short of one leaves the shapes of the table just about readable.")]
		[Range(0f, 1f)]
		[SerializeField] private float _darkness = 1f;

		private PokerBlackoutModule _module;
		private Tween _fadeTween;

		private void Awake() => Apply(false, true);

		private void OnDestroy() => _fadeTween?.Kill();

		protected override void OnBind()
		{
			_module = GameMode.FindModule<PokerBlackoutModule>();
			if (_module == null) return;

			_module.IsDark.OnValueChanged += HandleDarkChanged;

			// A client arriving mid blackout is already inside it, so the state is read as it stands rather
			// than waited on — and it snaps rather than fading, because it has nothing to fade from.
			Apply(_module.IsDark.Value, true);
		}

		protected override void OnUnbind()
		{
			if (_module != null) _module.IsDark.OnValueChanged -= HandleDarkChanged;

			_module = null;

			Apply(false, true);
		}

		private void HandleDarkChanged(bool previous, bool current) => Apply(current, false);

		private void Apply(bool dark, bool immediate)
		{
			if (!_screen) return;

			_fadeTween?.Kill();
			_fadeTween = null;

			var target = dark ? Mathf.Clamp01(_darkness) : 0f;

			// It never takes the pointer: the hand carries on underneath and the pad is still being pressed,
			// by players who simply cannot see it.
			_screen.blocksRaycasts = false;
			_screen.interactable = false;

			if (immediate)
			{
				_screen.alpha = target;
				return;
			}

			var duration = dark ? _fadeOutSeconds : _fadeInSeconds;
			if (duration <= 0f)
			{
				_screen.alpha = target;
				return;
			}

			// DOTween's UI module is not in the project, so the group is driven by value the way every other
			// fade here is.
			_fadeTween = DOTween.To(() => _screen.alpha, alpha => _screen.alpha = alpha, target, duration)
				.SetEase(dark ? _fadeOutEase : _fadeInEase)
				.SetUpdate(true)
				.OnComplete(() => _fadeTween = null);
		}
	}
}
