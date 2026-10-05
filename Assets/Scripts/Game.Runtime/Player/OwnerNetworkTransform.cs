using Unity.Netcode.Components;

namespace Game.Runtime.Player
{
	public class OwnerNetworkTransform : NetworkTransform
	{
		// A bot's owner never connects, so the server moves its body.
		protected override bool OnIsServerAuthoritative() => PlayerBot.IsBot(OwnerClientId);
	}
}
