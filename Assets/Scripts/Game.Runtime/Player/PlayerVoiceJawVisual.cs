using Game.Runtime.Voice;
using UnityEngine;

namespace Game.Runtime.Player
{
	// Opens the jaw with this player's voice chat level, louder wider. Sits on the jaw bone. Put back to rest in
	// Update and opened in LateUpdate on top of whatever the Animator posed, so a clip moving the jaw still plays.
	[DefaultExecutionOrder(50)]
	public class PlayerVoiceJawVisual : MonoBehaviour
	{
		[Tooltip("Local axis the jaw opens around.")]
		[SerializeField] private Vector3 _openAxis = Vector3.forward;

		[Tooltip("Jaw angle at full voice level, degrees.")]
		[SerializeField] private float _maxAngle = 25f;

		[Tooltip("Voice level below this keeps the mouth shut; filters background noise.")]
		[Range(0f, 1f)]
		[SerializeField] private float _quietLevel = 0.05f;

		[Tooltip("Voice level that opens the mouth fully.")]
		[Range(0f, 1f)]
		[SerializeField] private float _loudLevel = 0.6f;

		[Tooltip("How fast the jaw follows the voice, openness per second.")]
		[SerializeField] private float _speed = 12f;

		private PlayerData _data;
		private VoiceChatManager _voice;
		private Quaternion _rest;
		private float _open;

		private void Awake()
		{
			_data = GetComponentInParent<PlayerData>(true);
			_rest = transform.localRotation;
		}

		private void Start() => _voice = VoiceChatManager.Instance;

		private void OnDisable()
		{
			transform.localRotation = _rest;
			_open = 0f;
		}

		private void Update() => transform.localRotation = _rest;

		private void LateUpdate()
		{
			var voiceId = _data ? _data.VoiceId.Value.ToString() : string.Empty;
			var level = _voice && _voice.IsSpeaking(voiceId) ? _voice.GetAudioLevel(voiceId) : 0f;
			var target = Mathf.InverseLerp(_quietLevel, _loudLevel, level);

			_open = Mathf.MoveTowards(_open, target, _speed * Time.deltaTime);
			if (_open <= 0f) return;

			transform.localRotation *= Quaternion.AngleAxis(_open * _maxAngle, _openAxis);
		}
	}
}
