using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.VFX;

namespace Game.Runtime.Props
{
	// Puffs smoke now and then while a lit PropBurnOut sits under the holder (a cigarette at the mouth). Each puff
	// plays the effects, then stops them: the graph's OnStop ends spawning and the smoke already out fades on its own.
	public class PropBurnOutSmoke : MonoBehaviour
	{
		[Tooltip("Lit props under this transform make smoke (the jaw bone holding the cigarette copies).")]
		[Required]
		[SerializeField] private Transform _holder;

		[Tooltip("Effects played together on every puff; their objects stay on, idle until played.")]
		[Required]
		[SerializeField] private List<VisualEffect> _effects = new();

		[Tooltip("Seconds between puffs, picked at random between min and max.")]
		[MinMaxSlider(0f, 30f, true)]
		[SerializeField] private Vector2 _interval = new(3f, 8f);

		[Tooltip("Seconds a puff spawns smoke before it is stopped, picked at random between min and max.")]
		[MinMaxSlider(0f, 10f, true)]
		[SerializeField] private Vector2 _duration = new(1f, 2.5f);

		private int _litCount;
		private Tween _tween;

		private void OnEnable()
		{
			PropBurnOut.OnLit += HandlePropLit;
			PropBurnOut.OnBurntOut += HandlePropBurntOut;
		}

		private void OnDisable()
		{
			PropBurnOut.OnBurntOut -= HandlePropBurntOut;
			PropBurnOut.OnLit -= HandlePropLit;
			_litCount = 0;
			_tween?.Kill();
			StopEffects();
		}

		private void HandlePropLit(PropBurnOut prop)
		{
			if (!IsHeld(prop)) return;

			_litCount++;
			if (_litCount == 1) ScheduleNextPuff();
		}

		private void HandlePropBurntOut(PropBurnOut prop)
		{
			if (_litCount == 0 || !IsHeld(prop)) return;

			_litCount--;
			if (_litCount > 0) return;

			_tween?.Kill();
			StopEffects();
		}

		private bool IsHeld(PropBurnOut prop) => _holder && prop && prop.transform.IsChildOf(_holder);

		private void ScheduleNextPuff()
		{
			_tween?.Kill();
			_tween = DOVirtual.DelayedCall(Random.Range(_interval.x, _interval.y), StartPuff, false).SetLink(gameObject);
		}

		private void StartPuff()
		{
			foreach (var effect in _effects)
			{
				if (effect) effect.Play();
			}

			_tween = DOVirtual.DelayedCall(Random.Range(_duration.x, _duration.y), EndPuff, false).SetLink(gameObject);
		}

		private void EndPuff()
		{
			StopEffects();
			ScheduleNextPuff();
		}

		private void StopEffects()
		{
			foreach (var effect in _effects)
			{
				if (effect) effect.Stop();
			}
		}
	}
}
