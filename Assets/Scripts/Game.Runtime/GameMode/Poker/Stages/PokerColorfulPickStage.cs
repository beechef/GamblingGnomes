using Game.Runtime.GameMode.Poker.Items;
using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	// The hand's winner names one player to eat the Colorful cap. Public and compulsory: the whole value
	// of the moment is the table watching a choice being made, and letting the winner decline would mean
	// declining whenever they are ahead, which is always. They may name themselves — being fed a cap is
	// not only a punishment, and a reward for taking one is a card this leaves room for.
	[CreateAssetMenu(fileName = "PokerStage_ColorfulPick", menuName = "Game/Poker/Stages/Colorful Pick")]
	public class PokerColorfulPickStage : PokerStage
	{
		[Header("Cap")]
		[Tooltip("Which kind in the table's database is the Colorful cap. One-based, the way every stake index is.")]
		[MinValue(1)]
		[SerializeField] private int _colorfulItemType = 5;

		[Header("Timing")]
		[Tooltip("Seconds the winner has to choose. Zero or less waits for them.")]
		[SerializeField] private float _turnDuration = -1f;

		[Tooltip("Seconds the result is left on screen before the next hand.")]
		[SerializeField] private float _resultDuration = 2.5f;

		[Header("References")]
		[Tooltip("Where the next hand begins. Named rather than left to the sequence, which wraps to its first entry — and that is the waiting room, so a table would need the host to press start after every hand.")]
		[Required]
		[SerializeField] private PokerStage _nextStage;

		private bool _picked;
		private float _turnElapsed;

		public byte ColorfulItemType => (byte)Mathf.Clamp(_colorfulItemType, 1, byte.MaxValue);

		protected override void OnStartStage()
		{
			_picked = false;
			_turnElapsed = 0f;

			var winner = GameMode.FindSeatedPlayer(Data.LastWinnerClientId.Value);

			// Nobody won it — everyone folded out, or the hand never happened. There is no choice to put
			// to anybody, so the stage is over rather than waiting on a turn nobody holds.
			if (!winner || !CanBeFed(winner))
			{
				FinishStage(_nextStage);
				return;
			}

			GameMode.BeginTurn(winner.ClientId, _turnDuration);
		}

		protected override void OnTickStage(float deltaTime)
		{
			if (_picked)
			{
				if (GameMode.IsStageTimerExpired()) FinishStage(_nextStage);
				return;
			}

			if (_turnDuration <= 0f) return;

			_turnElapsed += deltaTime;
			if (_turnElapsed < _turnDuration) return;

			// A turn on a clock must always end, and this one has no polite answer to fall back on — so
			// the winner who says nothing feeds it to themselves. Silence should not let them aim it.
			var winner = GameMode.FindSeatedPlayer(Data.CurrentTurnClientId.Value);
			if (winner) Serve(winner);
			else FinishStage(_nextStage);
		}

		public override bool HandleAction(ulong clientId, PokerActionType action, int amount)
		{
			if (_picked) return false;
			if (action != PokerActionType.Target) return false;
			if (clientId != Data.CurrentTurnClientId.Value) return false;

			var target = FindSeatedPlayerAtSeat(amount);
			if (!target || !CanBeFed(target)) return false;

			Serve(target);
			return true;
		}

		// Anybody still conscious and at the table, the winner included. Somebody already under has
		// nothing left to lose and feeding them would be a move with no consequence at all.
		// In this match and still conscious. An empty purse is no protection from a mushroom, so this asks
		// InMatch and IsAlive rather than IsPlayingThisMatch, which adds the money the deal cares about.
		private bool CanBeFed(PokerPlayer player) =>
			player && player.Data && player.Data.IsSeated && player.Data.InMatch.Value && player.Data.IsAlive;

		private PokerPlayer FindSeatedPlayerAtSeat(int seatIndex)
		{
			foreach (var player in GameMode.SeatedPlayers)
			{
				if (player && player.Data && player.Data.SeatIndex.Value == seatIndex) return player;
			}

			return null;
		}

		private void Serve(PokerPlayer target)
		{
			_picked = true;
			GameMode.ClearTurn();

			// Put down in front of them, not swallowed here: the eating is its own beat, and a cap that took
			// effect during the announcement of who got it is a cap nobody watched go down. Through the same
			// wager path everything else on this table takes, so it arrives looking like every other cap.
			var database = GameMode.ItemDatabase;
			if (database && database.TryGetEntry(ColorfulItemType, out _))
			{
				PokerTableUtility.WagerItem(Data, target, ColorfulItemType);
			}
			else
			{
				// A cap the table does not carry is a setup that cannot work, and skipping it silently
				// would read as a winner whose choice simply does nothing.
				Debug.LogWarning($"[{StageId}] No Colorful cap at index {ColorfulItemType} in the table's mushroom database.");
			}

			if (_resultDuration <= 0f)
			{
				FinishStage(_nextStage);
				return;
			}

			GameMode.BeginStageTimer(_resultDuration);
		}
	}
}
