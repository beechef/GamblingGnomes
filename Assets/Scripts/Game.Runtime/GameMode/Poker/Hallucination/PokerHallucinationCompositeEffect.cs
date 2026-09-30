using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// Several effects that make one look together (an after image with motion blur) and are drawn as one:
	// running it runs every member, and taking it off takes them all off. The opposite of a group, which
	// runs exactly one. A composite may hold a group or another composite.
	[CreateAssetMenu(fileName = "Hallucination_Composite", menuName = "Game/Poker/Hallucination/Composite")]
	public class PokerHallucinationCompositeEffect : PokerHallucinationEffect
	{
		[Tooltip("Effects this one runs, all at once, for as long as it runs.")]
		[SerializeField] private List<PokerHallucinationEffect> _members = new();

		public IReadOnlyList<PokerHallucinationEffect> Members => _members;

		protected override PokerHallucinationEffectBehaviour Attach(GameObject host) => host.AddComponent<PokerHallucinationCompositeBehaviour>();

		private void OnValidate()
		{
			// A composite running itself would recurse until the stack gave out.
			_members.RemoveAll(member => member == this);
		}
	}
}
