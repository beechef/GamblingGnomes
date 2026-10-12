using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.Audio
{
	// One piece of music as an object: it plays while switched on and stops when switched off, so whoever
	// owns the music only toggles objects, and each track carries whatever drives it (parameters, layers)
	// as components of its own. Parameters set while it is off are kept and applied when it starts.
	public class MusicTrack : MonoBehaviour
	{
		[Required]
		[SerializeField] private AudioManager _audio;

		[Required]
		[SerializeField] private AudioEvent _music;

		private readonly Dictionary<string, float> _parameters = new();
		private AudioHandle _playing;

		private void OnEnable()
		{
			_playing = _audio.Play(_music);

			foreach (var parameter in _parameters) _audio.SetParameter(_playing, parameter.Key, parameter.Value);
		}

		private void OnDisable()
		{
			if (_audio) _audio.Stop(_playing);
			_playing = default;
		}

		public void SetParameter(string parameter, float value)
		{
			if (string.IsNullOrEmpty(parameter)) return;

			_parameters[parameter] = value;
			_audio.SetParameter(_playing, parameter, value);
		}
	}
}
