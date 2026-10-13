using System.Collections.Generic;
using DG.Tweening;
using Game.Runtime.GameMode.Poker.Items;
using Game.Runtime.GameMode.Poker.Visual;
using Game.Runtime.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Player
{
	// How this player acts one item out, on every screen. The prop is set up in the player prefab where it
	// belongs (in the right hand, at the mouth) and switched off; the gesture's clip drives it through cues
	// (shown at the grab, its own clip played at use, let go on the table), and the item's own moments
	// (resolved, outcome, fold lock lifted) finish it. One per item, on its own child of the player's
	// ItemPerformances; PokerItemPerformer starts it. Presentation only.
	// Runs first: a prop authored on the rig is lifted off it before the rig's renderer caches
	// (PlayerAnimationEventRelay, AnimationVfxPlayer) count it as part of the body.
	[DefaultExecutionOrder(-1000)]
	public class PokerItemPerformance : MonoBehaviour
	{
		[Header("Item")]
		[Tooltip("The item assets this acts out (items that share a look share a performance); a mode's own variant of an item can be acted out by another.")]
		[Required]
		[SerializeField] private List<PokerItem> _items = new();

		[Tooltip("Who acts it out: the user, the player they aimed it at, or both, each with a prop of their own.")]
		[SerializeField] private PokerItemPropActor _performer;

		[Tooltip("The performer cannot turn their view while it plays (their own screen only).")]
		[SerializeField] private bool _locksLook;

		[Header("Prop")]
		[Tooltip("The prop (an instance of a Prefabs/Items prop), switched off, authored where it belongs: on a bone of the full-body rig (POT_attach_R for the hand, Jaw_M for the mouth) at its pose there. It returns there on the rig being drawn; it is lifted under this object on Awake.")]
		[Required]
		[SerializeField] private GameObject _prop;

		[Tooltip("Where a card the item hands out flies from (when this player is the user).")]
		[SerializeField] private PokerItemPropCardSource _cardsFrom;

		[ShowIf(nameof(_cardsFrom), PokerItemPropCardSource.Prop)]
		[Tooltip("The point on the prop a card handed out rides until it leaves (the tip of a vomit stream). Empty: the prop's root.")]
		[SerializeField] private Transform _cardPoint;

		[ShowIf(nameof(_cardsFrom), PokerItemPropCardSource.Prop)]
		[Tooltip("Seconds after the item is played before a card handed out leaves the card point for the hand: the moment the pick-up gesture reaches it.")]
		[MinValue(0f)]
		[SerializeField] private float _cardDepartDelay;

		[Tooltip("Where a die prop (PokerSuitDie) is thrown to, seen from the performer.")]
		[SerializeField] private PokerItemPropAnchor _dieLandingAnchor = PokerItemPropAnchor.Table;

		[SerializeField] private Vector3 _dieLandingOffset;

		[Header("Actions")]
		[Tooltip("What happens, each at its moment. Cues come from the performer's clip; any order.")]
		[SerializeField] private List<PokerItemPerformanceAction> _actions = new();

		[Tooltip("A cue the clip never raises (missing event, rig not drawn) fires anyway this long after the play, so a prop is never left out.")]
		[MinValue(0f)]
		[SerializeField] private float _cueTimeout = 6f;

		private PokerPlayer _player;
		private PlayerController _playerController;
		private PlayerHandIkController _handIk;
		private PokerGameMode _gameMode;
		private PokerItemModule _module;
		private ulong _userClientId;
		private ulong _targetClientId;

		private Transform _authoredParent;
		private PlayerBone? _homeBone;
		private Vector3 _homePosition;
		private Quaternion _homeRotation;
		private Vector3 _restScale;
		private Transform _cardPointParent;
		private Vector3 _cardPointPosition;
		private Quaternion _cardPointRotation;
		private Animator _propAnimator;
		private PokerSuitDie _die;

		private readonly List<PlayerAnimationEventRelay> _relays = new();
		private readonly HashSet<string> _heardCues = new();
		private readonly List<Tween> _tweens = new();
		private readonly List<GameObject> _effects = new();
		private Tween _moveTween;
		private Tween _scaleTween;
		private Transform _cardOrigin;
		private int _useStreetSerial;
		private bool _playing;
		private bool _lookLocked;
		private bool _outcomeKnown;
		private bool _resolved;
		private bool _foldLockLifted;
		private bool _dieThrown;
		private bool _dieRolled;
		private bool _hasRewrite;
		private CardSuit? _rewriteSuit;

		public bool ActsOut(PokerItem item) => item && _items.Contains(item);

		public bool ActsOut(PokerItemType type)
		{
			foreach (var item in _items)
			{
				if (item && item.Type == type) return true;
			}

			return false;
		}

		private void Awake()
		{
			_player = GetComponentInParent<PokerPlayer>(true);
			_playerController = GetComponentInParent<PlayerController>(true);
			_handIk = _player ? _player.GetComponentInChildren<PlayerHandIkController>(true) : null;
			if (!_prop) return;

			var prop = _prop.transform;
			_authoredParent = prop.parent;
			_homeBone = BoneOf(_authoredParent);
			_homePosition = prop.localPosition;
			_homeRotation = prop.localRotation;
			_restScale = prop.localScale;
			_propAnimator = _prop.GetComponentInChildren<Animator>(true);
			_die = _prop.GetComponentInChildren<PokerSuitDie>(true);

			if (_cardPoint)
			{
				_cardPointParent = _cardPoint.parent;
				_cardPointPosition = _cardPoint.localPosition;
				_cardPointRotation = _cardPoint.localRotation;
			}

			prop.SetParent(transform, true);
			_prop.SetActive(false);
		}

		// Both rigs, each gating itself on being drawn, as PokerBetItemCarryController does.
		private void OnEnable()
		{
			if (_player && _player.Rig) _player.Rig.GetComponentsInChildren(true, _relays);

			foreach (var relay in _relays) relay.OnAnimationCue += HandleCue;
		}

		private void OnDisable()
		{
			foreach (var relay in _relays) relay.OnAnimationCue -= HandleCue;

			_relays.Clear();
			Stop();
		}

		public bool PlaysFor(bool isUser, bool isTarget) => _performer switch
		{
			PokerItemPropActor.User => isUser,
			PokerItemPropActor.Target => isTarget,
			_ => isUser || isTarget
		};

		// A second use before the first is over (cuffs still on) starts over. An outcome drawn as it was played is
		// acted out from the start; one the item only announces later arrives through ReceiveOutcome.
		public void Play(PokerGameMode gameMode, PokerItemModule module, ulong userClientId, ulong targetClientId, int outcome)
		{
			Stop();
			if (!_player || !_prop || !gameMode || !module) return;

			_gameMode = gameMode;
			_module = module;
			_userClientId = userClientId;
			_targetClientId = targetClientId;
			_useStreetSerial = module.StreetSerial.Value;
			_playing = true;

			PutHome();
			ReturnCardPoint();
			if (_locksLook && _player.IsOwner && _playerController)
			{
				_playerController.BeginLookLock();
				_lookLocked = true;
			}

			if (_die) _die.ShowSecret(!_player.IsOwner);
			if (_player.ClientId == userClientId) StartCardOrigin();

			foreach (var action in _actions)
			{
				if (action.When == PokerItemMoment.Cue && !string.IsNullOrEmpty(action.Cue)) Schedule(_cueTimeout, () => HandleCue(action.Cue));
			}

			Run(PokerItemMoment.Played);

			if (outcome != PokerItemUseRequest.NoOutcome)
			{
				_outcomeKnown = true;
				Run(PokerItemMoment.Outcome, null, outcome);
			}
		}

		public void Resolve(ulong userClientId)
		{
			if (!_playing || _resolved || userClientId != _userClientId) return;

			_resolved = true;
			Run(PokerItemMoment.Resolved);
			RefreshFoldLock();
		}

		// A chance item's outcome reaches the table as a notice before the item is done.
		public void ReceiveOutcome(ulong userClientId, int outcome)
		{
			if (!_playing || _resolved || _outcomeKnown || userClientId != _userClientId) return;

			_outcomeKnown = true;
			Run(PokerItemMoment.Outcome, null, outcome);
		}

		// The card the item is rewriting and what it becomes, told as the flicker starts. A screen that may not
		// see the card gets the secret die, so it never tells the table.
		public void ReceiveRewrite(PokerCardPlace place, CardData card)
		{
			if (!_playing || _resolved || _hasRewrite || place.HolderClientId != _userClientId) return;

			_hasRewrite = true;
			Run(PokerItemMoment.Rewriting);
			if (!_die) return;

			var holder = PokerPlayer.Find(place.HolderClientId);
			var data = holder ? holder.Data : null;
			var visible = data && place.Slot >= 0 && place.Slot < data.CardCount && data.IsHoleCardVisible(place.Slot);

			_rewriteSuit = visible && card.IsValid && !card.IsJoker ? card.SuitType : null;
			TryRollDie();
		}

		// A fold lock laid after the item was played lifts once the hand moves past it; the hand ending clears it too.
		public void RefreshFoldLock()
		{
			if (!_playing || !_resolved || _foldLockLifted || IsFoldLocked()) return;

			_foldLockLifted = true;
			Run(PokerItemMoment.FoldLockLifted);
		}

		public void Stop()
		{
			foreach (var tween in _tweens) tween?.Kill();
			_tweens.Clear();
			_moveTween?.Kill();
			_scaleTween?.Kill();

			var deck = PokerDeckVisual.Instance;
			if (_cardOrigin && deck) deck.ClearItemDrawOrigin(_cardOrigin);

			foreach (var effect in _effects)
			{
				if (effect) Destroy(effect);
			}

			_effects.Clear();

			if (_lookLocked && _playerController) _playerController.EndLookLock();
			_lookLocked = false;

			if (_prop)
			{
				if (_die) _die.Stop();

				var prop = _prop.transform;
				prop.SetParent(transform, false);
				prop.localScale = _restScale;
				_prop.SetActive(false);
			}

			_heardCues.Clear();
			_cardOrigin = null;
			_playing = false;
			_resolved = false;
			_foldLockLifted = false;
			_dieThrown = false;
			_outcomeKnown = false;
			_dieRolled = false;
			_hasRewrite = false;
			_rewriteSuit = null;
		}

		private void HandleCue(string cue)
		{
			if (!_playing || string.IsNullOrEmpty(cue) || !_heardCues.Add(cue)) return;

			Run(PokerItemMoment.Cue, cue);
		}

		private void Run(PokerItemMoment moment, string cue = null, int outcome = 0)
		{
			foreach (var action in _actions)
			{
				if (!action.Matches(moment, cue, outcome)) continue;

				var current = action;
				if (current.Delay > 0f) Schedule(current.Delay, () => Do(current));
				else Do(current);
			}
		}

		private void Schedule(float delay, TweenCallback callback) => _tweens.Add(DOVirtual.DelayedCall(delay, callback, false));

		private void Do(PokerItemPerformanceAction action)
		{
			if (!_playing || !_prop) return;

			var prop = _prop.transform;
			switch (action.Do)
			{
				case PokerItemPropAction.Show:
					if (!_prop.activeSelf) prop.localScale = action.Duration > 0f ? Vector3.zero : _restScale;
					_prop.SetActive(true);
					// Its animator's first pose this frame, not one frame of the rest pose before the clip takes it.
					if (_propAnimator && _propAnimator.isActiveAndEnabled) _propAnimator.Update(0f);
					ScaleTo(_restScale, action, false);
					break;
				case PokerItemPropAction.Hide:
					ScaleTo(Vector3.zero, action, true);
					break;
				case PokerItemPropAction.ReturnHome:
					_moveTween?.Kill();
					PutHome();
					break;
				case PokerItemPropAction.AttachTo:
					_moveTween?.Kill();
					var anchor = ResolveAnchor(action.Anchor);
					if (anchor) prop.SetParent(anchor, true);
					break;
				case PokerItemPropAction.MoveTo:
					MoveTo(action);
					break;
				case PokerItemPropAction.PlayState:
					if (_propAnimator && _propAnimator.isActiveAndEnabled)
					{
						// In seconds: a normalized fade advances with the state it leaves, and a prop's rest state is frozen.
						var hash = Animator.StringToHash(action.State);
						if (_propAnimator.HasState(0, hash)) _propAnimator.CrossFadeInFixedTime(hash, 0.1f, 0);
					}

					break;
				case PokerItemPropAction.PlayGesture:
					if (!string.IsNullOrEmpty(action.Gesture) && _player.ActionAnimator) _player.ActionAnimator.PlayLocal(action.Gesture);
					break;
				case PokerItemPropAction.SpawnEffect:
					if (!action.Effect) break;

					var point = action.EffectPoint ? action.EffectPoint : prop;
					var effect = Instantiate(action.Effect, point.position, point.rotation, point);
					_effects.Add(effect);
					if (action.EffectLifetime > 0f) Destroy(effect, action.EffectLifetime);
					break;
				case PokerItemPropAction.RollDie:
					_dieThrown = true;
					TryRollDie();
					break;
				case PokerItemPropAction.Finish:
					Stop();
					break;
				case PokerItemPropAction.AttachCardPoint:
					if (!_cardPoint) break;

					var carrier = ResolveAnchor(action.Anchor);
					_cardPoint.SetParent(carrier ? carrier : transform, true);
					break;
				case PokerItemPropAction.AimHand:
					var spot = ResolveAnchor(action.Anchor);
					if (_handIk && spot) _handIk.Aim(spot);
					break;
				case PokerItemPropAction.AimHandAtTarget:
					var target = PokerPlayer.Find(_targetClientId);
					if (_handIk && target && target.Rig) _handIk.Aim(target.Rig.FocusPoint);
					break;
			}
		}

		// Back on the prop, at the start of a play rather than the end of one: a card handed out may still be
		// riding it in the hand after the performance is over, until it leaves for the hand's cards.
		private void ReturnCardPoint()
		{
			if (!_cardPoint || !_cardPointParent || _cardPoint.parent == _cardPointParent) return;

			_cardPoint.SetParent(_cardPointParent, false);
			_cardPoint.SetLocalPositionAndRotation(_cardPointPosition, _cardPointRotation);
		}

		// On the drawn rig's bone the prop was authored on (the hand-only rig on its owner's screen), at its pose there.
		private void PutHome()
		{
			var prop = _prop.transform;
			var parent = _homeBone.HasValue && _player.Rig ? _player.Rig.GetBone(_homeBone.Value) : null;

			prop.SetParent(parent ? parent : _authoredParent, false);
			prop.SetLocalPositionAndRotation(_homePosition, _homeRotation);
		}

		private PlayerBone? BoneOf(Transform bone)
		{
			var rig = _player && _player.Rig ? _player.Rig.FullBodyRig : null;
			if (!rig || !bone) return null;

			foreach (PlayerBone candidate in System.Enum.GetValues(typeof(PlayerBone)))
			{
				if (rig.TryGet(candidate, out var found) && found == bone) return candidate;
			}

			return null;
		}

		private void ScaleTo(Vector3 scale, in PokerItemPerformanceAction action, bool switchOff)
		{
			_scaleTween?.Kill();

			_scaleTween = _prop.transform.DOScale(scale, action.Duration)
				.SetEase(action.Ease != Ease.Unset ? action.Ease : switchOff ? Ease.InBack : Ease.OutBack)
				.OnComplete(() =>
				{
					if (switchOff && _prop) _prop.SetActive(false);
				});
		}

		// Read every frame towards an anchor that moves, from a start kept in the anchor's space so a prop already
		// riding a moving hand stays on it; rides the anchor at the end when it is on the body.
		private void MoveTo(PokerItemPerformanceAction action)
		{
			_moveTween?.Kill();

			var prop = _prop.transform;
			var anchor = ResolveAnchor(action.Anchor);
			prop.SetParent(transform, true);

			var startPosition = anchor ? anchor.InverseTransformPoint(prop.position) : prop.position;
			var startRotation = anchor ? Quaternion.Inverse(anchor.rotation) * prop.rotation : prop.rotation;

			_moveTween = DOVirtual.Float(0f, 1f, Mathf.Max(0.0001f, action.Duration), t =>
				{
					if (!_prop) return;

					var frame = anchor && IsPalm(action.Anchor) ? anchor.rotation : Frame();
					var from = anchor ? anchor.TransformPoint(startPosition) : startPosition;
					var fromRotation = anchor ? anchor.rotation * startRotation : startRotation;
					var to = (anchor ? anchor.position : from) + frame * action.Offset;
					var hop = Vector3.up * (action.Arc * 4f * t * (1f - t));

					prop.SetPositionAndRotation(Vector3.LerpUnclamped(from, to, t) + hop,
						Quaternion.SlerpUnclamped(fromRotation, frame * Quaternion.Euler(action.Rotation), t));
				})
				.SetEase(action.Ease == Ease.Unset ? Ease.InOutSine : action.Ease)
				.OnComplete(() =>
				{
					if (_prop && anchor && IsOnBody(action.Anchor)) prop.SetParent(anchor, true);
				});
		}

		// Thrown once the hand lets go and the new suit is known, whichever comes last.
		private void TryRollDie()
		{
			if (!_die || !_dieThrown || !_hasRewrite || _dieRolled) return;

			_dieRolled = true;
			_moveTween?.Kill();
			_prop.transform.SetParent(transform, true);

			var anchor = ResolveAnchor(_dieLandingAnchor);
			var landing = (anchor ? anchor.position : _prop.transform.position) + Frame() * _dieLandingOffset;
			// The card flickers for the pacing's rewrite duration before its new face is written: the die settles then.
			var pacing = _module ? _module.ExchangePacing : null;
			_die.RollTo(landing, _rewriteSuit, pacing ? pacing.RewriteDuration : 1f);
		}

		private void StartCardOrigin()
		{
			var deck = PokerDeckVisual.Instance;
			if (!deck) return;

			var seat = Seat();
			_cardOrigin = _cardsFrom switch
			{
				PokerItemPropCardSource.Prop => _cardPoint ? _cardPoint : _prop.transform,
				PokerItemPropCardSource.Stash => seat ? seat.ItemStashAnchor : null,
				_ => null
			};

			var departAt = _cardsFrom == PokerItemPropCardSource.Prop ? Time.time + _cardDepartDelay : 0f;
			if (_cardOrigin) deck.SetItemDrawOrigin(_cardOrigin, departAt);
		}

		// A fold lock laid on this player after the item was played, still covering this street or one to come.
		private bool IsFoldLocked()
		{
			if (!_module) return false;

			var serial = _module.StreetSerial.Value;
			foreach (var rule in _module.TableRules)
			{
				if (rule.Kind != PokerItemTableRuleKind.NoFoldSelf || rule.SourceClientId != _player.ClientId) continue;
				if (rule.StreetSerial > _useStreetSerial && rule.StreetSerial + Mathf.Max(1, rule.Streets) > serial) return true;
			}

			return false;
		}

		private static bool IsPalm(PokerItemPropAnchor anchor) => anchor is PokerItemPropAnchor.Hand or PokerItemPropAnchor.HandLeft;

		private static bool IsOnBody(PokerItemPropAnchor anchor) =>
			anchor is PokerItemPropAnchor.Hand or PokerItemPropAnchor.HandLeft or PokerItemPropAnchor.Mouth or PokerItemPropAnchor.Nose
				or PokerItemPropAnchor.Cards or PokerItemPropAnchor.Focus;

		// The chair's facing, which is the sitter's: x their right, z towards the table.
		private Quaternion Frame()
		{
			var seat = Seat();
			return seat ? seat.transform.rotation : _player.transform.rotation;
		}

		private PokerSeat Seat() => _gameMode && _player.Data ? _gameMode.FindSeat(_player.Data.SeatIndex.Value) : null;

		private Transform ResolveAnchor(PokerItemPropAnchor anchor)
		{
			switch (anchor)
			{
				case PokerItemPropAnchor.Hand:
					return _player.Rig ? _player.Rig.GetBone(PlayerBone.AttachRight) : null;
				case PokerItemPropAnchor.HandLeft:
					return _player.Rig ? _player.Rig.GetBone(PlayerBone.AttachLeft) : null;
				case PokerItemPropAnchor.Stash:
					return Seat() ? Seat().ItemStashAnchor : null;
				case PokerItemPropAnchor.Table:
					return Seat() ? Seat().ItemTableAnchor : null;
				case PokerItemPropAnchor.Mouth:
					return _player.Rig ? _player.Rig.GetBone(PlayerBone.Jaw) : null;
				case PokerItemPropAnchor.Nose:
					return _player.Rig ? _player.Rig.GetBone(PlayerBone.Nose) : null;
				case PokerItemPropAnchor.Cards:
					return _player.HandVisual ? _player.HandVisual.HandAnchor : null;
				case PokerItemPropAnchor.Focus:
					return FocusOf(_player);
				case PokerItemPropAnchor.Body:
					return _player.transform;
				case PokerItemPropAnchor.CardPoint:
					return _cardPoint;
				default:
					return null;
			}
		}

		// About you, before your own eyes; about anybody else, on their body.
		private static Transform FocusOf(PokerPlayer player)
		{
			if (!player || !player.Rig) return null;

			return player.IsOwner ? player.Rig.SelfFocusPoint : player.Rig.FocusPoint;
		}
	}
}
