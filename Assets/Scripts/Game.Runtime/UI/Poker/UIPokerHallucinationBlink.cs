using DG.Tweening;
using Game.Runtime.GameMode.Poker.Hallucination;
using Game.Runtime.GameMode.Poker.Player;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The screen shutting and opening as a rung is climbed or lost. It exists to cover the swap: a room
	// that changes in front of an open eye reads as a glitch, and one that is different when the eye opens
	// reads as the mushrooms.
	//
	// Not one of the effects in a pool. A blink happens on every rung change whatever that rung drew, so
	// putting it in a pool would make it something a player might or might not get.
	//
	// It never takes the pointer. The hand carries on underneath, and a player who was mid-press when the
	// bar moved has not stopped pressing.
	public class UIPokerHallucinationBlink : UIPokerView
	{
		[Header("Screen")]
		[Tooltip("Faded to opaque and back. Placeholder for whatever eyelids the art lands on.")]
		[SerializeField] private CanvasGroup _screen;

		[SerializeField] private Ease _closeEase = Ease.InQuad;

		[SerializeField] private Ease _openEase = Ease.OutQuad;

		private PokerHallucinationController _controller;
		private Sequence _blink;

		private void Awake()
		{
			if (_screen) _screen.alpha = 0f;
		}

		private void OnDestroy() => _blink?.Kill();

		protected override void OnBind()
		{
			// Reached from the local player rather than serialized, because the player is spawned and this
			// view is in a HUD prefab that cannot hold a reference to something that does not exist yet.
			var local = PokerPlayer.Local;
			_controller = local ? local.GetComponentInChildren<PokerHallucinationController>(true) : null;

			if (_controller) _controller.OnTransitionStarted += HandleTransitionStarted;
		}

		protected override void OnUnbind()
		{
			if (_controller) _controller.OnTransitionStarted -= HandleTransitionStarted;

			_controller = null;

			_blink?.Kill();
			_blink = null;

			if (_screen) _screen.alpha = 0f;
		}

		// One blink per transition, restarted rather than layered: the controller already folds a change
		// arriving mid-blink into the beat that is running, so a second sequence here would be drawing a
		// beat that is not happening.
		private void HandleTransitionStarted()
		{
			if (!_screen || !_controller) return;

			var half = _controller.TransitionDuration * 0.5f;
			if (half <= 0f) return;

			_screen.blocksRaycasts = false;
			_screen.interactable = false;

			_blink?.Kill();
			_blink = DOTween.Sequence()
				// Driven by value rather than DOFade: DOTween's UI module is not in this project, which is
				// the same reason UIPokerBlackoutScreen drives its own group this way.
				.Append(DOTween.To(() => _screen.alpha, alpha => _screen.alpha = alpha, 1f, half).SetEase(_closeEase))
				.Append(DOTween.To(() => _screen.alpha, alpha => _screen.alpha = alpha, 0f, half).SetEase(_openEase))
				.SetUpdate(true)
				.SetTarget(_screen);
		}
	}
}
