using Game.Runtime.GameMode.Poker;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The helper's rule set page: shows the running mode's own rules, a prefab the mode carries
	// (PokerGameMode.RulesetPrefab), so each mode explains itself and the helper names no mode.
	public class UIPokerRuleset : MonoBehaviour
	{
		[Tooltip("Where the mode's rule set is placed.")]
		[SerializeField] private RectTransform _container;

		private GameObject _shownPrefab;
		private GameObject _instance;

		private void Start()
		{
			PokerGameMode.OnInstanceChanged += HandleGameModeChanged;
			Show(PokerGameMode.Instance);
		}

		private void OnDestroy() => PokerGameMode.OnInstanceChanged -= HandleGameModeChanged;

		private void HandleGameModeChanged(PokerGameMode mode) => Show(mode);

		private void Show(PokerGameMode mode)
		{
			var prefab = mode ? mode.RulesetPrefab : null;
			if (prefab == _shownPrefab) return;

			if (_instance) Destroy(_instance);

			_shownPrefab = prefab;
			_instance = prefab ? Instantiate(prefab, _container ? _container : (RectTransform)transform) : null;
			if (!_instance) return;

			foreach (var ruleArgs in _instance.GetComponentsInChildren<UIPokerRuleArgs>(true)) ruleArgs.Initialize(mode);
		}
	}
}
