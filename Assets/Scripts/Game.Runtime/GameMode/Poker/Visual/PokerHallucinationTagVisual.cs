using Game.Runtime.GameMode.Poker.Player;
using TMPro;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// How far gone somebody is, over their head where the table can read it. Rides the name tag's own
	// canvas rather than bringing a second one: that canvas already turns to face the viewer, already
	// hides at range and already hides for the owner, and none of that is worth writing twice. Poker
	// lives here rather than on PlayerNameTagVisual, which knows nothing of mushrooms.
	public class PokerHallucinationTagVisual : MonoBehaviour
	{
		[Header("References")]
		[SerializeField] private PokerPlayerData _data;
		[SerializeField] private TextMeshProUGUI _label;

		[Header("Colours")]
		[SerializeField] private Color _steadyColor = Color.white;
		[SerializeField] private Color _goneColor = new(1f, 0.35f, 0.3f);

		private void Awake()
		{
			if (!_data) _data = GetComponentInParent<PokerPlayerData>();
		}

		private void OnEnable()
		{
			if (_data) _data.OnHallucinationChanged += HandleChanged;

			Refresh();
		}

		private void OnDisable()
		{
			if (_data) _data.OnHallucinationChanged -= HandleChanged;
		}

		private void HandleChanged(int previous, int current) => Refresh();

		// Read rather than waited for: a client arriving at a table already halfway under would
		// otherwise show nothing until the next mouthful.
		private void Refresh()
		{
			if (!_data || !_label) return;

			var rate = _data.HallucinationRate.Value;

			_label.text = $"{rate}%";
			_label.color = Color.Lerp(_steadyColor, _goneColor, rate / (float)PokerPlayerData.MaxHallucination);
		}
	}
}
