using Game.Runtime.Controller;
using Game.Runtime.Interaction;
using Game.Runtime.UI;
using Game.Runtime.UI.Interaction;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Runtime.Player
{
	[DefaultExecutionOrder(10)]
	public class PlayerInteractionController : NetworkBehaviour
	{
		[Header("Detection")]
		[SerializeField] private float _interactRange = 3f;

		[Tooltip("Radius of the sphere cast — gives the aim a bit of tolerance instead of needing a pixel-perfect ray hit.")]
		[SerializeField] private float _interactRadius = 0.15f;

		[Tooltip("Extra range the server allows on top of _interactRange when re-validating, to absorb latency.")]
		[SerializeField] private float _serverRangeTolerance = 1f;

		[SerializeField] private LayerMask _interactableLayers = ~0;
		[SerializeField] private LayerMask _obstructionLayers = ~0;

		[Header("Debug")]
		[Tooltip("Draws the scan ray, the line of sight checks and the rendering camera's forward. Scene view, play mode.")]
		[SerializeField] private bool _drawDebugRays;

		[Header("Input Actions")]
		[SerializeField] private InputActionReference _interactAction;

		[Header("UI")]
		[Tooltip("Spawned into the shared canvas for the owner only. Presentation, so it is a plain prefab and never travels over the network.")]
		[SerializeField] private UIInteractPrompt _promptPrefab;

		[Header("References")]
		[SerializeField] private PlayerController _playerController;

		[SerializeField] private Transform _lookOrigin;

		private readonly RaycastHit[] _hits = new RaycastHit[8];
		private readonly RaycastHit[] _obstructionHits = new RaycastHit[8];

		private IInteractable _focused;
		private string _promptActionName;
		private NetworkBehaviourReference _selfReference;
		private UIInteractPrompt _prompt;

		public IInteractable Focused => _focused;

		public override void OnNetworkSpawn()
		{
			_selfReference = new NetworkBehaviourReference(this);

			if (!IsOwner) return;

			_interactAction.action.Enable();
			_interactAction.action.performed += OnInteractPerformed;

			SpawnPrompt();
		}

		public override void OnNetworkDespawn()
		{
			if (!IsOwner) return;

			_interactAction.action.performed -= OnInteractPerformed;
			_interactAction.action.Disable();

			SetFocused(null);
			DespawnPrompt();
		}

		private void SpawnPrompt()
		{
			if (!_promptPrefab || _prompt) return;

			// The prompt is a panel, not a canvas of its own, so without the shared canvas there is
			// nowhere for it to draw — worth saying out loud rather than spawning something invisible.
			if (!UIManager.Instance)
			{
				Debug.LogWarning("[PlayerInteractionController] No UIManager found — the interact prompt needs the bootstrap canvas.");
				return;
			}

			_prompt = UIManager.Instance.Show(_promptPrefab, UILayer.Hud);
		}

		private void DespawnPrompt()
		{
			if (!_prompt) return;

			if (UIManager.Instance) UIManager.Instance.Hide(_prompt.gameObject);
			else Destroy(_prompt.gameObject);

			_prompt = null;
		}

		// Scanning in LateUpdate, after PlayerController has written the pitch onto the head bone the
		// camera hangs off. In Update that bone still holds whatever pose the Animator gave it, so the
		// ray would leave the eye pointing somewhere the player isn't looking.
		private void LateUpdate()
		{
			if (!IsOwner || !IsSpawned) return;

			SetFocused(Scan());
			RefreshPrompt();
		}

		private IInteractable Scan()
		{
			if (!_lookOrigin) return null;
			// if (!CursorController.IsLocked) return null;

			var ray = new Ray(_lookOrigin.position, _lookOrigin.forward);
			var count = Physics.SphereCastNonAlloc(ray,
				_interactRadius,
				_hits,
				_interactRange,
				_interactableLayers,
				QueryTriggerInteraction.Collide);

			var bestDistance = float.MaxValue;
			IInteractable best = null;

			for (var i = 0; i < count; i++)
			{
				var hit = _hits[i];
				if (hit.distance >= bestDistance) continue;

				var interactable = hit.collider.GetComponentInParent<IInteractable>();
				if (interactable == null || !interactable.Transform) continue;
				if (interactable.Transform.IsChildOf(transform)) continue;
				if (!interactable.CanInteract(_selfReference)) continue;

				// A sphere cast starting already overlapping a collider reports distance 0 and no
				// usable hit point, so the collider's own centre stands in as the aim target.
				var target = hit.distance > 0f ? hit.point : hit.collider.bounds.center;
				var obstructed = IsObstructed(interactable, target);

				DrawDebugTarget(target, obstructed);
				if (obstructed) continue;

				bestDistance = hit.distance;
				best = interactable;
			}

			DrawDebugRay(ray, best != null);

			return best;
		}

		// A sphere cast can round a corner and report a target the player can't actually see, so the
		// winner is confirmed with a straight line to the point that was actually hit. Aiming at the
		// object's pivot instead would false-positive on anything sitting on the floor, whose pivot is
		// buried in the ground collider.
		//
		// Every hit is walked rather than taking the nearest one, because the nearest is regularly the
		// player's own capsule: the eye sits inside the CharacterController, and looking down or to the
		// side puts its wall between the camera and everything else.
		private bool IsObstructed(IInteractable interactable, Vector3 target)
		{
			var origin = _lookOrigin.position;
			var direction = target - origin;
			var distance = direction.magnitude;
			if (distance <= 0.01f) return false;

			var count = Physics.RaycastNonAlloc(new Ray(origin, direction / distance), _obstructionHits,
				distance - 0.01f, _obstructionLayers, QueryTriggerInteraction.Ignore);

			for (var i = 0; i < count; i++)
			{
				var hit = _obstructionHits[i];
				if (hit.transform.IsChildOf(transform)) continue;
				if (hit.collider.GetComponentInParent<IInteractable>() == interactable) continue;

				DrawDebugBlocker(hit.point);
				return true;
			}

			return false;
		}

		// Green/red is what the scan actually casts; cyan is where the rendering camera is pointing.
		// The two overlapping confirms the look origin is the eye the player sees through.
		private void DrawDebugRay(Ray ray, bool foundTarget)
		{
			if (!_drawDebugRays) return;

			Debug.DrawRay(ray.origin, ray.direction * _interactRange, foundTarget ? Color.green : Color.red);

			var view = GameCamera.View;
			if (view) Debug.DrawRay(view.position, view.forward * _interactRange, Color.cyan);
		}

		// Magenta marks whatever collider actually blocked the view, so a false block is traceable to the
		// object causing it rather than just showing up as a rejected target.
		private void DrawDebugBlocker(Vector3 point)
		{
			if (!_drawDebugRays) return;

			var size = _interactRadius * 0.5f;
			Debug.DrawLine(point - Vector3.right * size, point + Vector3.right * size, Color.magenta);
			Debug.DrawLine(point - Vector3.up * size, point + Vector3.up * size, Color.magenta);
			Debug.DrawLine(point - Vector3.forward * size, point + Vector3.forward * size, Color.magenta);
		}

		private void DrawDebugTarget(Vector3 target, bool obstructed)
		{
			if (!_drawDebugRays) return;

			var color = obstructed ? Color.red : Color.yellow;
			Debug.DrawLine(_lookOrigin.position, target, color);

			var size = _interactRadius;
			Debug.DrawLine(target - Vector3.right * size, target + Vector3.right * size, color);
			Debug.DrawLine(target - Vector3.up * size, target + Vector3.up * size, color);
			Debug.DrawLine(target - Vector3.forward * size, target + Vector3.forward * size, color);
		}

		private void SetFocused(IInteractable interactable)
		{
			if (ReferenceEquals(_focused, interactable)) return;

			if (_focused != null && _focused.Transform) _focused.SetFocused(false);

			_focused = interactable;
			_promptActionName = null;

			if (_focused != null) _focused.SetFocused(true);
		}

		private void RefreshPrompt()
		{
			if (!_prompt) return;

			if (_focused == null)
			{
				_promptActionName = null;
				_prompt.Hide();
				return;
			}

			var actionName = _focused.ActionName;
			if (actionName == _promptActionName) return;

			_promptActionName = actionName;
			_prompt.Show(actionName, _interactAction.action.GetBindingDisplayString());
		}

		private void OnInteractPerformed(InputAction.CallbackContext ctx)
		{
			if (_focused == null) return;
			if (_focused is not NetworkBehaviour networkBehaviour) return;
			if (!_focused.CanInteract(_selfReference)) return;

			InteractRPC(new NetworkBehaviourReference(networkBehaviour));
		}

		[Rpc(SendTo.Server)]
		private void InteractRPC(NetworkBehaviourReference reference, RpcParams rpcParams = default)
		{
			if (!reference.TryGet(out NetworkBehaviour networkBehaviour)) return;
			if (networkBehaviour is not IInteractable interactable) return;

			if (rpcParams.Receive.SenderClientId != OwnerClientId) return;

			var range = _interactRange + _serverRangeTolerance;
			if ((interactable.Transform.position - transform.position).sqrMagnitude > range * range) return;

			interactable.Interact(_selfReference);
		}
	}
}