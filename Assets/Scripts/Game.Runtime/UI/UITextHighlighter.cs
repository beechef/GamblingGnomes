using Localization;
using UnityEngine;

namespace Game.Runtime.UI
{
	// Expands <Highlight>...</Highlight> in every localized string into the highlight colour, so a translator
	// marks the words that matter without knowing the colour.
	public class UITextHighlighter : MonoBehaviour
	{
		private const string OpenTag = "<Highlight>";
		private const string CloseTag = "</Highlight>";

		[SerializeField] private Color _highlightColor = new Color32(0xC9, 0x3E, 0x40, 0xFF);

		private string _open;

		private void Awake()
		{
			_open = $"<color=#{ColorUtility.ToHtmlStringRGB(_highlightColor)}>";
			Localizer.SetTextProcessor(Expand);
		}

		private void OnDestroy() => Localizer.SetTextProcessor(null);

		private string Expand(string text)
		{
			if (string.IsNullOrEmpty(text) || !text.Contains(OpenTag)) return text;

			return text.Replace(OpenTag, _open).Replace(CloseTag, "</color>");
		}
	}
}
