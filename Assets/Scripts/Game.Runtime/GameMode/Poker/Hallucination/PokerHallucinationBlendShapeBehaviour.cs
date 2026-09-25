using System.Collections.Generic;
using DG.Tweening;
using Game.Runtime.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	public class PokerHallucinationBlendShapeBehaviour : PokerHallucinationEffectBehaviour<PokerHallucinationBlendShapeEffect>
	{
		// The modifiers this effect holds, one per body it reached. Held rather than looked up: the tween
		// pushes every frame, and both a FindOnBody and a search by handle in that path are the lookups the
		// no-polling rule exists to keep out of it.
		private readonly List<PlayerBlendShapeModifier> _bound = new();
		private readonly List<PlayerBlendShapeController> _bodies = new();
		private readonly List<Transform> _resolved = new();

		private Tween _tween;
		private float _progress;

		// The shape going back down is the whole reason a rung coming off is watchable, so the host waits
		// it out.
		protected override float LingerSeconds => EaseDuration;

		protected override void OnBegin()
		{
			Config.Target?.Subscribe(Viewer, Reapply);

			Reapply();

			_tween?.Kill();
			_tween = DOVirtual.Float(0f, 1f, EaseDuration, value =>
				{
					_progress = value;
					Push();
				})
				.SetEase(Config.Ease)
				.SetUpdate(true);
		}

		protected override void OnEnd()
		{
			Config.Target?.Unsubscribe(Viewer, Reapply);

			_tween?.Kill();
			_tween = DOVirtual.Float(_progress, 0f, EaseDuration, value =>
				{
					_progress = value;
					Push();
				})
				.SetEase(Config.Ease)
				.SetUpdate(true)
				.OnComplete(Release);
		}

		// A shape left part-way through going down is a body that never comes home.
		protected override void OnDisposed()
		{
			_tween?.Kill();
			Release();
		}

		// Re-resolved rather than resolved once: a player who sits down after this began has a body that
		// should be growing too, and the target is what knows when that happened.
		private void Reapply()
		{
			Release();

			if (!Config || !Config.Target || string.IsNullOrEmpty(Config.Shape)) return;

			// Only a target that hands over bodies. A card lies under the player holding it, so a walk up
			// from one finds that player's body just as surely as a walk up from a head does — and a shape
			// aimed at the cards would reshape the gnome instead.
			if (!Config.Target.ResolvesBodies)
			{
				Debug.LogWarning($"[{name}] '{Config.Target.name}' does not resolve bodies, and a blend shape is something only a body carries.", this);
				return;
			}

			Config.Target.Collect(Viewer, _resolved);

			foreach (var found in _resolved)
			{
				if (!found) continue;

				// Up to the body and then down, never straight up: the controller sits on a named child of
				// the player root while a bone is off under the rig, so a walk up from one passes the root
				// and finds nothing.
				var controller = PlayerRigController.FindOnBody<PlayerBlendShapeController>(found);

				if (!controller)
				{
					Debug.LogWarning($"[{name}] '{found.name}' belongs to no body carrying a {nameof(PlayerBlendShapeController)}; nothing will be reshaped.", found);
					continue;
				}

				if (_bodies.Contains(controller)) continue;

				_bodies.Add(controller);
				// Added at zero: a modifier that exists but has not grown yet must leave the body exactly as
				// it was, or every effect would jump to full strength the instant it resolved.
				_bound.Add(controller.Add(Config.Shape));
			}

			Push();
		}

		// A field write per body. Nothing is searched for and nothing is allocated, which is what the tween
		// calling this every frame wants.
		private void Push()
		{
			var weight = Mathf.Lerp(0f, Config.Weight, _progress);

			foreach (var modifier in _bound)
			{
				if (modifier != null) modifier.Weight = weight;
			}
		}

		private void Release()
		{
			foreach (var modifier in _bound)
			{
				modifier?.Remove();
			}

			_bound.Clear();
			_bodies.Clear();
			_resolved.Clear();
		}
	}
}
