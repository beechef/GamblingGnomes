namespace Game.Runtime.GameMode.Config
{
	// One tunable a mode offers its host. Declared by whoever owns the underlying field, wrapping it in
	// getter/setter delegates — the field stays private and serialized, so the authored asset value
	// remains the default and nothing grows a public setter for the panel's sake.
	//
	// Only MatchConfigData ever calls ApplyValue. Both UI surfaces go through a value-access seam, and
	// the pre-scene one builds its entries off prefab assets — applying a value there would write the
	// asset itself.
	public abstract class MatchConfigEntry
	{
		protected MatchConfigEntry(string sourceId, string sectionLabel, string key, string label)
		{
			SourceId = sourceId;
			SectionLabel = sectionLabel;
			Key = key;
			Label = label;
			Id = $"{sourceId}.{key}";
		}

		public string SourceId { get; }

		public string SectionLabel { get; }

		public string Key { get; }

		public string Label { get; }

		public string Id { get; }

		public abstract float ReadValue();

		public abstract float ClampValue(float value);

		public abstract void ApplyValue(float value);
	}
}
