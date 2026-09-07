using Game.Runtime.Controller;
using UnityEngine;
using UnityEngine.InputSystem.UI;

namespace Game.Runtime.UI
{
	// The pad's pointer. `VirtualMouseInput` does the work — it is the Input System's own answer and it
	// creates a real virtual Mouse device, so anything reading `Pointer.current` (the card picker, uGUI's
	// own raycasting) is pointed at by a stick without knowing it.
	//
	// This only decides *when* it runs: on a keyboard the hardware cursor is the pointer and a second one
	// drawn beside it would be two arrows answering the same hand. A software cursor is also why
	// CursorController may go on hiding the hardware one for a pad — the arrow the player sees is this
	// Image, not the OS's.
	[RequireComponent(typeof(VirtualMouseInput))]
	public class UIVirtualCursor : MonoBehaviour
	{
		[Header("References")]
		[Tooltip("The arrow itself. Switched off with the device, so a player who puts the pad down is not left with a pointer nothing is driving.")]
		[SerializeField] private RectTransform _cursor;

		private VirtualMouseInput _input;

		private void Awake()
		{
			_input = GetComponent<VirtualMouseInput>();
		}

		// Start rather than OnEnable: InputSchemeController is another object's static, and objects in one
		// scene wake in no guaranteed order — an OnEnable reading it can subscribe to nothing and stay
		// deaf for the session.
		private void Start()
		{
			InputSchemeController.OnSchemeChanged += HandleSchemeChanged;

			Refresh();
		}

		private void OnDestroy()
		{
			InputSchemeController.OnSchemeChanged -= HandleSchemeChanged;
		}

		private void HandleSchemeChanged(InputScheme scheme) => Refresh();

		private void Refresh()
		{
			var wanted = InputSchemeController.IsGamepad;

			if (_input) _input.enabled = wanted;
			if (_cursor) _cursor.gameObject.SetActive(wanted);
		}
	}
}
