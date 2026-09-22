using UnityEngine;
using UnityEngine.Video;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// Plays the clip into a texture of its own and hands it to the pass. Until the first frame is decoded the
	// texture is black, which the blend reads as no change, so there is no flash while the clip starts.
	public class PokerHallucinationVideoBehaviour : PokerHallucinationFullscreenBehaviour
	{
		private VideoPlayer _player;
		private RenderTexture _target;

		protected override void OnPassStarting(Material instance)
		{
			var video = (PokerHallucinationVideoEffect)Config;
			if (!video.Clip) return;

			_target = new RenderTexture((int)video.Clip.width, (int)video.Clip.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
			{
				name = video.Clip.name
			};
			_target.Create();

			instance.SetTexture(video.TextureProperty, _target);

			_player = gameObject.AddComponent<VideoPlayer>();
			_player.playOnAwake = false;
			_player.source = VideoSource.VideoClip;
			_player.clip = video.Clip;
			_player.isLooping = true;
			_player.skipOnDrop = true;
			_player.playbackSpeed = video.PlaybackSpeed;
			_player.audioOutputMode = VideoAudioOutputMode.None;
			_player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
			_player.renderMode = VideoRenderMode.RenderTexture;
			_player.targetTexture = _target;
			_player.Play();
		}

		protected override void OnPassDisposed()
		{
			if (_player) _player.Stop();

			if (_target)
			{
				_target.Release();
				Destroy(_target);
			}

			_target = null;
		}
	}
}
