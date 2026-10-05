using TMPro;
using UnityEngine;

namespace Localization
{
	/// <summary>
	/// Keeps a label showing one key's string in the running language. Drop it on any
	/// <see cref="TMP_Text"/> whose text is fixed; text built at runtime asks
	/// <see cref="Localizer"/> directly instead.
	/// </summary>
	[RequireComponent(typeof(TMP_Text))]
	public class LocalizedText : MonoBehaviour
	{
		[Tooltip("Which string this label shows.")]
		[LocalizationKey]
		[SerializeField] private string _key;

		private TMP_Text _label;

		public string Key
		{
			get => _key;
			set
			{
				_key = value;
				Apply();
			}
		}

		private void OnEnable()
		{
			Localizer.OnLocaleChanged += Apply;
			Apply();
		}

		private void OnDisable() => Localizer.OnLocaleChanged -= Apply;

		private void Apply()
		{
			// Resolved here rather than only in Awake: a clone of a disabled template never gets
			// Awake until something activates it.
			if (!_label) _label = GetComponent<TMP_Text>();
			if (!_label || string.IsNullOrEmpty(_key)) return;

			_label.text = ProcessText(Localizer.Get(_key));
		}

		/// <summary>
		/// The last say over what the label shows, after the key is looked up. Override to transform
		/// the text for this label alone; the base shows it as it came.
		/// </summary>
		protected virtual string ProcessText(string text) => text;

#if UNITY_EDITOR
		/// <summary>
		/// Outside Play mode the label shows the key's default-language string, so a layout is judged
		/// against the words it will hold rather than whatever placeholder the label was built with.
		/// </summary>
		/// <remarks>
		/// Deferred: a TMP label rebuilds its mesh when its text is set, which Unity refuses from
		/// inside OnValidate. Skipped on assets on disk — only an instance in a scene or an open
		/// prefab is previewed, so importing a prefab never rewrites it. Written only when the text
		/// differs, so opening a scene already previewed leaves it clean.
		/// </remarks>
		private void OnValidate()
		{
			if (Application.isPlaying || UnityEditor.EditorUtility.IsPersistent(this)) return;

			UnityEditor.EditorApplication.delayCall += ApplyEditorPreview;
		}

		private void ApplyEditorPreview()
		{
			if (!this || Application.isPlaying) return;
			if (!_label) _label = GetComponent<TMP_Text>();
			if (!_label || !LocalizationKeySource.TryGetDefaultValue(_key, out var value)) return;

			var preview = ProcessText(value);
			if (_label.text == preview) return;

			_label.text = preview;
			UnityEditor.EditorUtility.SetDirty(_label);
			UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(_label);
		}
#endif
	}
}
