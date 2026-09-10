using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player.Camera;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Camera
{
	// Looking down at this player's own place at the table — the row of cards, or the spot a staked cap
	// lands on. Which of the two is a serialized choice rather than two scripts: the behaviour is the
	// same to the letter and only the transform differs, so splitting it would be two files saying one
	// thing.
	public class PokerOwnSeatCameraState : PlayerCameraLookAtState
	{
		[Header("Seat")]
		[SerializeField] private PokerSeatAnchor _anchor = PokerSeatAnchor.Card;

		[Header("References")]
		[SerializeField] private PokerPlayerData _data;

		protected PokerPlayerData Data => _data;

		protected override void OnInitialize()
		{
			if (!_data) _data = GetComponentInParent<PokerPlayerData>();
		}

		protected override Transform ResolveTarget() => OwnSeatAnchor();

		// Read fresh each time rather than cached at spawn: chairs are moved and hidden as the table is
		// laid, and which chair is ours is a replicated value that arrives after this object exists.
		protected Transform OwnSeatAnchor()
		{
			var mode = PokerGameMode.Instance;
			if (!mode || !_data) return null;

			foreach (var seat in mode.Seats)
			{
				if (!seat || seat.SeatIndex != _data.SeatIndex.Value) continue;

				return _anchor switch
				{
					PokerSeatAnchor.Item => seat.ItemAnchor,
					PokerSeatAnchor.Ahead => seat.AheadAnchor,
					_ => seat.CardAnchor
				};
			}

			return null;
		}
	}
}
