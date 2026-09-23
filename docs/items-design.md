# Items — design

Date: 2026-09-23
Status: agreed, not yet built. Phases below track progress.

Items are one-use cards a player holds and plays during their own betting turn. They are a
`PokerModule`, so a mode has them by listing the module and loses them by removing it. Both current
modes (Normal, Liar's Poker) use them.

## Vocabulary

Renamed in Phase 1 so the whole project uses one set of words.

| Word | Means | Was |
|---|---|---|
| Match | first deal until one conscious player is left | — |
| Hand | one deal through settlement and eating | — |
| **Street** | one betting round inside a Hand (`PokerStreetStage`, `PokerPhase.FirstStreet`…) | Wager stage / round |
| Turn | one player being asked Bet or Fold | — |
| **Bet** | the act of staking caps (`PokerActionType.Bet`, `CanBet`, `IsBettable`, `_stakeSize`) | Wager |
| **BetItem** | a mushroom cap: staked, settled, eaten (`PokerBetItemType`, `PokerBetItemDatabase`…) | `PokerItem*` |
| **PotEntry** | one line of the pot ledger (owner, phase, kind) | `PokerBetItem` |
| **Item** | the usable card this document is about | — |

Renamed alongside: `PokerItemWagerStage` → `PokerStreetStage`, `UIPokerItemWagerBar` → `UIPokerBetBar`, the `ItemRound` configs → `Normal`, `Player_Poker/Items` → `Player_Poker/BetItems`, `UI_MushroomWagerBar` → `UI_BetBar`.

Death Rate means `HallucinationRate`. Death Roll means the existing roll
(`PokerHallucinationRollController`): fatal when `Random.Range(0, 100) < rate`.

## Rules

- **Deal.** At the start of every Hand (after the cards, before the first Street), every player who is
  `InMatch && IsAlive` receives 2 Items. Every player who did not win the previous Hand — folders
  included — receives 1 more. The first Hand of a Match has no previous Hand. The losers of a Hand
  are recorded by the module; the code keeps no such record today.
- **Draw.** Weighted random from the mode's item database, no duplicates within one handout.
- **Inventory.** Capacity 5. A full inventory drops the surplus. Kept across Hands, cleared when the
  Match ends. Kinds are owner-read; the count is public.
- **Use.** At most 1 Item per Street, only during the player's own betting Turn. Using an Item does not
  end the Turn. The Turn clock keeps running.
- **Responses.** An Item that makes another player choose gives them a response clock of their own; if
  it runs out, or the user's Turn ends first, the choice is made at random and the Item still
  resolves. Such an Item is dimmed while the Turn has less time left than the response clock.
- **Forbidden fold at timeout.** Where an Item forbids Fold and the Street's timeout action is Fold,
  the timeout bets instead — a turn on a clock always ends.
- **Death mid-Hand.** A player killed by an Item's roll is treated as folded (`Dead`, cards face down).
  Their stake stays in the pot and settles as a folder's. If they held the Turn, it advances.
- **Cost.** Every Item has `_hallucinationCost` (paid on use), default 0.
- **Choices.** Every card choice inside an Item is a `PokerChoiceMode { Chosen, Random }` field.

## The eleven Items

"This Street" and "next Street" are Streets, never Hands. An Item whose effect lands on the next
Street is hidden on the last Street (the rules forbid it, rather than the player being unable to
afford it).

| Asset | Name | Effect | Modes |
|---|---|---|---|
| `PokerItem_PeekHand` | Peek | See one card of a chosen player. That player is told which card. Choice: target's card = Chosen. | both |
| `PokerItem_PeekBoard` | Scry | See one unrevealed community card; you cannot Fold for the rest of this Street. Choice: slot = Chosen. | Liar |
| `PokerItem_MutualReveal` | Show Together | You and a chosen player each turn one held card face up for the whole table until the Hand ends. Unlooked cards (Normal) may be chosen. Choices: own = Chosen, target's = Chosen by the target. | both |
| `PokerItem_DeckCount` | Suit Count | See how many cards of each suit remain in the undealt deck (snapshot, shown until the Hand ends). | both |
| `PokerItem_SwapHand` | Swap | Exchange one card with a chosen player. Choices: own = Random, target's = Chosen by the target. Both cards land face up to their new owner and count as looked at. | both |
| `PokerItem_SwapBoard` | Board Swap | A random unrevealed community card is turned face up for everyone, then exchanged with one of your cards (so your old card lies face up on the board). Hidden once all five are revealed. Choices: own = Chosen, slot = Random. | Liar |
| `PokerItem_ExtraDraw` | Extra Draw | Draw one card from the undealt deck; you cannot Fold until the Hand ends. Showdown still scores the best five. | both |
| `PokerItem_HalfDose` | Half Dose | Halve your Death Rate (round down), then Death Roll against the new rate. | both |
| `PokerItem_SharedRoll` | Shared Roll | You and a chosen player each Death Roll against your own rate, at the same time. | both |
| `PokerItem_LockFold` | Lock | Nobody may Fold on the next Street. `_affectsAllIn` toggles whether the All-in stage is locked too. | both |
| `PokerItem_RaiseStakes` | Raise | Every Bet on the next Street stakes +1 cap. Stacks. Never applies to All-in. | both |

Default targets: card Items target players still `IsInHand`, not yourself; Shared Roll targets
`InMatch && IsAlive`, not yourself. No valid target dims the Item. All weights start equal.

Mode notes: Normal has no community cards, so Scry and Board Swap are not in its database. Its
first Street already forbids Fold, so Lock only matters used there against the second Street; Raise
likewise.

## Notices

`PokerNoticeChannel` (a `NetworkBehaviour` on the mode) has two channels:

- **Public** — `[Rpc(SendTo.Everyone)]`. RPC rather than a `NetworkVariable`, because two notices in
  one frame reach clients as the last one only.
- **Private** — `[Rpc(SendTo.SpecifiedInParams)]` to the players an Item affects.

A notice is structured (actor, item or action, target, card, count); the feed picks the wording and
the row shape. An Item carries the verb read after the actor's name (`NoticeVerb`, e.g. "COUNTED THE
DECK"); an Item aimed at somebody shows their name after it.
Public notices name the Item. Existing Bet/Fold notices move onto the same channel so there is one
feed. Late joiners do not replay notices; a notice is an event, not state.

Items affecting others notify the affected player in detail and the table in general
("A swapped cards with B"). Items affecting only the user notify the table in general
("A looked at a community card").

## UX

- **Picker.** `ActionMenu` has an **Items** button (dimmed when an Item was already used this Street or
  the inventory is empty). It opens an **ItemPicker** shaped like `BetPicker`, switched with the menu
  through `UIPanelStateGroup`: owned Items, dimmed with a reason when unusable, description of the
  marked one, `Choose` (Enter) to use, Esc back to the menu. After use the Turn returns to the menu.
- **Targeting.** A targeted Item enters pointing mode: point at a player (the Colorful pick mechanism,
  lifted into a shared player-pick) or at a 3D card. Esc / right click cancels via `UIEscapeStack`.
- **Responding.** The affected player gets a panel in the "what is the table waiting on" slot
  (`TurnPanel`'s) with a title and a `UITimerBar`, and picks by pointing at their own 3D card
  (`PokerCardPickController`, extended). Everyone else sees the focus move to them.
- **Privately seen cards** flip face up on the viewer's screen only, until the Hand ends. Visibility
  becomes per card (today it is per whole hand) and gains per-viewer community cards.
- **World tag row.** The hallucination tag over each head gains a bottom row of that player's cards
  this screen knows (peeked or publicly shown). The local meter shows your own exposed cards and who
  saw them.
- **Counts.** `UI_ItemsPanel` (bottom left, above the Helper column) shows how many Items you hold and,
  while you know it, the Suit Count. Over other heads, their count (Phase 3).
- **Deal.** Items fly into the inventory at handout; a public notice names the loser bonus.

## Architecture

- `PokerItemModule : PokerModule`, listed in `_modules` on both mode prefabs. Holds the config
  (database, per-Hand count, loser bonus, capacity, uses per Street, response duration) and the table
  rules currently in force in a `NetworkList` ("no Fold on Street X", "+N per Bet on Street X").
- `PokerItem : ScriptableObject` and a subclass per behaviour: icon, name, description, weight, cost,
  notice templates, target filter, choice modes, `CanUse`, `UseServer` (async, cancellable, run on the
  module).
- `PokerItemInventory : NetworkBehaviour` on `Player_Poker/Items` (the cap components move to
  `Player_Poker/BetItems`).
- Stages never know about Items: `PokerStreetStage.CanFold(player)` and its bet count ask
  `PokerGameMode`, which folds in two new module hooks, `IsActionAllowed(player, action)` and
  `ModifyBetCount(count)`. UI and server read the same methods.
- Swaps write list slots, never clear-and-refill. Card swaps get a crossing flight.
  `RevealedCommunityCards` becomes a bitmask so a random slot can turn.

## Phases

Each phase is one commit, verified in Play mode on host and client before the next.

1. **Rename** (done) — Wager → Street/Bet, mushroom `PokerItem*` → `PokerBetItem*`, `PokerBetItem` →
   `PokerPotEntry`; CLAUDE.md and CONTEXT.md in the same change.
2. **Foundation** (done) — module, inventory, deal, loser record, notice channel, ItemPicker, action hooks;
   Suit Count and Raise to prove the loop.
3. **Targeting and exposure** — shared player/card pick, response panel, per-card visibility, tag row;
   Peek, Scry, Show Together, Lock.
4. **Card exchange** — slot writes, crossing flight, reveal bitmask; Swap, Board Swap, Extra Draw.
5. **Death Roll** — Half Dose, Shared Roll, death mid-Hand.
