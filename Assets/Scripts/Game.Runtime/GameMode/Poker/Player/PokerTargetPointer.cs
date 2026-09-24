using System;
using Game.Runtime.Controller;
using Game.Runtime.GameMode.Poker.Visual;
using Game.Runtime.UI.CursorVisuals;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Runtime.GameMode.Poker.Player
{
	// Choosing somebody or a card by pointing at it. Owner-only: it reads this client's cursor, lights what a
	// click would commit, and hands the choice to whoever asked — it decides nothing and sends nothing itself.
	// One asker at a time: a new Begin replaces the last.
	//
	// The same act for every caller — the Colorful pick, an item's target, an answer an item asks for — so
	// what may be pointed at is the caller's filter and how it looks is the same everywhere.
	public class PokerTargetPointer : NetworkBehaviour
	{
		[Header("Input")]
		[Tooltip("Pressed to commit whatever is lit. UI/Click - pointing at a player is the same act as pointing at a card or a button.")]
		[SerializeField] private InputActionReference _pickAction;

		[Header("Raycast")]
		[Tooltip("Layers a player can be found on, through the AimTarget trigger every body carries.")]
		[SerializeField] private LayerMask _playerMask = ~0;

		[Tooltip("Layers a card can be found on.")]
		[SerializeField] private LayerMask _cardMask = ~0;

		[Tooltip("How far the pick reaches. Across the table, not across the room.")]
		[SerializeField] private float _maxDistance = 6f;

		[Header("Hover")]
		[Tooltip("How far a card lifts while it is the one a click would take.")]
		[SerializeField] private float _cardHoverLift = 0.015f;

		// Roomy: everything the ray passes through takes a slot, and a full buffer drops the rest in silence.
		private readonly RaycastHit[] _hits = new RaycastHit[24];

		private PokerTargetQuery _query;
		private Action<PokerTarget> _onPicked;

		private PokerPlayer _hoveredPlayer;
		private PokerCardVisual _hoveredCard;
		private PokerTarget _hovered;
		private bool _hasHovered;

		private int _cursorHandle;
		private int _lastPickFrame = -1;

		public bool IsActive => _query != null;

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

			End();
		}

		public void Begin(PokerTargetQuery query, Action<PokerTarget> onPicked)
		{
			if (!IsOwner) return;

			End();

			_query = query;
			_onPicked = onPicked;
			SetCursor(query != null ? query.Cursor : (CursorVisualState?)null);
		}

		public void End()
		{
			_query = null;
			_onPicked = null;

			Hover(default, false, null, null);
			SetCursor(null);
		}

		// Genuinely continuous: what is under the cursor changes as it moves and as bodies and cards move.
		private void Update()
		{
			if (!IsOwner || _query == null) return;

			if (CursorController.IsLocked)
			{
				Hover(default, false, null, null);
				return;
			}

			FindUnderCursor();
		}

		private void FindUnderCursor()
		{
			var view = GameCamera.Main;
			if (!view)
			{
				Hover(default, false, null, null);
				return;
			}

			var pointer = Pointer.current;
			var screenPosition = pointer != null ? pointer.position.ReadValue() : new Vector2(Screen.width, Screen.height) * 0.5f;
			var ray = view.ScreenPointToRay(screenPosition);

			var count = Physics.RaycastNonAlloc(ray, _hits, _maxDistance, _playerMask | _cardMask, QueryTriggerInteraction.Collide);

			var bestDistance = float.MaxValue;
			var best = default(PokerTarget);
			var found = false;
			PokerPlayer bestPlayer = null;
			PokerCardVisual bestCard = null;

			for (var i = 0; i < count; i++)
			{
				var hit = _hits[i];
				if (hit.distance >= bestDistance) continue;

				var card = hit.collider.GetComponentInParent<PokerCardVisual>();
				if (card)
				{
					if (!TryResolveCard(card, out var target)) continue;

					best = target;
					bestCard = card;
					bestPlayer = null;
					bestDistance = hit.distance;
					found = true;
					continue;
				}

				var player = hit.collider.GetComponentInParent<PokerPlayer>();

				// Our own body sits just under the eye, so a ray to anybody across the table passes through it.
				if (!player || player.ClientId == OwnerClientId || _query.AcceptPlayer == null || !_query.AcceptPlayer(player)) continue;

				best = new PokerTarget(player, -1, false);
				bestPlayer = player;
				bestCard = null;
				bestDistance = hit.distance;
				found = true;
			}

			Hover(best, found, bestPlayer, bestCard);
		}

		// Which hand or board this card lies in, and whether the query takes it. Cards are parented to whatever
		// anchor they rest on, so the owner is asked of the hands rather than read off the hierarchy.
		private bool TryResolveCard(PokerCardVisual card, out PokerTarget target)
		{
			target = default;

			if (_query.AcceptHoleCard != null)
			{
				foreach (var player in PokerPlayer.All)
				{
					var hand = player ? player.HandVisual : null;
					if (!hand) continue;

					var slot = hand.SlotOf(card);
					if (slot < 0) continue;
					if (!_query.AcceptHoleCard(player, slot)) return false;

					target = new PokerTarget(player, slot, false);
					return true;
				}
			}

			var board = PokerBoardVisual.Instance;
			if (_query.AcceptBoardCard != null && board)
			{
				for (var slot = 0; slot < board.Cards.Count; slot++)
				{
					if (board.Cards[slot] != card) continue;
					if (!_query.AcceptBoardCard(slot)) return false;

					target = new PokerTarget(null, slot, true);
					return true;
				}
			}

			return false;
		}

		private void Hover(PokerTarget target, bool found, PokerPlayer player, PokerCardVisual card)
		{
			if (_hoveredPlayer != player)
			{
				SetPlayerLit(_hoveredPlayer, false);
				_hoveredPlayer = player;
				SetPlayerLit(_hoveredPlayer, true);
			}

			if (_hoveredCard != card)
			{
				SetCardLit(_hoveredCard, false);
				_hoveredCard = card;
				SetCardLit(_hoveredCard, true);
			}

			_hovered = target;
			_hasHovered = found;
		}

		// The body and the name over it light together, so what is being chosen reads as one thing.
		private static void SetPlayerLit(PokerPlayer player, bool lit)
		{
			if (!player) return;

			if (player.Visual) player.Visual.SetLocalOutlined(lit);
			if (player.NameTag) player.NameTag.SetLocalHighlighted(lit);
		}

		private void SetCardLit(PokerCardVisual card, bool lit)
		{
			if (!card) return;

			card.SetHighlighted(lit);
			card.SetLift(lit ? _cardHoverLift : 0f);
		}

		private void SetCursor(CursorVisualState? state)
		{
			var cursor = CursorVisualController.Instance;

			if (_cursorHandle != 0)
			{
				if (cursor) cursor.Release(_cursorHandle);
				_cursorHandle = 0;
			}

			if (state.HasValue && cursor) _cursorHandle = cursor.Request(state.Value);
		}

		// UI/Click is PassThrough and performs on release as well as press, so the callback asks what the
		// button is doing; the frame guard covers two bindings actuating in one frame. What is committed is
		// what is lit, never a fresh raycast.
		private void HandlePick(InputAction.CallbackContext context)
		{
			if (!IsOwner || _query == null || CursorController.IsLocked) return;
			if (!context.ReadValueAsButton()) return;
			if (_lastPickFrame == Time.frameCount || !_hasHovered) return;

			_lastPickFrame = Time.frameCount;

			var picked = _hovered;
			var callback = _onPicked;

			callback?.Invoke(picked);
		}
	}
}
