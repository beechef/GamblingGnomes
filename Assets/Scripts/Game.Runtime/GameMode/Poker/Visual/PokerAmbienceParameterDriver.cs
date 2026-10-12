using Game.Runtime.Audio;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// Moves the gameplay track through the table's beats: 0 in the room before a match starts, 1 once it has,
	// 2 while plates are being eaten and back to 1 after. Sits on the gameplay track it drives, read off the
	// replicated phase on every screen; a late joiner lands on the beat already running.
	public class PokerAmbienceParameterDriver : PokerVisual
	{
		[Required]
		[SerializeField] private MusicTrack _track;

		[FMODUnity.ParamRef]
		[SerializeField] private string _parameter = "Parameter 1";

		[Header("Levels")]
		[Tooltip("In the room before a match starts, and after one ends.")]
		[SerializeField] private float _roomLevel;

		[Tooltip("Once a match has started, between plates.")]
		[SerializeField] private float _matchLevel = 1f;

		[Tooltip("While plates are being eaten.")]
		[SerializeField] private float _eatingLevel = 2f;

		[ShowInInspector, ReadOnly]
		private float _current;

		// The waiting room between hands is still the match; only a match ending sends the track back to 0.
		private bool _matchStarted;

		protected override void OnBind()
		{
			Data.Phase.OnValueChanged += HandlePhaseChanged;

			_matchStarted = Data.Phase.Value != PokerPhase.Waiting && !IsOver(Data.Phase.Value);
			Apply(Data.Phase.Value);
		}

		protected override void OnUnbind()
		{
			if (Data) Data.Phase.OnValueChanged -= HandlePhaseChanged;

			_matchStarted = false;
			SetLevel(_roomLevel);
		}

		private void HandlePhaseChanged(PokerPhase previous, PokerPhase current)
		{
			if (IsOver(current)) _matchStarted = false;
			else if (current != PokerPhase.Waiting) _matchStarted = true;

			Apply(current);
		}

		private void Apply(PokerPhase phase)
		{
			var level = phase == PokerPhase.Eating ? _eatingLevel : _matchStarted ? _matchLevel : _roomLevel;
			SetLevel(level);
		}

		private void SetLevel(float level)
		{
			_current = level;
			_track.SetParameter(_parameter, level);
		}

		private static bool IsOver(PokerPhase phase) => phase is PokerPhase.Finished or PokerPhase.MatchOver;
	}
}
