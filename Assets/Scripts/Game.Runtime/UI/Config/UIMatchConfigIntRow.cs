using Game.Runtime.GameMode.Config;
using UnityEngine;

namespace Game.Runtime.UI.Config
{
	public class UIMatchConfigIntRow : UIMatchConfigStepperRow
	{
		private MatchConfigInt Config => Entry as MatchConfigInt;

		protected override float StepSize => Config?.Step ?? 1;

		protected override float RangeMin => Config?.Min ?? 0;

		protected override float RangeMax => Config?.Max ?? 0;

		protected override string FormatValue(float value) => Mathf.RoundToInt(value).ToString();
	}
}
