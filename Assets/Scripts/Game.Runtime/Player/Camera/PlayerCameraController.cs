using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.Player.Camera
{
	// Which state the view is in. It owns entering and leaving and nothing else: what a state actually
	// does is the state's own business, so a new one is a subclass and a child object rather than another
	// branch here.
	//
	// Owner-only: a camera state is about what this client is looking at, and raising a shot on somebody
	// else's rig would fight their own view.
	//
	// Requests are a stack, the same shape UIEscapeStack and the cursor's counted release take: two
	// things can want the camera at once — a card being picked while a focus is up — and the newest wins
	// without the older one being lost. Releasing a request that is not on top simply removes it, so
	// callers unwind in any order and none of them has to know about the others.
	public class PlayerCameraController : NetworkBehaviour
	{
		[Header("States")]
		[Tooltip("What the view falls back to with nothing requested. Free flight, normally — the state that hands the look back to the player.")]
		[Required]
		[SerializeField] private PlayerCameraState _baseState;

		[Tooltip("Every state this player can be put into, including the base one. Left empty it takes the children of this object, so adding a state is adding a child.")]
		[SerializeField] private List<PlayerCameraState> _states = new();

		[Header("References")]
		[SerializeField] private PlayerController _controller;
		[SerializeField] private PlayerRigController _rig;

		private readonly List<Hold> _holds = new();

		private PlayerCameraState _current;
		private int _nextHandle = 1;

		private readonly struct Hold
		{
			public Hold(int handle, PlayerCameraState state)
			{
				Handle = handle;
				State = state;
			}

			public int Handle { get; }
			public PlayerCameraState State { get; }
		}

		public PlayerController PlayerController => _controller;

		// The eye this client actually renders through. A shot that wants to start from the player's own
		// view asks here rather than being authored at a height, because which rig is rendered — and so
		// which camera is live — stays PlayerVisual's business.
		public Transform Eye => _rig ? _rig.RenderedCamera : null;
		public PlayerCameraState Current => _current;

		public override void OnNetworkSpawn()
		{
			if (!_controller) _controller = GetComponentInParent<PlayerController>();
			if (!_rig) _rig = GetComponentInParent<PlayerRigController>();

			if (_states.Count == 0) GetComponentsInChildren(true, _states);
			foreach (var state in _states) if (state) state.Initialize(this);

			if (!IsOwner) return;

			Apply();
		}

		public override void OnNetworkDespawn()
		{
			if (!IsOwner) return;

			// The body is going, and a state left entered would hold a suspended look on a controller
			// that outlives this by a frame.
			_holds.Clear();
			Apply();
		}

		// The handle is what gives it back. A state released by name would be released by whoever asked
		// last rather than by whoever is holding it.
		public int Request(PlayerCameraState state)
		{
			if (!IsOwner || !state) return 0;

			var handle = _nextHandle++;
			_holds.Add(new Hold(handle, state));

			Apply();
			return handle;
		}

		public void Release(int handle)
		{
			if (!IsOwner || handle == 0) return;

			for (var i = 0; i < _holds.Count; i++)
			{
				if (_holds[i].Handle != handle) continue;

				_holds.RemoveAt(i);
				Apply();
				return;
			}
		}

		private void Apply()
		{
			var state = _holds.Count > 0 ? _holds[^1].State : _baseState;

			if (!state) return;

			// Left before the new one is entered. It used to be the other way round, back when a state
			// released the look flag on its way out and leaving last would have undone what arriving had
			// just set — no state does that any more, and the order had quietly become the bug: a shot
			// clears its look target in OnExit, so the outgoing state was wiping the target the incoming
			// one had set a line earlier. The head then stayed pointed at whatever the last beat was about,
			// which reads as a shot that never arrived rather than as one that arrived and was erased.
			//
			// Which of the two orders was wrong depended on the order two binders happened to run in, so it
			// was reproducible and looked like a stuck camera.
			var previous = _current;
			_current = state;

			if (previous && previous != state) previous.Exit();

			state.Enter();
		}
	}
}
