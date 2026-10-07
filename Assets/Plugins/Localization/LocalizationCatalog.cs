using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Localization
{
	/// <summary>
	/// The languages a project ships, and which one it starts in. One asset, handed to
	/// <see cref="Localizer"/> at boot.
	/// </summary>
	[CreateAssetMenu(menuName = "Localization/Catalog", fileName = "LocalizationCatalog")]
	public class LocalizationCatalog : ScriptableObject
	{
		[Title("Languages")]
		[Tooltip("Every language available. Order is the order a language switch cycles through.")]
		[ListDrawerSettings(ShowFoldout = false)]
		[SerializeField] private List<LocaleTable> _locales = new();

		[Tooltip("Locale code used when the player has not chosen one yet.")]
		[SerializeField] private string _defaultLocaleCode = "en";

		/// <summary>The languages, in the order a switch cycles them.</summary>
		public IReadOnlyList<LocaleTable> Locales => _locales;

		public string DefaultLocaleCode => _defaultLocaleCode;

		public bool TryGetLocale(string code, out LocaleTable locale)
		{
			foreach (var candidate in _locales)
			{
				if (!candidate || candidate.LocaleCode != code) continue;

				locale = candidate;
				return true;
			}

			locale = null;
			return false;
		}
	}
}
