using System.Collections.Generic;
using DG.Tweening;
using Game.Runtime.Audio;
using Game.Runtime.GameMode.Poker.Items;
using Game.Runtime.GameMode.Poker.Visual;
using Game.Runtime.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Player
{
	// How this player acts one item out, on every screen: the prop comes out, the intro is stretched to land as
	// the item acts (PokerItem.LeadIn, which the server waits), it holds, then the outro puts it away. One per
	// item, on its own child of the player's ItemPerformances; PokerItemPerformer starts it. Presentation only:
	// art replaces the prop prefab, a clip fills the gesture's state, and the steps stay.
	public class PokerItemPerformance : MonoBehaviour
	{
		[Header("Item")]
		[Tooltip("The item asset this acts out, so a mode's own variant of an item can be acted out differently.")]
		[Required]
		[SerializeField] private PokerItem _item;

		[Tooltip("Who acts it out: the user, the player they aimed it at, or both, each with a prop of their own.")]
		[SerializeField] private PokerItemPropActor _performer;

		[Header("Prop")]
		[Tooltip("The prop, set up as a child of this object (an instance of a Prefabs/Items prop, its root at the grip) and kept switched off until played. Art replaces the child.")]
		[Required]
		[SerializeField] private GameObject _prop;

		[Tooltip("Where a card the item hands out flies from (when this player is the user).")]
		[SerializeField] private PokerItemPropCardSource _cardsFrom;

		[Tooltip("Where a die prop (PokerSuitDie) is thrown to once the card's new suit is known, seen from the performer.")]
		[SerializeField] private PokerItemPropAnchor _dieLandingAnchor = PokerItemPropAnchor.Table;

		[SerializeField] private Vector3 _dieLandingOffset;

		[Header("Steps")]
		[Tooltip("Played as the item is used, stretched to end exactly when it acts (the item's Lead In). In order.")]
		[SerializeField] private List<PokerItemPerformanceStep> _intro = new();

		[SerializeField] private PokerItemPropHold _hold;

		[ShowIf(nameof(_hold), PokerItemPropHold.Duration)]
		[MinValue(0f)]
		[SerializeField] private float _holdDuration;

		[Tooltip("Played once the hold is over, then the prop is gone. In order.")]
		[SerializeField] private List<PokerItemPerformanceStep> _outro = new();

		[Header("Hand")]
		[Tooltip("Seconds the hand takes to reach for a prop it follows, or to let it go.")]
		[MinValue(0f)]
		[SerializeField] private float _handReachDuration = 0.2f;

		// Where a step set off from, filled as it starts.
		private sealed class StepStart
		{
			public Vector3 Position;
			public Quaternion Rotation;
			public Vector3 Scale;
			public Quaternion? EndRotation;
		}

		private PokerPlayer _player;
		private PokerGameMode _gameMode;
		private PokerItemModule _module;
		private PokerPlayer _other;
		private Transform _ownFacePoint;
		private ulong _userClientId;

		private Transform _propHome;
		private Vector3 _propHomePosition;
		private Quaternion _propHomeRotation;
		private Animator _propAnimator;
		private PokerSuitDie _die;
		private readonly List<GameObject> _effects = new();
		private Vector3 _restScale;
		private Sequence _sequence;
		private Tween _handTween;
		private bool _handFollowing;
		private Transform _cardOrigin;
		private int _useStreetSerial;
		private bool _playing;
		private bool _holding;
		private bool _resolved;
		private bool _hasOutcome;
		private int _outcome;

		public PokerItem Item => _item;

		private void Awake()
		{
			_player = GetComponentInParent<PokerPlayer>(true);
			if (!_prop) return;

			var prop = _prop.transform;
			_propHome = prop.parent;
			_propHomePosition = prop.localPosition;
			_propHomeRotation = prop.localRotation;
			_restScale = prop.localScale;
			_propAnimator = _prop.GetComponentInChildren<Animator>(true);
			_die = _prop.GetComponentInChildren<PokerSuitDie>(true);
			_prop.SetActive(false);
		}

		private void OnDestroy() => Stop();

		public bool PlaysFor(bool isUser, bool isTarget) => _performer switch
		{
			PokerItemPropActor.User => isUser,
			PokerItemPropActor.Target => isTarget,
			_ => isUser || isTarget
		};

		// A second use before the first is over (cuffs still on) starts over.
		public void Play(PokerGameMode gameMode, PokerItemModule module, ulong userClientId, PokerPlayer other, Transform ownFacePoint)
		{
			Stop();
			if (!_player || !_prop || !gameMode || !module) return;

			_ownFacePoint = ownFacePoint;
			_gameMode = gameMode;
			_module = module;
			_userClientId = userClientId;
			_other = other;
			_useStreetSerial = module.StreetSerial.Value;
			_playing = true;

			var at = _player.transform.position;
			var rotation = Frame();
			if (_intro.Count > 0)
			{
				at = TargetPosition(_intro[0], at);
				rotation = TargetRotation(_intro[0], at);
			}

			_prop.transform.SetPositionAndRotation(at, rotation);
			_prop.transform.localScale = Vector3.zero;
			_prop.SetActive(true);

			if (_player.ClientId == userClientId) StartCardOrigin();

			var intro = BuildSteps(_intro, out var duration);
			if (_item.LeadIn > 0f && duration > 0f) intro.timeScale = duration / _item.LeadIn;

			_sequence = intro.OnComplete(EnterHold);
		}

		public void Resolve(ulong userClientId)
		{
			if (!_playing || _resolved || userClientId != _userClientId) return;

			_resolved = true;
			TryRelease();
		}

		// A chance item's outcome reaches the table as a notice before the item is done, so the outro can tell.
		public void ReceiveOutcome(ulong userClientId, int outcome)
		{
			if (!_playing || _resolved || userClientId != _userClientId) return;

			_hasOutcome = true;
			_outcome = outcome;
		}

		// The card the item is rewriting and what it becomes, told as the flicker starts: a die is thrown to land on
		// the new suit. A screen that may not see the card gets any face, so the die never tells the table.
		public void ReceiveRewrite(PokerCardPlace place, CardData card)
		{
			if (!_playing || _resolved || !_die || place.HolderClientId != _userClientId) return;

			var holder = PokerPlayer.Find(place.HolderClientId);
			var data = holder ? holder.Data : null;
			var visible = data && place.Slot >= 0 && place.Slot < data.CardCount && data.IsHoleCardVisible(place.Slot);
			CardSuit? suit = visible && card.IsValid && !card.IsJoker ? card.SuitType : null;

			SetHandFollowing(false);
			_prop.transform.SetParent(_propHome, true);

			var anchor = ResolveAnchor(_dieLandingAnchor);
			var landing = (anchor ? anchor.position : _prop.transform.position) + Frame() * _dieLandingOffset;
			// The card flickers for the pacing's rewrite duration before its new face is written: the die settles then.
			var pacing = _module ? _module.ExchangePacing : null;
			_die.RollTo(landing, suit, pacing ? pacing.RewriteDuration : 1f);
		}

		public void RefreshHold()
		{
			if (_hold == PokerItemPropHold.UntilFoldLockLifted) TryRelease();
		}

		public void Stop()
		{
			_sequence?.Kill();
			_handTween?.Kill();
			_sequence = null;
			_handTween = null;

			var ik = _player ? _player.HandIk : null;
			if (_handFollowing && ik)
			{
				ik.FollowWeight = 0f;
				ik.Follow(null);
			}

			var deck = PokerDeckVisual.Instance;
			if (_cardOrigin && deck) deck.ClearItemDrawOrigin(_cardOrigin);

			foreach (var effect in _effects)
			{
				if (effect) Destroy(effect);
			}

			_effects.Clear();

			// Back where it was set up, switched off, ready for the next play.
			if (_prop)
			{
				if (_die) _die.Stop();

				var prop = _prop.transform;
				prop.SetParent(_propHome, false);
				prop.localPosition = _propHomePosition;
				prop.localRotation = _propHomeRotation;
				prop.localScale = _restScale;
				_prop.SetActive(false);
			}

			_cardOrigin = null;
			_handFollowing = false;
			_playing = false;
			_holding = false;
			_resolved = false;
			_hasOutcome = false;
		}

		private void StartCardOrigin()
		{
			var deck = PokerDeckVisual.Instance;
			if (!deck) return;

			var seat = Seat();
			_cardOrigin = _cardsFrom switch
			{
				PokerItemPropCardSource.Prop => _prop.transform,
				PokerItemPropCardSource.Stash => seat ? seat.ItemStashAnchor : null,
				_ => null
			};

			if (_cardOrigin) deck.SetItemDrawOrigin(_cardOrigin);
		}

		private Sequence BuildSteps(List<PokerItemPerformanceStep> steps, out float duration)
		{
			var sequence = DOTween.Sequence();
			duration = 0f;

			foreach (var step in steps)
			{
				if (step.OnlyOnOutcome && (!_hasOutcome || _outcome != step.Outcome)) continue;

				var current = step;
				var start = new StepStart();

				sequence.AppendCallback(() => BeginStep(current, start));
				sequence.Append(DOVirtual.Float(0f, 1f, Mathf.Max(0.0001f, current.Duration), t => ApplyStep(current, start, t))
					.SetEase(current.Ease == Ease.Unset ? Ease.InOutSine : current.Ease));
				sequence.AppendCallback(() => EndStep(current));

				duration += current.Duration;
			}

			return sequence;
		}

		private void BeginStep(PokerItemPerformanceStep step, StepStart start)
		{
			if (!_prop) return;

			var prop = _prop.transform;
			if (prop.parent != _propHome) prop.SetParent(_propHome, true);

			start.Position = prop.position;
			start.Rotation = prop.rotation;
			start.Scale = prop.localScale;
			start.EndRotation = step.HoldRotation ? prop.rotation : null;

			SetHandFollowing(step.HandFollows && step.To != PokerItemPropAnchor.Hand);

			if (!string.IsNullOrEmpty(step.Gesture) && _player.ActionAnimator) _player.ActionAnimator.PlayLocal(step.Gesture);

			if (!string.IsNullOrEmpty(step.PropState) && _propAnimator)
			{
				var hash = Animator.StringToHash(step.PropState);
				if (_propAnimator.HasState(0, hash)) _propAnimator.CrossFade(hash, 0.1f, 0);
			}

			if (step.Effect)
			{
				var effect = Instantiate(step.Effect, prop.position, prop.rotation, prop);
				_effects.Add(effect);
				if (step.EffectLifetime > 0f) Destroy(effect, step.EffectLifetime);
			}

			if (step.Sound && AudioManager.Instance) AudioManager.Instance.PlayOneShotAttached(step.Sound, prop);
		}

		private void ApplyStep(PokerItemPerformanceStep step, StepStart start, float t)
		{
			if (!_prop) return;

			var prop = _prop.transform;
			var position = Vector3.LerpUnclamped(start.Position, TargetPosition(step, start.Position), t);

			prop.SetPositionAndRotation(position,
				Quaternion.SlerpUnclamped(start.Rotation, start.EndRotation ?? TargetRotation(step, position), t) * Quaternion.Euler(step.Spin * t));

			// Clamped: an overshooting ease on the way out must not turn the prop inside out.
			prop.localScale = Vector3.Max(Vector3.zero, Vector3.LerpUnclamped(start.Scale, step.Hide ? Vector3.zero : _restScale, t));
		}

		// A prop left on the body rides it until the next step takes it away.
		private void EndStep(PokerItemPerformanceStep step)
		{
			if (!_prop || !IsOnBody(step.To)) return;

			var anchor = ResolveAnchor(step.To);
			if (anchor) _prop.transform.SetParent(anchor, true);
		}

		private static bool IsOnBody(PokerItemPropAnchor anchor) =>
			anchor is PokerItemPropAnchor.Hand or PokerItemPropAnchor.Mouth or PokerItemPropAnchor.Nose or PokerItemPropAnchor.Cards or PokerItemPropAnchor.Focus;

		private void SetHandFollowing(bool follow)
		{
			var ik = _player.HandIk;
			if (!ik || _handFollowing == follow) return;

			_handFollowing = follow;
			_handTween?.Kill();

			if (follow) ik.Follow(_prop.transform);

			_handTween = DOTween.To(() => ik.FollowWeight, weight => ik.FollowWeight = weight, follow ? 1f : 0f, _handReachDuration)
				.OnComplete(() =>
				{
					if (!follow) ik.Follow(null);
				});
		}

		private void EnterHold()
		{
			_holding = true;

			if (_hold == PokerItemPropHold.Duration)
			{
				_sequence = DOTween.Sequence().AppendInterval(_holdDuration).OnComplete(PlayOutro);
				return;
			}

			TryRelease();
		}

		private void TryRelease()
		{
			if (!_holding || !_resolved || _hold == PokerItemPropHold.Duration) return;
			if (_hold == PokerItemPropHold.UntilFoldLockLifted && IsFoldLocked()) return;

			PlayOutro();
		}

		private void PlayOutro()
		{
			_holding = false;
			_sequence?.Kill();
			_sequence = BuildSteps(_outro, out _).OnComplete(Stop);
		}

		// A fold lock laid on this player after the item was played, still covering this street or one to come.
		// The hand ending clears every rule, which lifts it too.
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

		private Vector3 TargetPosition(in PokerItemPerformanceStep step, Vector3 fallback)
		{
			var anchor = ResolveAnchor(step.To);
			return (anchor ? anchor.position : fallback) + Frame() * step.Offset;
		}

		private Quaternion TargetRotation(in PokerItemPerformanceStep step, Vector3 at)
		{
			var frame = Frame();

			if (step.AimAtOther)
			{
				var other = FocusOf(_other);
				var direction = other ? other.position - at : Vector3.zero;
				if (direction.sqrMagnitude > 0.0001f) frame = Quaternion.LookRotation(direction, Vector3.up);
			}

			return frame * Quaternion.Euler(step.Rotation);
		}

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
					return _player.Rig ? _player.Rig.GetBone(PlayerBone.HoldRight) : null;
				case PokerItemPropAnchor.Stash:
					return Seat() ? Seat().ItemStashAnchor : null;
				case PokerItemPropAnchor.Table:
					return Seat() ? Seat().CardAnchor : null;
				case PokerItemPropAnchor.Mouth:
					return FaceBone(PlayerBone.Jaw);
				case PokerItemPropAnchor.Nose:
					return FaceBone(PlayerBone.Nose);
				case PokerItemPropAnchor.Cards:
					return _player.HandVisual ? _player.HandVisual.HandAnchor : null;
				case PokerItemPropAnchor.Focus:
					return FocusOf(_player);
				case PokerItemPropAnchor.Other:
					return FocusOf(_other);
				case PokerItemPropAnchor.BoardCard:
					return LastFaceDownBoardCard();
				case PokerItemPropAnchor.Body:
					return _player.transform;
				default:
					return null;
			}
		}

		// The full body's face for everybody watching; for the performer themselves, the performer's own face
		// point, since their real face is the camera and a hand raised to it would cover the whole view.
		private Transform FaceBone(PlayerBone bone)
		{
			if (_player.IsOwner && _ownFacePoint) return _ownFacePoint;

			var rig = _player.Rig ? _player.Rig.FullBodyRig : null;
			if (!rig) return null;

			return rig.TryGet(bone, out var found) ? found : rig.Get(PlayerBone.Head);
		}

		// About you, before your own eyes; about anybody else, on their body.
		private static Transform FocusOf(PokerPlayer player)
		{
			if (!player || !player.Rig) return null;

			return player.IsOwner ? player.Rig.SelfFocusPoint : player.Rig.FocusPoint;
		}

		private Transform LastFaceDownBoardCard()
		{
			var board = PokerBoardVisual.Instance;
			var data = _gameMode ? _gameMode.Data : null;
			if (!board || !data) return null;

			var cards = board.Cards;
			for (var i = cards.Count - 1; i >= 0; i--)
			{
				if (cards[i] && !data.IsCommunityCardRevealed(i)) return cards[i].transform;
			}

			return null;
		}
	}
}
