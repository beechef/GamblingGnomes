using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker
{
	// The rooms the table can be sitting in, and the one it is in now. The scene owns them — a room is
	// scenery, and a ScriptableObject cannot name a scene object — so this registers itself the way
	// PokerScenery, PokerSeat and GameCamera already do rather than being searched for.
	//
	// A room is authored **in the scene, switched on and off**, and never spawned from a prefab. A prefab
	// grown under the room is one copy of one room: it carries its own lighting and its own placement, it
	// cannot be laid out against the table it is replacing, and every further room is another prefab to
	// keep in step with the first. Here the artist builds each one where it belongs and the switch is a
	// switch.
	//
	// The swap is immediate, because the caller is what hides it: a hallucination asks for a room at the
	// moment its blink is shut, and a delay of the room's own lands the swap after the eye has opened.
	//
	// Requests are counted, like the bone scale's modifiers and the material overrides: two effects can
	// want a room at once, the newest wins, and dropping one puts back what the caller before it asked
	// for rather than snapping to Default.
	public class PokerRoomController : MonoBehaviour
	{
		[Serializable]
		public class Room
		{
			[Tooltip("Which room this is. Picked on both sides, so an effect cannot name one the scene does not offer.")]
			[SerializeField] private PokerRoomVariant _variant;

			[Tooltip("What is switched on for it. Everything belonging to every other room goes off, so a piece shared by two rooms belongs in both lists.")]
			[SerializeField] private List<GameObject> _objects = new();

			public PokerRoomVariant Variant => _variant;
			public IReadOnlyList<GameObject> Objects => _objects;
		}

		[Tooltip("Every room the scene carries, the default one included. A room nobody authored is a room an effect asking for it leaves alone.")]
		[SerializeField] private List<Room> _rooms = new();

		public static PokerRoomController Instance { get; private set; }

		public static event Action OnInstanceChanged;

		// Raised when the room has actually changed, for anything in the scene that is more than a set of
		// objects being switched — a water surface that has to fill, a light that has to warm up.
		public event Action<PokerRoomVariant> OnRoomChanged;

		public PokerRoomVariant Current { get; private set; } = PokerRoomVariant.Default;

		private readonly List<object> _handles = new();
		private readonly List<PokerRoomVariant> _wanted = new();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			Instance = null;
			OnInstanceChanged = null;
		}

		private void OnEnable()
		{
			if (Instance && Instance != this)
			{
				Debug.LogWarning($"{nameof(PokerRoomController)}: a second one is in the scene, keeping the first.", this);
				return;
			}

			Instance = this;

			// A room entry that lost its object switches nothing, which reads as a room that never changes.
			foreach (var room in _rooms)
			{
				if (room == null) continue;

				foreach (var member in room.Objects)
				{
					if (!member) Debug.LogWarning($"{nameof(PokerRoomController)}: room {room.Variant} lists a missing object; it will not be switched.", this);
				}
			}

			Apply(PokerRoomVariant.Default);

			OnInstanceChanged?.Invoke();
		}

		private void OnDisable()
		{
			if (Instance != this) return;

			_handles.Clear();
			_wanted.Clear();

			Instance = null;
			OnInstanceChanged?.Invoke();
		}

		public void Set(object handle, PokerRoomVariant variant)
		{
			if (handle == null) return;

			Remove(handle);

			_handles.Add(handle);
			_wanted.Add(variant);

			Apply(variant);
		}

		public void Clear(object handle)
		{
			if (handle == null || !Remove(handle)) return;

			Apply(_wanted.Count > 0 ? _wanted[^1] : PokerRoomVariant.Default);
		}

		private bool Remove(object handle)
		{
			for (var i = 0; i < _handles.Count; i++)
			{
				if (_handles[i] != handle) continue;

				_handles.RemoveAt(i);
				_wanted.RemoveAt(i);
				return true;
			}

			return false;
		}

		private void Apply(PokerRoomVariant variant)
		{
			// A room the scene never authored is left alone rather than switching everything off: an effect
			// pointing at a room this table does not have should do nothing, not empty the world.
			if (!Has(variant)) return;

			// Everything off first, then the chosen room on, so a piece listed in two rooms is left on rather than
			// switched off by whichever room happens to come later in the list.
			foreach (var room in _rooms)
			{
				if (room == null || room.Variant == variant) continue;

				foreach (var member in room.Objects)
				{
					if (member) member.SetActive(false);
				}
			}

			foreach (var room in _rooms)
			{
				if (room == null || room.Variant != variant) continue;

				foreach (var member in room.Objects)
				{
					if (member) member.SetActive(true);
				}
			}

			if (Current == variant) return;

			Current = variant;
			OnRoomChanged?.Invoke(variant);
		}

		private bool Has(PokerRoomVariant variant)
		{
			foreach (var room in _rooms)
			{
				if (room != null && room.Variant == variant) return true;
			}

			return false;
		}
	}
}
