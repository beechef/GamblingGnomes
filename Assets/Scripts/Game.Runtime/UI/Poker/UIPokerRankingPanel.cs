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
	// A table with community cards gets the other face of the same board (UI_RankingBoardPanel): no winner
	// prefab, a head with the RESULT title and the board the hand ended on, and every place a plain row. The
	// two sit side by side in the HUD and each answers only its kind of table (_forBoardTables).
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

		[Tooltip("On, this board answers only a hand with community cards; off, only one without. Both boards sit in the HUD and exactly one goes up for any showdown.")]
		[SerializeField] private bool _forBoardTables;

		[Header("Head")]
		[Tooltip("Optional, used when there is no winner prefab: what is revealed first, before the rows (the RESULT title and the board).")]
		[SerializeField] private UIPokerRankingEntryVisual _headVisual;

		[Tooltip("Optional. The community cards the hand ended with.")]
		[SerializeField] private UIPokerRankingBoardCards _boardCards;

		[Header("Rows")]
		[Tooltip("Optional. The first place (UI_PokerRankingWinner). Empty draws the winner as a plain row, first among them.")]
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
		private Tween _fade;

		// The reveal is a timeline every place is laid on by its index, not one sequence built when the board
		// goes up: the server adds the showdown one entry at a time and each add is its own change, so when
		// the board first appears only the winner is in the list. A row arriving a moment later still takes
		// its own slot on the timeline instead of being snapped into place.
		private Sequence _winnerReveal;
		private readonly List<Sequence> _rowReveals = new();
		private float _revealStartedAt = -1f;
		private float _rowsAt;

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

			// Decided when the board goes up and kept until it comes down: the other board answers the other kind
			// of table.
			if (!_shown && Data.CommunityCards.Count > 0 != _forBoardTables) return;

			var appearing = !_shown;
			_shown = true;

			_fade?.Kill();
			if (_panelGroup) _panelGroup.alpha = 1f;
			if (_panel && !_panel.activeSelf) _panel.SetActive(true);

			if (_boardCards) _boardCards.Draw(Data);

			RefreshWinner();
			if (appearing) PlayHeadReveal();

			RefreshRows(showdown.Count - FirstRowEntry, appearing);
		}

		// With a winner face, the first entry is theirs and the rows start at the second.
		private int FirstRowEntry => _winnerPrefab ? 1 : 0;

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
				_rowReveals.Add(null);
			}

			for (var i = 0; i < _rows.Count; i++)
			{
				var used = i < count;

				// Rows kept from the last board are still switched on, so a board going up treats every one of
				// them as arriving.
				var arriving = used && (appearing || !_rows[i].gameObject.activeSelf);

				if (_rows[i].gameObject.activeSelf != used) _rows[i].gameObject.SetActive(used);
				if (!used) continue;

				var entry = Data.Showdown[i + FirstRowEntry];
				_rows[i].SetEntry(entry, PokerPlayer.Find(entry.ClientId));

				if (arriving) RevealRow(i);
			}
		}

		// What arrives first: the winner's face, or the head when the board has none.
		private void PlayHeadReveal()
		{
			KillTweens();

			_revealStartedAt = Time.time;
			_rowsAt = _rowsDelay;

			var visual = _winnerPrefab ? _winnerVisual : _headVisual;
			if (!visual) return;

			visual.Conceal();
			_winnerReveal = visual.Reveal().SetLink(gameObject);
			_rowsAt = _winnerReveal.Duration() + _rowsDelay;
		}

		// Placed on the board's timeline by index. A row whose slot has already started rises at once; one
		// arriving long after the board finished arriving is simply there.
		private void RevealRow(int index)
		{
			var visual = _rowVisuals[index];
			if (!visual) return;

			_rowReveals[index]?.Kill();
			_rowReveals[index] = null;

			var delay = _revealStartedAt < 0f ? float.NegativeInfinity : _revealStartedAt + _rowsAt + index * _rowStagger - Time.time;

			if (delay < -LateRowGrace)
			{
				visual.ShowAtRest();
				return;
			}

			visual.Conceal();
			_rowReveals[index] = DOTween.Sequence()
				.AppendInterval(Mathf.Max(0f, delay))
				.Append(visual.Reveal())
				.SetLink(gameObject);
		}

		private const float LateRowGrace = 1f;

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
			KillReveal();

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

		private void KillReveal()
		{
			_winnerReveal?.Kill();
			_winnerReveal = null;

			for (var i = 0; i < _rowReveals.Count; i++)
			{
				_rowReveals[i]?.Kill();
				_rowReveals[i] = null;
			}

			_revealStartedAt = -1f;
		}

		private void KillTweens()
		{
			KillReveal();

			_fade?.Kill();
			_fade = null;
		}
	}
}
