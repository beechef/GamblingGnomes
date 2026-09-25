using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.VFX;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// Every body the target names leaves afterimages while it moves: a copy of its mesh in the pose it held,
	// left where it stood, sliding off and fading. One VisualEffect per body, handed each snapshot.
	[CreateAssetMenu(fileName = "Hallucination_Ghost", menuName = "Game/Poker/Hallucination/Ghost")]
	public class PokerHallucinationGhostEffect : PokerHallucinationEffect
	{
		// The graph draws each echo as one of this many meshes, so this many can be on screen at once.
		public const int SnapshotCount = 4;

		// Current leaves the pose the body has once it has moved; Previous leaves the pose it just moved out of,
		// so the image stays behind where the body was.
		public enum EchoPose
		{
			Current,
			Previous,
		}

		[Tooltip("Whose bodies. A player target whose bone is the root, so the whole rig echoes.")]
		[Required]
		[SerializeField] private PokerHallucinationTarget _target;

		[Tooltip("The graph. Its 'Echo' event spawns one particle at the event's position drawn as Mesh0..Mesh3 (by meshIndex), moving at Drift (Vector3) for Lifetime (float), coloured Tint (Color), born at Alpha (float) times AlphaOverLife (AnimationCurve) over its life.")]
		[Required]
		[SerializeField] private VisualEffectAsset _visualEffect;

		[Tooltip("Which pose an afterimage shows: the one the body has moved into, or the one it has just left.")]
		[SerializeField] private EchoPose _pose = EchoPose.Current;

		[Tooltip("Seconds between two looks at each body; at most one afterimage per look.")]
		[MinValue(0.02f)]
		[SerializeField] private float _echoInterval = 0.12f;

		[Tooltip("Seconds an afterimage lives. Capped just under the time its snapshot is rebaked: four intervals for Current, three for Previous, which holds one snapshot back.")]
		[MinValue(0.02f)]
		[SerializeField] private float _echoLifetime = 0.3f;

		[Tooltip("Metres any bone must have moved since the last afterimage before another is left. A body holding still leaves none.")]
		[MinValue(0.001f)]
		[SerializeField] private float _motionThreshold = 0.02f;

		[Tooltip("Metres a second an afterimage slides off the body, in a random direction each time. Ignored by a graph with no Drift.")]
		[MinValue(0f)]
		[SerializeField] private float _driftSpeed = 0.25f;

		[SerializeField] private Color _tint = new(0.55f, 0.9f, 1f, 1f);

		[Tooltip("Alpha an afterimage is born with.")]
		[PropertyRange(0f, 1f)]
		[SerializeField] private float _alpha = 0.55f;

		[Tooltip("Multiplies the starting alpha over an afterimage's life: time 0–1 across its life, value 0–1 of the starting alpha.")]
		[SerializeField] private AnimationCurve _alphaOverLife = AnimationCurve.Linear(0f, 1f, 1f, 0f);

		public PokerHallucinationTarget Target => _target;
		public VisualEffectAsset VisualEffect => _visualEffect;
		public float EchoInterval => _echoInterval;
		public float MotionThreshold => _motionThreshold;
		public float DriftSpeed => _driftSpeed;
		public Color Tint => _tint;
		public float Alpha => _alpha;
		public AnimationCurve AlphaOverLife => _alphaOverLife;

		public EchoPose Pose => _pose;

		// Gone before its snapshot is rebaked for a later echo.
		public float EchoLifetime => Mathf.Min(_echoLifetime, _echoInterval * (_pose == EchoPose.Previous ? SnapshotCount - 1 : SnapshotCount) * 0.95f);

		protected override PokerHallucinationEffectBehaviour Attach(GameObject host) => host.AddComponent<PokerHallucinationGhostBehaviour>();
	}
}
