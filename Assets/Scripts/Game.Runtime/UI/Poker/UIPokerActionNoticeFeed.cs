using Game.Runtime.GameMode.Poker;

namespace Game.Runtime.UI.Poker
{
	// Turns the table's replicated announcement into something on screen: every accepted action arrives
	// through Data.ActionNotice, and each one becomes a notice that fades in, lingers, and leaves.
	public class UIPokerActionNoticeFeed : UIPokerNoticeFeed
	{
		protected override void OnBind()
		{
			Data.ActionNotice.OnValueChanged += HandleNotice;
		}

		protected override void OnUnbind()
		{
			Data.ActionNotice.OnValueChanged -= HandleNotice;
		}

		private void HandleNotice(PokerActionNotice previous, PokerActionNotice current)
		{
			// Sequence zero is the default a fresh table spawns with, not an announcement.
			if (current.Sequence == 0) return;

			AnnounceAmount(NameOf(current.ClientId), current.Action.ToString().ToUpperInvariant(), current.Amount);
		}
	}
}
