using System;
using Game.Runtime.Audio;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Player
{
	// The Colorful pick's snap names its target by turning every head to them, so the snapper is snapping at
	// themselves when the table's focus is on their own seat. Read off replicated focus, the same on every screen.
	[Serializable]
	public class PokerSnapAtSelfChoice : AnimationAudioCueChoice
	{
		public override bool UseAlternate(Transform rig)
		{
			var player = rig ? rig.GetComponentInParent<PokerPlayer>() : null;
			var mode = PokerGameMode.Instance;

			return player && mode && mode.Data && mode.Data.FocusClientId.Value == player.ClientId;
		}
	}
}
