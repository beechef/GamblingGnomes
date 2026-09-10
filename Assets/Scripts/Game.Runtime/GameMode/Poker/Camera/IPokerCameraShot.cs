namespace Game.Runtime.GameMode.Poker.Camera
{
	// What a camera state on the player answers to. Each state declares its own shot rather than the
	// controller holding a table of (shot, state) pairs — a table is a list somebody has to keep in step,
	// and a row left out is a shot that silently never runs. Adding a shot is a child object.
	public interface IPokerCameraShot
	{
		PokerCameraShot Shot { get; }
	}
}
