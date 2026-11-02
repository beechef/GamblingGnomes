#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.Runtime.InteropServices;
#endif

namespace Game.Runtime.Platform
{
	// Whether this Windows build is running under Wine or Proton (Steam Deck, any Linux player). Asked of
	// ntdll rather than Steam: Steam may be absent, and the export exists only under Wine.
	public static class WineRuntime
	{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
		[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
		private static extern IntPtr GetModuleHandle(string moduleName);

		[DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
		private static extern IntPtr GetProcAddress(IntPtr module, string procName);

		public static bool IsRunning { get; } = GetProcAddress(GetModuleHandle("ntdll.dll"), "wine_get_version") != IntPtr.Zero;
#else
		public static bool IsRunning => false;
#endif
	}
}
