using Unity.Netcode.Components;

namespace Game.Runtime.Player
{
	// Movement is not critical gameplay state, so it follows the project's Owner Authority rule
	// rather than Netcode's server-authoritative default.
	public class OwnerNetworkTransform : NetworkTransform
	{
		protected override bool OnIsServerAuthoritative() => false;
	}
}
