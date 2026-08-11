using Unity.Netcode.Components;

namespace Game.Runtime.Player
{
	public class OwnerNetworkTransform : NetworkTransform
	{
		protected override bool OnIsServerAuthoritative() => false;
	}
}
