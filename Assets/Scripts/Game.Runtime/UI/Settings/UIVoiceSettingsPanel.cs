using System.Collections.Generic;
using Game.Runtime.Voice;
using Localization;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Settings
{
	// The voice section of the settings: talk mode, microphone, noise suppression, the two levels, then one
	// level per player in the channel (rows re-bound, never rebuilt). Each
	// choice is saved the moment it is made; VoiceChatManager applies it on SettingController.OnChanged.
	public class UIVoiceSettingsPanel : MonoBehaviour
	{
		[Required] [SerializeField] private UIOptionStepper _talkMode;
		[Required] [SerializeField] private UIOptionStepper _microphone;
		[Required] [SerializeField] private UIOptionStepper _noiseSuppression;
		[Required] [SerializeField] private Slider _inputVolume;
		[Required] [SerializeField] private Slider _outputVolume;

		[Header("Players")]
		[Tooltip("Shown above the player rows while anyone else is in the channel.")]
		[Required] [SerializeField] private GameObject _peerHeader;

		[Tooltip("Where player rows are added: the scrolled column, after the header.")]
		[Required] [SerializeField] private Transform _peerParent;

		[Required] [SerializeField] private UIVoicePeerVolumeRow _peerRowPrefab;

		// Index 0 is the system default (empty id); the rest follow the stepper's options.
		private readonly List<string> _microphoneIds = new();
		private readonly List<string> _microphoneNames = new();
		private readonly List<UIVoicePeerVolumeRow> _peerRows = new();
		private VoiceChatManager _voice;

		private void Start()
		{
			_voice = VoiceChatManager.Instance;
			if (_voice)
			{
				_voice.OnDevicesChanged += RefreshMicrophones;
				_voice.OnPeersChanged += RefreshPeers;
			}
			RefreshMicrophones();
			RefreshPeers();
		}

		private void OnDestroy()
		{
			if (_voice)
			{
				_voice.OnPeersChanged -= RefreshPeers;
				_voice.OnDevicesChanged -= RefreshMicrophones;
			}
			_voice = null;
		}

		private void OnEnable()
		{
			_talkMode.OnIndexChanged += ApplyTalkMode;
			_microphone.OnIndexChanged += ApplyMicrophone;
			_noiseSuppression.OnIndexChanged += ApplyNoiseSuppression;
			_inputVolume.onValueChanged.AddListener(ApplyInputVolume);
			_outputVolume.onValueChanged.AddListener(ApplyOutputVolume);
			Localizer.OnLocaleChanged += Refresh;

			Refresh();
		}

		private void OnDisable()
		{
			Localizer.OnLocaleChanged -= Refresh;
			_outputVolume.onValueChanged.RemoveListener(ApplyOutputVolume);
			_inputVolume.onValueChanged.RemoveListener(ApplyInputVolume);
			_noiseSuppression.OnIndexChanged -= ApplyNoiseSuppression;
			_microphone.OnIndexChanged -= ApplyMicrophone;
			_talkMode.OnIndexChanged -= ApplyTalkMode;
		}

		private void Refresh()
		{
			_talkMode.SetOptions(
				new[]
				{
					Localizer.Get(LocalizationKeys.Settings.Voice.TalkMode.OpenMic),
					Localizer.Get(LocalizationKeys.Settings.Voice.TalkMode.PushToTalk)
				},
				(int)VoiceSettings.TalkMode);

			_noiseSuppression.SetOptions(
				new[] { Localizer.Get(LocalizationKeys.Common.Off), Localizer.Get(LocalizationKeys.Common.On) },
				VoiceSettings.NoiseSuppression ? 1 : 0);

			ShowLevel(_inputVolume, VoiceSettings.InputVolume);
			ShowLevel(_outputVolume, VoiceSettings.OutputVolume);
			RefreshMicrophones();
			RefreshPeers();
		}

		private void RefreshMicrophones()
		{
			_microphoneIds.Clear();
			_microphoneNames.Clear();
			_microphoneIds.Add(string.Empty);
			_microphoneNames.Add(Localizer.Get(LocalizationKeys.Settings.Voice.Microphone.Default));

			if (_voice)
			{
				foreach (var device in _voice.GetInputDevices())
				{
					_microphoneIds.Add(device.Id);
					_microphoneNames.Add(device.Name);
				}
			}

			// A saved microphone that is unplugged is not in use; the system default is, and says so.
			_microphone.SetOptions(_microphoneNames, Mathf.Max(0, _microphoneIds.IndexOf(VoiceSettings.InputDeviceId)));
			_microphone.IsInteractable = _microphoneIds.Count > 1;
		}

		private void RefreshPeers()
		{
			var peers = _voice ? _voice.Peers : (IReadOnlyList<VoicePeer>)System.Array.Empty<VoicePeer>();

			while (_peerRows.Count < peers.Count) _peerRows.Add(Instantiate(_peerRowPrefab, _peerParent));

			for (var i = 0; i < _peerRows.Count; i++)
			{
				var used = i < peers.Count;
				_peerRows[i].gameObject.SetActive(used);
				if (used) _peerRows[i].Bind(peers[i]);
			}

			_peerHeader.SetActive(peers.Count > 0);
		}

		private static void ShowLevel(Slider slider, float level) =>
			slider.SetValueWithoutNotify(Mathf.Lerp(slider.minValue, slider.maxValue, level));

		private void ApplyTalkMode(int index) => VoiceSettings.TalkMode = (VoiceTalkMode)index;

		private void ApplyMicrophone(int index)
		{
			if (_voice) _voice.SetInputDevice(_microphoneIds[index]);
			else VoiceSettings.InputDeviceId = _microphoneIds[index];
		}

		private void ApplyNoiseSuppression(int index) => VoiceSettings.NoiseSuppression = index == 1;

		private void ApplyInputVolume(float _) => VoiceSettings.InputVolume = _inputVolume.normalizedValue;

		private void ApplyOutputVolume(float _) => VoiceSettings.OutputVolume = _outputVolume.normalizedValue;
	}
}
