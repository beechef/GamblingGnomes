using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	// The round loop as a preset. Hold several of these — a full Texas hold'em sequence, a one street
	// party variant — and the table plays whichever one is plugged in.
	[CreateAssetMenu(fileName = "PokerStageSequence", menuName = "Game/Poker/Stage Sequence")]
	public class PokerStageSequence : ScriptableObject
	{
		[Header("Sequence")]
		[Tooltip("Stages in the order they run. The first one is where the table sits between hands.")]
		[SerializeField] private List<PokerStage> _stages = new();

		public IReadOnlyList<PokerStage> Stages => _stages;

		public void CollectStages(List<PokerStage> target)
		{
			foreach (var stage in _stages)
			{
				if (stage) target.Add(stage);
			}
		}
	}
}
