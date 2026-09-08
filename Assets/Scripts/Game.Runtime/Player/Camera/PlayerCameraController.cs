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
			public Hold(int handle, PlayerCameraState state, Transform target)
			{
				Handle = handle;
				State = state;
				Target = target;
			}

			public int Handle { get; }
			public PlayerCameraState State { get; }
			public Transform Target { get; }
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
		public int Request(PlayerCameraState state, Transform target = null)
		{
			if (!IsOwner || !state) return 0;

			var handle = _nextHandle++;
			_holds.Add(new Hold(handle, state, target));

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
			var target = _holds.Count > 0 ? _holds[^1].Target : null;

			if (!state) return;

			// Entered before the old one is left: a state that hands the look back and a state that takes
			// it away are both writing the same flag, and leaving last would undo what arriving just set.
			var previous = _current;
			_current = state;

			state.Enter(target);

			if (previous && previous != state) previous.Exit();
		}
	}
}
