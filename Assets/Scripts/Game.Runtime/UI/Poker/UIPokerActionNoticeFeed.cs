using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Items;
using Localization;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// Turns what the table is told into something on screen: accepted actions, items played, and the item
	// news only this player receives. Each becomes a notice that fades in, lingers, and leaves.
	// Every notice names who it is about ("BOB ALL IN").
	public class UIPokerActionNoticeFeed : UIPokerNoticeFeed
	{
		[Header("Match")]
		[Tooltip("Seconds the match start notice stands. Negative uses the feed's own.")]
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
					Announce(NameOf(notice.ActorClientId), ActionText(notice.Action));
					break;

				case PokerNoticeKind.ItemUsed:
					AnnounceItemUsed(notice);
					break;

				case PokerNoticeKind.ItemDetail:
					AnnounceItemDetail(notice);
					break;

				case PokerNoticeKind.MatchStarted:
					Announce(Localizer.Get(LocalizationKeys.Poker.Notice.MatchStarted), Localizer.Format(LocalizationKeys.Poker.Notice.MatchPlayers, notice.Amount), _matchStartedLifetime);
					break;

				case PokerNoticeKind.ItemBonus:
					Announce(NameOf(notice.ActorClientId), Localizer.Format(LocalizationKeys.Poker.Notice.ItemBonus, notice.Amount));
					break;

				case PokerNoticeKind.ItemReward:
					Announce(NameOf(notice.ActorClientId), Localizer.Format(LocalizationKeys.Poker.Notice.ItemReward, notice.Amount));
					break;

				case PokerNoticeKind.ItemsLost:
					Announce(NameOf(notice.ActorClientId), Localizer.Format(LocalizationKeys.Poker.Notice.ItemsLost, notice.Amount));
					break;

				case PokerNoticeKind.ItemOutcome:
					AnnounceItemOutcome(notice);
					break;
			}
		}

		private void AnnounceItemOutcome(PokerNotice notice)
		{
			var module = GameMode.FindModule<PokerItemModule>();
			if (!module || !module.TryGetItem(notice.Item, out var item)) return;

			var verb = item.GetOutcomeVerb(notice.Amount);
			if (string.IsNullOrEmpty(verb)) return;

			if (notice.HasTarget) AnnounceTarget(NameOf(notice.ActorClientId), verb, NameOf(notice.TargetClientId));
			else Announce(NameOf(notice.ActorClientId), verb);
		}

		private string ActionText(PokerActionType action) => action switch
		{
			PokerActionType.Bet => Localizer.Get(LocalizationKeys.Poker.Action.Bet),
			PokerActionType.Fold => Localizer.Get(LocalizationKeys.Poker.Action.Fold),
			PokerActionType.AllIn => Localizer.Get(LocalizationKeys.Poker.Action.AllIn),
			_ => action.ToString().ToUpperInvariant()
		};

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

			// The player an item asks to answer reads it on the response panel instead.
			if (item.NeedsResponse && notice.HasTarget && notice.TargetClientId == LocalClientId) return;

			if (notice.HasTarget) AnnounceTarget(NameOf(notice.ActorClientId), item.NoticeVerb, NameOf(notice.TargetClientId));
			else Announce(NameOf(notice.ActorClientId), item.NoticeVerb);
		}
	}
}
