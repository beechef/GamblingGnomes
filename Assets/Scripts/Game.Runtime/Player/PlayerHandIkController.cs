using System;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Game.Runtime.Player
{
	// Puts a hand where something actually is, instead of where the clip happened to leave it. The bet
	// gesture puts a cap down, but the spot it lands on belongs to the chair — so the clip alone is always a
	// little wrong.
	//
	// **The animation drives the weight; the curve for it lives here.** It used to be an `m_Weight` curve
	// baked into a project-owned `.anim`, and that stopped working the moment the art shipped the takes as
	// FBX: an FBX clip cannot carry a curve on a component, so the IK silently never came on again. Each
	// state that may raise the IK names a curve over its own normalized time, and the weight is read off the
	// state the animator is actually in — so timing still follows the animation (speed, cuts, crossfades),
	// and re-exporting a clip cannot lose it.
	//
	// Being authoritative every frame is also what keeps the weight from being stranded: a state that is
	// not listed evaluates to zero, so a gesture cut short or an animator returning to Empty drops the arm
	// back to the clip without anyone resetting anything.
	//
	// Both rigs carry a constraint, because the owner renders the hand-only model and everybody else the full
	// body; each is weighted from its own animator. It carries no game meaning — what the hand reaches for
	// is the caller's business.
	public class PlayerHandIkController : MonoBehaviour
	{
		[Serializable]
		private struct StateWeight
		{
			[Tooltip("Animator state on the gesture layer that raises the IK while it plays.")]
			public string StateName;

			[Tooltip("IK weight over the state's normalized time, 0 at its first frame and 1 at its last.")]
			public AnimationCurve Weight;

			[NonSerialized] public int Hash;
		}

		[Header("Rig")]
		[Tooltip("The hand constraints this aims — one per rig, since the owner and the table render different models. Left empty they are collected from the children.")]
		[SerializeField] private TwoBoneIKConstraint[] _constraints;

		[Tooltip("The animators the gestures play on — one per rig, in the same order as the constraints. Left empty they are found from the constraints themselves.")]
		[SerializeField] private Animator[] _animators;

		[Header("Weight")]
		[Tooltip("The layer the states below live on.")]
		[Min(0)]
		[SerializeField] private int _gestureLayer = 1;

		[Tooltip("Every state allowed to raise the IK, each with its own weight curve. Anything else holds the weight at zero.")]
		[SerializeField] private StateWeight[] _states = Array.Empty<StateWeight>();

		private void Awake()
		{
			if (_constraints == null || _constraints.Length == 0)
				_constraints = GetComponentsInChildren<TwoBoneIKConstraint>(true);

			if (_animators == null || _animators.Length == 0) CollectAnimators();

			for (var i = 0; i < _states.Length; i++) _states[i].Hash = Animator.StringToHash(_states[i].StateName);
		}

		private void CollectAnimators()
		{
			if (_constraints == null) return;

			_animators = new Animator[_constraints.Length];
			for (var i = 0; i < _constraints.Length; i++)
			{
				if (_constraints[i]) _animators[i] = _constraints[i].GetComponentInParent<Animator>();
			}
		}

		// Before the animator updates, which is when the rig job reads the weight — so the value written
		// here is the one this frame's pose is built with.
		private void Update()
		{
			if (_constraints == null) return;

			for (var i = 0; i < _constraints.Length; i++)
			{
				var constraint = _constraints[i];
				if (!constraint) continue;

				var weight = EvaluateWeight(i);
				if (!Mathf.Approximately(constraint.weight, weight)) constraint.weight = weight;
			}
		}

		// Mid-transition blends both sides by how far the crossfade has got, so the arm eases between the
		// state leaving and the state arriving instead of snapping at either end.
		private float EvaluateWeight(int index)
		{
			if (_animators == null || index >= _animators.Length) return 0f;

			var animator = _animators[index];
			if (!animator || !animator.isActiveAndEnabled || _gestureLayer >= animator.layerCount) return 0f;

			var current = Evaluate(animator.GetCurrentAnimatorStateInfo(_gestureLayer));
			if (!animator.IsInTransition(_gestureLayer)) return current;

			var next = Evaluate(animator.GetNextAnimatorStateInfo(_gestureLayer));
			var blend = animator.GetAnimatorTransitionInfo(_gestureLayer).normalizedTime;

			return Mathf.Lerp(current, next, blend);
		}

		private float Evaluate(AnimatorStateInfo state)
		{
			foreach (var entry in _states)
			{
				if (entry.Hash != state.shortNameHash || entry.Weight == null) continue;

				// A non-looping state keeps counting past 1 while it holds its last frame.
				var time = state.loop ? Mathf.Repeat(state.normalizedTime, 1f) : Mathf.Clamp01(state.normalizedTime);

				return Mathf.Clamp01(entry.Weight.Evaluate(time));
			}

			return 0f;
		}

		// Reach for this spot. Only the target's position moves: where the hand goes is the caller's, how the
		// hand is turned stays the clip's — a spot on the table has no opinion about a wrist, and copying its
		// rotation would twist the hand to match a transform authored for a cap. Placed once, at the call — a
		// spot on the table does not move.
		public void Aim(Transform placement)
		{
			if (!placement || _constraints == null) return;

			foreach (var constraint in _constraints)
			{
				if (!constraint) continue;

				var target = constraint.data.target;
				if (target) target.position = placement.position;
			}
		}
	}
}
