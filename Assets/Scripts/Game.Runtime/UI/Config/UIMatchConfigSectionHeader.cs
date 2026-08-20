using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.Config
{
	public class UIMatchConfigSectionHeader : MonoBehaviour
	{
		[SerializeField] private TextMeshProUGUI _label;

		public void SetLabel(string label)
		{
			if (_label) _label.text = label;
		}
	}
}
