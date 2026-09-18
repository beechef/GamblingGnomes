using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// A slot in a rung's pool that stands for several effects which must never run together. Drawing the
	// group rolls again inside it and runs exactly one member, so effects that fight over the same thing
	// (Small Head and Big Head on one bone) are put in one group and can no longer be drawn side by side.
	// A group may hold another group; the roll simply goes one level deeper.
	[CreateAssetMenu(fileName = "Hallucination_Group", menuName = "Game/Poker/Hallucination/Group")]
	public class PokerHallucinationGroupEffect : PokerHallucinationEffect
	{
		[Tooltip("Effects this group stands for. One of them is picked at random each time the group is drawn; the others never run beside it.")]
		[SerializeField] private List<PokerHallucinationEffect> _members = new();

		public IReadOnlyList<PokerHallucinationEffect> Members => _members;

		protected override PokerHallucinationEffectBehaviour Attach(GameObject host) => host.AddComponent<PokerHallucinationGroupBehaviour>();

		private void OnValidate()
		{
			// A group drawing itself would recurse until the stack gave out.
			_members.RemoveAll(member => member == this);
		}
	}
}
