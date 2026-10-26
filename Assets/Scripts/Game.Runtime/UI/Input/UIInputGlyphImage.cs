using Game.Runtime.Controller;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Runtime.UI.Input
{
	// Draws the device-in-hand's button for one action on an Image, swapped as the player changes device.
	[RequireComponent(typeof(Image))]
	public class UIInputGlyphImage : MonoBehaviour
	{
		[Required]
		[SerializeField] private InputGlyphDatabase _database;

		[Required]
		[SerializeField] private InputActionReference _action;

		[Tooltip("Hides the image while the action has no drawn sprite for the device in hand.")]
		[SerializeField] private bool _hideWhenMissing = true;

		private Image _image;

		private void Awake()
		{
			_image = GetComponent<Image>();
		}

		private void OnEnable()
		{
			InputSchemeController.OnDeviceChanged += Refresh;

			Refresh();
		}

		private void OnDisable()
		{
			InputSchemeController.OnDeviceChanged -= Refresh;
		}

		public void Refresh()
		{
			var sprite = _database && _action && _database.TryResolve(_action.action, out var spriteName, out _)
				? _database.FindSprite(spriteName)
				: null;

			_image.sprite = sprite;
			_image.enabled = sprite || !_hideWhenMissing;
		}
	}
}
