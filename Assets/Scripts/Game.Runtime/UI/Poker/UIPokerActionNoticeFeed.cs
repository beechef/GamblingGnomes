using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Items;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// Turns what the table is told into something on screen: accepted actions, items played, and the item
	// news only this player receives. Each becomes a notice that fades in, lingers, and leaves.
	public class UIPokerActionNoticeFeed : UIPokerNoticeFeed
	{
		[Header("Items")]
		[Tooltip("Read after the name when somebody is dealt extra items for losing the last hand.")]
		[SerializeField] private string _itemBonusVerb = "LOST LAST HAND: +{0} ITEM";

		private PokerNoticeChannel _channel;

		protected override void OnBind()
		{
			_channel = GameMode.Notices;
			if (_channel) _channel.OnNotice += HandleNotice;
		}

		protected override void OnUnbind()
		{
			if (_channel) _channel.OnNotice -= HandleNotice;
			_channel = null;
		}

		private void HandleNotice(PokerNotice notice)
		{
			switch (notice.Kind)
			{
				case PokerNoticeKind.Action:
					Announce(NameOf(notice.ActorClientId), notice.Action.ToString().ToUpperInvariant());
					break;

				case PokerNoticeKind.ItemUsed:
					AnnounceItemUsed(notice);
					break;

				case PokerNoticeKind.ItemBonus:
					Announce(NameOf(notice.ActorClientId), string.Format(_itemBonusVerb, notice.Amount));
					break;
			}
		}

		private void AnnounceItemUsed(PokerNotice notice)
		{
			var module = GameMode.FindModule<PokerItemModule>();
			if (!module || !module.TryGetItem(notice.Item, out var item)) return;

			if (notice.HasTarget) AnnounceTarget(NameOf(notice.ActorClientId), item.NoticeVerb, NameOf(notice.TargetClientId));
			else Announce(NameOf(notice.ActorClientId), item.NoticeVerb);
		}
	}
}
