using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Game.Runtime.UI
{
	// Eyelids drawn from a video: black in it is lid, the rest is seen through (UI_LumaMask on the image).
	// Closing plays it from the start and pins it on the shut frame, where it stays until told to open;
	// opening plays it on from the reopen frame and hides it at the end. When those happen is the caller's,
	// so a blink timed by a tween and one timed by a replicated flag draw the same lids.
	//
	// It never takes the pointer.
	public class UIVideoBlink : MonoBehaviour
	{
		[Tooltip("Full-screen image wearing a UI_LumaMask material. Off between blinks.")]
		[SerializeField] private RawImage _eyelids;

		[Tooltip("Plays the clip into a texture this makes.")]
		[SerializeField] private VideoPlayer _video;

		[Tooltip("The lids' texture size as a fraction of the video's. They are soft shapes; a quarter of the pixels is plenty.")]
		[Range(0.1f, 1f)]
		[SerializeField] private float _resolutionScale = 0.5f;

		private RenderTexture _texture;
		private Tween _pending;

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
			_pending?.Kill();

			if (!_texture) return;

			_texture.Release();
			Destroy(_texture);
		}

		// Ready ahead of the first blink, so the lids start moving on the frame they are asked to.
		public void Prepare(VideoClip clip)
		{
			if (!_video || !_eyelids || !clip) return;

			if (!_texture)
			{
				var width = Mathf.Max(1, Mathf.RoundToInt(clip.width * _resolutionScale));
				var height = Mathf.Max(1, Mathf.RoundToInt(clip.height * _resolutionScale));
				_texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { name = "VideoBlink" };
				_texture.Create();
				Fill(Color.white);
			}

			_video.targetTexture = _texture;
			_eyelids.texture = _texture;

			if (_video.clip != clip) _video.clip = clip;
			if (!_video.isPrepared) _video.Prepare();
		}

		// Plays to the shut frame and stays there; put on it exactly at shutTime however playback drifted.
		public void Close(VideoClip clip, float shutTime)
		{
			if (!Begin(clip)) return;

			Fill(Color.white);
			_video.time = 0d;
			_video.Play();

			_pending = DOVirtual.DelayedCall(shutTime, () =>
			{
				_video.Pause();
				_video.time = shutTime;
			}, ignoreTimeScale: true).SetTarget(this);
		}

		// Shut at once, for a screen arriving while the eye is already closed.
		public void SnapShut(VideoClip clip)
		{
			if (!Begin(clip)) return;

			_video.Stop();
			Fill(Color.black);
		}

		public void Open(VideoClip clip, float reopenTime, float openDuration)
		{
			if (!Begin(clip)) return;

			_video.time = reopenTime;
			_video.Play();

			_pending = DOVirtual.DelayedCall(openDuration, Hide, ignoreTimeScale: true).SetTarget(this);
		}

		public void Hide()
		{
			_pending?.Kill();
			_pending = null;

			if (_video) _video.Stop();
			if (_eyelids) _eyelids.enabled = false;
		}

		private bool Begin(VideoClip clip)
		{
			_pending?.Kill();
			_pending = null;

			if (!_video || !_eyelids || !clip) return false;

			Prepare(clip);
			_eyelids.enabled = true;
			return true;
		}

		// White is an open eye and black a shut one, for the frames the video has not drawn.
		private void Fill(Color color)
		{
			if (!_texture) return;

			var previous = RenderTexture.active;
			RenderTexture.active = _texture;
			GL.Clear(false, true, color);
			RenderTexture.active = previous;
		}
	}
}
