using System;
using Unity.Netcode;

namespace Game.Runtime.GameMode.Poker
{
	// How the table tells people things. Two roads: announced to everyone, or told only to the players it
	// is about. RPCs rather than a replicated value, because a notice is an event — two written in one frame
	// to a NetworkVariable reach a client as the second one only — and a late joiner has no use for the
	// notices it missed.
	public class PokerNoticeChannel : NetworkBehaviour
	{
		// Raised on every peer that received the notice, public and private alike.
		public event Action<PokerNotice> OnNotice;

		public void ServerAnnounce(PokerNotice notice)
		{
			if (!IsServer || !IsSpawned) return;

			notice.IsPrivate = false;
			AnnounceRPC(notice);
		}

		public void ServerTell(ulong clientId, PokerNotice notice)
		{
			if (!IsServer || !IsSpawned) return;

			notice.IsPrivate = true;
			TellRPC(notice, RpcTarget.Single(clientId, RpcTargetUse.Temp));
		}

		[Rpc(SendTo.Everyone)]
		private void AnnounceRPC(PokerNotice notice) => OnNotice?.Invoke(notice);

		[Rpc(SendTo.SpecifiedInParams)]
		private void TellRPC(PokerNotice notice, RpcParams rpcParams = default) => OnNotice?.Invoke(notice);
	}
}
