using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Items;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player;
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
		[Tooltip("Which kind the table feeds here. Picked rather than typed: it used to be a one-based index into the database, which is a number nobody could check and every reorder could break.")]
		[SerializeField] private PokerItemType _colorfulItemType = PokerItemType.Colorful;

		[Header("Timing")]
		[Tooltip("Seconds the winner has to choose. Zero or less waits for them.")]
		[SerializeField] private float _turnDuration = -1f;

		[Tooltip("Seconds the result is left on screen before the next hand.")]
		[SerializeField] private float _resultDuration = 2.5f;

		[Header("Snap")]
		[Tooltip("Cue on the snap clip that puts the cap down. The chosen player is named by the snap, so the cap arriving before the fingers have closed reads as the choice having already happened.")]
		[SerializeField] private string _serveCue = "SnapServe";

		[Tooltip("Seconds to wait for that cue before the cap is put down anyway. The cue rides the chooser's own rig, so a table that is not drawing them hears nothing — and a winner's choice must never be the thing that hangs the round.")]
		[MinValue(0f)]
		[SerializeField] private float _serveCueTimeout = 1.5f;

		[Header("References")]
		[Tooltip("Where the next hand begins. Named rather than left to the sequence, which wraps to its first entry — and that is the waiting room, so a table would need the host to press start after every hand.")]
		[Required]
		[SerializeField] private PokerStage _nextStage;

		private bool _picked;
		private float _turnElapsed;

		// The choice has been made and announced, and the cap is waiting on the snap that names it.
		private readonly List<PlayerAnimationEventRelay> _serveRelays = new();
		private PokerPlayer _pendingTarget;
		private bool _awaitingServeCue;
		private float _serveElapsed;

		public PokerItemType ColorfulItemType => _colorfulItemType;

		protected override void OnStartStage()
		{
			_picked = false;
			_turnElapsed = 0f;

			ClearPendingServe();

			// The celebration ends here: from this beat the winner is choosing who eats, which is a decision
			// rather than a victory lap. Dropped on everybody, because the only thing that raised it was the
			// showdown and nothing else is watching the flag.
			foreach (var player in GameMode.SeatedPlayers)
			{
				if (player) player.WinnerPose?.ServerSetSmiling(false);
			}

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
			// The snap is running and the cap is owed. Nothing else about this stage moves until it lands,
			// so the wait sits ahead of everything.
			if (_awaitingServeCue)
			{
				_serveElapsed += deltaTime;
				if (_serveElapsed >= _serveCueTimeout) DeliverCap();
				return;
			}

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

			Serve(target, GameMode.FindSeatedPlayer(clientId));
			return true;
		}

		// In this match and still conscious, the winner included. Somebody already under has nothing left to
		// lose and feeding them would be a move with no consequence at all. An empty purse is no protection
		// from a mushroom, so this asks InMatch and IsAlive rather than IsPlayingThisMatch.
		public bool CanBeFed(PokerPlayer player) =>
			player && player.Data && player.Data.IsSeated && player.Data.InMatch.Value && player.Data.IsAlive;

		private PokerPlayer FindSeatedPlayerAtSeat(int seatIndex)
		{
			foreach (var player in GameMode.SeatedPlayers)
			{
				if (player && player.Data && player.Data.SeatIndex.Value == seatIndex) return player;
			}

			return null;
		}

		// The chooser is null when the clock chose: a silence is not a choice, so it gets no gesture.
		private void Serve(PokerPlayer target, PokerPlayer chooser = null)
		{
			_picked = true;
			GameMode.ClearTurn();

			// Chosen first, announced after: the winner points at people freely while deciding, and only
			// the settled choice is acted out — a snap at whoever was named, with every head at the table
			// turning to them. After ClearTurn, which drops the focus, and before the stage can finish,
			// because the machine clears the focus at the handover and a focus written later would stick.
			_pendingTarget = target;

			if (chooser)
			{
				chooser.ActionAnimator?.ServerPlay(PlayerActionIds.SnapFingers);
				GameMode.ServerSetFocus(target.ClientId);

				// The cap waits for the snap that names them. The clip says when that is, rather than a
				// number counted here — but the cue rides whichever rig this machine draws, so the timeout
				// beside it is what stops a table that is drawing nobody from waiting forever.
				if (BindServeCue(chooser)) return;
			}

			DeliverCap();
		}

		private bool BindServeCue(PokerPlayer chooser)
		{
			if (string.IsNullOrEmpty(_serveCue) || _serveCueTimeout <= 0f || !chooser.Rig) return false;

			chooser.Rig.GetComponentsInChildren(true, _serveRelays);
			if (_serveRelays.Count == 0) return false;

			foreach (var relay in _serveRelays) relay.OnAnimationCue += HandleServeCue;

			_awaitingServeCue = true;
			_serveElapsed = 0f;

			return true;
		}

		private void HandleServeCue(string cue)
		{
			if (!_awaitingServeCue || cue != _serveCue) return;

			DeliverCap();
		}

		// Put down in front of them, not swallowed here: the eating is its own beat, and a cap that took
		// effect during the announcement of who got it is a cap nobody watched go down. Through the same
		// wager path everything else on this table takes, so it arrives looking like every other cap.
		private void DeliverCap()
		{
			var target = _pendingTarget;

			ClearPendingServe();

			var database = GameMode.ItemDatabase;

			if (target && database && database.TryGetEntry(ColorfulItemType, out _))
			{
				PokerTableUtility.WagerItem(Data, target, ColorfulItemType);
			}
			else if (!database || !database.TryGetEntry(ColorfulItemType, out _))
			{
				// A cap the table does not carry is a setup that cannot work, and skipping it silently
				// would read as a winner whose choice simply does nothing.
				Debug.LogWarning($"[{StageId}] No entry for {ColorfulItemType} in the table's mushroom database.");
			}

			if (_resultDuration <= 0f)
			{
				FinishStage(_nextStage);
				return;
			}

			GameMode.BeginStageTimer(_resultDuration);
		}

		// A stage outlives the bodies it subscribed to, so the handlers come off on every way out of the
		// wait — the cue landing, the timeout, and the stage ending under either.
		protected override void OnEndStage() => ClearPendingServe();

		private void ClearPendingServe()
		{
			foreach (var relay in _serveRelays)
			{
				if (relay) relay.OnAnimationCue -= HandleServeCue;
			}

			_serveRelays.Clear();

			_pendingTarget = null;
			_awaitingServeCue = false;
			_serveElapsed = 0f;
		}
	}
}
