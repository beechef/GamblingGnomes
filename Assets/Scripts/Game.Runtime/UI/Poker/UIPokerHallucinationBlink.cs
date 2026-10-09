using DG.Tweening;
using Game.Runtime.GameMode.Poker.Hallucination;
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
	// The lids are the pacing's blink video (UIVideoBlink): closed onto the shut frame exactly as the rungs
	// land, held there, and opened from the reopen frame. Every one of those times is the controller's, read
	// off the video, so nothing here keeps its own.
	public class UIPokerHallucinationBlink : UIPokerView
	{
		[Header("Eyelids")]
		[SerializeField] private UIVideoBlink _eyelids;

		private PokerHallucinationController _controller;
		private Tween _open;

		private void OnDestroy() => _open?.Kill();

		protected override void OnBind()
		{
			// Reached from the local player rather than serialized, because the player is spawned and this
			// view is in a HUD prefab that cannot hold a reference to something that does not exist yet.
			var local = LocalPlayer;
			_controller = local ? local.GetComponentInChildren<PokerHallucinationController>(true) : null;

			if (_controller) _controller.OnTransitionStarted += HandleTransitionStarted;

			if (_controller && _eyelids) _eyelids.Prepare(_controller.BlinkVideo);
		}

		protected override void OnUnbind()
		{
			if (_controller) _controller.OnTransitionStarted -= HandleTransitionStarted;

			_controller = null;

			_open?.Kill();
			_open = null;

			if (_eyelids) _eyelids.Hide();
		}

		// One blink per transition, restarted rather than layered: the controller already folds a change
		// arriving mid-blink into the beat that is running, so a second blink here would be drawing a beat
		// that is not happening.
		private void HandleTransitionStarted()
		{
			if (!_controller || !_eyelids || !_controller.BlinkVideo || _controller.TransitionDuration <= 0f) return;

			var clip = _controller.BlinkVideo;
			var reopen = _controller.BlinkReopenTime;
			var openDuration = _controller.OpenDuration;

			_eyelids.Close(clip, _controller.BlinkShutTime, _controller.ApplyDelay);

			// Held shut while the effects ease into place, so the eye opens on a room that has finished changing.
			_open?.Kill();
			_open = DOVirtual.DelayedCall(_controller.ApplyDelay + _controller.HoldDuration,
				() => _eyelids.Open(clip, reopen, openDuration), ignoreTimeScale: true).SetTarget(this);
		}
	}
}
