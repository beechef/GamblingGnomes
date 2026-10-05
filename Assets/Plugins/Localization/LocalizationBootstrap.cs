using Sirenix.OdinInspector;
using UnityEngine;

namespace Localization
{
	/// <summary>
	/// Brings <see cref="Localizer"/> up with the project's catalog for as long as this object
	/// lives. Sits in the persistent boot scene, so the language is chosen before any screen opens.
	/// </summary>
	/// <remarks>
	/// Torn down in OnDestroy, not only on quit: leaving Play Mode destroys objects without quitting,
	/// and a service left standing would serve the previous session into the next.
	/// </remarks>
	public class LocalizationBootstrap : MonoBehaviour
	{
		[Required]
		[SerializeField] private LocalizationCatalog _catalog;

		private void Awake() => Localizer.Initialize(_catalog);

		private void OnDestroy() => Localizer.Deinitialize();
	}
}
