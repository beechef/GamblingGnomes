using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Game.Runtime.Player
{
	// Puts a hand where something actually is, instead of where the clip happened to leave it. The bet
	// gesture reaches under the table for a cap and then puts it down, but the spot it lands on belongs to
	// the chair — so the clip alone is always a little wrong, and the cap had to be snapped to cover it.
	//
	// **The clip says when the aim counts; this says when it does not.** `m_Weight` is an animatable
	// property, so the blend rides a curve on the gesture itself (Animation_Bet.anim) rather than a
	// stopwatch here — the animator already knows how far through the reach it is, and a number in code
	// mirroring a frame in an animation is two things kept in step by hand.
	//
	// But a curve only writes *while its clip is playing*, and nothing resets the property afterwards. The
	// gestures layer returns to an empty state with no clip at all, and `_Bet` transitions out at 90% of
	// its length — where the curve still reads 0.891. So the weight was stranded near 1 and the arm stayed
	// IK-driven for the rest of the session: "the IK is on all the time", from a curve that was correct.
	//
	// The resting value therefore belongs here, to the one component that exists whether or not a clip is
	// playing. Every frame the weight is pulled back to zero unless the gesture that owns it is actually
	// running, so the curve can only ever *raise* it. Authoring the curve's fall-off to match the exit time
	// would be the same two-numbers-in-step trap in a different coat, and would still strand the weight
	// whenever another gesture cuts this one short.
	//
	// Both rigs carry a constraint, because the owner renders the hand-only model and everybody else the
	// full body. The binding path is the same on each (`IkRig/HandRightIk`, relative to that rig's own
	// Animator), so one curve drives whichever rig is playing it.
	//
	// It carries no game meaning. What the hand is reaching for is the caller's business.
	public class PlayerHandIkController : MonoBehaviour
	{
		[Header("Rig")]
		[Tooltip("The hand constraints this aims — one per rig, since the owner and the table render different models. Left empty they are collected from the children. The gesture clip raises their weight; this component is what puts it back.")]
		[SerializeField] private TwoBoneIKConstraint[] _constraints;

		[Tooltip("The animators the gesture plays on — one per rig. Left empty they are found from the constraints themselves.")]
		[SerializeField] private Animator[] _animators;

		[Tooltip("The state whose clip is allowed to raise the IK weight. While no animator is in it, the weight is held at zero.")]
		[SerializeField] private string _gestureStateName = "_Bet";

		[Tooltip("The layer that state lives on.")]
		[Min(0)]
		[SerializeField] private int _gestureLayer = 1;

		private int _gestureStateHash;

		private void Awake()
		{
			if (_constraints == null || _constraints.Length == 0)
				_constraints = GetComponentsInChildren<TwoBoneIKConstraint>(true);

			if (_animators == null || _animators.Length == 0) CollectAnimators();

			_gestureStateHash = Animator.StringToHash(_gestureStateName);
		}

		// One animator per constraint, in the same order, so the pair can be asked about together: each
		// rig's own animator is the only one that can say whether *that* rig is mid-gesture.
		private void CollectAnimators()
		{
			if (_constraints == null) return;

			_animators = new Animator[_constraints.Length];
			for (var i = 0; i < _constraints.Length; i++)
			{
				if (_constraints[i]) _animators[i] = _constraints[i].GetComponentInParent<Animator>();
			}
		}

		// After the animator has written its curves and before the rig evaluates, which is the one moment a
		// stranded value can be caught and put back.
		private void LateUpdate()
		{
			if (_constraints == null) return;

			for (var i = 0; i < _constraints.Length; i++)
			{
				var constraint = _constraints[i];
				if (!constraint || constraint.weight <= 0f) continue;

				if (IsGesturePlaying(i)) continue;

				constraint.weight = 0f;
			}
		}

		private bool IsGesturePlaying(int index)
		{
			if (_animators == null || index >= _animators.Length) return false;

			var animator = _animators[index];
			if (!animator || !animator.isActiveAndEnabled) return false;
			if (_gestureLayer >= animator.layerCount) return false;

			// Mid-transition counts as playing on either side of the blend: the clip is still writing the
			// curve while it fades out, and cutting the weight there would snap the arm.
			if (animator.IsInTransition(_gestureLayer))
			{
				return animator.GetNextAnimatorStateInfo(_gestureLayer).shortNameHash == _gestureStateHash
					|| animator.GetCurrentAnimatorStateInfo(_gestureLayer).shortNameHash == _gestureStateHash;
			}

			return animator.GetCurrentAnimatorStateInfo(_gestureLayer).shortNameHash == _gestureStateHash;
		}

		// Reach for this spot. Only the target moves: the weight rides the gesture's own curve, so a caller
		// says *where* and the animation says *when*.
		//
		// Placed once, at the call — a spot on the table does not move, and following it every frame would
		// be a per-frame path for an answer that never changes.
		public void Aim(Transform placement)
		{
			if (!placement || _constraints == null) return;

			foreach (var constraint in _constraints)
			{
				if (!constraint) continue;

				var target = constraint.data.target;
				if (target) target.SetPositionAndRotation(placement.position, placement.rotation);
			}
		}
	}
}
