using Game.Runtime.GameMode.Poker.Visual;
using Game.Runtime.UI.Button;
using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// Stands the shared board up on end so it can be read, and lays it back down. Only this screen's board
	// moves. Shown only while there is a board, so a table that deals none never offers it.
	//
	// Hides its content child rather than itself, or it would stop hearing the next board arrive.
	public class UIPokerBoardStandButton : MonoBehaviour
	{
		[SerializeField] private GameObject _content;
		[SerializeField] private UIButton _button;
		[SerializeField] private TMP_Text _label;

		[SerializeField] private string _standLabel = "[VIEW BOARD]";
		[SerializeField] private string _layLabel = "[LAY BOARD]";

		private PokerBoardVisual _board;

		private void OnEnable()
		{
			PokerBoardVisual.OnInstanceChanged += Bind;
			if (_button) _button.OnClick += HandleClick;

			Bind(PokerBoardVisual.Instance);
		}

		private void OnDisable()
		{
			if (_button) _button.OnClick -= HandleClick;
			PokerBoardVisual.OnInstanceChanged -= Bind;

			Bind(null);
		}

		private void Bind(PokerBoardVisual board)
		{
			if (_board)
			{
				_board.OnStandingChanged -= Refresh;
				_board.OnCardsChanged -= Refresh;
			}

			_board = board;

			if (_board)
			{
				_board.OnCardsChanged += Refresh;
				_board.OnStandingChanged += Refresh;
			}

			Refresh();
		}

		private void HandleClick()
		{
			if (_board) _board.SetStanding(!_board.IsStanding);
		}

		private void Refresh()
		{
			var show = _board && _board.HasCards;
			if (_content && _content.activeSelf != show) _content.SetActive(show);

			if (_label) _label.text = _board && _board.IsStanding ? _layLabel : _standLabel;
		}
	}
}
