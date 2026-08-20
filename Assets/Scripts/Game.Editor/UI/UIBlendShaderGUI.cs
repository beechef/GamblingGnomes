using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Editor.UI
{
	// The friendly face of UI/Blend: one dropdown that writes the four hidden state properties together.
	// They are set as a set or not at all — a source factor picked apart from the neutral colour is a
	// material that silently ignores its own alpha, which looks like a broken fade rather than like a
	// half-applied setting.
	public class UIBlendShaderGUI : ShaderGUI
	{
		public enum UIBlendMode
		{
			Normal,
			Multiply,
			Screen,
			Additive,
			Subtract,
			Darken,
			Lighten,
		}

		private const string BlendModeProperty = "_BlendMode";
		private const string BlendNeutralProperty = "_BlendNeutral";
		private const string SourceBlendProperty = "_SrcBlend";
		private const string DestinationBlendProperty = "_DstBlend";
		private const string BlendOperationProperty = "_BlendOp";

		private static readonly GUIContent BlendModeLabel = new(
			"Blend Mode",
			"How this image combines with whatever the canvas drew before it — the parent it sits on, or an earlier sibling.");

		public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
		{
			var modeProperty = FindProperty(BlendModeProperty, properties);

			EditorGUI.BeginChangeCheck();
			EditorGUI.showMixedValue = modeProperty.hasMixedValue;

			var mode = (UIBlendMode)EditorGUILayout.EnumPopup(BlendModeLabel, (UIBlendMode)modeProperty.floatValue);

			EditorGUI.showMixedValue = false;

			if (EditorGUI.EndChangeCheck())
			{
				materialEditor.RegisterPropertyChangeUndo(BlendModeLabel.text);
				modeProperty.floatValue = (float)mode;

				foreach (var target in materialEditor.targets)
				{
					if (target is Material material) ApplyBlendMode(material, mode);
				}
			}

			EditorGUILayout.HelpBox(DescriptionFor(mode), MessageType.None);
			EditorGUILayout.Space();

			foreach (var property in properties)
			{
				if ((property.propertyFlags & ShaderPropertyFlags.HideInInspector) != 0) continue;

				materialEditor.ShaderProperty(property, property.displayName);
			}

			EditorGUILayout.Space();
			materialEditor.RenderQueueField();
		}

		// A material dragged onto this shader arrives carrying whatever state the last one left; without
		// this it would render with a blend mode nothing in the inspector admits to.
		public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
		{
			base.AssignNewShaderToMaterial(material, oldShader, newShader);

			if (!material.HasProperty(BlendModeProperty)) return;

			ApplyBlendMode(material, (UIBlendMode)material.GetFloat(BlendModeProperty));
		}

		public static void ApplyBlendMode(Material material, UIBlendMode mode)
		{
			material.SetFloat(BlendModeProperty, (float)mode);

			switch (mode)
			{
				// The source arrives already scaled by its alpha (the shader's neutral lerp does it), so
				// every factor-based mode reads as its premultiplied form.
				case UIBlendMode.Normal:
					SetState(material, BlendMode.One, BlendMode.OneMinusSrcAlpha, BlendOp.Add, Color.clear);
					break;

				case UIBlendMode.Multiply:
					SetState(material, BlendMode.DstColor, BlendMode.OneMinusSrcAlpha, BlendOp.Add, Color.clear);
					break;

				case UIBlendMode.Screen:
					SetState(material, BlendMode.OneMinusDstColor, BlendMode.One, BlendOp.Add, Color.clear);
					break;

				case UIBlendMode.Additive:
					SetState(material, BlendMode.One, BlendMode.One, BlendOp.Add, Color.clear);
					break;

				case UIBlendMode.Subtract:
					SetState(material, BlendMode.One, BlendMode.One, BlendOp.ReverseSubtract, Color.clear);
					break;

				// Min and Max ignore the blend factors entirely, so these two fade through the neutral
				// alone: white is what Min leaves the backdrop untouched against, black is Max's.
				case UIBlendMode.Darken:
					SetState(material, BlendMode.One, BlendMode.One, BlendOp.Min, Color.white);
					break;

				case UIBlendMode.Lighten:
					SetState(material, BlendMode.One, BlendMode.One, BlendOp.Max, Color.clear);
					break;
			}
		}

		private static void SetState(Material material, BlendMode source, BlendMode destination, BlendOp operation, Color neutral)
		{
			material.SetFloat(SourceBlendProperty, (float)source);
			material.SetFloat(DestinationBlendProperty, (float)destination);
			material.SetFloat(BlendOperationProperty, (float)operation);
			material.SetColor(BlendNeutralProperty, neutral);
		}

		private static string DescriptionFor(UIBlendMode mode) => mode switch
		{
			UIBlendMode.Normal => "Straight alpha over the backdrop.",
			UIBlendMode.Multiply => "Darkens by the source: white leaves the backdrop alone, black wipes it out.",
			UIBlendMode.Screen => "Lightens by the source: black leaves the backdrop alone, white blows it out.",
			UIBlendMode.Additive => "Adds light. Good for glows, never for a tint.",
			UIBlendMode.Subtract => "Takes the source out of the backdrop.",
			UIBlendMode.Darken => "Keeps whichever of the two is darker, per channel. A stroke only ever darkens what it crosses.",
			UIBlendMode.Lighten => "Keeps whichever of the two is lighter, per channel.",
			_ => string.Empty,
		};
	}
}
