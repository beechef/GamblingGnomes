using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// Lays the chairs out for the number of people the lobby holds: three players sit at the points of
	// a triangle rather than filling three of six places and leaving a gap down one side. The chairs
	// are the ones the scene already placed — moved, never spawned, because a scene NetworkObject
	// created at runtime comes up with no GlobalObjectIdHash and never spawns at all.
	//
	// Every client runs the same arithmetic on the same replicated count, so the table is one picture
	// everywhere without the transforms themselves having to be synchronised.
	//
	// The chairs are held here as serialized references rather than read from PokerSeat.All: a seat
	// switched off before its OnNetworkSpawn never joins that registry, so a ring built from it would
	// lose the chairs it had just hidden and could never lay them again.
	public class PokerSeatRingVisual : MonoBehaviour
	{
		[Header("References")]
		[SerializeField] private PokerGameData _data;

		[Tooltip("Every chair at this table, laid out from the first. Order does not matter — each chair is placed at its own SeatIndex.")]
		[SerializeField] private List<PokerSeat> _seats = new();

		[Header("Ring")]
		[Tooltip("Off leaves every chair exactly where the scene put it, and this component only shows and hides them.")]
		[SerializeField] private bool _placeSeats = true;

		[Tooltip("Centre the chairs are placed around. The table itself, normally.")]
		[SerializeField] private Transform _centre;

		[Tooltip("Distance from the centre. Zero or less measures it off where the chairs already stand.")]
		[SerializeField] private float _radius;

		[Tooltip("Where seat index zero sits, in degrees around the centre.")]
		[SerializeField] private float _startAngle;

		private float _measuredRadius;

		private void Awake()
		{
			// Measured before anything is moved, and off serialized references rather than a registry
			// that fills later — so this is the one place it can be read honestly.
			_measuredRadius = _radius > 0f ? _radius : MeasureRadius();
		}

		private void OnEnable()
		{
			if (_data) _data.ActiveSeatCount.OnValueChanged += HandleCountChanged;

			Place();
		}

		private void OnDisable()
		{
			if (_data) _data.ActiveSeatCount.OnValueChanged -= HandleCountChanged;
		}

		private void HandleCountChanged(int previous, int current) => Place();

		// Read rather than waited for: a client joining a table already laid for three has no change
		// coming to tell it so.
		private void Place()
		{
			if (!_data) return;

			var count = Mathf.Clamp(_data.ActiveSeatCount.Value, 0, _seats.Count);
			if (count <= 0) return;

			// A chair the scene laid out by hand is left exactly where it stands; which chairs are
			// laid at all is still this component's business either way.
			var laying = _placeSeats && _centre && _measuredRadius > 0f;

			foreach (var seat in _seats)
			{
				if (!seat) continue;

				// Keyed on the chair's authored index, never on its position in a list: the mode hands
				// chairs out by that same index, so the two cannot disagree about which are laid.
				var index = seat.SeatIndex;
				var inUse = index >= 0 && index < count;

				seat.gameObject.SetActive(inUse);
				if (!inUse || !laying) continue;

				var angle = (_startAngle + 360f * index / count) * Mathf.Deg2Rad;
				var position = _centre.position + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * _measuredRadius;

				seat.transform.position = position;

				// Facing comes from where the chair ended up rather than from the angle that put it
				// there, so the two can never drift — the same reason the scene's chairs stopped
				// carrying hand-typed yaws.
				var toCentre = _centre.position - position;
				toCentre.y = 0f;
				if (toCentre.sqrMagnitude > 0.0001f) seat.transform.rotation = Quaternion.LookRotation(toCentre);
			}
		}

		private float MeasureRadius()
		{
			if (!_centre) return 0f;

			var total = 0f;
			var counted = 0;

			foreach (var seat in _seats)
			{
				if (!seat) continue;

				var offset = seat.transform.position - _centre.position;
				offset.y = 0f;
				total += offset.magnitude;
				counted++;
			}

			return counted > 0 ? total / counted : 0f;
		}
	}
}
