using System;
using UnityEngine;

namespace Game.Runtime.GameMode.Config
{
	public class MatchConfigFloat : MatchConfigEntry
	{
		private readonly Func<float> _get;
		private readonly Action<float> _set;

		public MatchConfigFloat(string sourceId, string sectionLabel, string key, string label,
			float min, float max, float step, Func<float> get, Action<float> set, string displayFormat = "0.##")
			: base(sourceId, sectionLabel, key, label)
		{
			Min = min;
			Max = max;
			Step = Mathf.Max(0.01f, step);
			DisplayFormat = displayFormat;
			_get = get;
			_set = set;
		}

		public float Min { get; }

		public float Max { get; }

		public float Step { get; }

		public string DisplayFormat { get; }

		public override float ReadValue() => _get();

		public override float ClampValue(float value) => Mathf.Clamp(value, Min, Max);

		public override void ApplyValue(float value) => _set(Mathf.Clamp(value, Min, Max));
	}
}
