using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player.Camera;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Camera
{
	// Looking down at this player's own place at the table — the row of cards, or the spot a staked cap
	// lands on. One script for both because the behaviour is identical and only the anchor differs; which
	// of the two is the shot this object serves, so there is one enum rather than two saying the same
	// thing in different words.
	public class PokerOwnSeatCameraState : PlayerCameraLookAtState, IPokerCameraShot
	{
		[Header("Shot")]
		[Tooltip("Which beat this object answers to. OwnCards looks at the seat's card anchor, OwnItems at where a wagered cap is put down.")]
		[SerializeField] private PokerCameraShot _shot = PokerCameraShot.OwnCards;

		[Header("References")]
		[SerializeField] private PokerPlayerData _data;

		public PokerCameraShot Shot => _shot;

		protected override void OnInitialize()
		{
			if (!_data) _data = GetComponentInParent<PokerPlayerData>();
		}

		// Read fresh each time rather than cached at spawn: chairs are moved and hidden as the table is
		// laid, and which chair is ours is a replicated value that arrives after this object exists.
		protected override Transform ResolveTarget()
		{
			var mode = PokerGameMode.Instance;
			if (!mode || !_data) return null;

			foreach (var seat in mode.Seats)
			{
				if (!seat || seat.SeatIndex != _data.SeatIndex.Value) continue;

				return _shot == PokerCameraShot.OwnItems ? seat.ItemAnchor : seat.CardAnchor;
			}

			return null;
		}
	}
}
