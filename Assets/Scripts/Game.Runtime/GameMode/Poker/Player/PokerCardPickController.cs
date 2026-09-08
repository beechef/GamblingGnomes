using Game.Runtime.Controller;
using Game.Runtime.GameMode.Poker.Stages;
using Game.Runtime.GameMode.Poker.Visual;
using Game.Runtime.Player;
using Game.Runtime.Player.Camera;
using Unity.Collections;
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

		[Header("Camera")]
		[Tooltip("Takes the view to the card while it is being picked up. Empty leaves the camera alone.")]
		[SerializeField] private PlayerCameraController _camera;

		[Tooltip("Which state the view goes into while a card is coming up. Picked rather than named, so swapping the shot is a drag rather than an edit.")]
		[SerializeField] private PlayerCameraState _pickState;


		private readonly RaycastHit[] _hits = new RaycastHit[8];

		private PokerCardVisual _hovered;

		private int _focusHandle;
		private bool _picking;
		private FixedString32Bytes _lastStageId;

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

			RefreshStage();
		}

		public override void OnNetworkDespawn()
		{
			if (_pickAction && _pickAction.action != null)
			{
				_pickAction.action.performed -= HandlePick;
			}

			SetHovered(null);
			ReleaseFocus();
		}

		// Genuinely continuous: what is under the cursor changes as the cursor moves and as the cards
		// travel, and there is no event for either.
		private void Update()
		{
			if (!IsOwner) return;

			// Read here rather than through StageId.OnValueChanged. The subscription has to be taken in
			// OnNetworkSpawn, and the table's data may not have spawned by then — binding to nothing there is
			// silent, and the symptom is a beat that opens and then never closes. This compares the raw
			// FixedString and does nothing at all while it has not moved, so the cost is one comparison.
			var mode = PokerGameMode.Instance;
			var id = mode && mode.Data ? mode.Data.StageId.Value : default;
			if (!id.Equals(_lastStageId))
			{
				_lastStageId = id;
				RefreshStage();
			}

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

			// It is in the hand now, so it is no longer something to reach for. The set is re-opened by the
			// stage rather than here, which keeps "may anyone pick" in one place.
			card.Pickupable = false;
			SetHovered(null);
		}

		// The whole beat, not one pick: the view is held on the cards for as long as the round is asking
		// which of them to turn, and comes back only when that step is over. Held on the seat's own card
		// anchor rather than on a card, because the cards move — one being picked up would drag the shot
		// with it, and the shot is meant to frame the row.

		private void RefreshStage()
		{
			if (!IsOwner) return;

			var mode = PokerGameMode.Instance;
			var stage = mode ? mode.FindStage(mode.Data.StageId.Value.ToString()) : null;
			var picking = stage is PokerCardLookStage;

			if (picking == _picking) return;

			_picking = picking;

			if (_handVisual) _handVisual.SetPickupable(picking);

			if (picking) HoldFocus();
			else ReleaseFocus();
		}

		private void HoldFocus()
		{
			if (_focusHandle != 0 || !_camera || !_pickState) return;

			_focusHandle = _camera.Request(_pickState, ResolveCardAnchor());
		}

		private void ReleaseFocus()
		{
			if (_focusHandle == 0) return;

			if (_camera) _camera.Release(_focusHandle);
			_focusHandle = 0;
		}

		private Transform ResolveCardAnchor()
		{
			var mode = PokerGameMode.Instance;
			if (!mode || !_data) return null;

			foreach (var seat in mode.Seats)
			{
				if (seat && seat.SeatIndex == _data.SeatIndex.Value) return seat.CardAnchor;
			}

			return null;
		}


		// Only while the cursor is free, and only while the round is asking. With the view being turned the
		// pointer is not on screen and a pick would be aimed by the crosshair, which is a different control
		// nobody asked for.
		private bool CanPick() => _picking && _data && _data.HasLookLimit && !CursorController.IsLocked;

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
				// Only what the beat has opened. A card already up in the hand has its flag cleared, so it
				// stops hovering the moment it is taken rather than lifting under the cursor for a pick the
				// server would refuse.
				if (!card || !card.Pickupable || !_handVisual || _handVisual.SlotOf(card) < 0) continue;

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
