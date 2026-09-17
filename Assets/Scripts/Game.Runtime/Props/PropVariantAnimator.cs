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

		private int _hash;
		private bool _hasParameter;

		private void Awake()
		{
			if (!_variants) _variants = GetComponentInParent<PropVariantController>();
			if (!_animator) _animator = GetComponentInChildren<Animator>(true);

			_hash = Animator.StringToHash(_parameter);

			if (!_animator) return;

			foreach (var parameter in _animator.parameters)
			{
				if (parameter.nameHash != _hash || parameter.type != AnimatorControllerParameterType.Bool) continue;

				_hasParameter = true;
				break;
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

		private void OnDisable()
		{
			if (_variants) _variants.OnVariantChanged -= HandleVariantChanged;
		}

		private void HandleVariantChanged(PropVariant current)
		{
			if (!_hasParameter || !_animator) return;

			_animator.SetBool(_hash, current == _variant);
		}
	}
}
