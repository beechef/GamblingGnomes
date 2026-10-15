using System;
using System.Collections.Generic;
using Game.Runtime.Audio;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// Moves the room ambience to whichever room the table is sitting in. Sits on the track it drives; the room is
	// local scenery (each screen's own hallucination), so this reads the local PokerRoomController, not the network.
	public class PokerRoomAmbienceParameterDriver : MonoBehaviour
	{
		[Serializable]
		private struct RoomLevel
		{
			public PokerRoomVariant Room;
			public float Level;
		}

		[Required]
		[SerializeField] private MusicTrack _track;

		[FMODUnity.ParamRef]
		[SerializeField] private string _parameter = "room";

		[Tooltip("Any room not listed here.")]
		[SerializeField] private float _defaultLevel;

		[SerializeField] private List<RoomLevel> _levels = new();

		[ShowInInspector, ReadOnly]
		private float _current;

		private PokerRoomController _rooms;

		private void Start()
		{
			PokerRoomController.OnInstanceChanged += HandleInstanceChanged;
			HandleInstanceChanged();
		}

		private void OnDestroy()
		{
			PokerRoomController.OnInstanceChanged -= HandleInstanceChanged;
			Unbind();
		}

		private void HandleInstanceChanged()
		{
			Unbind();

			_rooms = PokerRoomController.Instance;
			if (_rooms) _rooms.OnRoomChanged += HandleRoomChanged;

			HandleRoomChanged(_rooms ? _rooms.Current : PokerRoomVariant.Default);
		}

		private void Unbind()
		{
			if (_rooms) _rooms.OnRoomChanged -= HandleRoomChanged;
			_rooms = null;
		}

		private void HandleRoomChanged(PokerRoomVariant room)
		{
			_current = _defaultLevel;

			foreach (var entry in _levels)
			{
				if (entry.Room != room) continue;

				_current = entry.Level;
				break;
			}

			_track.SetParameter(_parameter, _current);
		}
	}
}
