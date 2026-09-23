using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.BetItems;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	// Somebody went all in, and everyone else still in the hand answers at once against one clock: match it
	// (as many caps as the all-in player, their all-in cap among them), or fold. Nobody holds a turn, and the answers are sealed until the last one
	// is in, so nobody can wait to see what the others did. Then the rest of the board is turned over.
	//
	// Sits in the sequence after the last street and passes straight through when nobody went all in, which
	// is read off the pot rather than handed over by the street: the all-in cap on the table is the fact.
	[CreateAssetMenu(fileName = "PokerStage_AllIn", menuName = "Game/Poker/Stages/All In")]
	public class PokerAllInStage : PokerStage
	{
		[Header("Stake")]
		[Tooltip("What going all in puts up. Whoever has one in the pot went all in; everyone else is asked to match it with one.")]
		[SerializeField] private PokerBetItemType _allInBetItemType = PokerBetItemType.Colorful;

		[Header("Timing")]
		[Tooltip("Seconds everybody has to answer. Anyone still silent when it runs out folds.")]
		[MinValue(1f)]
		[SerializeField] private float _duration = 10f;

		[Tooltip("Seconds the table looks at the answers and the board turned over before the hands are shown.")]
		[MinValue(0f)]
		[SerializeField] private float _resultHold = 1.5f;

		[Tooltip("Seconds the table looks at each player it is waiting on before turning to the next, so the room is watched making up its mind. Zero keeps the view on the first.")]
		[MinValue(0f)]
		[SerializeField] private float _focusDwell = 2f;

		[Header("References")]
		[Tooltip("Where the hand goes when every player asked folded, leaving the all-in player alone in it.")]
		[SerializeField] private PokerStage _handOverStage;

		private readonly Dictionary<ulong, bool> _answers = new();
		private readonly List<PokerPlayer> _asked = new();

		private bool _asking;
		private bool _holding;
		private float _focusElapsed;
		private int _focusIndex;

		public PokerBetItemType AllInBetItemType => _allInBetItemType;

		// Sealed: nobody is told who answered what until every answer lands together.
		public override bool AnnouncesActions => false;

		protected override void OnStartStage()
		{
			_answers.Clear();
			_asked.Clear();
			_asking = false;
			_holding = false;

			if (!PokerTableUtility.HasAnyStakedKind(Data, _allInBetItemType))
			{
				FinishStage();
				return;
			}

			Data.Phase.Value = PokerPhase.AllIn;
			GameMode.ClearTurn();

			foreach (var player in GameMode.SeatedPlayers)
			{
				if (IsAsked(player)) _asked.Add(player);
			}

			if (_asked.Count == 0)
			{
				Settle();
				return;
			}

			_asking = true;
			GameMode.BeginStageTimer(_duration);

			_focusIndex = -1;
			FocusNext();
		}

		// Still in the hand and not the one who went all in. Asked by the bar too, so the two agree on who is
		// being offered the choice.
		public bool IsAsked(PokerPlayer player) =>
			player && PokerStreetStage.CanBet(player.Data) && !PokerTableUtility.HasStakedKind(Data, player.ClientId, _allInBetItemType);

		// Whether folding is an answer here at all. A module can take it away, and then going all in is the only one.
		public bool CanFold(PokerPlayerData player) => GameMode && GameMode.IsActionAllowed(player, PokerActionType.Fold);

		protected override void OnTickStage(float deltaTime)
		{
			if (_holding)
			{
				if (GameMode.IsStageTimerExpired()) Finish();
				return;
			}

			if (!_asking) return;

			if (GameMode.IsStageTimerExpired())
			{
				Settle();
				return;
			}

			_focusElapsed += deltaTime;
			if (_focusDwell > 0f && _focusElapsed >= _focusDwell) FocusNext();
		}

		// Round the players still being asked, in seat order. The camera follows the focus, so the stage says
		// who the room is looking at and nothing about how it looks.
		private void FocusNext()
		{
			_focusElapsed = 0f;

			for (var step = 0; step < _asked.Count; step++)
			{
				_focusIndex = (_focusIndex + 1) % _asked.Count;

				var player = _asked[_focusIndex];
				if (!player) continue;

				GameMode.ServerSetFocus(player.ClientId);
				return;
			}
		}

		public override bool HandleAction(ulong clientId, PokerActionType action, int amount)
		{
			if (!_asking) return false;
			if (action != PokerActionType.AllIn && action != PokerActionType.Fold) return false;
			if (_answers.ContainsKey(clientId)) return false;

			var player = GameMode.FindSeatedPlayer(clientId);
			if (!player || !_asked.Contains(player)) return false;
			if (action == PokerActionType.Fold && !CanFold(player.Data)) return false;

			_answers[clientId] = action == PokerActionType.AllIn;

			if (AllAnswered()) Settle();
			return true;
		}

		public override void HandlePlayerLeft(ulong clientId, int seatIndex)
		{
			if (!_asking) return;

			_asked.RemoveAll(player => !player || player.ClientId == clientId);
			if (AllAnswered()) Settle();
		}

		private bool AllAnswered()
		{
			foreach (var player in _asked)
			{
				if (player && !_answers.ContainsKey(player.ClientId)) return false;
			}

			return true;
		}

		// Matching is having as many caps in the pot as whoever went all in, their all-in cap as the last of
		// them: whatever this street's bet was never paid is made up with drawn kinds first, so no pile ends
		// taller or shorter than another however the hand got here.
		private void MatchAllIn(PokerPlayer player, PokerBetItemDatabase database)
		{
			var target = 0;
			foreach (var other in GameMode.SeatedPlayers)
			{
				if (other && PokerTableUtility.HasStakedKind(Data, other.ClientId, _allInBetItemType))
					target = Mathf.Max(target, PokerTableUtility.CountPotEntries(Data, other.ClientId));
			}

			var owed = target - PokerTableUtility.CountPotEntries(Data, player.ClientId) - 1;
			for (var i = 0; i < owed; i++)
			{
				PokerTableUtility.PlaceBet(Data, player, database.DrawBetItemType());
			}

			PokerTableUtility.PlaceBet(Data, player, _allInBetItemType);
		}

		// Every answer lands at once: the caps go up and the folds go down, and they are sealed, so none is
		// announced. Somebody who never answered folds, unless folding was taken away, in which case they go in.
		private void Settle()
		{
			_asking = false;
			GameMode.ClearStageTimer();
			GameMode.ServerSetFocus(PokerGameData.NoTurn);

			var database = GameMode.BetItemDatabase;
			var hasCap = database && database.TryGetEntry(_allInBetItemType, out _);

			foreach (var player in _asked)
			{
				if (!player || !player.Data) continue;

				// Silence folds where folding is allowed and goes all in where it is not.
				var answered = _answers.TryGetValue(player.ClientId, out var answer);
				var wantsAllIn = answered ? answer : !CanFold(player.Data);
				var allIn = wantsAllIn && hasCap;

				if (wantsAllIn && !hasCap)
					Debug.LogWarning($"[{StageId}] {player.name} had to fold: no {_allInBetItemType} entry in the table's mushroom database to go all in with.");

				if (allIn)
				{
					MatchAllIn(player, database);
					player.ActionAnimator?.ServerPlay(PlayerActionIds.Bet);
				}
				else
				{
					player.ServerFold();
				}
			}

			GameMode.ServerRevealAllCommunityCards();

			_holding = true;
			if (_resultHold > 0f) GameMode.BeginStageTimer(_resultHold);
			else Finish();
		}

		private void Finish()
		{
			_holding = false;

			var stillIn = PokerTableUtility.CountInHand(GameMode.SeatedPlayers);
			FinishStage(stillIn <= 1 ? _handOverStage : null);
		}
	}
}
