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
		[Tooltip("How long a notice stands before fading. Long enough to read, short enough that the next hand's announcements never queue behind it.")]
		[MinValue(0f)]
		[SerializeField] private float _lifetime = 2f;

		protected void Announce(string playerName, string action, string detail)
		{
			if (!_noticePrefab || !_container) return;

			var notice = Instantiate(_noticePrefab, _container);
			notice.Show(playerName, action, detail, _lifetime);
		}

		protected static string NameOf(ulong clientId)
		{
			var player = PokerPlayer.Find(clientId);
			return player ? player.DisplayName : $"Player {clientId}";
		}
	}
}
