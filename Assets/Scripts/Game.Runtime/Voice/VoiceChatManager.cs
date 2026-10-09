using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Game.Runtime.Controller;
using Sirenix.OdinInspector;
using Steamworks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Runtime.Voice
{
	// Voice chat through Unity Vivox: everyone in a session shares one channel. Vivox carries the voice on
	// its own servers beside the Steam connection. The channel name is a random secret the host makes and
	// sends each client by a named message as it connects, so nobody outside the session can name it.
	// Services, sign-in, Vivox init and login happen once at start; a session only joins and leaves.
	public class VoiceChatManager : MonoBehaviour
	{
		public static VoiceChatManager Instance { get; private set; }

		[Required]
		[SerializeField] private NetworkManager _networkManager;

		[Tooltip("Held to talk while the talk mode is Push To Talk.")]
		[SerializeField] private InputActionReference _pushToTalk;

		private const string ChannelMessage = "voice.channel";
		private const string ChannelPrefix = "session_";
		private const string SystemDefaultDeviceId = "Default System Device";
		private const string NoDeviceId = "No Device";

		public event Action OnPeersChanged;
		public event Action OnDevicesChanged;
		public event Action OnTalkStateChanged;
		public event Action OnLocalVoiceIdChanged;

		private readonly Dictionary<string, VivoxParticipant> _peers = new();
		private readonly List<VoicePeer> _peerList = new();
		private readonly HashSet<string> _speaking = new();
		private readonly Dictionary<VivoxParticipant, Action> _speechHandlers = new();

		private Task _servicesReady;
		private string _channel;
		private string _hostedChannel;
		private int _sessionVersion;
		private bool _isTalkKeyHeld;
		private bool _isSending;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics() => Instance = null;

		private static IVivoxService Vivox => VivoxService.Instance;

		public bool IsReady { get; private set; }

		public bool IsInChannel => !string.IsNullOrEmpty(_channel) && IsReady && Vivox.ActiveChannels.ContainsKey(_channel);

		public IReadOnlyList<VoicePeer> Peers => _peerList;

		public string LocalVoiceId => IsReady && Vivox.IsLoggedIn ? Vivox.SignedInPlayerId : string.Empty;

		// What this sets, not what Vivox reports: IsInputDeviceMuted lags a moment behind a mute.
		public bool IsTransmitting => IsReady && Vivox.IsLoggedIn && _isSending;

		public bool IsSpeaking(string voiceId) => !string.IsNullOrEmpty(voiceId) && _speaking.Contains(voiceId);

		public string ActiveInputDeviceId => IsReady && Vivox.ActiveInputDevice != null ? Vivox.ActiveInputDevice.DeviceID : string.Empty;

		private void Awake()
		{
			if (Instance && Instance != this)
			{
				Destroy(gameObject);
				return;
			}

			Instance = this;
#if UNITY_EDITOR
			UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ShutdownVivox;
#endif
		}

		private void OnEnable()
		{
			if (Instance != this) return;

			_networkManager.OnClientStarted += HandleClientStarted;
			_networkManager.OnClientConnectedCallback += HandleClientConnected;
			_networkManager.OnClientStopped += HandleClientStopped;
			SettingController.OnChanged += ApplySettings;

			if (!_pushToTalk) return;

			_pushToTalk.action.performed += HandleTalkKey;
			_pushToTalk.action.canceled += HandleTalkKey;
			_pushToTalk.action.Enable();
		}

		private void OnDisable()
		{
			if (Instance != this) return;

			if (_pushToTalk)
			{
				_pushToTalk.action.canceled -= HandleTalkKey;
				_pushToTalk.action.performed -= HandleTalkKey;
			}

			SettingController.OnChanged -= ApplySettings;
			_networkManager.OnClientStopped -= HandleClientStopped;
			_networkManager.OnClientConnectedCallback -= HandleClientConnected;
			_networkManager.OnClientStarted -= HandleClientStarted;
		}

		private async void Start()
		{
			if (Instance != this) return;

			try
			{
				await EnsureServicesAsync();
			}
			catch (Exception exception)
			{
				Debug.LogException(exception, this);
			}
		}

		private void OnDestroy()
		{
			if (Instance != this) return;

#if UNITY_EDITOR
			UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ShutdownVivox;
#endif
			ShutdownVivox();
			Instance = null;
		}

		// With Domain Reload off the native Vivox core outlives Play mode and a mid-Play recompile; the next
		// session's init would skip a core it thinks is running, so it is stopped with its owner.
		private void ShutdownVivox()
		{
			_sessionVersion++;
			ClearPeers();
			if (!IsReady) return;

			IsReady = false;
			UnsubscribeVivox();
			Vivox.Uninitialize();
			OnLocalVoiceIdChanged?.Invoke();
		}

		// The microphones a player can pick; the system default and "no device" are not choices, an empty id is the default.
		public IReadOnlyList<VoiceDevice> GetInputDevices()
		{
			var devices = new List<VoiceDevice>();
			if (IsReady)
				foreach (var device in Vivox.AvailableInputDevices)
					if (device.DeviceID != SystemDefaultDeviceId && device.DeviceID != NoDeviceId) devices.Add(new VoiceDevice(device.DeviceID, device.DeviceName));
			return devices;
		}

		private async Task EnsureServicesAsync()
		{
			try
			{
				_servicesReady ??= StartServicesAsync();
				await _servicesReady;
			}
			catch (Exception exception)
			{
				// No internet, or Vivox not enabled for the project, must never stop the game; retried next session.
				_servicesReady = null;
				Debug.LogWarning($"[{nameof(VoiceChatManager)}] Voice chat is unavailable: {exception.Message}", this);
			}
		}

		private async Task StartServicesAsync()
		{
			if (UnityServices.State == ServicesInitializationState.Uninitialized)
				await UnityServices.InitializeAsync(new InitializationOptions().SetProfile(ProfileName()));

#if UNITY_EDITOR
			// Vivox keeps login sessions by player id in statics only an assembly reload clears; a fresh
			// anonymous player per Play session never meets the last one's stale session.
			var auth = AuthenticationService.Instance;
			if (auth.IsSignedIn) auth.SignOut(true);
			auth.SwitchProfile(RandomProfileName());
#endif
			if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();

			await Vivox.InitializeAsync();
			if (!this) return;

			SubscribeVivox();
			Vivox.EnableAcousticEchoCancellation();
			IsReady = true;

			await ApplyDevicesAsync();
			await LoginAsync();
			OnDevicesChanged?.Invoke();
		}

		private async Task LoginAsync()
		{
			if (Vivox.IsLoggedIn) return;

			var options = new LoginOptions { DisplayName = LocalDisplayName() };
			try
			{
				await Vivox.LoginAsync(options);
			}
			catch (InvalidOperationException exception) when (exception.Message.Contains("not initialized"))
			{
				// A native core left stale by an earlier Play session: tear down and start again.
				Vivox.Uninitialize();
				await Vivox.InitializeAsync();
				await Vivox.LoginAsync(options);
				await ApplyDevicesAsync();
			}

			ApplySettings();
			OnTalkStateChanged?.Invoke();
			OnLocalVoiceIdChanged?.Invoke();
		}

		private static string LocalDisplayName() => SteamClient.IsValid ? SteamClient.Name : Environment.UserName;

		// Named after the player so two instances on one machine sign in as two people.
		private static string ProfileName()
		{
			if (Application.isEditor || !SteamClient.IsValid) return RandomProfileName();

			var name = Regex.Replace(SteamClient.SteamId.Value.ToString(), "[^a-zA-Z0-9_-]", "_");
			return name.Length > 30 ? name[..30] : name;
		}

		private static string RandomProfileName() => $"local_{Guid.NewGuid():N}"[..30];

		private void SubscribeVivox()
		{
			Vivox.ParticipantAddedToChannel += HandleParticipantAdded;
			Vivox.ParticipantRemovedFromChannel += HandleParticipantRemoved;
			Vivox.ChannelJoined += HandleChannelJoined;
			Vivox.ChannelLeft += HandleChannelLeft;
			Vivox.AvailableInputDevicesChanged += HandleDevicesChanged;
			Vivox.AvailableOutputDevicesChanged += HandleDevicesChanged;
		}

		private void UnsubscribeVivox()
		{
			Vivox.AvailableOutputDevicesChanged -= HandleDevicesChanged;
			Vivox.AvailableInputDevicesChanged -= HandleDevicesChanged;
			Vivox.ChannelLeft -= HandleChannelLeft;
			Vivox.ChannelJoined -= HandleChannelJoined;
			Vivox.ParticipantRemovedFromChannel -= HandleParticipantRemoved;
			Vivox.ParticipantAddedToChannel -= HandleParticipantAdded;
		}

		// Registered before connecting, so the host's message can never arrive before its handler.
		private void HandleClientStarted()
		{
			if (!_networkManager.IsServer)
				_networkManager.CustomMessagingManager.RegisterNamedMessageHandler(ChannelMessage, HandleChannelMessage);
		}

		private void HandleClientConnected(ulong clientId)
		{
			if (!_networkManager.IsServer) return;

			_hostedChannel ??= ChannelPrefix + Guid.NewGuid().ToString("N");

			if (clientId == _networkManager.LocalClientId)
			{
				JoinSession(_hostedChannel);
				return;
			}

			using var writer = new FastBufferWriter(64, Allocator.Temp);
			writer.WriteValueSafe(_hostedChannel, true);
			_networkManager.CustomMessagingManager.SendNamedMessage(ChannelMessage, clientId, writer, NetworkDelivery.Reliable);
		}

		private void HandleChannelMessage(ulong senderId, FastBufferReader reader)
		{
			if (senderId != NetworkManager.ServerClientId) return;

			reader.ReadValueSafe(out string channel, true);
			if (channel.StartsWith(ChannelPrefix)) JoinSession(channel);
		}

		private void HandleClientStopped(bool wasHost)
		{
			_hostedChannel = null;
			_networkManager.CustomMessagingManager?.UnregisterNamedMessageHandler(ChannelMessage);
			LeaveChannel();
		}

		private async void JoinSession(string channel)
		{
			var version = ++_sessionVersion;

			try
			{
				await EnsureServicesAsync();
				if (!IsReady || version != _sessionVersion) return;

				await LoginAsync();
				if (version != _sessionVersion) return;

				_channel = channel;
				ApplySettings();
				await Vivox.JoinGroupChannelAsync(channel, ChatCapability.AudioOnly);
			}
			catch (Exception exception)
			{
				Debug.LogWarning($"[{nameof(VoiceChatManager)}] Could not join voice channel: {exception.Message}", this);
			}
		}

		private async void LeaveChannel()
		{
			_sessionVersion++;
			_channel = null;
			ClearPeers();

			if (!IsReady || !Vivox.IsLoggedIn) return;

			try
			{
				await Vivox.LeaveAllChannelsAsync();
			}
			catch (Exception exception)
			{
				Debug.LogWarning($"[{nameof(VoiceChatManager)}] Leaving voice chat failed: {exception.Message}", this);
			}
		}

		private void HandleChannelJoined(string channel)
		{
			if (channel != _channel) return;

			ApplyTransmission();
			OnPeersChanged?.Invoke();
			OnTalkStateChanged?.Invoke();
		}

		private void HandleChannelLeft(string channel)
		{
			if (channel == _channel) ClearPeers();
		}

		private void HandleParticipantAdded(VivoxParticipant participant)
		{
			if (participant.ChannelName != _channel) return;

			WatchSpeech(participant);
			if (participant.IsSelf)
			{
				OnTalkStateChanged?.Invoke();
				return;
			}

			_peers[participant.PlayerId] = participant;
			ApplyPeer(participant);
			RebuildPeerList();
		}

		private void HandleParticipantRemoved(VivoxParticipant participant)
		{
			UnwatchSpeech(participant);
			if (!_peers.Remove(participant.PlayerId))
			{
				OnTalkStateChanged?.Invoke();
				return;
			}

			RebuildPeerList();
		}

		private void ClearPeers()
		{
			foreach (var (participant, handler) in _speechHandlers) participant.ParticipantSpeechDetected -= handler;
			_speechHandlers.Clear();
			_speaking.Clear();
			_peers.Clear();

			RebuildPeerList();
		}

		private void RebuildPeerList()
		{
			_peerList.Clear();
			foreach (var participant in _peers.Values) _peerList.Add(new VoicePeer(participant.PlayerId, participant.DisplayName));
			OnPeersChanged?.Invoke();
			OnTalkStateChanged?.Invoke();
		}

		private void WatchSpeech(VivoxParticipant participant)
		{
			if (_speechHandlers.ContainsKey(participant)) return;

			void Handler()
			{
				var changed = participant.SpeechDetected ? _speaking.Add(participant.PlayerId) : _speaking.Remove(participant.PlayerId);
				if (changed) OnTalkStateChanged?.Invoke();
			}

			_speechHandlers[participant] = Handler;
			participant.ParticipantSpeechDetected += Handler;
		}

		private void UnwatchSpeech(VivoxParticipant participant)
		{
			if (!_speechHandlers.Remove(participant, out var handler)) return;

			participant.ParticipantSpeechDetected -= handler;
			_speaking.Remove(participant.PlayerId);
		}

		public async void SetInputDevice(string deviceId)
		{
			VoiceSettings.InputDeviceId = deviceId;
			try
			{
				await ApplyDevicesAsync();
			}
			catch (Exception exception)
			{
				Debug.LogException(exception, this);
			}
		}

		// Always told, never assumed: the SDK reports "Default System Device" active from the start but never
		// tells its core, which left the mic off the Windows default. Speakers always follow the system, as
		// FMOD does.
		private async Task ApplyDevicesAsync()
		{
			if (!IsReady) return;

			try
			{
				var input = PickDevice(Vivox.AvailableInputDevices, d => d.DeviceID, VoiceSettings.InputDeviceId);
				if (input != null) await SetDeviceAsync(Vivox.SetActiveInputDeviceAsync(input), input.DeviceID == Vivox.ActiveInputDevice?.DeviceID);

				var output = PickDevice(Vivox.AvailableOutputDevices, d => d.DeviceID, null);
				if (output != null) await SetDeviceAsync(Vivox.SetActiveOutputDeviceAsync(output), output.DeviceID == Vivox.ActiveOutputDevice?.DeviceID);
			}
			catch (Exception exception)
			{
				Debug.LogWarning($"[{nameof(VoiceChatManager)}] Could not switch audio device: {exception.Message}", this);
			}

			OnDevicesChanged?.Invoke();
		}

		// A switch to the device the SDK already reports still reaches the core, but never completes; awaiting it hangs.
		private static async Task SetDeviceAsync(Task request, bool isAlreadyActive)
		{
			if (!isAlreadyActive) await request;
		}

		private static T PickDevice<T>(IEnumerable<T> devices, Func<T, string> idOf, string savedId) where T : class
		{
			T systemDefault = null;
			foreach (var device in devices)
			{
				var id = idOf(device);
				if (!string.IsNullOrEmpty(savedId) && id == savedId) return device;
				if (id == SystemDefaultDeviceId) systemDefault = device;
			}

			return systemDefault;
		}

		private async void HandleDevicesChanged()
		{
			try
			{
				await ApplyDevicesAsync();
			}
			catch (Exception exception)
			{
				Debug.LogException(exception, this);
			}
		}

		private void HandleTalkKey(InputAction.CallbackContext context)
		{
			_isTalkKeyHeld = context.ReadValueAsButton();
			ApplyTransmission();
		}

		private void ApplySettings()
		{
			if (!IsReady) return;

			Vivox.VivoxGlobalAudioSettings.NoiseSuppressionEnabled = VoiceSettings.NoiseSuppression;

			// Device levels are per login: set before one, Vivox fails them with "Target Object Does Not Exist".
			if (!Vivox.IsLoggedIn) return;

			Vivox.SetInputDeviceVolume(ToVivoxLevel(VoiceSettings.InputVolume));
			Vivox.SetOutputDeviceVolume(ToVivoxAttenuation(VoiceSettings.OutputVolume));
			ApplyTransmission();

			foreach (var participant in _peers.Values) ApplyPeer(participant);
		}

		private void ApplyTransmission()
		{
			if (!IsReady || !Vivox.IsLoggedIn) return;

			var isOpen = VoiceSettings.TalkMode == VoiceTalkMode.OpenMic || _isTalkKeyHeld;
			var changed = isOpen != _isSending;
			_isSending = isOpen;

			if (isOpen == Vivox.IsInputDeviceMuted)
			{
				if (isOpen) Vivox.UnmuteInputDevice();
				else Vivox.MuteInputDevice();
			}

			if (changed) OnTalkStateChanged?.Invoke();
		}

		private static void ApplyPeer(VivoxParticipant participant) =>
			participant.SetLocalVolume(ToVivoxAttenuation(VoiceSettings.GetPlayerVolume(participant.PlayerId)));

		// 0..1 onto Vivox's -50..+50, 0.5 being the level recorded.
		private static int ToVivoxLevel(float volume) => Mathf.RoundToInt(Mathf.Lerp(-50f, 50f, volume));

		// 0..1 onto Vivox's -50..0: never louder than sent.
		private static int ToVivoxAttenuation(float volume) => Mathf.RoundToInt(Mathf.Lerp(-50f, 0f, volume));
	}
}
