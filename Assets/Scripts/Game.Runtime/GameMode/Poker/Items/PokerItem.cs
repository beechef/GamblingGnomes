using System.Collections.Generic;
using System.Threading;
using Game.Runtime.GameMode.Poker.Player;
using Localization;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

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

		[LocalizationKey]
		[FormerlySerializedAs("_displayName")]
		[SerializeField] private string _nameKey;

		[LocalizationKey]
		[FormerlySerializedAs("_description")]
		[SerializeField] private string _descriptionKey;

		[SerializeField] private Sprite _icon;

		[Header("Cost")]
		[Tooltip("Hallucination the user takes on the moment the item is used, on top of whatever it does.")]
		[MinValue(0)]
		[SerializeField] private int _hallucinationCost;

		[Header("Notice")]
		[Tooltip("What the whole table reads after the user's name, e.g. \"COUNTED THE DECK\". An item aimed at somebody shows their name after it. Empty reads \"USED <name>\".")]
		[LocalizationKey]
		[FormerlySerializedAs("_noticeVerb")]
		[SerializeField] private string _noticeKey;

		[Tooltip("What the player an item was used on reads after the user's name, told to them alone. {0} is the card. Empty tells them nothing beyond the public notice.")]
		[LocalizationKey]
		[FormerlySerializedAs("_privateNoticeVerb")]
		[SerializeField] private string _privateNoticeKey;

		[Header("Pacing")]
		[Tooltip("Seconds between the item being played and it acting: the server waits this long, and the intro of its PokerItemPerformance is stretched to arrive exactly then — the spatula at the card, the knife on the target.")]
		[MinValue(0f)]
		[SerializeField] private float _leadIn;

		public PokerItemType Type => _type;
		public float LeadIn => Mathf.Max(0f, _leadIn);
		public string DisplayName => string.IsNullOrEmpty(_nameKey) ? name : Localizer.Get(_nameKey);
		public string Description => Localizer.Get(_descriptionKey);
		public Sprite Icon => _icon;
		public int HallucinationCost => Mathf.Max(0, _hallucinationCost);
		public string NoticeVerb => string.IsNullOrEmpty(_noticeKey)
			? Localizer.Format(LocalizationKeys.Poker.Notice.ItemUsed, DisplayName.ToUpperInvariant())
			: Localizer.Get(_noticeKey);

		public string PrivateNoticeVerb => string.IsNullOrEmpty(_privateNoticeKey) ? null : Localizer.Get(_privateNoticeKey);

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
			PokerItemTargetKind.Player => Localizer.Get(LocalizationKeys.Item.Prompt.Player),
			PokerItemTargetKind.OpponentCard => Localizer.Get(LocalizationKeys.Item.Prompt.OpponentCard),
			PokerItemTargetKind.OwnCard => Localizer.Get(LocalizationKeys.Item.Prompt.OwnCard),
			_ => Localizer.Get(LocalizationKeys.Item.Prompt.BoardCard)
		};

		// What the table reads after the user's name once a chance-driven item has come out; null tells nothing.
		public virtual string GetOutcomeVerb(int outcome) => null;

		// On, the item tells the table its outcome itself, and that one notice is all the table hears of it:
		// the module says nothing when it is played.
		public virtual bool AnnouncesOutcome => false;

		// What the player this item asks to answer reads, after the user's name.
		public virtual string GetResponsePrompt() => Localizer.Get(LocalizationKeys.Item.Prompt.OwnCard);

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
