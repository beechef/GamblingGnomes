using Game.Runtime.Controller;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.Audio
{
	// Which bed of sound is under the game: the menu music while the player is outside a table (launch,
	// every menu screen, the quick match wait), the gameplay ambience while they are in one. At most one
	// plays; the menu stops as a way into a table starts, the ambience as the table is left.
	public class MusicController : MonoBehaviour
	{
		public static MusicController Instance { get; private set; }

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics() => Instance = null;

		[Required]
		[SerializeField] private AudioManager _audio;

		[Required]
		[SerializeField] private AudioEvent _menuMusic;

		[Required]
		[SerializeField] private AudioEvent _gameplayAmbience;

		[Tooltip("The ambience parameter the table moves through its beats (0 waiting, 1 playing, 2 eating).")]
		[FMODUnity.ParamRef]
		[SerializeField] private string _ambienceParameter = "Parameter 1";

		private float _ambienceValue;

		private GameNetworkManager _network;
		private AudioHandle _menu;
		private AudioHandle _ambience;

		private void Awake()
		{
			if (Instance && Instance != this)
			{
				Destroy(this);
				return;
			}

			Instance = this;
		}

		private void Start()
		{
			_network = GameNetworkManager.Instance;
			if (_network)
			{
				_network.OnConnectStarted += HandleConnectStarted;
				_network.OnLobbyEnter += HandleTableEntered;
				_network.OnGameLeaving += HandleTableLeaving;
				_network.OnGameLeft += HandleTableLeft;
				_network.OnConnectFailed += HandleConnectFailed;
			}

			if (_network && _network.IsInGame) HandleTableEntered();
			else PlayMenu();
		}

		private void OnDestroy()
		{
			if (_network)
			{
				_network.OnConnectFailed -= HandleConnectFailed;
				_network.OnGameLeft -= HandleTableLeft;
				_network.OnGameLeaving -= HandleTableLeaving;
				_network.OnLobbyEnter -= HandleTableEntered;
				_network.OnConnectStarted -= HandleConnectStarted;
			}

			Stop(ref _ambience);
			Stop(ref _menu);

			if (Instance == this) Instance = null;
		}

		// Kept when no ambience is playing, so the bed starts at the beat the table is already in.
		public void SetAmbienceLevel(float value)
		{
			_ambienceValue = value;
			_audio.SetParameter(_ambience, _ambienceParameter, value);
		}

		private void HandleConnectStarted() => Stop(ref _menu);

		private void HandleTableEntered()
		{
			Stop(ref _menu);
			if (_ambience.IsValid) return;

			_ambience = _audio.Play(_gameplayAmbience);
			_audio.SetParameter(_ambience, _ambienceParameter, _ambienceValue);
		}

		private void HandleTableLeaving()
		{
			Stop(ref _ambience);
			_ambienceValue = 0f;
		}

		private void HandleTableLeft()
		{
			Stop(ref _ambience);
			PlayMenu();
		}

		// A failed attempt may leave nothing to tear down, so OnGameLeft never comes to bring the menu back.
		private void HandleConnectFailed(string reason)
		{
			if (_network.IsInGame) return;

			Stop(ref _ambience);
			PlayMenu();
		}

		private void PlayMenu()
		{
			if (!_menu.IsValid) _menu = _audio.Play(_menuMusic);
		}

		private void Stop(ref AudioHandle handle)
		{
			if (!handle.IsValid) return;

			if (_audio) _audio.Stop(handle);
			handle = default;
		}
	}
}
