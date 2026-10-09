namespace Game.Runtime.Voice
{
	public readonly struct VoiceDevice
	{
		public readonly string Id;
		public readonly string Name;

		public VoiceDevice(string id, string name)
		{
			Id = id;
			Name = name;
		}
	}
}
