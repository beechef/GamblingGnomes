using UnityEditor;
using UnityEngine;

namespace Game.Editor.UI
{
	// Unity's own [Enum(...)] tops out at seven name/value pairs and fails silently past that — it logs
	// "Failed to create material drawer" once and then draws a bare float field, which is a blend mode
	// nobody can pick and everybody can mistype. This is the same dropdown without the limit.
	//
	// The order here is the order UI_BlendOverlay.shader branches on, and nothing checks that at compile
	// time: reordering one without the other silently renames every mode.
	public enum UIOverlayBlendMode
	{
		Normal,
		Darken,
		Multiply,
		ColorBurn,
		LinearBurn,
		Lighten,
		Screen,
		ColorDodge,
		LinearDodge,
		Overlay,
		SoftLight,
		HardLight,
		VividLight,
		LinearLight,
		PinLight,
		Difference,
		Exclusion,
		Subtract,
		Divide,
	}

	public class UIBlendModeDrawer : MaterialPropertyDrawer
	{
		public override float GetPropertyHeight(MaterialProperty prop, string label, MaterialEditor editor) =>
			EditorGUIUtility.singleLineHeight;

		public override void OnGUI(Rect position, MaterialProperty prop, GUIContent label, MaterialEditor editor)
		{
			EditorGUI.BeginChangeCheck();
			EditorGUI.showMixedValue = prop.hasMixedValue;

			var mode = (UIOverlayBlendMode)Mathf.RoundToInt(prop.floatValue);
			mode = (UIOverlayBlendMode)EditorGUI.EnumPopup(position, label, mode);

			EditorGUI.showMixedValue = false;

			if (EditorGUI.EndChangeCheck()) prop.floatValue = (float)mode;
		}
	}
}
