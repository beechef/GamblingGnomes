using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// Where announcements land. What is worth announcing is the subclass's business — an accepted action,
	// an accusation, a verdict — and every one of them arrives on the same card, so the table reads them
	// all the same way. The feed only listens and spawns; what a notice looks like is the prefab's affair.
	public abstract class UIPokerNoticeFeed : UIPokerView
	{
		[Header("References")]
		[Required]
		[SerializeField] private RectTransform _container;

		[Required]
		[SerializeField] private UIPokerActionNotice _noticePrefab;

		[Header("Timing")]
		[Tooltip("How long a notice stands when the caller has no opinion. Long enough to read, short enough that the next hand's announcements never queue behind it.")]
		[MinValue(0f)]
		[SerializeField] private float _lifetime = 2f;

		[Header("Currency")]
		[Tooltip("What this feed's numbers are counted in. Empty keeps the sprite the notice prefab was authored with.")]
		[SerializeField] private Sprite _amountIcon;

		// A caller that knows how long its moment lasts says so — a verdict held on screen for three
		// seconds wants a notice that is gone in three seconds, not one on the feed's own clock.
		protected void Announce(string playerName, string action, float lifetime = -1f)
			=> Spawn()?.Show(playerName, action, Resolve(lifetime));

		protected void AnnounceAmount(string playerName, string action, int amount, float lifetime = -1f)
			=> Spawn()?.ShowAmount(playerName, action, amount, _amountIcon, Resolve(lifetime));

		protected void AnnounceTarget(string playerName, string action, string targetName, float lifetime = -1f)
			=> Spawn()?.ShowTarget(playerName, action, targetName, Resolve(lifetime));

		private float Resolve(float lifetime) => lifetime < 0f ? _lifetime : lifetime;

		private UIPokerActionNotice Spawn()
		{
			if (!_noticePrefab || !_container) return null;

			return Instantiate(_noticePrefab, _container);
		}

		protected static string NameOf(ulong clientId)
		{
			var player = PokerPlayer.Find(clientId);
			return player ? player.DisplayName : $"Player {clientId}";
		}
	}
}
