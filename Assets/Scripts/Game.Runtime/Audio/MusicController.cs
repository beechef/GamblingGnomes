using Game.Runtime.Controller;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.Audio
{
	// Which bed of sound is under the game: the menu track while the player is outside a table (launch, every
	// menu screen, the quick match wait), the gameplay track while they are in one. Each track is an object
	// switched on and off here; what it plays and what moves it lives on the track itself.
	public class MusicController : MonoBehaviour
	{
		[Required]
		[SerializeField] private GameObject _menuTrack;

		[Required]
		[SerializeField] private GameObject _gameplayTrack;

		private GameNetworkManager _network;

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

			var inGame = _network && _network.IsInGame;
			_gameplayTrack.SetActive(inGame);
			_menuTrack.SetActive(!inGame);
		}

		private void OnDestroy()
		{
			if (!_network) return;

			_network.OnConnectFailed -= HandleConnectFailed;
			_network.OnGameLeft -= HandleTableLeft;
			_network.OnGameLeaving -= HandleTableLeaving;
			_network.OnLobbyEnter -= HandleTableEntered;
			_network.OnConnectStarted -= HandleConnectStarted;
		}

		private void HandleConnectStarted() => _menuTrack.SetActive(false);

		private void HandleTableEntered()
		{
			_menuTrack.SetActive(false);
			_gameplayTrack.SetActive(true);
		}

		private void HandleTableLeaving() => _gameplayTrack.SetActive(false);

		private void HandleTableLeft()
		{
			_gameplayTrack.SetActive(false);
			_menuTrack.SetActive(true);
		}

		// A failed attempt may leave nothing to tear down, so OnGameLeft never comes to bring the menu back.
		private void HandleConnectFailed(string reason)
		{
			if (_network.IsInGame) return;

			_gameplayTrack.SetActive(false);
			_menuTrack.SetActive(true);
		}
	}
}
