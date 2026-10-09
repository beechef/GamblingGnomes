using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Items;
using Game.Runtime.GameMode.Poker.Visual;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Player
{
	// Starts this player's item performances (the PokerItemPerformance children below it) from the item module's
	// events, on every screen: the one for the item played, when this player used it or was its target.
	public class PokerItemPerformer : PokerVisual
	{
		[Tooltip("Where a prop raised to the mouth or nose sits on this player's own screen: low and close before the eyes, since their real face is the camera.")]
		[SerializeField] private Transform _ownFacePoint;

		private readonly List<PokerItemPerformance> _performances = new();

		private PokerPlayer _player;
		private PokerItemModule _module;
		private PokerNoticeChannel _notices;

		private void Awake()
		{
			_player = GetComponentInParent<PokerPlayer>(true);
			GetComponentsInChildren(true, _performances);
		}

		protected override void OnBind()
		{
			_module = GameMode.FindModule<PokerItemModule>();
			if (_module)
			{
				_module.OnItemUsed += HandleItemUsed;
				_module.OnItemResolved += HandleItemResolved;
				_module.OnCardRewriting += HandleCardRewriting;
				_module.StreetSerial.OnValueChanged += HandleStreetSerialChanged;
				_module.TableRules.OnListChanged += HandleTableRulesChanged;
			}

			_notices = GameMode.Notices;
			if (_notices) _notices.OnNotice += HandleNotice;
		}

		protected override void OnUnbind()
		{
			if (_notices) _notices.OnNotice -= HandleNotice;
			_notices = null;

			if (_module)
			{
				_module.TableRules.OnListChanged -= HandleTableRulesChanged;
				_module.StreetSerial.OnValueChanged -= HandleStreetSerialChanged;
				_module.OnCardRewriting -= HandleCardRewriting;
				_module.OnItemResolved -= HandleItemResolved;
				_module.OnItemUsed -= HandleItemUsed;
			}

			_module = null;

			foreach (var performance in _performances) performance.Stop();
		}

		private void HandleItemUsed(ulong userClientId, PokerItemType item, ulong targetClientId)
		{
			if (!_player || !_module.TryGetItem(item, out var asset)) return;

			var isUser = _player.ClientId == userClientId;
			var isTarget = _player.ClientId == targetClientId;
			if (!isUser && !isTarget) return;

			var other = PokerPlayer.Find(isUser ? targetClientId : userClientId);

			foreach (var performance in _performances)
			{
				if (performance.Item == asset && performance.PlaysFor(isUser, isTarget)) performance.Play(GameMode, _module, userClientId, other, _ownFacePoint);
			}
		}

		private void HandleItemResolved(ulong userClientId, PokerItemType item)
		{
			foreach (var performance in _performances)
			{
				if (performance.Item && performance.Item.Type == item) performance.Resolve(userClientId);
			}
		}

		private void HandleCardRewriting(PokerCardPlace place, PokerCardFlickerFaces faces, CardData card)
		{
			foreach (var performance in _performances) performance.ReceiveRewrite(place, card);
		}

		private void HandleNotice(PokerNotice notice)
		{
			if (notice.Kind != PokerNoticeKind.ItemOutcome) return;

			foreach (var performance in _performances)
			{
				if (performance.Item && performance.Item.Type == notice.Item) performance.ReceiveOutcome(notice.ActorClientId, notice.Amount);
			}
		}

		private void HandleStreetSerialChanged(int previous, int current) => RefreshHolds();
		private void HandleTableRulesChanged(NetworkListEvent<PokerItemTableRule> change) => RefreshHolds();

		private void RefreshHolds()
		{
			foreach (var performance in _performances) performance.RefreshHold();
		}
	}
}
