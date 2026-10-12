using System;
using System.Collections.Generic;
using Game.Runtime.Voice;

namespace Game.Runtime.Controller
{
	// Every player setting, saved as one JSON document by SettingController. Field names are saved data:
	// rename one only with [FormerlySerializedAs].
	[Serializable]
	public class SettingData
	{
		[Serializable]
		public struct VoicePlayerVolume
		{
			public string VoiceId;
			public float Volume;
		}

		public int TargetFrameRate = 60;
		public bool VSync;

		// A locale code; empty for the catalog's default.
		public string Language = string.Empty;

		public float VoiceInputVolume = 0.5f;
		public float VoiceOutputVolume = 1f;
		public VoiceTalkMode VoiceTalkMode = VoiceTalkMode.OpenMic;
		public string VoiceInputDeviceId = string.Empty;
		public bool VoiceNoiseSuppression = true;
		public List<VoicePlayerVolume> VoicePlayerVolumes = new();
	}
}
