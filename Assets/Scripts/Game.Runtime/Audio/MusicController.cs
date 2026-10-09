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
		[Required]
		[SerializeField] private AudioManager _audio;

		[Required]
		[SerializeField] private AudioEvent _menuMusic;

		[Required]
		[SerializeField] private AudioEvent _gameplayAmbience;

		private GameNetworkManager _network;
		private AudioHandle _menu;
		private AudioHandle _ambience;

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
		}

		private void HandleConnectStarted() => Stop(ref _menu);

		private void HandleTableEntered()
		{
			Stop(ref _menu);
			if (!_ambience.IsValid) _ambience = _audio.Play(_gameplayAmbience);
		}

		private void HandleTableLeaving() => Stop(ref _ambience);

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
