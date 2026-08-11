using Unity.Netcode;

namespace Game.Runtime.Player
{
	// Empty on purpose: the home for future server-authoritative player state, kept separate
	// from PlayerController so replicated state never mixes with input handling.
	public class PlayerData : NetworkBehaviour
	{
	}
}
