using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// Turns the table's replicated announcement into something on screen: every accepted action arrives
	// through Data.ActionNotice, and each one becomes a notice that fades in, lingers, and leaves. The
	// feed only listens and spawns — what a notice looks like is entirely the prefab's business.
	public class UIPokerActionNoticeFeed : UIPokerView
	{
		[Header("References")]
		[Required]
		[SerializeField] private RectTransform _container;

		[Required]
		[SerializeField] private UIPokerActionNotice _noticePrefab;

		[Header("Timing")]
		[Tooltip("How long a notice stands before fading. Long enough to read, short enough that the next hand's announcements never queue behind it.")]
		[MinValue(0f)]
		[SerializeField] private float _lifetime = 2f;

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
			if (current.Sequence == 0 || !_noticePrefab || !_container) return;

			var player = PokerPlayer.Find(current.ClientId);
			var playerName = player ? player.DisplayName : $"Player {current.ClientId}";

			var notice = Instantiate(_noticePrefab, _container);
			notice.Show(playerName, current.Action.ToString().ToUpperInvariant(), current.Amount, _lifetime);
		}
	}
}
