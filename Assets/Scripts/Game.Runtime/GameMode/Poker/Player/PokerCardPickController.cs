using Game.Runtime.Controller;
using Game.Runtime.GameMode.Poker.Visual;
using Game.Runtime.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Runtime.GameMode.Poker.Player
{
	// Picking a card up off the table by pointing at it. Owner-only: it reads this client's cursor and
	// asks the server, which decides whether the round still allows another look.
	//
	// A raycast rather than a button per card, because the cards are objects on a table the player is
	// sitting at — a UI row beside them would be a second place the same five cards exist, and the two
	// would have to be kept in step for every animation the real ones play.
	public class PokerCardPickController : NetworkBehaviour
	{
		[Header("References")]
		[SerializeField] private PokerPlayerData _data;
		[SerializeField] private PokerHandVisual _handVisual;

		[Header("Input")]
		[Tooltip("Pressed to pick up whatever is under the cursor.")]
		[SerializeField] private InputActionReference _pickAction;

		[Header("Raycast")]
		[Tooltip("Layers a card can be found on. Everything else is ignored, so a chair between the eye and the table cannot swallow the pick.")]
		[SerializeField] private LayerMask _cardMask = ~0;

		[Tooltip("How far the pick reaches. A table's width, not a room's.")]
		[SerializeField] private float _maxDistance = 4f;

		[Header("Hover")]
		[Tooltip("How far a card lifts while the cursor is over it, so the player can see which one they are about to take.")]
		[SerializeField] private float _hoverLift = 0.015f;

		private readonly RaycastHit[] _hits = new RaycastHit[8];

		private PokerCardVisual _hovered;

		public override void OnNetworkSpawn()
		{
			if (!IsOwner) return;

			if (!_data) _data = GetComponentInParent<PokerPlayerData>();
			if (!_handVisual) _handVisual = GetComponentInParent<PokerHandVisual>();

			if (_pickAction && _pickAction.action != null)
			{
				_pickAction.action.performed += HandlePick;
				_pickAction.action.Enable();
			}
		}

		public override void OnNetworkDespawn()
		{
			if (_pickAction && _pickAction.action != null)
			{
				_pickAction.action.performed -= HandlePick;
			}

			SetHovered(null);
		}

		// Genuinely continuous: what is under the cursor changes as the cursor moves and as the cards
		// travel, and there is no event for either.
		private void Update()
		{
			if (!IsOwner) return;

			SetHovered(CanPick() ? Raycast() : null);
		}

		private void HandlePick(InputAction.CallbackContext context)
		{
			if (!IsOwner || !CanPick()) return;

			var card = Raycast();
			if (!card || !_handVisual) return;

			var slot = _handVisual.SlotOf(card);
			if (slot < 0 || !_data.CanLookAt(slot)) return;

			_data.LookAtHoleCardRPC(slot);
		}

		// Only while the cursor is free: with the view being turned, the pointer is not on screen and a
		// pick would be aimed by the crosshair, which is a different control nobody asked for.
		private bool CanPick() => _data && _data.HasLookLimit && !CursorController.IsLocked;

		private PokerCardVisual Raycast()
		{
			var view = GameCamera.Main;
			if (!view) return null;

			var pointer = Pointer.current;
			var screenPosition = pointer != null ? pointer.position.ReadValue() : new Vector2(Screen.width, Screen.height) * 0.5f;

			var ray = view.ScreenPointToRay(screenPosition);
			var count = Physics.RaycastNonAlloc(ray, _hits, _maxDistance, _cardMask, QueryTriggerInteraction.Collide);

			// Nearest first is not guaranteed by RaycastNonAlloc, and the cards on the table overlap by
			// design — so the closest hit that is one of *our* cards is the one being pointed at.
			PokerCardVisual best = null;
			var bestDistance = float.MaxValue;

			for (var i = 0; i < count; i++)
			{
				var card = _hits[i].collider.GetComponentInParent<PokerCardVisual>();
				if (!card || !_handVisual || _handVisual.SlotOf(card) < 0) continue;

				if (_hits[i].distance >= bestDistance) continue;

				best = card;
				bestDistance = _hits[i].distance;
			}

			return best;
		}

		private void SetHovered(PokerCardVisual card)
		{
			if (_hovered == card) return;

			if (_hovered) _hovered.SetHoverLift(0f);

			_hovered = card;

			if (_hovered) _hovered.SetHoverLift(_hoverLift);
		}
	}
}
