using System.Collections.Generic;
using Game.Runtime.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// Finds every body the target names and hands each one's appearance controller to the subclass, which
	// only says what it asks for and how it takes it back.
	public abstract class PokerHallucinationAppearanceBehaviour<TConfig> : PokerHallucinationEffectBehaviour<TConfig>
		where TConfig : PokerHallucinationAppearanceEffect
	{
		private readonly List<Transform> _resolved = new();
		private readonly List<PlayerAppearanceController> _bound = new();

		protected sealed override void OnBegin()
		{
			Config.Target?.Subscribe(Viewer, Reapply);

			Reapply();
		}

		protected sealed override void OnEnd()
		{
			Config.Target?.Unsubscribe(Viewer, Reapply);

			Release();
		}

		protected abstract void Request(PlayerAppearanceController appearance);
		protected abstract void Withdraw(PlayerAppearanceController appearance);

		// Swept again whenever the set moves, so somebody sitting down while this runs is caught up too.
		private void Reapply()
		{
			Release();

			// Only a target that hands over bodies: the walk up from a card finds the player holding it, and
			// that player is not what a card target was aimed at.
			if (!Config || !Config.Target || !Config.Target.ResolvesBodies) return;

			Config.Target.Collect(Viewer, _resolved);

			foreach (var found in _resolved)
			{
				if (!found) continue;

				var appearance = PlayerRigController.FindOnBody<PlayerAppearanceController>(found);

				if (!appearance)
				{
					Debug.LogWarning($"{found.name} belongs to a body with no {nameof(PlayerAppearanceController)}, so how it is drawn cannot change.", found);
					continue;
				}

				if (_bound.Contains(appearance)) continue;

				_bound.Add(appearance);
				Request(appearance);
			}
		}

		private void Release()
		{
			foreach (var appearance in _bound)
			{
				if (appearance) Withdraw(appearance);
			}

			_bound.Clear();
			_resolved.Clear();
		}
	}
}
