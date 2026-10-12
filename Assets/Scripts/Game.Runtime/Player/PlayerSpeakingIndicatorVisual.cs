using Game.Runtime.Voice;
using Sirenix.OdinInspector;
using TMPro;
using Unity.Collections;
using UnityEngine;

namespace Game.Runtime.Player
{
	// Shows an icon beside the name while this player is heard speaking in voice chat, matched through
	// PlayerData.VoiceId.
	public class PlayerSpeakingIndicatorVisual : MonoBehaviour
	{
		[Required]
		[SerializeField] private PlayerData _data;

		[Tooltip("Centre-anchored under the name label, pivot on its right edge; placed just left of the drawn text.")]
		[Required]
		[SerializeField] private RectTransform _speakingIcon;

		[Required]
		[SerializeField] private TMP_Text _nameLabel;

		[SerializeField] private float _gap = 8f;

		private VoiceChatManager _voice;

		private void Start()
		{
			_voice = VoiceChatManager.Instance;
			if (_voice) _voice.OnTalkStateChanged += Refresh;
			Refresh();
		}

		private void OnDestroy()
		{
			if (_voice) _voice.OnTalkStateChanged -= Refresh;
			_voice = null;
		}

		private void OnEnable()
		{
			_data.VoiceId.OnValueChanged += HandleVoiceIdChanged;
			Refresh();
		}

		private void OnDisable()
		{
			_data.VoiceId.OnValueChanged -= HandleVoiceIdChanged;
		}

		private void HandleVoiceIdChanged(FixedString128Bytes previous, FixedString128Bytes current) => Refresh();

		private void Refresh()
		{
			var speaking = _voice && _voice.IsSpeaking(_data.VoiceId.Value.ToString());
			if (_speakingIcon.gameObject.activeSelf == speaking) return;

			// Measured as it appears: the name is long set by the time anyone speaks, and auto-sizing moves its edge.
			if (speaking)
			{
				_nameLabel.ForceMeshUpdate();
				var left = _nameLabel.text.Length > 0 ? _nameLabel.textBounds.min.x : 0f;
				_speakingIcon.anchoredPosition = new Vector2(left - _gap, _speakingIcon.anchoredPosition.y);
			}

			_speakingIcon.gameObject.SetActive(speaking);
		}
	}
}
