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

		[Header("Match")]
		[Tooltip("Read on top when the first hand of a match is about to be dealt.")]
		[SerializeField] private string _matchStartedTitle = "MATCH START";

		[Tooltip("Read under it. {0} is how many are playing.")]
		[SerializeField] private string _matchStartedText = "{0} PLAYERS";

		[Tooltip("Seconds it stands. Negative uses the feed's own.")]
		[SerializeField] private float _matchStartedLifetime = 3f;

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

				case PokerNoticeKind.ItemDetail:
					AnnounceItemDetail(notice);
					break;

				case PokerNoticeKind.MatchStarted:
					Announce(_matchStartedTitle, string.Format(_matchStartedText, notice.Amount), _matchStartedLifetime);
					break;

				case PokerNoticeKind.ItemBonus:
					Announce(NameOf(notice.ActorClientId), string.Format(_itemBonusVerb, notice.Amount));
					break;
			}
		}

		// Told to this player alone: what the item did to them, the card included.
		private void AnnounceItemDetail(PokerNotice notice)
		{
			var module = GameMode.FindModule<PokerItemModule>();
			if (!module || !module.TryGetItem(notice.Item, out var item) || string.IsNullOrEmpty(item.PrivateNoticeVerb)) return;

			Announce(NameOf(notice.ActorClientId), string.Format(item.PrivateNoticeVerb, notice.Card));
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
