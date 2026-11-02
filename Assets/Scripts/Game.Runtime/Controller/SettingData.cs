using System;
using System.Collections.Generic;
using Game.Runtime.Voice;
using UnityEngine;

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

		// Set only by SettingController.CreateDefault, so a save from before this field reads false.
		public bool FirstTime;

		public int TargetFrameRate = 60;
		public bool VSync;

		public FullScreenMode WindowMode = FullScreenMode.FullScreenWindow;

		// 0 until one is chosen: the display's own size.
		public int ResolutionWidth;
		public int ResolutionHeight;

		// A locale code; empty for the catalog's default.
		public string Language = string.Empty;

		public float MasterVolume = 1f;
		public float MusicVolume = 1f;
		public float SfxVolume = 1f;

		public float VoiceInputVolume = 0.5f;
		public float VoiceOutputVolume = 1f;
		public VoiceTalkMode VoiceTalkMode = VoiceTalkMode.OpenMic;
		public string VoiceInputDeviceId = string.Empty;
		public bool VoiceNoiseSuppression = true;
		public List<VoicePlayerVolume> VoicePlayerVolumes = new();
	}
}
