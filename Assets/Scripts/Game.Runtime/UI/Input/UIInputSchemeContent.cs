using Game.Runtime.Controller;
using UnityEngine;

namespace Game.Runtime.UI.Input
{
	// Shows its content only while the player holds the named kind of device (the pad's Select/Back hints).
	public class UIInputSchemeContent : MonoBehaviour
	{
		[SerializeField] private InputScheme _shownFor = InputScheme.Gamepad;

		[Tooltip("Switched on and off; never this object, or it would stop hearing the scheme change.")]
		[SerializeField] private GameObject _content;

		private void OnEnable()
		{
			InputSchemeController.OnSchemeChanged += HandleSchemeChanged;

			HandleSchemeChanged(InputSchemeController.Current);
		}

		private void OnDisable()
		{
			InputSchemeController.OnSchemeChanged -= HandleSchemeChanged;
		}

		private void HandleSchemeChanged(InputScheme scheme)
		{
			if (_content) _content.SetActive(scheme == _shownFor);
		}
	}
}
