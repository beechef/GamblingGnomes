using System.Linq;
using Localization;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEngine;

namespace Game.Editor.UI
{
	public class LocalizationKeyAttributeDrawer : OdinAttributeDrawer<LocalizationKeyAttribute, string>
	{
		protected override void DrawPropertyLayout(GUIContent label)
		{
			var current = ValueEntry.SmartValue ?? string.Empty;
			var known = string.IsNullOrEmpty(current) || LocalizationKeySource.Contains(current);

			// A key that has gone is flagged in place rather than silently corrected, so the
			// rename that broke it is findable.
			if (!known)
			{
				SirenixEditorGUI.WarningMessageBox(
					$"'{current}' is not in the default locale table. It will render as [{current}].");
			}

			var buttonText = string.IsNullOrEmpty(current) ? "(none)" : LocalizationKeySource.CachedDropdownLabel(current);

			var picked = OdinSelector<string>.DrawSelectorDropdown(
				label ?? new GUIContent(ValueEntry.Property.NiceName), buttonText, CreateSelector);

			if (picked == null) return;

			var key = picked.FirstOrDefault() ?? string.Empty;
			if (key != current) ValueEntry.SmartValue = key;
		}

		private static OdinSelector<string> CreateSelector(Rect rect)
		{
			// An empty entry so a field can be cleared, which a list of only real keys cannot.
			var keys = new[] { string.Empty }.Concat(LocalizationKeySource.KeysWithValues().Select(pair => pair.Key));

			var selector = new GenericSelector<string>(
				"Keys in the default table", false,
				key => string.IsNullOrEmpty(key) ? "(none)" : LocalizationKeySource.CachedDropdownLabel(key),
				keys);

			selector.FlattenedTree = true;
			selector.ShowInPopup(rect, 560f);
			return selector;
		}
	}
}
