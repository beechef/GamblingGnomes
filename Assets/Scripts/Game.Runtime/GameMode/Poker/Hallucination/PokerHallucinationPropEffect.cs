using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// Hides what the target names, grows something in its place, or both. That covers four of the deck's
	// categories at once: swapping the room, swapping an object on the table, adding an object to the room
	// and adding a limb to a body are all the same move, because a swap is only ever a hide plus an add.
	//
	// What it spawns is a plain prefab with no NetworkObject on it. The whole point is that it exists on
	// one screen, and a networked object would put it on everybody's.
	[CreateAssetMenu(fileName = "Hallucination_Prop", menuName = "Game/Poker/Hallucination/Prop")]
	public class PokerHallucinationPropEffect : PokerHallucinationEffect
	{
		public enum HideMode
		{
			Nothing,
			Renderers,
			GameObject
		}

		[Required]
		[SerializeField] private PokerHallucinationTarget _target;

		[Tooltip("Renderers leaves the colliders standing, which is what the room needs or the player falls through the floor. GameObject is for a piece of a body, where PlayerVisual owns renderer.enabled and would hand a hidden one straight back.")]
		[SerializeField] private HideMode _hide = HideMode.Renderers;

		[Tooltip("Grown under each thing the target names. Leave empty to only hide.")]
		[SerializeField] private GameObject _prefab;

		[SerializeField] private Vector3 _localPosition;

		[SerializeField] private Vector3 _localEuler;

		[SerializeField] private Vector3 _localScale = Vector3.one;

		public PokerHallucinationTarget Target => _target;
		public HideMode Hide => _hide;
		public GameObject Prefab => _prefab;
		public Vector3 LocalPosition => _localPosition;
		public Vector3 LocalEuler => _localEuler;
		public Vector3 LocalScale => _localScale;

		protected override PokerHallucinationEffectBehaviour Attach(GameObject host) => host.AddComponent<PokerHallucinationPropBehaviour>();
	}
}
