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

		private static readonly Vector2Int[] Presets = { new(1280, 720), new(1600, 900), new(1920, 1080) };

		[Test]
		public void ResolutionChoices_KeepsThePresetsOnTheirOwnShape()
		{
			var modes = new[] { new Resolution { width = 1366, height = 768 } };

			CollectionAssert.AreEqual(Presets, GameSettings.ResolutionChoices(modes, new Vector2Int(1920, 1080), Presets));
		}

		[Test]
		public void ResolutionChoices_TakesReportedSizesOnAnotherShape()
		{
			var modes = new[]
			{
				new Resolution { width = 800, height = 1280 }, new Resolution { width = 720, height = 1152 },
				new Resolution { width = 500, height = 800 }, new Resolution { width = 600, height = 800 },
			};

			CollectionAssert.AreEqual(new[] { new Vector2Int(1280, 800), new Vector2Int(1152, 720) },
				GameSettings.ResolutionChoices(modes, new Vector2Int(1280, 800), Presets));
		}

		[Test]
		public void ResolutionChoices_FallsBackToThePresetsWhenTooFewAreReported()
		{
			var modes = new[] { new Resolution { width = 800, height = 1280 } };

			CollectionAssert.AreEqual(new[] { new Vector2Int(1280, 720), new Vector2Int(1280, 800) },
				GameSettings.ResolutionChoices(modes, new Vector2Int(1280, 800), Presets));
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
