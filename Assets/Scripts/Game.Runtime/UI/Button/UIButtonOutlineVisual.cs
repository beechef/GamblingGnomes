using System.Collections.Generic;
using Game.Runtime.Props;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.UI.Button
{
	// Lights a 3D button: while it is pointed at, every renderer under the root wears an extra outline pass.
	// The pass is hung through PropMaterialOverrideController, the one writer of a renderer's materials, so
	// it composes with anything else painting the same model instead of overwriting it.
	//
	// Renderers are read when the state changes, not cached, because what sits under the root can be
	// swapped at runtime (UIModelView replaces its model); the walk fills a reused list.
	public class UIButtonOutlineVisual : UIButtonVisual
	{
		[Header("Target")]
		[Tooltip("Every renderer under this is outlined.")]
		[Required]
		[SerializeField] private Transform _root;

		[Header("Outline")]
		[Tooltip("An extra pass drawn around the model, e.g. UI_ModelOutline.")]
		[Required]
		[SerializeField] private Material _outline;

		[Tooltip("Also lit while pressed, not only hovered or selected.")]
		[SerializeField] private bool _litWhilePressed = true;

		private readonly List<Renderer> _renderers = new();
		private readonly List<PropMaterialOverrideController> _claimed = new();

		protected override void OnDisabled()
		{
			ClearOutline();
		}

		protected override void OnApply(UIButtonState state, bool instant)
		{
			var lit = state is UIButtonState.Hovered or UIButtonState.Selected
				|| (_litWhilePressed && state == UIButtonState.Pressed);

			ClearOutline();

			if (!lit || !_root || !_outline) return;

			_root.GetComponentsInChildren(true, _renderers);

			foreach (var renderer in _renderers)
			{
				var controller = PropMaterialOverrideController.Claim(renderer);
				if (!controller) continue;

				controller.Set(this, _outline, PropPaintMode.Add);
				_claimed.Add(controller);
			}
		}

		// The model may have been destroyed since it was lit, so each controller is checked before clearing.
		private void ClearOutline()
		{
			foreach (var controller in _claimed)
			{
				if (controller) controller.Clear(this);
			}

			_claimed.Clear();
		}
	}
}
