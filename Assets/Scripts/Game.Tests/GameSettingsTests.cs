using Game.Runtime.Controller;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
	public class GameSettingsTests
	{
		[Test]
		public void LargestLandscape_TurnsAPortraitPanelOnItsSide()
		{
			var modes = new[] { new Resolution { width = 600, height = 800 }, new Resolution { width = 800, height = 1280 } };

			Assert.AreEqual(new Vector2Int(1280, 800), GameSettings.LargestLandscape(modes));
		}

		[TestCase(0, 0, 1280, 800)]
		[TestCase(3840, 2160, 1280, 800)]
		[TestCase(1280, 720, 1280, 720)]
		public void FitToDisplay_KeepsOnlyWhatTheDisplayCanShow(int savedWidth, int savedHeight, int width, int height)
		{
			var fitted = GameSettings.FitToDisplay(new Vector2Int(savedWidth, savedHeight), new Vector2Int(1280, 800));

			Assert.AreEqual(new Vector2Int(width, height), fitted);
		}
	}
}
