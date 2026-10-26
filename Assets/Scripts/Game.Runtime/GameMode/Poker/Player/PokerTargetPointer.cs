using System;
using System.Collections.Generic;
using Game.Runtime.Controller;
using Game.Runtime.GameMode.Poker.Visual;
using Game.Runtime.UI.CursorVisuals;
using Game.Runtime.UI.Selection;
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
	//
	// A pad has no cursor here: left and right step through everything the caller would take, in screen
	// order (bodies, cards, and this player themselves where they may be chosen, which lights the name the
	// self pick shows), and Submit commits. Bodies flash once as the choice opens; then only the lit one is outlined.
	public class PokerTargetPointer : NetworkBehaviour
	{
		[Header("Input")]
		[Tooltip("Pressed to commit whatever is lit. UI/Click - pointing at a player is the same act as pointing at a card or a button.")]
		[SerializeField] private InputActionReference _pickAction;

		[Tooltip("UI/Navigate: steps through the choices on a pad. Read, never enabled or disabled here.")]
		[SerializeField] private InputActionReference _navigateAction;

		[Tooltip("UI/Submit: commits the stepped-onto choice on a pad. Read, never enabled or disabled here.")]
		[SerializeField] private InputActionReference _submitAction;

		[Tooltip("Empty, with a confining UIFocusRoot: switched on while a pad steps through the choices, so no button navigates.")]
		[SerializeField] private UIFocusRoot _padFocus;

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

		// Everything the asker would take, pulsing until the choice ends. Read once at Begin.
		private readonly List<PokerPlayer> _promptedPlayers = new();
		private readonly List<PokerCardVisual> _promptedCards = new();

		private int _cursorHandle;
		private int _lastPickFrame = -1;

		public bool IsActive => _query != null;

		// This player is the stepped-onto choice on a pad: the self pick lights its name for it.
		public bool IsSelfFocused { get; private set; }
		public event Action OnSelfFocusChanged;

		// A mouse gets the cursor while something must be pointed at; a pad steps instead.
		private bool _pointerHeld;

		private UINavigationPicker _navigation;
		private bool _padMode;
		private int _padIndex;
		private readonly List<PadChoice> _padChoices = new();

		private struct PadChoice
		{
			public PokerTarget Target;
			public PokerPlayer Body;
			public PokerCardVisual Card;
			public float ScreenX;
		}

		private void Awake()
		{
			_navigation = new UINavigationPicker(_padFocus, _navigateAction, _submitAction);
		}

		public override void OnNetworkSpawn()
		{
			if (!IsOwner) return;

			if (_pickAction && _pickAction.action != null)
			{
				_pickAction.action.performed += HandlePick;
				_pickAction.action.Enable();
			}

			_navigation.OnStep += HandlePadStep;
			_navigation.OnSubmit += HandlePadSubmit;
		}

		public override void OnNetworkDespawn()
		{
			if (!IsOwner) return;

			End();

			_navigation.OnSubmit -= HandlePadSubmit;
			_navigation.OnStep -= HandlePadStep;
			if (_pickAction && _pickAction.action != null) _pickAction.action.performed -= HandlePick;
		}

		public void Begin(PokerTargetQuery query, Action<PokerTarget> onPicked)
		{
			if (!IsOwner) return;

			End();

			_query = query;
			_onPicked = onPicked;
			SetCursor(query != null ? query.Cursor : (CursorVisualState?)null);
			PromptTargets();
			SyncPadMode();
		}

		public void End()
		{
			_query = null;
			_onPicked = null;

			Hover(default, false, null, null);
			SetCursor(null);
			ClearPrompts();
			SetPointerHeld(false);

			_padMode = false;
			_padChoices.Clear();
			_navigation.End();
		}

		// Picking the pad up or putting it down mid-choice swaps how the choice is made.
		private void SyncPadMode()
		{
			var pad = _query != null && InputSchemeController.IsGamepad;
			SetPointerHeld(_query != null && !pad);

			if (pad == _padMode) return;

			_padMode = pad;
			Hover(default, false, null, null);

			if (pad)
			{
				_navigation.Begin();
				_padIndex = 0;
				StepPad(0);
			}
			else
			{
				_navigation.End();
			}
		}

		private void HandlePadStep(int direction) => StepPad(direction);

		private void HandlePadSubmit()
		{
			if (_query == null || !_hasHovered) return;

			_onPicked?.Invoke(_hovered);
		}

		// Re-collected on every step, so a card dealt or a body gone since the last step is accounted for.
		// Wraps at either end.
		private void StepPad(int direction)
		{
			CollectPadChoices();

			if (_padChoices.Count == 0)
			{
				Hover(default, false, null, null);
				return;
			}

			_padIndex = ((_padIndex + direction) % _padChoices.Count + _padChoices.Count) % _padChoices.Count;
			var choice = _padChoices[_padIndex];
			Hover(choice.Target, true, choice.Body, choice.Card);
		}

		private void CollectPadChoices()
		{
			_padChoices.Clear();

			var view = GameCamera.Main;
			if (!view || _query == null) return;

			foreach (var player in _promptedPlayers)
			{
				if (player) AddChoice(view, new PokerTarget(player, -1, false), player, null, player.transform.position);
			}

			foreach (var card in _promptedCards)
			{
				if (card && TryResolveCard(card, out var target)) AddChoice(view, target, null, card, card.transform.position);
			}

			// ponytail: the self pick's name sits at the bottom centre, so this player is placed mid-screen;
			// read the button's rect instead if the name ever moves off centre.
			var self = PokerPlayer.Find(OwnerClientId);
			if (self && _query.AcceptPlayer != null && _query.AcceptPlayer(self))
			{
				_padChoices.Add(new PadChoice { Target = new PokerTarget(self, -1, false), ScreenX = Screen.width * 0.5f });
			}

			_padChoices.Sort((a, b) => a.ScreenX.CompareTo(b.ScreenX));
		}

		private void AddChoice(UnityEngine.Camera view, PokerTarget target, PokerPlayer body, PokerCardVisual card, Vector3 position)
		{
			_padChoices.Add(new PadChoice { Target = target, Body = body, Card = card, ScreenX = view.WorldToScreenPoint(position).x });
		}

		private void SetPointerHeld(bool held)
		{
			if (_pointerHeld == held) return;

			_pointerHeld = held;
			if (held) CursorController.RequestPointer();
			else CursorController.ReleasePointer();
		}

		// ponytail: snapshot at Begin; a card dealt mid-choice does not pulse until the next Begin.
		private void PromptTargets()
		{
			if (_query == null) return;

			foreach (var player in PokerPlayer.All)
			{
				if (!player) continue;

				if (_query.AcceptPlayer != null && player.ClientId != OwnerClientId && player.Visual && _query.AcceptPlayer(player))
				{
					player.Visual.SetLocalPrompted(true);
					_promptedPlayers.Add(player);
				}

				var hand = player.HandVisual;
				if (_query.AcceptHoleCard == null || !hand) continue;

				foreach (var card in hand.Cards)
				{
					var slot = card ? hand.SlotOf(card) : -1;
					if (slot < 0 || !_query.AcceptHoleCard(player, slot)) continue;

					card.SetPrompted(true);
					_promptedCards.Add(card);
				}
			}

			var board = PokerBoardVisual.Instance;
			if (_query.AcceptBoardCard == null || !board) return;

			for (var slot = 0; slot < board.Cards.Count; slot++)
			{
				var card = board.Cards[slot];
				if (!card || !_query.AcceptBoardCard(slot)) continue;

				card.SetPrompted(true);
				_promptedCards.Add(card);
			}
		}

		private void ClearPrompts()
		{
			foreach (var player in _promptedPlayers)
			{
				if (player && player.Visual) player.Visual.SetLocalPrompted(false);
			}

			foreach (var card in _promptedCards)
			{
				if (card) card.SetPrompted(false);
			}

			_promptedPlayers.Clear();
			_promptedCards.Clear();
		}

		// Genuinely continuous: what is under the cursor changes as it moves and as bodies and cards move.
		private void Update()
		{
			if (!IsOwner || _query == null) return;

			SyncPadMode();
			if (_padMode) return;

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

			var selfFocused = found && _padMode && !player && !card && target.Player && target.Player.ClientId == OwnerClientId;
			if (IsSelfFocused != selfFocused)
			{
				IsSelfFocused = selfFocused;
				OnSelfFocusChanged?.Invoke();
			}
		}

		// The body and the name over it light together, so what is being chosen reads as one thing. Bodies
		// flash once when the choice opens (PlayerVisual._promptFlashes) and rest after; only the lit one stays
		// outlined, and leaving it does not flash it again.
		private void SetPlayerLit(PokerPlayer player, bool lit)
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
			if (_promptedCards.Contains(card)) card.SetPrompted(!lit);
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
