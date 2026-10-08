using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.Props
{
	// A variant that is an animation rather than a change of objects: while the prop wears the variant a bool
	// on its animator is on, and the controller authored on the prop decides what that plays — a mushroom
	// transforming, and back again when the look is dropped. Listens to PropVariantController and never asks
	// for a variant itself.
	public class PropVariantAnimator : MonoBehaviour
	{
		[Required]
		[SerializeField] private PropVariantController _variants;

		[Required]
		[SerializeField] private Animator _animator;

		[Tooltip("The variant that turns the bool on.")]
		[SerializeField] private PropVariant _variant = PropVariant.Transformed;

		[Tooltip("Bool parameter on the animator. A controller without it is skipped, so the prop works before its art does.")]
		[SerializeField] private string _parameter = "IsTransformed";

		[Tooltip("Float parameter the controller reads as the transform's playback speed. A controller without it is skipped.")]
		[SerializeField] private string _speedParameter = "TransformSpeed";

		[Tooltip("How fast the transform plays: 1 as authored, 2 twice as fast (a menu icon that has to answer the pointer at once).")]
		[MinValue(0f)]
		[SerializeField] private float _speed = 1f;

		private int _hash;
		private int _speedHash;
		private bool _hasParameter;
		private bool _hasSpeedParameter;

		private void Awake()
		{
			if (!_variants) _variants = GetComponentInParent<PropVariantController>();
			if (!_animator) _animator = GetComponentInChildren<Animator>(true);

			_hash = Animator.StringToHash(_parameter);
			_speedHash = Animator.StringToHash(_speedParameter);

			if (!_animator) return;

			foreach (var parameter in _animator.parameters)
			{
				if (parameter.nameHash == _hash && parameter.type == AnimatorControllerParameterType.Bool) _hasParameter = true;
				if (parameter.nameHash == _speedHash && parameter.type == AnimatorControllerParameterType.Float) _hasSpeedParameter = true;
			}
		}

		private void OnEnable()
		{
			if (!_variants) return;

			_variants.OnVariantChanged += HandleVariantChanged;

			// A prop switched on while already wearing the look — a cap drawn after the hallucination began —
			// arrives in it rather than waiting for a change that has already happened.
			HandleVariantChanged(_variants.Current);
		}

		// An animator resets its parameters when it initialises, which for a prop spawned this frame is after
		// OnEnable — so a look asked for at spawn (a menu icon already lit, a cap drawn mid-hallucination) is re-applied.
		private void Start()
		{
			if (_variants) HandleVariantChanged(_variants.Current);
		}

		private void OnDisable()
		{
			if (_variants) _variants.OnVariantChanged -= HandleVariantChanged;
		}

		private void HandleVariantChanged(PropVariant current)
		{
			if (!_animator) return;

			// Written with the bool, so it survives the same initialisation reset Start re-applies for.
			if (_hasSpeedParameter) _animator.SetFloat(_speedHash, _speed);
			if (_hasParameter) _animator.SetBool(_hash, current == _variant);
		}
	}
}
