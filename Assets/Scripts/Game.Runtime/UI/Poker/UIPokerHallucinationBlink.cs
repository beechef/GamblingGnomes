using DG.Tweening;
using Game.Runtime.GameMode.Poker.Hallucination;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Game.Runtime.UI.Poker
{
	// The screen shutting and opening as a rung is climbed or lost. It exists to cover the swap: a room
	// that changes in front of an open eye reads as a glitch, and one that is different when the eye opens
	// reads as the mushrooms.
	//
	// Not one of the effects in a pool. A blink happens on every rung change whatever that rung drew, so
	// putting it in a pool would make it something a player might or might not get.
	//
	// The lids are a video (the pacing's Blink Video) drawn through UI_LumaMask: black in it is lid, the rest
	// is seen through. It plays up to the shut frame, is put on that frame exactly as the rungs land, waits
	// there for the hold, and plays on from the reopen frame. Every one of those times is the controller's,
	// read off the video, so nothing here keeps its own.
	//
	// It never takes the pointer. The hand carries on underneath, and a player who was mid-press when the
	// bar moved has not stopped pressing.
	public class UIPokerHallucinationBlink : UIPokerView
	{
		[Header("Eyelids")]
		[Tooltip("Full-screen image wearing a UI_LumaMask material. Off between blinks.")]
		[SerializeField] private RawImage _eyelids;

		[Tooltip("Plays the blink video into a texture this makes; the clip is set from the pacing.")]
		[SerializeField] private VideoPlayer _video;

		[Tooltip("The lids' texture size as a fraction of the video's. They are soft shapes; a quarter of the pixels is plenty.")]
		[Range(0.1f, 1f)]
		[SerializeField] private float _resolutionScale = 0.5f;

		private PokerHallucinationController _controller;
		private RenderTexture _texture;
		private Sequence _blink;

		private void Awake()
		{
			if (_eyelids)
			{
				_eyelids.raycastTarget = false;
				_eyelids.enabled = false;
			}

			if (_video)
			{
				_video.playOnAwake = false;
				_video.isLooping = false;
				_video.renderMode = VideoRenderMode.RenderTexture;
				_video.audioOutputMode = VideoAudioOutputMode.None;
				_video.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
			}
		}

		private void OnDestroy()
		{
			_blink?.Kill();

			if (_texture)
			{
				_texture.Release();
				Destroy(_texture);
			}
		}

		protected override void OnBind()
		{
			// Reached from the local player rather than serialized, because the player is spawned and this
			// view is in a HUD prefab that cannot hold a reference to something that does not exist yet.
			var local = LocalPlayer;
			_controller = local ? local.GetComponentInChildren<PokerHallucinationController>(true) : null;

			if (_controller) _controller.OnTransitionStarted += HandleTransitionStarted;

			// Ready before the first blink, so the lids start moving on the frame the change does.
			PrepareVideo();
		}

		protected override void OnUnbind()
		{
			if (_controller) _controller.OnTransitionStarted -= HandleTransitionStarted;

			_controller = null;

			StopBlink();
		}

		private void PrepareVideo()
		{
			var clip = _controller ? _controller.BlinkVideo : null;
			if (!_video || !_eyelids || !clip) return;

			if (!_texture)
			{
				var width = Mathf.Max(1, Mathf.RoundToInt(clip.width * _resolutionScale));
				var height = Mathf.Max(1, Mathf.RoundToInt(clip.height * _resolutionScale));
				_texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { name = "HallucinationBlink" };
				_texture.Create();
			}

			ClearTexture();

			_video.targetTexture = _texture;
			_eyelids.texture = _texture;

			if (_video.clip != clip) _video.clip = clip;
			_video.Prepare();
		}

		// White is an open eye, so a texture the video has not drawn into yet never flashes a shut one.
		private void ClearTexture()
		{
			var previous = RenderTexture.active;
			RenderTexture.active = _texture;
			GL.Clear(false, true, Color.white);
			RenderTexture.active = previous;
		}

		// One blink per transition, restarted rather than layered: the controller already folds a change
		// arriving mid-blink into the beat that is running, so a second sequence here would be drawing a
		// beat that is not happening.
		private void HandleTransitionStarted()
		{
			if (!_controller || !_video || !_eyelids || !_controller.BlinkVideo) return;
			if (_controller.TransitionDuration <= 0f) return;

			StopBlink();
			PrepareVideo();

			var shut = _controller.ApplyDelay;
			var reopen = _controller.BlinkReopenTime;

			_eyelids.enabled = true;
			_video.time = 0d;
			_video.Play();

			_blink = DOTween.Sequence()
				.AppendInterval(shut)
				// Put on the shut frame however the playback drifted, on the very moment the controller
				// switches the effects.
				.AppendCallback(() =>
				{
					_video.Pause();
					_video.time = shut;
				})
				// Held shut while the effects ease into place, so the eye opens on a room that has finished changing.
				.AppendInterval(_controller.HoldDuration)
				.AppendCallback(() =>
				{
					_video.time = reopen;
					_video.Play();
				})
				.AppendInterval(_controller.OpenDuration)
				.AppendCallback(StopBlink)
				.SetUpdate(true)
				.SetTarget(this);
		}

		private void StopBlink()
		{
			_blink?.Kill();
			_blink = null;

			if (_video) _video.Stop();
			if (_eyelids) _eyelids.enabled = false;
		}
	}
}
