using System.Collections.Generic;
using DG.Tweening;
using Game.Runtime.GameMode.Poker.Items;
using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// Shows the prop an item is played with (PokerItemPropDatabase): appears, moves, holds, vanishes, then is
	// destroyed. Every screen sees it; nothing here decides a rule.
	public class PokerItemPropVisual : PokerVisual
	{
		[Required]
		[SerializeField] private PokerItemPropDatabase _database;

		// Props waiting for their item to finish before they vanish.
		private struct HeldProp
		{
			public ulong User;
			public PokerItemType Item;
			public Sequence Sequence;
			public float VanishAt;
		}

		private readonly List<HeldProp> _held = new();

		private PokerItemModule _module;

		protected override void OnBind()
		{
			_module = GameMode.FindModule<PokerItemModule>();
			if (!_module) return;

			_module.OnItemUsed += HandleItemUsed;
			_module.OnItemResolved += HandleItemResolved;
		}

		protected override void OnUnbind()
		{
			if (_module)
			{
				_module.OnItemResolved -= HandleItemResolved;
				_module.OnItemUsed -= HandleItemUsed;
			}

			_module = null;
			_held.Clear();
		}

		private void HandleItemUsed(ulong userClientId, PokerItemType item, ulong targetClientId)
		{
			if (!_database || !_database.TryGet(item, out var entry)) return;

			var user = AnchorOf(PokerPlayer.Find(userClientId));
			var target = targetClientId == PokerGameData.NoTurn ? null : AnchorOf(PokerPlayer.Find(targetClientId));

			switch (entry.Place)
			{
				case PokerItemPropPlace.User:
					Play(entry, userClientId, item, user, user, target);
					break;
				case PokerItemPropPlace.Target:
					Play(entry, userClientId, item, target, user, target);
					break;
				case PokerItemPropPlace.UserAndTarget:
					Play(entry, userClientId, item, user, user, target);
					if (target != user) Play(entry, userClientId, item, target, user, user);
					break;
				case PokerItemPropPlace.LastFaceDownBoardCard:
					Play(entry, userClientId, item, LastFaceDownBoardCard(), user, target);
					break;
			}
		}

		// The hold is cut short: the prop goes as soon as its item is done.
		private void HandleItemResolved(ulong userClientId, PokerItemType item)
		{
			for (var i = _held.Count - 1; i >= 0; i--)
			{
				var held = _held[i];
				if (held.User != userClientId || held.Item != item) continue;

				_held.RemoveAt(i);
				if (held.Sequence.IsActive() && held.Sequence.Elapsed() < held.VanishAt) held.Sequence.Goto(held.VanishAt, true);
			}
		}

		private void Play(in PokerItemPropDatabase.Entry entry, ulong userClientId, PokerItemType item, Transform place, Transform user, Transform faced)
		{
			if (!place) return;

			var end = place.position + entry.EndOffset;
			var start = entry.FromUser && user ? user.position : place.position + entry.StartOffset;

			var prop = Instantiate(entry.Prefab, start, Quaternion.identity);
			var restScale = prop.transform.localScale;
			prop.transform.localScale = Vector3.zero;

			if (entry.FaceTarget && faced) prop.transform.rotation = Quaternion.LookRotation(faced.position - start);

			var vanishAt = entry.AppearDuration + entry.MoveDuration + entry.HoldDuration;

			var sequence = DOTween.Sequence()
				.Append(prop.transform.DOScale(restScale, entry.AppearDuration).SetEase(Ease.OutBack))
				.Append(prop.transform.DOMove(end, entry.MoveDuration).SetEase(entry.MoveEase))
				.AppendInterval(entry.HoldDuration)
				.Append(prop.transform.DOScale(Vector3.zero, entry.VanishDuration).SetEase(Ease.InBack))
				.OnComplete(() => Destroy(prop))
				.SetLink(prop);

			if (entry.Spin != Vector3.zero && vanishAt > entry.AppearDuration)
			{
				sequence.Insert(entry.AppearDuration,
					prop.transform.DOLocalRotate(entry.Spin, vanishAt - entry.AppearDuration, RotateMode.LocalAxisAdd).SetEase(Ease.OutCubic));
			}

			if (entry.HoldUntilResolved) _held.Add(new HeldProp { User = userClientId, Item = item, Sequence = sequence, VanishAt = vanishAt });
		}

		// A prop about you sits where your own eye can see it; about anybody else, on their body.
		private static Transform AnchorOf(PokerPlayer player)
		{
			if (!player || !player.Rig) return null;

			return player.IsOwner ? player.Rig.SelfFocusPoint : player.Rig.FocusPoint;
		}

		private Transform LastFaceDownBoardCard()
		{
			var board = PokerBoardVisual.Instance;
			if (!board || !Data) return null;

			var cards = board.Cards;
			for (var i = cards.Count - 1; i >= 0; i--)
			{
				if (cards[i] && !Data.IsCommunityCardRevealed(i)) return cards[i].transform;
			}

			return null;
		}
	}
}
