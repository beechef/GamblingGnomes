using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.Props
{
	// Holds a prop in one of its looks for as long as this is switched on: scenery that should always be the
	// transformed version of a prop asks for it here instead of waiting for an effect to. It is one request among
	// the prop's counted ones, so an effect asking on top of it still wins and dropping that effect comes back
	// to this look rather than to Default.
	public class PropVariantRequest : MonoBehaviour
	{
		[Required]
		[SerializeField] private PropVariantController _variants;

		[Tooltip("The look held while this is on.")]
		[SerializeField] private PropVariant _variant = PropVariant.Transformed;

		private void Reset() => _variants = GetComponentInParent<PropVariantController>();

		private void OnEnable()
		{
			if (_variants) _variants.Set(this, _variant);
		}

		private void OnDisable()
		{
			if (_variants) _variants.Clear(this);
		}
	}
}
