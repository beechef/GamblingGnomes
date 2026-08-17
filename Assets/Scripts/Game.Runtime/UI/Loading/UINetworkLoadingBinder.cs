using System;
using System.Threading;
using Game.Runtime.Controller;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Runtime.UI.Loading
{
	// Puts the loading screen over the wait between pressing Play and sitting at a table. The screen
	// itself knows nothing about lobbies or scenes; this is the piece that does, so the two can be
	// changed apart — and so bootstrap's network service never has to know a UI exists.
	public class UINetworkLoadingBinder : MonoBehaviour
	{
		[Header("References")]
		[Required]
		[SerializeField] private UILoadingScreen _screen;

		[Header("Text")]
		[SerializeField] private string _connectingTitle = "CONNECTING";
		[SerializeField] private string _loadingTitle = "LOADING";
		[SerializeField] private string _leavingTitle = "LEAVING";

		[Tooltip("How far the bar has come by the time the scene itself starts loading — the wait before that has no number to report.")]
		[Range(0f, 1f)]
		[SerializeField] private float _connectingProgress = 0.2f;

		private NetworkSceneManager _sceneManager;
		private CancellationTokenSource _sceneLoad;

		// Subscribed from Start, not OnEnable: the network service wakes in the same scene as this screen in
		// no guaranteed order, and an OnEnable that reads a null Instance subscribes to nothing and never
		// tries again — a binder that is silently deaf for the rest of the session.
		private void Start()
		{
			var network = GameNetworkManager.Instance;
			if (network)
			{
				network.OnConnectStarted += HandleConnectStarted;
				network.OnHostStarted += HandleConnected;
				network.OnLobbyEnter += HandleConnected;
				network.OnGameLeaving += HandleGameLeaving;
				network.OnConnectFailed += HandleConnectFailed;
				network.OnGameLeft += HandleGameLeft;
			}

			// The scene manager only exists once a host or client is up, so it is picked up as the
			// session starts rather than assumed to be there.
			if (NetworkManager.Singleton) NetworkManager.Singleton.OnClientStarted += BindSceneManager;
		}

		private void OnDestroy()
		{
			var network = GameNetworkManager.Instance;
			if (network)
			{
				network.OnConnectStarted -= HandleConnectStarted;
				network.OnHostStarted -= HandleConnected;
				network.OnLobbyEnter -= HandleConnected;
				network.OnGameLeaving -= HandleGameLeaving;
				network.OnConnectFailed -= HandleConnectFailed;
				network.OnGameLeft -= HandleGameLeft;
			}

			if (NetworkManager.Singleton) NetworkManager.Singleton.OnClientStarted -= BindSceneManager;

			UnbindSceneManager();
			CancelSceneLoad();
		}

		// The click itself, before Steam has been asked. Nothing here has a number to report yet — the bar
		// starts from empty and only moves once the answer comes back.
		private void HandleConnectStarted() => _screen.Show(_connectingTitle);

		private void HandleConnected()
		{
			_screen.Show(_connectingTitle);
			_screen.SetProgress(_connectingProgress);

			BindSceneManager();
		}

		// Covers the walk out as well as the walk in: the teardown unloads the gameplay scene, so without
		// this the table blinks out and the menu appears over whatever is left of the frame.
		private void HandleGameLeaving() => _screen.Show(_leavingTitle);

		private void HandleConnectFailed(string reason) => _screen.Hide();

		private void HandleGameLeft()
		{
			// The session that owned it is gone, so the subscription goes with it rather than sitting on a
			// scene manager the next host will replace.
			UnbindSceneManager();
			CancelSceneLoad();

			_screen.Hide();
		}

		private void BindSceneManager()
		{
			var manager = NetworkManager.Singleton;
			if (!manager || manager.SceneManager == null || _sceneManager == manager.SceneManager) return;

			UnbindSceneManager();

			_sceneManager = manager.SceneManager;
			_sceneManager.OnLoad += HandleSceneLoadStarted;

			// OnLoadComplete for this client, not OnLoadEventCompleted: the latter is the server's "everyone
			// is in" and is never raised for a client that arrives through synchronization, which is exactly
			// how someone joining an existing table gets the scene — their screen would never come down.
			// OnLoadComplete is raised on both paths, and the local id is what makes it ours.
			_sceneManager.OnLoadComplete += HandleSceneLoadFinished;

			// The backstop for a client that synchronizes without loading anything, because it is already
			// standing in the right scene: no load event of any kind arrives, only this.
			_sceneManager.OnSynchronizeComplete += HandleSynchronizeComplete;
		}

		private void UnbindSceneManager()
		{
			if (_sceneManager == null) return;

			_sceneManager.OnLoad -= HandleSceneLoadStarted;
			_sceneManager.OnLoadComplete -= HandleSceneLoadFinished;
			_sceneManager.OnSynchronizeComplete -= HandleSynchronizeComplete;
			_sceneManager = null;
		}

		private void HandleSceneLoadStarted(ulong clientId, string sceneName, LoadSceneMode mode, AsyncOperation operation)
		{
			if (clientId != NetworkManager.Singleton.LocalClientId) return;

			_screen.Show(_loadingTitle);
			ReportProgress(operation).LogExceptionsAndForget();
		}

		private void HandleSceneLoadFinished(ulong clientId, string sceneName, LoadSceneMode mode)
		{
			if (clientId != NetworkManager.Singleton.LocalClientId) return;

			FinishLoading();
		}

		private void HandleSynchronizeComplete(ulong clientId)
		{
			if (clientId != NetworkManager.Singleton.LocalClientId) return;

			FinishLoading();
		}

		private void FinishLoading()
		{
			CancelSceneLoad();

			_screen.SetProgress(1f);
			_screen.Hide();
		}

		// Watched frame by frame because that is the only shape Unity offers for a load in flight; the
		// wait ends the moment the operation does, and cancels with the screen if the player leaves first.
		private async Awaitable ReportProgress(AsyncOperation operation)
		{
			CancelSceneLoad();
			_sceneLoad = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);

			var token = _sceneLoad.Token;

			try
			{
				while (operation != null && !operation.isDone)
				{
					// Unity reports 0..0.9 while loading and only reaches 1 once activation happens.
					_screen.SetProgress(Mathf.Lerp(_connectingProgress, 1f, operation.progress / 0.9f), true);

					await Awaitable.NextFrameAsync(token);
				}
			}
			catch (OperationCanceledException)
			{
			}
		}

		private void CancelSceneLoad()
		{
			if (_sceneLoad == null) return;

			_sceneLoad.Cancel();
			_sceneLoad.Dispose();
			_sceneLoad = null;
		}
	}
}
