using Game.Runtime.Voice;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Runtime.Player
{
	// Flaps the mouth while this player speaks in voice chat, louder wider: the jaw drops and the head (the upper
	// jaw, there is no bone of its own) tips back. Vivox reports the level only ten times a second, so the
	// syllable-like flapping is noise over that level. Sits on the jaw bone. The jaw is put back to rest in Update;
	// both open in LateUpdate on top of whatever the Animator and the look posed, so a clip moving them still plays.
	[DefaultExecutionOrder(50)]
	public class PlayerVoiceJawVisual : MonoBehaviour
	{
		[Header("Lower jaw")]
		[Tooltip("Local axis of this bone the jaw drops around.")]
		[FormerlySerializedAs("_openAxis")]
		[SerializeField] private Vector3 _lowerJawAxis = Vector3.back;

		[Tooltip("Angle the jaw drops at full voice level, degrees.")]
		[FormerlySerializedAs("_maxAngle")]
		[SerializeField] private float _lowerJawAngle = 40f;

		[Header("Upper jaw")]
		[Tooltip("The bone the upper jaw moves with: the head. Empty: only the lower jaw moves.")]
		[SerializeField] private Transform _upperJaw;

		[Tooltip("Local axis of the upper jaw bone it tips back around.")]
		[SerializeField] private Vector3 _upperJawAxis = Vector3.forward;

		[Tooltip("Angle the upper jaw tips back at full voice level, degrees.")]
		[SerializeField] private float _upperJawAngle = 10f;

		[Header("Voice")]
		[Tooltip("Voice level below this keeps the mouth shut; filters background noise.")]
		[Range(0f, 1f)]
		[SerializeField] private float _quietLevel = 0.05f;

		[Tooltip("Voice level that opens the mouth fully.")]
		[Range(0f, 1f)]
		[SerializeField] private float _loudLevel = 0.35f;

		[Tooltip("Openness while speaking at the quiet level, so even a soft voice keeps the jaw moving.")]
		[Range(0f, 1f)]
		[SerializeField] private float _minOpen = 0.5f;

		[Tooltip("Mouth openings per second while speaking.")]
		[Min(0f)]
		[SerializeField] private float _syllableRate = 4f;

		[Tooltip("How fast the jaw follows, openness per second; high enough to keep up with the syllables.")]
		[SerializeField] private float _speed = 30f;

		private PlayerData _data;
		private VoiceChatManager _voice;
		private Quaternion _rest;
		private float _open;
		private float _noiseSeed;
		private Quaternion _upperJawApplied = Quaternion.identity;

		private void Awake()
		{
			_data = GetComponentInParent<PlayerData>(true);
			_rest = transform.localRotation;
			_noiseSeed = Random.value * 100f;
		}

		private void Start() => _voice = VoiceChatManager.Instance;

		private void OnDisable()
		{
			transform.localRotation = _rest;
			UndoUpperJaw();
			_open = 0f;
		}

		// The head is posed by the Animator and the look, so its tip is taken back off rather than reset to a rest.
		private void Update()
		{
			transform.localRotation = _rest;
			UndoUpperJaw();
		}

		private void LateUpdate()
		{
			var voiceId = _data ? _data.VoiceId.Value.ToString() : string.Empty;
			var target = 0f;
			var level = _voice && _voice.IsSpeaking(voiceId) ? _voice.GetAudioLevel(voiceId) : 0f;
			// Vivox holds speech on for a moment after the voice stops; the level says when it really has.
			if (level >= _quietLevel)
			{
				var loudness = Mathf.Lerp(_minOpen, 1f, Mathf.InverseLerp(_quietLevel, _loudLevel, level));
				var syllable = Mathf.InverseLerp(0.25f, 0.75f, Mathf.PerlinNoise(Time.time * _syllableRate, _noiseSeed));
				target = loudness * syllable;
			}

			_open = Mathf.MoveTowards(_open, target, _speed * Time.deltaTime);
			if (_open <= 0f) return;

			transform.localRotation *= Quaternion.AngleAxis(_open * _lowerJawAngle, _lowerJawAxis);
			if (!_upperJaw) return;

			_upperJawApplied = Quaternion.AngleAxis(_open * _upperJawAngle, _upperJawAxis);
			_upperJaw.localRotation *= _upperJawApplied;
		}

		private void UndoUpperJaw()
		{
			if (_upperJaw) _upperJaw.localRotation *= Quaternion.Inverse(_upperJawApplied);
			_upperJawApplied = Quaternion.identity;
		}
	}
}
