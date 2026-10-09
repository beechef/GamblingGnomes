using Game.Runtime.Controller;
using UnityEngine;

namespace Game.Runtime.Voice
{
	// The player's voice settings, kept in SettingController.
	public static class VoiceSettings
	{
		private static SettingData Data => SettingController.Data;

		// 0.5 sends the microphone as recorded.
		public static float InputVolume
		{
			get => Data.VoiceInputVolume;
			set
			{
				Data.VoiceInputVolume = Mathf.Clamp01(value);
				SettingController.Save();
			}
		}

		public static float OutputVolume
		{
			get => Data.VoiceOutputVolume;
			set
			{
				Data.VoiceOutputVolume = Mathf.Clamp01(value);
				SettingController.Save();
			}
		}

		public static VoiceTalkMode TalkMode
		{
			get => Data.VoiceTalkMode;
			set
			{
				Data.VoiceTalkMode = value;
				SettingController.Save();
			}
		}

		// Empty for the system default.
		public static string InputDeviceId
		{
			get => Data.VoiceInputDeviceId;
			set
			{
				Data.VoiceInputDeviceId = value ?? string.Empty;
				SettingController.Save();
			}
		}

		public static bool NoiseSuppression
		{
			get => Data.VoiceNoiseSuppression;
			set
			{
				Data.VoiceNoiseSuppression = value;
				SettingController.Save();
			}
		}

		public static float GetPlayerVolume(string voiceId)
		{
			foreach (var entry in Data.VoicePlayerVolumes)
				if (entry.VoiceId == voiceId) return entry.Volume;
			return 1f;
		}

		public static void SetPlayerVolume(string voiceId, float volume)
		{
			var entry = new SettingData.VoicePlayerVolume { VoiceId = voiceId, Volume = Mathf.Clamp01(volume) };
			var index = Data.VoicePlayerVolumes.FindIndex(e => e.VoiceId == voiceId);
			if (index >= 0) Data.VoicePlayerVolumes[index] = entry;
			else Data.VoicePlayerVolumes.Add(entry);
			SettingController.Save();
		}
	}
}
