using Game.Runtime.Controller;
using UnityEditor;

namespace Game.Editor.Save
{
	public static class ClearSaveMenu
	{
		[MenuItem("Tools/Clear Save")]
		private static void ClearSave() => SettingController.Clear();
	}
}
