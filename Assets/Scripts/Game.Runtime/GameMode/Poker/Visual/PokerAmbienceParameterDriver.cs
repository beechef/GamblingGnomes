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
		private const float RoomLevel = 0f;
		private const float MatchLevel = 1f;
		private const float EatingLevel = 2f;

		[Required]
		[SerializeField] private MusicTrack _track;

		[FMODUnity.ParamRef]
		[SerializeField] private string _parameter = "Parameter 1";

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
			_track.SetParameter(_parameter, RoomLevel);
		}

		private void HandlePhaseChanged(PokerPhase previous, PokerPhase current)
		{
			if (IsOver(current)) _matchStarted = false;
			else if (current != PokerPhase.Waiting) _matchStarted = true;

			Apply(current);
		}

		private void Apply(PokerPhase phase)
		{
			var level = phase == PokerPhase.Eating ? EatingLevel : _matchStarted ? MatchLevel : RoomLevel;
			_track.SetParameter(_parameter, level);
		}

		private static bool IsOver(PokerPhase phase) => phase is PokerPhase.Finished or PokerPhase.MatchOver;
	}
}
