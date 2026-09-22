using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// The viewer's own eyes: the first-person view swings wide (or narrow) and the room stretches around the
	// table. Written through PlayerCameraFovController, which owns the lens and adds this to anything else
	// already pulling on it.
	[CreateAssetMenu(fileName = "Hallucination_Fov", menuName = "Game/Poker/Hallucination/Field Of View")]
	public class PokerHallucinationFovEffect : PokerHallucinationEffect
	{
		[Tooltip("Vertical field of view the first-person camera eases to, in degrees.")]
		[PropertyRange(1f, 179f)]
		[SerializeField] private float _fieldOfView = 100f;

		[SerializeField] private Ease _ease = Ease.InOutSine;

		public float FieldOfView => _fieldOfView;
		public Ease Ease => _ease;

		protected override PokerHallucinationEffectBehaviour Attach(GameObject host) => host.AddComponent<PokerHallucinationFovBehaviour>();
	}
}
