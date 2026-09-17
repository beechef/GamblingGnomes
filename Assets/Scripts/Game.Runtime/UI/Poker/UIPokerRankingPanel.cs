using System.Collections.Generic;
using DG.Tweening;
using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The board that goes up when the hand is settled. It mirrors the table's showdown list rather than
	// working the ranking out for itself — the server already decided who placed where, and two clients
	// disagreeing about that would be worse than a frame of lag.
	//
	// The first place is its own prefab — a crown, the name large over a soft oval, the hand fanned out —
	// and every place after it a plain row, all stacked by the container's vertical layout with the
	// winner on top. Both are the same view with a different face, so everything the row knows about
	// keeping a hand that is already over applies to the winner too.
	//
	// It arrives in beats: the winner's crown and name pop, their hand fans open from the left, then the
	// other places rise one after another. What each place's arrival looks like is its own
	// UIPokerRankingEntryVisual; the board only lays the beats on one timeline. It leaves by fading once
	// the showdown's clock has run out and the list is cleared.
	public class UIPokerRankingPanel : UIPokerView
	{
		[Header("Panel")]
		[SerializeField] private GameObject _panel;

		[Tooltip("Faded out when the showdown ends. On the panel.")]
		[SerializeField] private CanvasGroup _panelGroup;

		[Header("Rows")]
		[Tooltip("The first place (UI_PokerRankingWinner).")]
		[SerializeField] private UIPokerRankingRow _winnerPrefab;

		[Tooltip("Every other place (UI_PokerRankingRow), one per entry — a table of two and a table of eight both fit.")]
		[SerializeField] private UIPokerRankingRow _rowPrefab;

		[Tooltip("Laid out by its own vertical layout group, so this only has to add and remove children.")]
		[SerializeField] private RectTransform _rowContainer;

		[Header("Reveal")]
		[Tooltip("Wait after the winner has finished arriving before the first row rises.")]
		[MinValue(0f)]
		[SerializeField] private float _rowsDelay = 0.15f;

		[Tooltip("Between one row starting to rise and the next.")]
		[MinValue(0f)]
		[SerializeField] private float _rowStagger = 0.15f;

		[MinValue(0f)]
		[SerializeField] private float _fadeOutDuration = 0.4f;

		[SerializeField] private Ease _fadeOutEase = Ease.OutQuad;

		private readonly List<UIPokerRankingRow> _rows = new();
		private readonly List<UIPokerRankingEntryVisual> _rowVisuals = new();

		private UIPokerRankingRow _winner;
		private UIPokerRankingEntryVisual _winnerVisual;

		private bool _shown;
		private Sequence _reveal;
		private Tween _fade;

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnBind()
		{
			Data.OnShowdownChanged += Refresh;

			Refresh();
		}

		protected override void OnUnbind()
		{
			Data.OnShowdownChanged -= Refresh;

			KillTweens();
			_shown = false;

			if (_panel) _panel.SetActive(false);
		}

		private void OnDestroy() => KillTweens();

		private void Refresh()
		{
			var showdown = Data.Showdown;

			// The board is up for exactly as long as the showdown is: it goes away because the stage clock
			// ran out and cleared the list, not because anybody dismissed it.
			if (showdown.Count == 0)
			{
				Hide();
				return;
			}

			var appearing = !_shown;
			_shown = true;

			_fade?.Kill();
			if (_panelGroup) _panelGroup.alpha = 1f;
			if (_panel && !_panel.activeSelf) _panel.SetActive(true);

			RefreshWinner();
			RefreshRows(showdown.Count - 1, appearing);

			if (appearing) PlayReveal(showdown.Count - 1);
		}

		// Views already made are re-bound rather than rebuilt, so a board refreshed on the reveal landing
		// never draws a frame of both.
		private void RefreshWinner()
		{
			if (!_winnerPrefab || !_rowContainer) return;

			if (!_winner)
			{
				_winner = Instantiate(_winnerPrefab, _rowContainer);
				_winnerVisual = _winner.GetComponent<UIPokerRankingEntryVisual>();
			}

			_winner.transform.SetAsFirstSibling();

			var entry = Data.Showdown[0];
			_winner.SetEntry(entry, PokerPlayer.Find(entry.ClientId));
		}

		private void RefreshRows(int count, bool appearing)
		{
			if (!_rowPrefab || !_rowContainer) return;

			while (_rows.Count < count)
			{
				var row = Instantiate(_rowPrefab, _rowContainer);
				row.gameObject.SetActive(false);

				_rows.Add(row);
				_rowVisuals.Add(row.GetComponent<UIPokerRankingEntryVisual>());
			}

			for (var i = 0; i < _rows.Count; i++)
			{
				var used = i < count;
				var arriving = used && !_rows[i].gameObject.activeSelf;

				if (_rows[i].gameObject.activeSelf != used) _rows[i].gameObject.SetActive(used);
				if (!used) continue;

				// A place landing after the board has already arrived is simply there; the reveal is for the
				// moment the board goes up.
				if (arriving && !appearing && _rowVisuals[i]) _rowVisuals[i].ShowAtRest();

				var entry = Data.Showdown[i + 1];
				_rows[i].SetEntry(entry, PokerPlayer.Find(entry.ClientId));
			}
		}

		private void PlayReveal(int rowCount)
		{
			KillTweens();

			if (_winnerVisual) _winnerVisual.Conceal();
			for (var i = 0; i < rowCount && i < _rowVisuals.Count; i++)
				if (_rowVisuals[i]) _rowVisuals[i].Conceal();

			_reveal = DOTween.Sequence().SetLink(gameObject);

			var rowsAt = 0f;
			if (_winnerVisual)
			{
				var winner = _winnerVisual.Reveal();
				_reveal.Insert(0f, winner);
				rowsAt = winner.Duration() + _rowsDelay;
			}

			for (var i = 0; i < rowCount && i < _rowVisuals.Count; i++)
			{
				if (_rowVisuals[i]) _reveal.Insert(rowsAt + i * _rowStagger, _rowVisuals[i].Reveal());
			}
		}

		// Faded rather than switched off, and switched off only once the fade is done, since a switched-off
		// object draws no fade. The rows keep their own copy of the hand, so the board still reads correctly
		// while it goes.
		private void Hide()
		{
			if (!_shown)
			{
				if (_panel && _panel.activeSelf && (_fade == null || !_fade.IsActive())) _panel.SetActive(false);
				return;
			}

			_shown = false;
			_reveal?.Kill();

			if (!_panelGroup || _fadeOutDuration <= 0f || !_panel || !_panel.activeInHierarchy)
			{
				if (_panel) _panel.SetActive(false);
				return;
			}

			_fade?.Kill();
			_fade = DOTween.To(() => _panelGroup.alpha, value => _panelGroup.alpha = value, 0f, _fadeOutDuration)
				.SetEase(_fadeOutEase)
				.SetLink(gameObject)
				.OnComplete(() =>
				{
					if (_panel) _panel.SetActive(false);
					if (_panelGroup) _panelGroup.alpha = 1f;
				});
		}

		private void KillTweens()
		{
			_reveal?.Kill();
			_reveal = null;

			_fade?.Kill();
			_fade = null;
		}
	}
}
