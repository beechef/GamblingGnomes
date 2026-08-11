using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.Interaction
{
	public class UIInteractPrompt : MonoBehaviour
	{
		[Header("References")]
		[SerializeField] private CanvasGroup _canvasGroup;
		[SerializeField] private TextMeshProUGUI _actionLabel;
		[SerializeField] private TextMeshProUGUI _keyLabel;

		public static UIInteractPrompt Instance { get; private set; }

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
			Hide();
		}

		private void OnDestroy()
		{
			if (Instance == this) Instance = null;
		}

		public void Show(string actionName, string keyDisplayName)
		{
			if (_actionLabel) _actionLabel.text = actionName;
			if (_keyLabel) _keyLabel.text = keyDisplayName;

			if (_canvasGroup) _canvasGroup.alpha = 1f;
		}

		public void Hide()
		{
			if (_canvasGroup) _canvasGroup.alpha = 0f;
		}
	}
}
