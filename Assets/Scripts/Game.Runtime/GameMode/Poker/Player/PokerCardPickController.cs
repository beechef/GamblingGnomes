using System.Collections.Generic;
using Game.Runtime.Controller;
using Game.Runtime.GameMode.Poker.Stages;
using Game.Runtime.GameMode.Poker.Visual;
using Game.Runtime.Player;
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

		[Tooltip("How far a chosen card stands off the table. Higher than the hover, so a hand half chosen reads at a glance.")]
		[SerializeField] private float _selectedLift = 0.035f;

		private readonly RaycastHit[] _hits = new RaycastHit[8];

		private readonly List<PokerCardVisual> _selected = new();
		private readonly List<PokerCardVisual> _liftBuffer = new();

		private PokerCardVisual _hovered;

		private bool _picking;
		private int _lastPickFrame = -1;
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
			if (_pickAction && _pickAction.action != null) _pickAction.action.performed -= HandlePick;

			SetHovered(null);
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

			var card = CanPick() ? Raycast() : null;
			SetHovered(card);
		}

		// UI/Click is PassThrough — that is what the UI module wants of it — and a PassThrough action raises
		// performed on every value change, the release included. So the callback is asked what the button is
		// actually doing rather than trusted to mean a press: without it one click took two cards, the press
		// taking the one under the cursor and the release taking whatever that uncovered. The frame guard is
		// the other half of it, since the action carries a binding per device it answers to and the pad's
		// cursor is a real Mouse.
		private void HandlePick(InputAction.CallbackContext context)
		{
			if (!IsOwner || !CanPick()) return;
			if (!context.ReadValueAsButton()) return;
			if (_lastPickFrame == Time.frameCount) return;

			var card = Raycast();
			if (!card || !_handVisual) return;

			_lastPickFrame = Time.frameCount;

			Pick(card);
		}

		// Chosen rather than taken. A card lifted the moment it is clicked is a decision the player cannot
		// take back, and the round asks for several at once — so a click marks a card, a click on a marked
		// card puts it back down, and only a full hand is committed. Nothing leaves the table until then,
		// which is also what keeps the server out of a half-made mind.
		private void Pick(PokerCardVisual card)
		{
			if (!_handVisual) return;

			var slot = _handVisual.SlotOf(card);
			if (slot < 0) return;

			if (_selected.Remove(card))
			{
				ApplyLift(card);
				return;
			}

			if (!_data.CanLookAt(slot)) return;

			_selected.Add(card);
			ApplyLift(card);

			if (_selected.Count >= Remaining) Commit();
		}

		// How many more this player is still owed. Read rather than assumed to be the whole hand, so a round
		// that hands out its looks in more than one beat commits each of them on its own count.
		private int Remaining => _data ? Mathf.Max(0, _data.ViewableHoleCards.Value - _data.LookedAtCount) : 0;

		private void Commit()
		{
			foreach (var card in _selected)
			{
				if (!card) continue;

				var slot = _handVisual.SlotOf(card);
				if (slot < 0 || !_data.CanLookAt(slot)) continue;

				_data.LookAtHoleCardRPC(slot);

				// It is in the hand now, so it is no longer something to reach for. The set is re-opened by the
				// stage rather than here, which keeps "may anyone pick" in one place.
				card.Pickupable = false;
			}

			ClearSelection();
			SetHovered(null);
		}

		private void ClearSelection()
		{
			// Copied out before the list is emptied: a card put back down is only at rest once nothing still
			// claims it, and ApplyLift asks the list.
			_liftBuffer.Clear();
			_liftBuffer.AddRange(_selected);
			_selected.Clear();

			foreach (var card in _liftBuffer) ApplyLift(card);

			_liftBuffer.Clear();
		}

		private void RefreshStage()
		{
			if (!IsOwner) return;

			var mode = PokerGameMode.Instance;
			var stage = mode ? mode.FindStage(mode.Data.StageId.Value.ToString()) : null;
			// Only somebody who was dealt into this hand is being asked anything: a player who took a chair
			// mid round has no cards to turn, so taking their view down to a row of nobody else's would be a
			// shot of the table with the game happening somewhere above it.
			var picking = stage is PokerCardLookStage && _data && _data.IsInHand;

			if (picking == _picking) return;

			_picking = picking;

			if (_handVisual) _handVisual.SetPickupable(picking);

			if (!picking) ClearSelection();
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

			var previous = _hovered;
			_hovered = card;

			ApplyLift(previous);
			ApplyLift(card);
		}

		// One height, decided in one place: being chosen outranks being under the cursor, so a card the
		// player has picked does not drop back down when the pointer wanders off it.
		private void ApplyLift(PokerCardVisual card)
		{
			if (!card) return;

			if (_selected.Contains(card)) card.SetLift(_selectedLift);
			else card.SetLift(card == _hovered ? _hoverLift : 0f);
		}
	}
}
