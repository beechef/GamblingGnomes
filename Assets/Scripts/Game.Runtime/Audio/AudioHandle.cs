namespace Game.Runtime.Audio
{
	// A sound that plays until it is stopped (music, ambience), as AudioManager.Play hands it out. The
	// default value is no sound, so a field can hold one before anything has played.
	public readonly struct AudioHandle
	{
		public readonly int Id;

		public AudioHandle(int id) => Id = id;

		public bool IsValid => Id != 0;
	}
}
