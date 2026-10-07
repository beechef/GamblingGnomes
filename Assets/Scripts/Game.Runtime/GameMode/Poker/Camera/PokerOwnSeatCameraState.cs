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

		[Tooltip("Metres the aim is raised above the anchor, so the shot sits higher and the HUD along the bottom clears what it frames. 0 aims at the anchor itself.")]
		[SerializeField] private float _aimHeight;

		[Header("References")]
		[SerializeField] private PokerPlayerData _data;

		protected PokerPlayerData Data => _data;

		protected override void OnInitialize()
		{
			if (!_data) _data = GetComponentInParent<PokerPlayerData>();
		}

		private Transform _aimPoint;

		protected override Transform ResolveTarget()
		{
			var anchor = OwnSeatAnchor();
			if (!anchor || Mathf.Approximately(_aimHeight, 0f)) return anchor;

			// One point riding the anchor, raised in world space. Kept on the chair and reused by whoever sits
			// there next, so players coming and going never pile points up.
			if (!_aimPoint || _aimPoint.parent != anchor)
			{
				var pointName = $"{name}_AimPoint";
				_aimPoint = anchor.Find(pointName);
				if (!_aimPoint)
				{
					_aimPoint = new GameObject(pointName).transform;
					_aimPoint.SetParent(anchor, false);
				}
			}

			_aimPoint.position = anchor.position + Vector3.up * _aimHeight;

			return _aimPoint;
		}

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
					PokerSeatAnchor.BetItem => seat.BetItemAnchor,
					PokerSeatAnchor.Ahead => seat.AheadAnchor,
					_ => seat.CardAnchor
				};
			}

			return null;
		}
	}
}
