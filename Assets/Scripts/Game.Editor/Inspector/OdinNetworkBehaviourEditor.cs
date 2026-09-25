using Sirenix.OdinInspector.Editor;
using Unity.Netcode;
using UnityEditor;

namespace Game.Editor.Inspector
{
	// Netcode claims every NetworkBehaviour with an editor of its own, which draws plain serialized fields and
	// silently drops every Odin attribute (buttons, dropdowns, info boxes). This hands them back to Odin.
	[CustomEditor(typeof(NetworkBehaviour), true)]
	public class OdinNetworkBehaviourEditor : OdinEditor
	{
	}
}
