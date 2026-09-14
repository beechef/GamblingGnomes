using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
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

		[Header("Transition")]
		[Tooltip("Seconds the swap takes. The rooms change at the halfway point, so a blink already covering the change hides it — the same shape PokerHallucinationController's own transition takes. Zero swaps outright.")]
		[MinValue(0f)]
		[SerializeField] private float _transitionDuration;

		public static PokerRoomController Instance { get; private set; }

		public static event Action OnInstanceChanged;

		// Raised when the room has actually changed, for anything in the scene that is more than a set of
		// objects being switched — a water surface that has to fill, a light that has to warm up.
		public event Action<PokerRoomVariant> OnRoomChanged;

		public PokerRoomVariant Current { get; private set; } = PokerRoomVariant.Default;

		public float TransitionDuration => Mathf.Max(0f, _transitionDuration);

		private readonly List<object> _handles = new();
		private readonly List<PokerRoomVariant> _wanted = new();

		private float _timer;
		private bool _switching;
		private PokerRoomVariant _pending;

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

			Request(variant);
		}

		public void Clear(object handle)
		{
			if (handle == null || !Remove(handle)) return;

			Request(_wanted.Count > 0 ? _wanted[^1] : PokerRoomVariant.Default);
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

		// The wanted room, either at once or halfway through the transition. A second request arriving
		// mid-swap replaces what is pending rather than queueing behind it: the later answer is the one
		// that lands anyway, and a queue would spend a whole transition on a room nobody ends up in.
		private void Request(PokerRoomVariant variant)
		{
			if (TransitionDuration <= 0f)
			{
				Apply(variant);
				return;
			}

			_pending = variant;

			if (_switching) return;

			_switching = true;
			_timer = TransitionDuration * 0.5f;
		}

		private void Update()
		{
			if (!_switching) return;

			// Unscaled, because a swap is exactly the beat a paused game would otherwise hold open forever.
			_timer -= Time.unscaledDeltaTime;
			if (_timer > 0f) return;

			_switching = false;
			Apply(_pending);
		}

		private void Apply(PokerRoomVariant variant)
		{
			// A room the scene never authored is left alone rather than switching everything off: an effect
			// pointing at a room this table does not have should do nothing, not empty the world.
			if (!Has(variant)) return;

			foreach (var room in _rooms)
			{
				if (room == null) continue;

				var on = room.Variant == variant;

				foreach (var member in room.Objects)
				{
					if (member) member.SetActive(on);
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
