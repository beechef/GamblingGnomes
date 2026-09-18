using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	public class PokerHallucinationGroupBehaviour : PokerHallucinationEffectBehaviour<PokerHallucinationGroupEffect>
	{
		private PokerHallucinationEffectBehaviour _picked;

		// The picked effect hangs under this object, so this one has to stay alive for as long as its ease-out.
		protected override float LingerSeconds => _picked ? _picked.Linger : 0f;

		protected override void OnBegin()
		{
			var members = Config.Members;
			var valid = 0;

			for (var i = 0; i < members.Count; i++)
			{
				if (members[i] && members[i] != Config) valid++;
			}

			if (valid == 0) return;

			var pick = Random.Range(0, valid);

			for (var i = 0; i < members.Count; i++)
			{
				var member = members[i];
				if (!member || member == Config) continue;
				if (pick-- > 0) continue;

				_picked = member.Run(transform, Viewer);
				return;
			}
		}

		protected override void OnEnd()
		{
			if (_picked) _picked.Stop();
		}
	}
}
