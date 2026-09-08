using Game.Runtime.GameMode.Poker.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// The running half of a hallucination: one object per rung, carrying every field the effect mutates.
	// Effects stack, so several of these are alive at once and each has to be able to take itself down
	// without disturbing the others — which is exactly what a GameObject's own lifetime already gives.
	//
	// Stop is the deliberate way down and OnDestroy is the backstop, so a player leaving the table undoes
	// what they were seeing without the caller having to know which path it is on.
	public abstract class PokerHallucinationEffectBehaviour : MonoBehaviour
	{
		private bool _running;
		private bool _disposing;

		public PokerPlayer Viewer { get; private set; }

		// What this is, in the words the asset was named with. Taken at Begin rather than read off the config
		// later, so a readout can still say what came off after the clone is gone.
		public string DisplayName { get; private set; }

		// An effect that eases out needs its host to outlive the moment it was taken off, or the tween is
		// killed with the object carrying it and the world snaps back.
		protected virtual float LingerSeconds => 0f;

		public void Begin(PokerHallucinationEffect config, PokerPlayer viewer)
		{
			if (_running) return;

			_running = true;
			Viewer = viewer;
			DisplayName = config ? config.name : string.Empty;

			OnBegin(config);
		}

		public void Stop()
		{
			if (!_running) return;

			_running = false;

			OnEnd();

			// Already on the way out, so there is nothing left to schedule.
			if (_disposing) return;

			if (LingerSeconds > 0f) Destroy(gameObject, LingerSeconds);
			else Destroy(gameObject);
		}

		// One OnDestroy for the whole hierarchy: a Unity message redeclared in a subclass hides the base
		// one and the backstop is silently gone, so the hook is what a subclass extends.
		private void OnDestroy()
		{
			_disposing = true;

			Stop();
			OnDisposed();
		}

		protected abstract void OnBegin(PokerHallucinationEffect config);
		protected abstract void OnEnd();

		// Whatever an ease-out has not finished by the time the object goes. Put it back outright here.
		protected virtual void OnDisposed() { }
	}

	public abstract class PokerHallucinationEffectBehaviour<TConfig> : PokerHallucinationEffectBehaviour
		where TConfig : PokerHallucinationEffect
	{
		protected TConfig Config { get; private set; }

		protected sealed override void OnBegin(PokerHallucinationEffect config)
		{
			// A plain cast: Attach and the config come off the same asset, so a mismatch is a wiring error
			// worth hearing about rather than one to swallow.
			Config = (TConfig)config;

			OnBegin();
		}

		protected abstract void OnBegin();
	}
}
