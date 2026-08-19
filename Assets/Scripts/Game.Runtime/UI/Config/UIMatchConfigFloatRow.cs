using Game.Runtime.GameMode.Config;

namespace Game.Runtime.UI.Config
{
	public class UIMatchConfigFloatRow : UIMatchConfigStepperRow
	{
		private MatchConfigFloat Config => Entry as MatchConfigFloat;

		protected override float StepSize => Config?.Step ?? 1f;

		protected override float RangeMin => Config?.Min ?? 0f;

		protected override float RangeMax => Config?.Max ?? 0f;

		protected override string FormatValue(float value) => value.ToString(Config?.DisplayFormat ?? "0.##");
	}
}
