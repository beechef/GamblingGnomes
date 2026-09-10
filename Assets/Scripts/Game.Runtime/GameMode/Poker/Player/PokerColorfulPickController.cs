using Game.Runtime.Controller;
using Game.Runtime.GameMode.Poker.Stages;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Runtime.GameMode.Poker.Player
{
	// The hand's winner naming who eats the Colorful cap, by pointing at them. A row of name buttons was a
	// second place the same players existed, kept in step by hand, and it asked the winner to read a list
	// at the one moment the whole table is watching faces.
	//
	// Owner-only: it reads this client's cursor and asks the server, which checks the choice again with
	// the very method used here to decide who may be hovered.
	public class PokerColorfulPickController : NetworkBehaviour
	{
		[Header("Input")]
		[Tooltip("Pressed to feed the cap to whoever is outlined. UI/Click - pointing at a player is the same act as pointing at a card or a button.")]
		[SerializeField] private InputActionReference _pickAction;

		[Header("Raycast")]
		[Tooltip("Layers a player can be found on. A player is found through AimTarget, the trigger every body carries so it can be pointed at on machines where its CharacterController is off.")]
		[SerializeField] private LayerMask _playerMask = ~0;

		[Tooltip("How far the pick reaches. Across the table, not across the room.")]
		[SerializeField] private float _maxDistance = 6f;

		// Roomy for the same reason the accusation's cast is: everybody the ray passes through takes a slot
		// whether or not they can be fed, and a full buffer drops the rest of the list in silence.
		private readonly RaycastHit[] _hits = new RaycastHit[20];

		private PokerColorfulPickStage _stage;
		private PokerPlayer _hovered;
		private int _lastPickFrame = -1;

		private FixedString32Bytes _lastStageId;
		private FixedString32Bytes _lastOverlayId;
		private ulong _lastTurn = ulong.MaxValue;

		public override void OnNetworkSpawn()
		{
			if (!IsOwner) return;

			if (_pickAction && _pickAction.action != null)
			{
				_pickAction.action.performed += HandlePick;
				_pickAction.action.Enable();
			}
		}

		public override void OnNetworkDespawn()
		{
			if (_pickAction && _pickAction.action != null) _pickAction.action.performed -= HandlePick;

			SetHovered(null);
		}

		// Genuinely continuous: who is under the cursor changes as the cursor moves and as the bodies
		// breathe, and there is no event for either. The stage is only looked up when one of the three
		// values it depends on has actually moved, so the per-frame path allocates nothing.
		private void Update()
		{
			if (!IsOwner) return;

			var mode = PokerGameMode.Instance;
			var data = mode ? mode.Data : null;

			var stageId = data ? data.StageId.Value : default;
			var overlayId = data ? data.OverlayStageId.Value : default;
			var turn = data ? data.CurrentTurnClientId.Value : PokerGameData.NoTurn;

			if (!stageId.Equals(_lastStageId) || !overlayId.Equals(_lastOverlayId) || turn != _lastTurn)
			{
				_lastStageId = stageId;
				_lastOverlayId = overlayId;
				_lastTurn = turn;
				_stage = ResolvePickStage(mode);
			}

			SetHovered(CanPick() ? Raycast() : null);
		}

		// Only while this client is the one being asked. Everybody else is watching the winner choose, and a
		// cursor that lit people up for them would be a second pointer answering a question nobody put.
		private PokerColorfulPickStage ResolvePickStage(PokerGameMode mode)
		{
			if (!mode || !mode.Data) return null;
			if (!mode.Data.OverlayStageId.Value.IsEmpty) return null;
			if (mode.Data.CurrentTurnClientId.Value != OwnerClientId) return null;

			return mode.FindStage(mode.Data.StageId.Value.ToString()) as PokerColorfulPickStage;
		}

		// A freed cursor, as for picking cards: with the view being turned the pointer is not on screen, and
		// a pick aimed by the crosshair would be a different control nobody asked for.
		private bool CanPick() => _stage && !CursorController.IsLocked;

		private PokerPlayer Raycast()
		{
			var view = GameCamera.Main;
			if (!view) return null;

			var pointer = Pointer.current;
			var screenPosition = pointer != null ? pointer.position.ReadValue() : new Vector2(Screen.width, Screen.height) * 0.5f;

			var ray = view.ScreenPointToRay(screenPosition);
			var count = Physics.RaycastNonAlloc(ray, _hits, _maxDistance, _playerMask, QueryTriggerInteraction.Collide);

			PokerPlayer best = null;
			PokerPlayer self = null;
			var bestDistance = float.MaxValue;

			for (var i = 0; i < count; i++)
			{
				var player = _hits[i].collider.GetComponentInParent<PokerPlayer>();

				// Asked of the stage the server answers with, so nothing can be outlined that a click would
				// then have refused.
				if (!player || !_stage.CanBeFed(player)) continue;

				// Our own body sits just under the eye, so a ray to somebody across the table can pass through
				// it on the way. It is only the answer when nothing else is: pointing down at yourself with
				// nobody behind is how the winner names themselves, which the rules allow on purpose.
				if (player.ClientId == OwnerClientId)
				{
					self = player;
					continue;
				}

				// Nearest first is not guaranteed by RaycastNonAlloc.
				if (_hits[i].distance >= bestDistance) continue;

				best = player;
				bestDistance = _hits[i].distance;
			}

			return best ? best : self;
		}

		private void SetHovered(PokerPlayer player)
		{
			if (_hovered == player) return;

			if (_hovered && _hovered.Visual) _hovered.Visual.SetLocalOutlined(false);

			_hovered = player;

			if (_hovered && _hovered.Visual) _hovered.Visual.SetLocalOutlined(true);
		}

		// UI/Click is PassThrough and performs on release as well as press, so the callback asks what the
		// button is doing; the frame guard is the other half, since the pad's cursor is a real Mouse and two
		// bindings can actuate in one frame. What is chosen is what is outlined, never a fresh raycast —
		// the thing the player saw lit is exactly the thing they are committing to.
		private void HandlePick(InputAction.CallbackContext context)
		{
			if (!IsOwner || !CanPick()) return;
			if (!context.ReadValueAsButton()) return;
			if (_lastPickFrame == Time.frameCount) return;

			var target = _hovered;
			if (!target || !target.Data) return;

			_lastPickFrame = Time.frameCount;

			var mode = PokerGameMode.Instance;
			if (mode) mode.SubmitActionRPC(PokerActionType.Target, target.Data.SeatIndex.Value);
		}
	}
}
