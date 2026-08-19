using System;
using UnityEngine;

namespace Game.Runtime.GameMode.Config
{
	public class MatchConfigInt : MatchConfigEntry
	{
		private readonly Func<int> _get;
		private readonly Action<int> _set;

		public MatchConfigInt(string sourceId, string sectionLabel, string key, string label,
			int min, int max, int step, Func<int> get, Action<int> set)
			: base(sourceId, sectionLabel, key, label)
		{
			Min = min;
			Max = max;
			Step = Mathf.Max(1, step);
			_get = get;
			_set = set;
		}

		public int Min { get; }

		public int Max { get; }

		public int Step { get; }

		public override float ReadValue() => _get();

		public override float ClampValue(float value) => Mathf.Clamp(Mathf.RoundToInt(value), Min, Max);

		public override void ApplyValue(float value) => _set(Mathf.Clamp(Mathf.RoundToInt(value), Min, Max));
	}
}
