using Game.Runtime.GameMode;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.Controller
{
	public class GameModeController : NetworkBehaviour
	{
		public static GameModeController Instance { get; private set; }
		public static IGameMode CurrentGameMode => Instance != null ? Instance._gameMode : null;

		// Domain Reload is disabled, so statics survive between play sessions.
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			Instance = null;
		}

		public static T CastedGameMode<T>() where T : class, IGameMode
		{
			return CurrentGameMode as T;
		}

		[Tooltip("The MonoBehaviour in this Gameplay scene that implements IGameMode (e.g. SandboxGameMode).")]
		[SerializeField] private MonoBehaviour _gameModeBehaviour;

		private IGameMode _gameMode;

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}

			Instance = this;

			_gameMode = _gameModeBehaviour as IGameMode;
			if (_gameMode == null)
			{
				Debug.LogError("[GameModeController] Assigned _gameModeBehaviour does not implement IGameMode.");
			}
		}

		public override void OnDestroy()
		{
			if (Instance == this)
				Instance = null;

			base.OnDestroy();
		}
	}
}
