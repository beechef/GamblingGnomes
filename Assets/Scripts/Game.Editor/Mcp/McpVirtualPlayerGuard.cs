using Unity.AI.MCP.Editor;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Mcp
{
	// Keeps the MCP bridge off in Multiplayer Play Mode's virtual players. Every clone is a full editor that
	// runs the same package with the same project settings, so each one opened its own bridge and an MCP
	// client could land on a clone — which runs read-only, so every asset write through it failed in silence.
	// Only the main editor answers now. A clone is told apart by where its Assets folder lives.
	[InitializeOnLoad]
	internal static class McpVirtualPlayerGuard
	{
		private static readonly bool IsVirtualPlayer = Application.dataPath.Replace("\\", "/").Contains("/Library/VP/");

		static McpVirtualPlayerGuard()
		{
			if (!IsVirtualPlayer) return;

			// Checked on every editor tick rather than once: the bridge schedules its own start after load and
			// restarts after a domain reload, and Stop leaves the project setting alone so the main editor keeps it.
			EditorApplication.update += StopBridge;
		}

		private static void StopBridge()
		{
			if (UnityMCPBridge.IsRunning) UnityMCPBridge.Stop();
		}
	}
}
