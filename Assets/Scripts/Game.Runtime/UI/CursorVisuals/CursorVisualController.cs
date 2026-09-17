using System.Collections.Generic;
using Game.Runtime.Controller;
using Game.Runtime.UI.Button;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Runtime.UI.CursorVisuals
{
	// What the pointer looks like right now. Anything wanting a different look asks for a state and gives
	// its handle back when it is done, the same shape as the camera requests and the cursor's counted
	// release, so two things asking at once unwind in any order. The highest state asked for is drawn
	// (CursorVisualState says why the order is the priority), and Default with nothing asked for.
	//
	// Pointing at a UIButton that answers is asked for here, once, from UIButton.OnPointerOverChanged — so
	// every button in the game shows the Interact look without any of them knowing a cursor exists.
	//
	// Each look is a CursorVisual child switched on while its state is wanted. While one is up the ordinary
	// arrow is hidden through CursorController (the hardware one on a keyboard, the pad's software one on a
	// pad) and the look follows Pointer.current, which is the virtual mouse on a pad — so the pointer keeps
	// working and only its picture changes. Nothing is drawn while the cursor is locked or a pad has no
	// pointer to move, because then there is no pointer on screen to dress.
	//
	// Lives in bootstrap with the canvas; gameplay reaches it through Instance.
	public class CursorVisualController : MonoBehaviour
	{
		[Header("References")]
		[Tooltip("The rect the looks are placed in — the canvas they live under. Empty uses this object.")]
		[SerializeField] private RectTransform _area;

		[Tooltip("Every look. Left empty it takes the children, so adding a look is adding a child.")]
		[SerializeField] private List<CursorVisual> _visuals = new();

		public static CursorVisualController Instance { get; private set; }

		private readonly struct Hold
		{
			public Hold(int handle, CursorVisualState state)
			{
				Handle = handle;
				State = state;
			}

			public int Handle { get; }
			public CursorVisualState State { get; }
		}

		private readonly List<Hold> _holds = new();
		private readonly Dictionary<UIButton, int> _buttonHolds = new();
		private int _nextHandle = 1;

		private Canvas _canvas;
		private CursorVisual _shown;
		private bool _hidingArrow;

		public CursorVisualState State
		{
			get
			{
				var state = CursorVisualState.Default;

				foreach (var hold in _holds)
					if (hold.State > state) state = hold.State;

				return state;
			}
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics() => Instance = null;

		private void Awake()
		{
			if (Instance && Instance != this)
			{
				Destroy(gameObject);
				return;
			}

			Instance = this;

			if (!_area) _area = (RectTransform)transform;
			_canvas = GetComponentInParent<Canvas>();

			if (_visuals.Count == 0) GetComponentsInChildren(true, _visuals);
			foreach (var visual in _visuals)
				if (visual) visual.gameObject.SetActive(false);
		}

		// Start rather than OnEnable: these are other objects' statics, and an OnEnable can run before the
		// scheme controller has woken.
		private void Start()
		{
			CursorController.OnLockChanged += HandleLockChanged;
			CursorController.OnPointerWantedChanged += Refresh;
			InputSchemeController.OnSchemeChanged += HandleSchemeChanged;
			UIButton.OnPointerOverChanged += HandleButtonPointerOverChanged;

			Refresh();
		}

		private void OnDestroy()
		{
			CursorController.OnLockChanged -= HandleLockChanged;
			CursorController.OnPointerWantedChanged -= Refresh;
			InputSchemeController.OnSchemeChanged -= HandleSchemeChanged;
			UIButton.OnPointerOverChanged -= HandleButtonPointerOverChanged;
			_buttonHolds.Clear();

			SetArrowHidden(false);

			if (Instance == this) Instance = null;
		}

		public int Request(CursorVisualState state)
		{
			var handle = _nextHandle++;
			_holds.Add(new Hold(handle, state));

			Refresh();
			return handle;
		}

		public void Release(int handle)
		{
			if (handle == 0) return;

			for (var i = 0; i < _holds.Count; i++)
			{
				if (_holds[i].Handle != handle) continue;

				_holds.RemoveAt(i);
				Refresh();
				return;
			}
		}

		private void HandleButtonPointerOverChanged(UIButton button, bool over)
		{
			if (over)
			{
				if (!_buttonHolds.ContainsKey(button)) _buttonHolds.Add(button, Request(CursorVisualState.Interact));
				return;
			}

			if (!_buttonHolds.Remove(button, out var handle)) return;

			Release(handle);
		}

		private void HandleLockChanged(bool locked) => Refresh();
		private void HandleSchemeChanged(InputScheme scheme) => Refresh();

		private void Refresh()
		{
			var pointerOnScreen = !CursorController.IsLocked && (!InputSchemeController.IsGamepad || CursorController.IsPointerWanted);
			var target = pointerOnScreen ? Find(State) : null;

			if (_shown != target)
			{
				if (_shown) _shown.gameObject.SetActive(false);

				_shown = target;

				if (_shown)
				{
					Follow();
					_shown.gameObject.SetActive(true);
				}
			}

			SetArrowHidden(_shown);
		}

		// A state with no look of its own draws nothing and leaves the ordinary arrow up — Default included, so
		// a project with no Default child keeps the hardware cursor.
		private CursorVisual Find(CursorVisualState state)
		{
			foreach (var visual in _visuals)
				if (visual && visual.State == state) return visual;

			return null;
		}

		// Genuinely continuous: the pointer moves every frame and raises nothing when it does.
		private void LateUpdate()
		{
			if (_shown) Follow();
		}

		private void Follow()
		{
			var pointer = Pointer.current;
			if (pointer == null || !_area) return;

			var camera = _canvas && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

			if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, pointer.position.ReadValue(), camera, out var local))
				_shown.Rect.localPosition = local;
		}

		private void SetArrowHidden(bool hidden)
		{
			if (_hidingArrow == hidden) return;

			_hidingArrow = hidden;

			if (hidden) CursorController.RequestArrowHidden();
			else CursorController.ReleaseArrowHidden();
		}
	}
}
