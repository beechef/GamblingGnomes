using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// One usable card: what it is called, what it costs, how the table hears of it, and what it does. The
	// asset is config only — whatever a use has to remember lives on the table (PokerItemModule) or on the
	// player it touched, never here, because every mode that lists this asset shares it.
	//
	// The gates every item shares (your turn, one per street, in your hand) are the module's; an item only
	// adds the reasons that are its own.
	public abstract class PokerItem : ScriptableObject
	{
		[Header("Identity")]
		[Tooltip("What replicates. Must be unique within a database.")]
		[SerializeField] private PokerItemType _type;

		[SerializeField] private string _displayName;

		[TextArea(2, 5)]
		[SerializeField] private string _description;

		[SerializeField] private Sprite _icon;

		[Header("Cost")]
		[Tooltip("Hallucination the user takes on the moment the item is used, on top of whatever it does.")]
		[MinValue(0)]
		[SerializeField] private int _hallucinationCost;

		[Header("Notice")]
		[Tooltip("What the whole table reads after the user's name, e.g. \"COUNTED THE DECK\". An item aimed at somebody shows their name after it.")]
		[SerializeField] private string _noticeVerb;

		public PokerItemType Type => _type;
		public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
		public string Description => _description;
		public Sprite Icon => _icon;
		public int HallucinationCost => Mathf.Max(0, _hallucinationCost);
		public string NoticeVerb => string.IsNullOrEmpty(_noticeVerb) ? $"USED {DisplayName.ToUpperInvariant()}" : _noticeVerb;

		public PokerItemAvailability GetAvailability(in PokerItemContext context) => OnGetAvailability(context);

		public void UseServer(in PokerItemContext context) => OnUseServer(context);

		protected virtual PokerItemAvailability OnGetAvailability(in PokerItemContext context) => PokerItemAvailability.Usable;

		protected abstract void OnUseServer(in PokerItemContext context);
	}
}
