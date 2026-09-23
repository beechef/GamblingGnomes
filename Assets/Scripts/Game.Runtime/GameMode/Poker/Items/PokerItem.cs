using System.Collections.Generic;
using System.Threading;
using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// One usable card: what it is called, what it costs, how the table hears of it, what it asks its user to
	// point at, and what it does. The asset is config only — whatever a use has to remember lives on the
	// table (PokerItemModule) or on the players it touched, never here, because every mode that lists this
	// asset shares it.
	//
	// The gates every item shares (your turn, one per street, in your hand) are the module's; an item only
	// adds the reasons that are its own. What may be pointed at is asked through the same Accepts* calls on
	// the user's screen and on the server, so nothing can be picked that the server would then refuse.
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

		[Tooltip("What the player an item was used on reads after the user's name, told to them alone. {0} is the card. Empty tells them nothing beyond the public notice.")]
		[SerializeField] private string _privateNoticeVerb;

		public PokerItemType Type => _type;
		public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
		public string Description => _description;
		public Sprite Icon => _icon;
		public int HallucinationCost => Mathf.Max(0, _hallucinationCost);
		public string NoticeVerb => string.IsNullOrEmpty(_noticeVerb) ? $"USED {DisplayName.ToUpperInvariant()}" : _noticeVerb;
		public string PrivateNoticeVerb => _privateNoticeVerb;

		// Whether playing it makes another player answer, which the turn clock has to leave room for.
		public virtual bool NeedsResponse => false;

		public PokerItemAvailability GetAvailability(in PokerItemContext context) => OnGetAvailability(context);

		// What the user points at, in order, before the item is sent.
		public void CollectTargetSteps(List<PokerItemTargetKind> steps)
		{
			steps.Clear();
			OnCollectTargetSteps(steps);
		}

		public virtual string GetTargetPrompt(PokerItemTargetKind kind) => kind switch
		{
			PokerItemTargetKind.Player => "POINT AT A PLAYER",
			PokerItemTargetKind.OpponentCard => "POINT AT ANOTHER PLAYER'S CARD",
			PokerItemTargetKind.OwnCard => "POINT AT ONE OF YOUR CARDS",
			_ => "POINT AT A FACE-DOWN BOARD CARD"
		};

		// What the player this item asks to answer reads, after the user's name.
		public virtual string GetResponsePrompt() => "POINT AT ONE OF YOUR CARDS";

		public virtual bool AcceptsPlayer(in PokerItemContext context, PokerPlayer target) => false;
		public virtual bool AcceptsOpponentCard(in PokerItemContext context, PokerPlayer target, int slot) => false;
		public virtual bool AcceptsOwnCard(in PokerItemContext context, int slot) => false;
		public virtual bool AcceptsBoardCard(in PokerItemContext context, int slot) => false;

		// Which of their own cards the player being answered for may put forward.
		public virtual bool AcceptsResponseCard(in PokerItemContext context, PokerPlayer responder, int slot) => false;

		// The server's check that what arrived is what the steps allow — the same Accepts* the pointer used.
		public bool IsValidRequest(in PokerItemContext context, in PokerItemUseRequest request)
		{
			var steps = new List<PokerItemTargetKind>();
			CollectTargetSteps(steps);

			var target = context.GameMode ? context.GameMode.FindSeatedPlayerAtSeat(request.TargetSeat) : null;

			foreach (var step in steps)
			{
				var ok = step switch
				{
					PokerItemTargetKind.Player => target && AcceptsPlayer(context, target),
					PokerItemTargetKind.OpponentCard => target && request.HasCard && AcceptsOpponentCard(context, target, request.CardSlot),
					PokerItemTargetKind.OwnCard => request.HasOwnCard && AcceptsOwnCard(context, request.OwnSlot),
					_ => request.HasCard && AcceptsBoardCard(context, request.CardSlot)
				};

				if (!ok) return false;
			}

			return true;
		}

		public Awaitable UseServerAsync(PokerItemContext context, PokerItemUseRequest request, CancellationToken ct) =>
			OnUseServerAsync(context, request, ct);

		protected virtual PokerItemAvailability OnGetAvailability(in PokerItemContext context) => PokerItemAvailability.Usable;

		protected virtual void OnCollectTargetSteps(List<PokerItemTargetKind> steps) { }

		// Most items finish on the spot; one that waits for somebody to answer overrides the async form.
		protected virtual void OnUseServer(in PokerItemContext context, in PokerItemUseRequest request) { }

		protected virtual Awaitable OnUseServerAsync(PokerItemContext context, PokerItemUseRequest request, CancellationToken ct)
		{
			OnUseServer(context, request);

			var done = new AwaitableCompletionSource();
			done.SetResult();
			return done.Awaitable;
		}
	}
}
