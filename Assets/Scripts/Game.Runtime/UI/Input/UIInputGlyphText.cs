using System.Text.RegularExpressions;
using Game.Runtime.Controller;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.Input
{
	// Turns <ActionName> (or <Map/Action>) in this text into the device-in-hand's button for that action,
	// whatever wrote the text (a LocalizedText, code). A tag naming no action is left for TMP, so <b> and
	// friends pass through.
	[RequireComponent(typeof(TMP_Text))]
	public class UIInputGlyphText : MonoBehaviour, ITextPreprocessor
	{
		private static readonly Regex ActionTag =
			new(@"<([A-Za-z][A-Za-z0-9]*(?:/[A-Za-z][A-Za-z0-9]*)?)>", RegexOptions.Compiled);

		[Required]
		[SerializeField] private InputGlyphDatabase _database;

		private TMP_Text _text;
		private MatchEvaluator _expand;

		private void Awake()
		{
			_text = GetComponent<TMP_Text>();
			_text.textPreprocessor = this;
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

		// Setting the sprite asset marks the text changed, so TMP re-parses and runs PreprocessText again.
		private void Refresh()
		{
			if (_database) _text.spriteAsset = _database.Current;
		}

		public string PreprocessText(string text)
		{
			if (!_database || string.IsNullOrEmpty(text) || text.IndexOf('<') < 0) return text;

			_expand ??= Expand;
			return ActionTag.Replace(text, _expand);
		}

		private string Expand(Match match)
		{
			var action = _database.FindAction(match.Groups[1].Value);
			if (action == null) return match.Value;

			return _database.TryResolve(action, out var spriteName, out var fallbackText)
				? $"<sprite name=\"{spriteName}\">"
				: fallbackText;
		}
	}
}
