using System;

namespace Game.Runtime.GameMode.Config
{
	public class MatchConfigBool : MatchConfigEntry
	{
		private readonly Func<bool> _get;
		private readonly Action<bool> _set;

		public MatchConfigBool(string sourceId, string sectionLabel, string key, string label,
			Func<bool> get, Action<bool> set)
			: base(sourceId, sectionLabel, key, label)
		{
			_get = get;
			_set = set;
		}

		public override float ReadValue() => _get() ? 1f : 0f;

		public override float ClampValue(float value) => value >= 0.5f ? 1f : 0f;

		public override void ApplyValue(float value) => _set(value >= 0.5f);
	}
}
