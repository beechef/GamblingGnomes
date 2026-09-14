using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// Changes how the bodies the target names are drawn, through each body's PlayerAppearanceController.
	// A subclass says which axis — the model, the version — and never which meshes or materials: the
	// model being worn resolves those, so a character added later answers every effect already written.
	public abstract class PokerHallucinationAppearanceEffect : PokerHallucinationEffect
	{
		[Required]
		[InfoBox("Aim this at a player target. Anything else names no body, and nothing changes.", InfoMessageType.Warning, nameof(TargetNamesNoBodies))]
		[SerializeField] private PokerHallucinationTarget _target;

		public PokerHallucinationTarget Target => _target;

		private bool TargetNamesNoBodies => _target && !_target.ResolvesBodies;
	}
}
