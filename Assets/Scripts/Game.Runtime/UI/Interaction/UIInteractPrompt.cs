using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.Interaction
{
	// Spawned into the shared canvas by whoever needs prompting, and held by them — there is one of
	// these per local player, so it is handed out rather than looked up.
	public class UIInteractPrompt : MonoBehaviour
	{
		[Header("References")]
		[SerializeField] private CanvasGroup _canvasGroup;
		[SerializeField] private TextMeshProUGUI _actionLabel;
		[SerializeField] private TextMeshProUGUI _keyLabel;

		private void Awake()
		{
			Hide();
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
