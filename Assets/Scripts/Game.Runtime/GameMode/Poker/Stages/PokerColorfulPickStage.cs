using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.BetItems;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	// Who is fed when the winner lets the clock run out.
	public enum PokerColorfulTimeoutTarget : byte
	{
		Self = 0,
		RandomOther = 1
	}

	// The hand's winner names one player to eat the Colorful cap. Public and compulsory: the whole value
	// of the moment is the table watching a choice being made, and letting the winner decline would mean
	// declining whenever they are ahead, which is always. Whether they may name themselves is the table's
	// call (_allowSelfTarget): where winning costs nothing, naming yourself is a choice nobody makes.
	[CreateAssetMenu(fileName = "PokerStage_ColorfulPick", menuName = "Game/Poker/Stages/Colorful Pick")]
	public class PokerColorfulPickStage : PokerStage
	{
		[Header("Cap")]
		[Tooltip("Which kind the table feeds here. Picked rather than typed: it used to be a one-based index into the database, which is a number nobody could check and every reorder could break.")]
		[SerializeField] private PokerBetItemType _colorfulBetItemType = PokerBetItemType.Colorful;

		[Header("Choice")]
		[Tooltip("On, the winner may name themselves. Off where winning costs nothing, since naming yourself would then be a choice that is never made.")]
		[SerializeField] private bool _allowSelfTarget = true;

		[Tooltip("Who is fed when the winner's clock runs out. Self falls back to a random player when the winner cannot be named.")]
		[SerializeField] private PokerColorfulTimeoutTarget _timeoutTarget = PokerColorfulTimeoutTarget.Self;

		[Header("Timing")]
		[Tooltip("Seconds the winner has to choose. Zero or less waits for them.")]
		[SerializeField] private float _turnDuration = -1f;

		[Tooltip("Seconds the result is left on screen before the next hand.")]
		[SerializeField] private float _resultDuration = 2.5f;

		[Header("Snap")]
		[Tooltip("Cue on the snap clip that puts the cap down. The chosen player is named by the snap, so the cap arriving before the fingers have closed reads as the choice having already happened.")]
		[SerializeField] private string _serveCue = "SnapServe";

		[Tooltip("The snap clip carrying that cue. The cap is put down anyway once the clip has run out plus the margin below. The cue rides the chooser's own rig, so a table that is not drawing them hears nothing — and a winner's choice must never be the thing that hangs the round.")]
		[Required]
		[SerializeField] private AnimationClip _serveClip;

		[Tooltip("Seconds past the end of the snap clip before the cue is given up on. The backstop has to outlast the clip, or it fires every time and the cue never decides anything.")]
		[MinValue(0f)]
		[SerializeField] private float _serveCueMargin = 0.3f;

		private float ServeCueTimeout => _serveClip ? _serveClip.length + _serveCueMargin : 0f;

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

		public PokerBetItemType ColorfulBetItemType => _colorfulBetItemType;

		protected override void OnStartStage()
		{
			_picked = false;
			_turnElapsed = 0f;

			ClearPendingServe();

			var winner = GameMode.FindSeatedPlayer(Data.LastWinnerClientId.Value);

			// Nobody won it, the winner is out of the running, or there is nobody left they may name. There
			// is no choice to put to anybody, so the stage is over rather than waiting on a turn nobody holds.
			if (!winner || !IsInTheRunning(winner) || !HasAnyTarget())
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
				if (_serveElapsed >= ServeCueTimeout) DeliverCap();
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

			// A turn on a clock must always end, and this one has no polite answer to fall back on — so the
			// clock picks, and never aims: silence feeds the winner themselves or somebody at random.
			var winner = GameMode.FindSeatedPlayer(Data.CurrentTurnClientId.Value);
			var target = _timeoutTarget == PokerColorfulTimeoutTarget.Self && winner && CanBeFed(winner) ? winner : RandomTarget();

			if (target) Serve(target);
			else FinishStage(_nextStage);
		}

		private bool HasAnyTarget()
		{
			foreach (var player in GameMode.SeatedPlayers)
			{
				if (CanBeFed(player)) return true;
			}

			return false;
		}

		private PokerPlayer RandomTarget()
		{
			var count = 0;
			foreach (var player in GameMode.SeatedPlayers)
			{
				if (CanBeFed(player)) count++;
			}

			if (count == 0) return null;

			var pick = Random.Range(0, count);
			foreach (var player in GameMode.SeatedPlayers)
			{
				if (!CanBeFed(player)) continue;
				if (pick-- == 0) return player;
			}

			return null;
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
		// The winner is excluded where they may not name themselves — they hold the turn, so every screen
		// can ask the same question.
		public bool CanBeFed(PokerPlayer player) =>
			IsInTheRunning(player) && (_allowSelfTarget || player.ClientId != Data.LastWinnerClientId.Value);

		private static bool IsInTheRunning(PokerPlayer player) =>
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

			// The winner gloats for as long as they are deciding, and stops the moment a name is settled —
			// the clock choosing counts too. Dropped before the snap, because the smile's layer sits above
			// the gestures and would hide it while it held.
			StopSmiling();

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
			if (string.IsNullOrEmpty(_serveCue) || ServeCueTimeout <= 0f || !chooser.Rig) return false;

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
		// bet path everything else on this table takes, so it arrives looking like every other cap.
		private void DeliverCap()
		{
			var target = _pendingTarget;

			ClearPendingServe();

			var database = GameMode.BetItemDatabase;

			if (target && database && database.TryGetEntry(ColorfulBetItemType, out _))
			{
				PokerTableUtility.PlaceBet(Data, target, ColorfulBetItemType);
			}
			else if (!database || !database.TryGetEntry(ColorfulBetItemType, out _))
			{
				// A cap the table does not carry is a setup that cannot work, and skipping it silently
				// would read as a winner whose choice simply does nothing.
				Debug.LogWarning($"[{StageId}] No entry for {ColorfulBetItemType} in the table's mushroom database.");
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
		// Also the smile: a winner nobody can feed ends this stage the moment it opens, without a choice ever
		// being made, and the eating that follows would play under a celebration still hiding every gesture.
		protected override void OnEndStage()
		{
			ClearPendingServe();
			StopSmiling();
		}

		private void StopSmiling()
		{
			foreach (var player in GameMode.SeatedPlayers)
			{
				if (player) player.WinnerPose?.ServerSetSmiling(false);
			}
		}

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
