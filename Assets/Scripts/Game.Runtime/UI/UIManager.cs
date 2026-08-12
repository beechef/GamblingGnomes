using UnityEngine;

namespace Game.Runtime.UI
{
	// The one canvas everything else hangs off. It lives in the bootstrap scene and survives the
	// additive gameplay loads, so a mode's HUD is spawned into a canvas that is already there rather
	// than each feature shipping a canvas of its own and fighting over sorting order.
	public class UIManager : MonoBehaviour
	{
		public static UIManager Instance { get; private set; }

		// Domain Reload is disabled, so statics survive between play sessions.
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics() => Instance = null;

		[Header("References")]
		[SerializeField] private Canvas _canvas;

		[Tooltip("Gameplay HUD — the bottom layer, drawn under everything else.")]
		[SerializeField] private RectTransform _hudLayer;

		[Tooltip("Full screen panels: menus, lobby, results.")]
		[SerializeField] private RectTransform _screenLayer;

		[Tooltip("Anything that must sit on top: modal prompts, an interrupting stage, a transition.")]
		[SerializeField] private RectTransform _overlayLayer;

		[Header("Settings")]
		[SerializeField] private bool _persistAcrossScenes = true;

		public Canvas Canvas => _canvas;

		private void Awake()
		{
			if (Instance && Instance != this)
			{
				Destroy(gameObject);
				return;
			}

			Instance = this;

			if (_persistAcrossScenes && !transform.parent) DontDestroyOnLoad(gameObject);
		}

		private void OnDestroy()
		{
			if (Instance == this) Instance = null;
		}

		public RectTransform GetLayer(UILayer layer)
		{
			var target = layer switch
			{
				UILayer.Screen => _screenLayer,
				UILayer.Overlay => _overlayLayer,
				_ => _hudLayer
			};

			return target ? target : (RectTransform)transform;
		}

		public T Show<T>(T prefab, UILayer layer = UILayer.Hud) where T : Component
		{
			if (!prefab) return null;

			var instance = Instantiate(prefab, GetLayer(layer));
			instance.name = prefab.name;
			return instance;
		}

		public GameObject Show(GameObject prefab, UILayer layer = UILayer.Hud)
		{
			if (!prefab) return null;

			var instance = Instantiate(prefab, GetLayer(layer));
			instance.name = prefab.name;
			return instance;
		}

		public void Hide(GameObject instance)
		{
			if (instance) Destroy(instance);
		}
	}
}
