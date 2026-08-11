using Game.Runtime.Controller;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Sandbox
{
	public class SandboxGameMode : NetworkBehaviour, IGameMode
	{
		[field: SerializeField] public PlayerManager PlayerManager { get; private set; }

		public void StartGame()
		{
			Debug.Log("[SandboxGameMode] StartGame — no mode-specific logic yet.");
		}

		public void ResetGame()
		{
			Debug.Log("[SandboxGameMode] ResetGame — no mode-specific logic yet.");
		}

		public void EndGame()
		{
			Debug.Log("[SandboxGameMode] EndGame — no mode-specific logic yet.");
		}
	}
}
