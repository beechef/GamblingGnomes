using System;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace Game.Runtime.Steam
{
	// The one door to Steam achievements and stats for the local player. Without Steam every call does nothing,
	// and a name Steamworks does not know is warned about rather than thrown, so a sound or a rule calling in is
	// never broken by it.
	public static class AchievementController
	{
		public static void Unlock(AchievementId id)
		{
			if (!SteamController.IsInitialized) return;

			try
			{
				var achievement = new Achievement(id.ToString());
				if (achievement.State) return;

				if (achievement.Trigger()) Debug.Log($"[AchievementController] Unlocked {id}.");
				else Debug.LogWarning($"[AchievementController] Steam refused {id}; is it defined in Steamworks?");
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[AchievementController] Unlocking {id} failed: {e.Message}");
			}
		}

		public static int GetStat(AchievementStat stat)
		{
			if (!SteamController.IsInitialized) return 0;

			try
			{
				return SteamUserStats.GetStatInt(stat.ToString());
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[AchievementController] Reading {stat} failed: {e.Message}");
				return 0;
			}
		}

		public static void SetStat(AchievementStat stat, int value)
		{
			if (!SteamController.IsInitialized) return;

			try
			{
				if (SteamUserStats.SetStat(stat.ToString(), value)) SteamUserStats.StoreStats();
				else Debug.LogWarning($"[AchievementController] Steam refused {stat}; is it defined in Steamworks?");
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[AchievementController] Writing {stat} failed: {e.Message}");
			}
		}
	}
}
