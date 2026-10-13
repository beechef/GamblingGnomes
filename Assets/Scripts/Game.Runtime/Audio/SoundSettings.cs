using Game.Runtime.Controller;
using UnityEngine;

namespace Game.Runtime.Audio
{
	// The player's sound levels, kept in SettingController; AudioManager applies them on SettingController.OnChanged.
	public static class SoundSettings
	{
		private static SettingData Data => SettingController.Data;

		public static float MasterVolume
		{
			get => Data.MasterVolume;
			set
			{
				Data.MasterVolume = Mathf.Clamp01(value);
				SettingController.Save();
			}
		}

		public static float MusicVolume
		{
			get => Data.MusicVolume;
			set
			{
				Data.MusicVolume = Mathf.Clamp01(value);
				SettingController.Save();
			}
		}

		public static float SfxVolume
		{
			get => Data.SfxVolume;
			set
			{
				Data.SfxVolume = Mathf.Clamp01(value);
				SettingController.Save();
			}
		}

		public static bool Muted
		{
			get => Data.AudioMuted;
			set
			{
				Data.AudioMuted = value;
				SettingController.Save();
			}
		}
	}
}
