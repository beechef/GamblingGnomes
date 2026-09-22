using System;
using System.Collections.Generic;
using DG.Tweening;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.GameMode.Poker.Stages;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// The shared cards in the middle of the table. Dealt off the deck after the hands and turned over as
	// the table reveals them; where they lie is the row's business, as it is for the cards in front of a
	// chair. A table that deals no board simply never gets a card here.
	//
	// How the board is turned is this screen's alone: it faces the local player's chair so the row reads
	// the right way up from where they sit, and it can be stood up on end to be read. Neither replicates —
	// everybody at the table sees the same cards, each from their own side.
	//
	// Registered rather than serialized, because the button that stands it up is on the HUD, which is
	// spawned by the mode and cannot hold a reference to the table.
	public class PokerBoardVisual : PokerVisual
	{
		[Required]
		[SerializeField] private PokerCardGroupVisual _row;

		[Required]
		[SerializeField] private PokerCardVisual _cardPrefab;

		[Required]
		[SerializeField] private PokerCardDatabase _database;

		[Tooltip("Seconds between cards turned over by one reveal, left to right, so a flop is three cards being turned rather than one picture changing.")]
		[Min(0f)]
		[SerializeField] private float _flipStagger = 0.15f;

		[Header("Standing")]
		[Tooltip("Degrees back from upright the board leans while stood up. Zero is dead upright; 90 is lying flat.")]
		[Range(0f, 90f)]
		[SerializeField] private float _standingTilt = 15f;

		[Tooltip("How far the board rises off the table while stood up, so the bottom edge clears the wood.")]
		[Min(0f)]
		[SerializeField] private float _standingLift = 0.1f;

		[Min(0f)]
		[SerializeField] private float _standDuration = 0.35f;

		[SerializeField] private Ease _standEase = Ease.OutBack;

		private const float LyingTilt = 90f;

		public static PokerBoardVisual Instance { get; private set; }
		public static event Action<PokerBoardVisual> OnInstanceChanged;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			Instance = null;
			OnInstanceChanged = null;
		}

		private readonly List<PokerCardVisual> _cards = new();
		private readonly List<Tween> _flips = new();
		private int _shownRevealed;

		private Vector3 _restPosition;
		private float _yaw;
		private float _standing;
		private Tween _standTween;
		private PokerPlayerData _localData;

		public bool IsStanding { get; private set; }
		public bool HasCards => _cards.Count > 0;

		public event Action OnStandingChanged;
		public event Action OnCardsChanged;

		private void Awake()
		{
			_restPosition = transform.localPosition;
			_yaw = transform.localEulerAngles.y;

			Instance = this;
			OnInstanceChanged?.Invoke(this);
		}

		private void OnDestroy()
		{
			_standTween?.Kill();

			if (Instance != this) return;

			Instance = null;
			OnInstanceChanged?.Invoke(null);
		}

		protected override void OnBind()
		{
			Data.OnCommunityCardsChanged += HandleCommunityCardsChanged;
			Data.OnCommunityRevealChanged += HandleRevealChanged;
			PokerPlayer.OnLocalPlayerChanged += HandleLocalPlayerChanged;

			BindLocalPlayer(PokerPlayer.Local);

			// Late join: the board as it lies, turned as far as it has been turned.
			RebuildAll();
		}

		protected override void OnUnbind()
		{
			BindLocalPlayer(null);

			PokerPlayer.OnLocalPlayerChanged -= HandleLocalPlayerChanged;
			Data.OnCommunityRevealChanged -= HandleRevealChanged;
			Data.OnCommunityCardsChanged -= HandleCommunityCardsChanged;

			ClearCards();
		}

		// Stood up to be read, or laid back down. Asked by the local player, and only ever of this screen.
		public void SetStanding(bool standing)
		{
			if (IsStanding == standing) return;

			IsStanding = standing;

			_standTween?.Kill();
			_standTween = DOTween.To(() => _standing, value => { _standing = value; ApplyPose(); }, standing ? 1f : 0f, _standDuration)
				.SetEase(_standEase)
				.SetLink(gameObject);

			OnStandingChanged?.Invoke();
		}

		private void HandleLocalPlayerChanged(PokerPlayer player) => BindLocalPlayer(player);

		private void BindLocalPlayer(PokerPlayer player)
		{
			var data = player ? player.Data : null;
			if (data == _localData) return;

			if (_localData) _localData.SeatIndex.OnValueChanged -= HandleLocalSeatChanged;

			_localData = data;

			if (_localData) _localData.SeatIndex.OnValueChanged += HandleLocalSeatChanged;

			FaceLocalSeat();
		}

		private void HandleLocalSeatChanged(int previous, int current) => FaceLocalSeat();

		// Turned the way the local chair faces, so the row runs across in front of them and the top of every
		// card points away. Read off the chair itself, which the ring has already squared to the table; a
		// player with no chair leaves the board as it was authored.
		private void FaceLocalSeat()
		{
			var seat = LocalSeat();
			if (seat)
			{
				var parent = transform.parent;
				var relative = parent ? Quaternion.Inverse(parent.rotation) * seat.transform.rotation : seat.transform.rotation;
				_yaw = relative.eulerAngles.y;
			}

			ApplyPose();
		}

		private PokerSeat LocalSeat()
		{
			if (!_localData || !GameMode) return null;

			foreach (var seat in GameMode.Seats)
			{
				if (seat && seat.SeatIndex == _localData.SeatIndex.Value) return seat;
			}

			return null;
		}

		private void ApplyPose()
		{
			var tilt = Mathf.LerpUnclamped(LyingTilt, _standingTilt, _standing);

			transform.localPosition = _restPosition + Vector3.up * (_standingLift * _standing);
			transform.localRotation = Quaternion.Euler(0f, _yaw, 0f) * Quaternion.Euler(tilt, 0f, 0f);
		}

		private void HandleCommunityCardsChanged(NetworkListEvent<CardData> change)
		{
			switch (change.Type)
			{
				case NetworkListEvent<CardData>.EventType.Add:
					// The first card of a hand is where the board is squared up again: the chairs have long been
					// laid by then, whichever order this client heard about them in.
					if (_cards.Count == 0) FaceLocalSeat();
					AddCard(true);
					break;

				case NetworkListEvent<CardData>.EventType.Clear:
					ClearCards();
					break;

				default:
					RebuildAll();
					break;
			}

			OnCardsChanged?.Invoke();
		}

		// Only the cards the count has newly passed are turned, one after another; a count going back down
		// is a new hand being laid, and those cards are simply put face down.
		private void HandleRevealChanged()
		{
			var revealed = Mathf.Min(Data.RevealedCommunityCards.Value, _cards.Count);

			if (revealed < _shownRevealed)
			{
				KillFlips();
				for (var i = revealed; i < _cards.Count; i++) DrawCard(i, false);
			}

			var order = 0;
			for (var i = _shownRevealed; i < revealed; i++)
			{
				var index = i;
				_flips.Add(DOVirtual.DelayedCall(order++ * _flipStagger, () => DrawCard(index, true), false)
					.SetLink(gameObject));
			}

			_shownRevealed = revealed;
		}

		private void AddCard(bool animate)
		{
			if (!_cardPrefab || !_row) return;

			var index = _cards.Count;
			var visual = Instantiate(_cardPrefab, _row.Anchor);
			_cards.Add(visual);

			var deck = PokerDeckVisual.Instance;
			if (animate && deck) deck.DealBoard(visual, index, DealtHoleCards());

			var visible = Data.IsCommunityCardVisible(index);
			visual.SetCard(visible ? CardAt(index) : CardData.None, visible, _database);

			if (visible) _shownRevealed = Mathf.Max(_shownRevealed, index + 1);

			_row.Add(visual, index, animate);
		}

		private void DrawCard(int index, bool animate)
		{
			if (index < 0 || index >= _cards.Count || !_cards[index]) return;

			var visible = Data.IsCommunityCardVisible(index);
			_cards[index].SetCard(visible ? CardAt(index) : CardData.None, visible, _database, animate && visible);
		}

		private CardData CardAt(int index) =>
			index >= 0 && index < Data.CommunityCards.Count ? Data.CommunityCards[index] : CardData.None;

		// Asked of the deal running now, since the board goes out after the hands and the hands may not have
		// reached this client yet.
		private int DealtHoleCards()
		{
			var stage = GameMode.FindStage(Data.StageId.Value.ToString()) as PokerDealStage;
			return stage ? stage.HoleCardsPerPlayer : 0;
		}

		private void RebuildAll()
		{
			ClearCards();

			FaceLocalSeat();
			for (var i = 0; i < Data.CommunityCards.Count; i++) AddCard(false);

			_shownRevealed = Mathf.Min(Data.RevealedCommunityCards.Value, _cards.Count);
			OnCardsChanged?.Invoke();
		}

		// A board that is gone is laid back down, so the next one is dealt onto the table rather than into the
		// air.
		private void ClearCards()
		{
			KillFlips();

			if (_row) _row.Clear();

			foreach (var card in _cards)
			{
				if (card) Destroy(card.gameObject);
			}

			_cards.Clear();
			_shownRevealed = 0;

			SetStanding(false);
		}

		private void KillFlips()
		{
			foreach (var flip in _flips) flip?.Kill();
			_flips.Clear();
		}
	}
}
