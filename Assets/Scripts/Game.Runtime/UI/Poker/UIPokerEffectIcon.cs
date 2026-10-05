using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Poker
{
	// One effect in a player's effect row: its icon on the shared round backing.
	public class UIPokerEffectIcon : MonoBehaviour
	{
		[SerializeField] private Image _icon;

		public void Bind(Sprite icon)
		{
			if (_icon) _icon.sprite = icon;
		}
	}
}
