using Game.Runtime.Props;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.UI.Button
{
	// Asks the model on a UIModelView to wear one of its own looks while the button is pointed at, held or
	// chosen — a mushroom transforming and dancing. The prop decides what the look is; this only asks, as one
	// counted request, so a hallucination asking for the same prop meanwhile is not undone. Follows the view,
	// because the model is replaced whenever a different prefab is shown.
	public class UIButtonPropVariantVisual : UIButtonVisual
	{
		[Header("Target")]
		[Required]
		[SerializeField] private UIModelView _model;

		[Tooltip("The look asked for while the button is lit. A prop that has not authored it stays as it was.")]
		[SerializeField] private PropVariant _variant = PropVariant.Transformed;

		private PropVariantController _variants;
		private bool _lit;

		protected override void OnReset()
		{
			_model = GetComponentInChildren<UIModelView>(true);
		}

		private void Awake()
		{
			if (_model) _model.OnModelChanged += HandleModelChanged;
		}

		private void OnDestroy()
		{
			if (_model) _model.OnModelChanged -= HandleModelChanged;

			Bind(null);
		}

		protected override void OnDisabled()
		{
			SetLit(false);
		}

		protected override void OnApply(UIButtonState state, bool instant)
		{
			SetLit(state is UIButtonState.Hovered or UIButtonState.Selected or UIButtonState.Pressed);
		}

		private void HandleModelChanged(GameObject model) => Bind(model ? model.GetComponentInChildren<PropVariantController>(true) : null);

		private void Bind(PropVariantController variants)
		{
			if (_variants == variants) return;

			if (_variants) _variants.Clear(this);
			_variants = variants;
			if (_variants && _lit) _variants.Set(this, _variant);
		}

		private void SetLit(bool lit)
		{
			_lit = lit;
			if (!_variants) return;

			if (lit) _variants.Set(this, _variant);
			else _variants.Clear(this);
		}
	}
}
