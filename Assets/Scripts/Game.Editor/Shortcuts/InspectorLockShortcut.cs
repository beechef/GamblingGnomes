using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Game.Editor.Shortcuts
{
	// Ctrl+L locks or unlocks the inspector, the padlock without the reach. Rebindable under
	// Edit > Shortcuts > Inspector.
	//
	// The inspector under the mouse or in focus is the one toggled; from anywhere else every open inspector
	// follows the first one. InspectorWindow is internal, so its public isLocked is reached by reflection:
	// ActiveEditorTracker.sharedTracker does not hold a lock set from outside an inspector.
	public static class InspectorLockShortcut
	{
		private static readonly Type InspectorType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");

		private static readonly PropertyInfo IsLockedProperty =
			InspectorType?.GetProperty("isLocked", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

		private static readonly List<EditorWindow> Targets = new();

		[Shortcut("Inspector/Toggle Lock", KeyCode.L, ShortcutModifiers.Action)]
		[MenuItem("Tools/Toggle Inspector Lock")]
		private static void ToggleLock()
		{
			if (IsLockedProperty == null)
			{
				Debug.LogWarning("[InspectorLockShortcut] InspectorWindow.isLocked not found in this Unity version.");
				return;
			}

			CollectTargets();
			if (Targets.Count == 0) return;

			var locked = !(bool)IsLockedProperty.GetValue(Targets[0]);

			foreach (var inspector in Targets)
			{
				IsLockedProperty.SetValue(inspector, locked);
				inspector.Repaint();
			}

			Targets.Clear();
		}

		private static void CollectTargets()
		{
			Targets.Clear();

			if (IsInspector(EditorWindow.mouseOverWindow)) Targets.Add(EditorWindow.mouseOverWindow);
			else if (IsInspector(EditorWindow.focusedWindow)) Targets.Add(EditorWindow.focusedWindow);
			else
			{
				foreach (var window in Resources.FindObjectsOfTypeAll(InspectorType))
					if (window is EditorWindow inspector) Targets.Add(inspector);
			}
		}

		private static bool IsInspector(EditorWindow window) => window && InspectorType.IsInstanceOfType(window);
	}
}
