using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Items;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	// Somebody went all in, and everyone else still in the hand answers at once against one clock: match it
	// with a cap of the same kind, or fold. Nobody holds a turn, and the answers are sealed until the last one
	// is in, so nobody can wait to see what the others did. Then the rest of the board is turned over.
	//
	// Sits in the sequence after the last street and passes straight through when nobody went all in, which
	// is read off the pot rather than handed over by the street: the all-in cap on the table is the fact.
	[CreateAssetMenu(fileName = "PokerStage_AllIn", menuName = "Game/Poker/Stages/All In")]
	public class PokerAllInStage : PokerStage
	{
		[Header("Stake")]
		[Tooltip("What going all in puts up. Whoever has one in the pot went all in; everyone else is asked to match it with one.")]
		[SerializeField] private PokerItemType _allInItemType = PokerItemType.Colorful;

		[Header("Timing")]
		[Tooltip("Seconds everybody has to answer. Anyone still silent when it runs out folds.")]
		[MinValue(1f)]
		[SerializeField] private float _duration = 10f;

		[Tooltip("Seconds the table looks at the answers and the board turned over before the hands are shown.")]
		[MinValue(0f)]
		[SerializeField] private float _resultHold = 1.5f;

		[Header("References")]
		[Tooltip("Where the hand goes when every player asked folded, leaving the all-in player alone in it.")]
		[SerializeField] private PokerStage _handOverStage;

		private readonly Dictionary<ulong, bool> _answers = new();
		private readonly List<PokerPlayer> _asked = new();

		private bool _asking;
		private bool _holding;

		public PokerItemType AllInItemType => _allInItemType;

		// Sealed: nobody is told who answered what until every answer lands together.
		public override bool AnnouncesActions => false;

		protected override void OnStartStage()
		{
			_answers.Clear();
			_asked.Clear();
			_asking = false;
			_holding = false;

			if (!PokerTableUtility.HasAnyStakedKind(Data, _allInItemType))
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
		}

		// Still in the hand and not the one who went all in. Asked by the bar too, so the two agree on who is
		// being offered the choice.
		public bool IsAsked(PokerPlayer player) =>
			player && PokerItemWagerStage.CanWager(player.Data) && !PokerTableUtility.HasStakedKind(Data, player.ClientId, _allInItemType);

		protected override void OnTickStage(float deltaTime)
		{
			if (_holding)
			{
				if (GameMode.IsStageTimerExpired()) Finish();
				return;
			}

			if (_asking && GameMode.IsStageTimerExpired()) Settle();
		}

		public override bool HandleAction(ulong clientId, PokerActionType action, int amount)
		{
			if (!_asking) return false;
			if (action != PokerActionType.AllIn && action != PokerActionType.Fold) return false;
			if (_answers.ContainsKey(clientId)) return false;

			var player = GameMode.FindSeatedPlayer(clientId);
			if (!player || !_asked.Contains(player)) return false;

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

		// Every answer lands at once: the caps go up and the folds go down. Not announced one by one — the
		// notice is one replicated value, and several writes in a frame reach a client as the last of them.
		private void Settle()
		{
			_asking = false;
			GameMode.ClearStageTimer();

			var database = GameMode.ItemDatabase;
			var hasCap = database && database.TryGetEntry(_allInItemType, out _);

			foreach (var player in _asked)
			{
				if (!player || !player.Data) continue;

				var allIn = _answers.TryGetValue(player.ClientId, out var answer) && answer && hasCap;

				if (allIn)
				{
					PokerTableUtility.WagerItem(Data, player, _allInItemType);
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
