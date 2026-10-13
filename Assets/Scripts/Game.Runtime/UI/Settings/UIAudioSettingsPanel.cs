using Game.Runtime.Audio;
using Localization;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Settings
{
	// The sound section of the settings: master, music and effect levels, and mute. Each choice is saved the
	// moment it is made; AudioManager applies it on SettingController.OnChanged.
	public class UIAudioSettingsPanel : MonoBehaviour
	{
		[Required] [SerializeField] private Slider _masterVolume;
		[Required] [SerializeField] private Slider _musicVolume;
		[Required] [SerializeField] private Slider _sfxVolume;
		[Required] [SerializeField] private UIOptionStepper _mute;

		private void OnEnable()
		{
			_masterVolume.onValueChanged.AddListener(ApplyMasterVolume);
			_musicVolume.onValueChanged.AddListener(ApplyMusicVolume);
			_sfxVolume.onValueChanged.AddListener(ApplySfxVolume);
			_mute.OnIndexChanged += ApplyMute;
			Localizer.OnLocaleChanged += Refresh;

			Refresh();
		}

		private void OnDisable()
		{
			Localizer.OnLocaleChanged -= Refresh;
			_mute.OnIndexChanged -= ApplyMute;
			_sfxVolume.onValueChanged.RemoveListener(ApplySfxVolume);
			_musicVolume.onValueChanged.RemoveListener(ApplyMusicVolume);
			_masterVolume.onValueChanged.RemoveListener(ApplyMasterVolume);
		}

		private void Refresh()
		{
			ShowLevel(_masterVolume, SoundSettings.MasterVolume);
			ShowLevel(_musicVolume, SoundSettings.MusicVolume);
			ShowLevel(_sfxVolume, SoundSettings.SfxVolume);
			_mute.SetOptions(
				new[] { Localizer.Get(LocalizationKeys.Common.Off), Localizer.Get(LocalizationKeys.Common.On) },
				SoundSettings.Muted ? 1 : 0);
		}

		private static void ShowLevel(Slider slider, float level) =>
			slider.SetValueWithoutNotify(Mathf.Lerp(slider.minValue, slider.maxValue, level));

		private void ApplyMasterVolume(float _) => SoundSettings.MasterVolume = _masterVolume.normalizedValue;

		private void ApplyMusicVolume(float _) => SoundSettings.MusicVolume = _musicVolume.normalizedValue;

		private void ApplySfxVolume(float _) => SoundSettings.SfxVolume = _sfxVolume.normalizedValue;

		private void ApplyMute(int index) => SoundSettings.Muted = index == 1;
	}
}
