using Game.Runtime.Voice;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Settings
{
	// One other player's voice level, saved under their voice id so it follows them to the next session.
	public class UIVoicePeerVolumeRow : MonoBehaviour
	{
		[Required] [SerializeField] private TMP_Text _nameLabel;
		[Required] [SerializeField] private Slider _slider;

		private string _voiceId;

		public void Bind(VoicePeer peer)
		{
			_voiceId = peer.Id;
			_nameLabel.text = peer.DisplayName;
			_slider.SetValueWithoutNotify(Mathf.Lerp(_slider.minValue, _slider.maxValue, VoiceSettings.GetPlayerVolume(peer.Id)));
		}

		private void OnEnable() => _slider.onValueChanged.AddListener(ApplyVolume);

		private void OnDisable() => _slider.onValueChanged.RemoveListener(ApplyVolume);

		private void ApplyVolume(float _)
		{
			if (!string.IsNullOrEmpty(_voiceId)) VoiceSettings.SetPlayerVolume(_voiceId, _slider.normalizedValue);
		}
	}
}
