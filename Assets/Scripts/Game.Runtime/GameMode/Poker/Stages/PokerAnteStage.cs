using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.BetItems;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	// The opening caps as a beat of their own, before the deal: each player still in the match puts theirs up
	// in seat order, with the bet gesture, a pause apart, so the table sees the first mushroom land. Written in
	// the Dealing phase, which is what the settlement reads as an opening cap.
	[CreateAssetMenu(fileName = "PokerStage_Ante", menuName = "Game/Poker/Stages/Ante")]
	public class PokerAnteStage : PokerStage
	{
		[Tooltip("Caps each player puts up, each of a kind drawn at random.")]
		[MinValue(1)]
		[SerializeField] private int _anteSize = 1;

		[Tooltip("Seconds between one player's cap and the next.")]
		[MinValue(0f)]
		[SerializeField] private float _stagger = 0.6f;

		[Tooltip("Seconds the table holds after the last cap lands, before the deal.")]
		[MinValue(0f)]
		[SerializeField] private float _hold = 1f;

		private readonly List<PokerPlayer> _queue = new();
		private float _timer;

		protected override void OnStartStage()
		{
			Data.Phase.Value = PokerPhase.Dealing;
			GameMode.ClearTurn();

			_queue.Clear();
			foreach (var player in GameMode.SeatedPlayers)
			{
				if (player && GameMode.IsPlayingThisMatch(player.Data)) _queue.Add(player);
			}

			_queue.Sort((a, b) => a.Data.SeatIndex.Value.CompareTo(b.Data.SeatIndex.Value));
			_timer = 0f;
		}

		protected override void OnTickStage(float deltaTime)
		{
			_timer -= deltaTime;
			if (_timer > 0f) return;

			if (_queue.Count == 0)
			{
				FinishStage();
				return;
			}

			PostAnte(_queue[0]);
			_queue.RemoveAt(0);
			_timer = _queue.Count > 0 ? _stagger : _hold;
		}

		private void PostAnte(PokerPlayer player)
		{
			if (!player || !player.Data) return;

			var database = GameMode.BetItemDatabase;
			for (var i = 0; i < _anteSize; i++)
			{
				PokerTableUtility.PlaceBet(Data, player, database ? database.DrawBetItemType() : PokerBetItemDatabase.PlainChip);
			}

			player.ActionAnimator?.ServerPlay(PlayerActionIds.Bet);
		}
	}
}
