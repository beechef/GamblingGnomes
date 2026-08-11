using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.Controller
{
	public class PlayerSpawnPoint : MonoBehaviour
	{
		public NetworkObjectReference Player { get; private set; }
		public bool IsValid => Player.TryGet(out _);

		public void SetPlayer(NetworkObject player)
		{
			Player = new NetworkObjectReference(player);
		}

		public void ClearPlayer()
		{
			Player = default;
		}
	}
}
