using Game.Runtime.Controller;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Runtime.UI.Input
{
	// Prints which key an action is currently bound to. Read off the binding rather than typed, so a rebind
	// follows on its own and the face can never claim a key the action does not answer to — the same reason
	// UIButtonHotkey reads its own label instead of carrying one.
	public class UIActionKeyLabel : MonoBehaviour
	{
		[Header("Input")]
		[Required]
		[SerializeField] private InputActionReference _action;

		[Header("References")]
		[Required]
		[SerializeField] private TMP_Text _label;

		private void OnEnable()
		{
			InputSchemeController.OnSchemeChanged += HandleSchemeChanged;

			Refresh();
		}

		private void OnDisable()
		{
			InputSchemeController.OnSchemeChanged -= HandleSchemeChanged;
		}

		private void HandleSchemeChanged(InputScheme scheme) => Refresh();

		public void Refresh()
		{
			if (!_label) return;

			// Masked by the scheme in hand, so picking up a pad renames the key rather than leaving the face
			// naming a keyboard nobody is touching.
			_label.text = _action
				? _action.action.GetBindingDisplayString(InputSchemeController.DisplayMask)
				: string.Empty;
		}

#if UNITY_EDITOR
		private void OnValidate()
		{
			if (isActiveAndEnabled) Refresh();
		}
#endif
	}
}
