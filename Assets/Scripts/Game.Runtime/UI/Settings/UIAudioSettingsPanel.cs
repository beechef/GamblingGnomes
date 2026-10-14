using Game.Runtime.Audio;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Settings
{
	// The sound section of the settings: master, music and effect levels. Each choice is saved the
	// moment it is made; AudioManager applies it on SettingController.OnChanged.
	public class UIAudioSettingsPanel : MonoBehaviour
	{
		[Required] [SerializeField] private Slider _masterVolume;
		[Required] [SerializeField] private Slider _musicVolume;
		[Required] [SerializeField] private Slider _sfxVolume;

		private void OnEnable()
		{
			_masterVolume.onValueChanged.AddListener(ApplyMasterVolume);
			_musicVolume.onValueChanged.AddListener(ApplyMusicVolume);
			_sfxVolume.onValueChanged.AddListener(ApplySfxVolume);

			Refresh();
		}

		private void OnDisable()
		{
			_sfxVolume.onValueChanged.RemoveListener(ApplySfxVolume);
			_musicVolume.onValueChanged.RemoveListener(ApplyMusicVolume);
			_masterVolume.onValueChanged.RemoveListener(ApplyMasterVolume);
		}

		private void Refresh()
		{
			ShowLevel(_masterVolume, SoundSettings.MasterVolume);
			ShowLevel(_musicVolume, SoundSettings.MusicVolume);
			ShowLevel(_sfxVolume, SoundSettings.SfxVolume);
		}

		private static void ShowLevel(Slider slider, float level) =>
			slider.SetValueWithoutNotify(Mathf.Lerp(slider.minValue, slider.maxValue, level));

		private void ApplyMasterVolume(float _) => SoundSettings.MasterVolume = _masterVolume.normalizedValue;

		private void ApplyMusicVolume(float _) => SoundSettings.MusicVolume = _musicVolume.normalizedValue;

		private void ApplySfxVolume(float _) => SoundSettings.SfxVolume = _sfxVolume.normalizedValue;
	}
}
