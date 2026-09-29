# Baccarat Poker — design

Date: 2026-09-28
Status: built. Scoring verified in the editor; mode spawns and the helper reads it in solo Play mode; a two-player match is not yet played.

A fifth mode on `Gameplay_Poker`. It plays like the Normal mode (two Streets around a look, a
Colorful pick at the end of every Hand), but each player holds 2 cards and shares a 3-card board, the
deck carries 4 Jokers, and every Hand deals 2 Items to each player, plus one for surviving a Colorful.
Words follow `CONTEXT.md` and `docs/items-design.md` (Match, Hand, Street, Bet, BetItem, Item).

## Rules

- **Deck.** 56 cards: the 52 plus 4 Jokers. All four Jokers share one picture. A Joker stands for any
  rank and any suit, including a card already in the hand. Other modes keep the 52-card deck.
- **Deal.** 2 hole cards per player, face down on the table, and 3 community cards, face down. Every
  player dealt in antes 1 cap of a random kind (the kinds are worth the same) as the cards go out — no
  turn, nobody decides it.
- **Look.** Right after the deal, both hole cards go into every hand at once. Nothing to choose, so
  nothing to click.
- **Second Street.** The first community card turns, then Bet (1 random cap) or Fold. No All-in. Items
  may be used.
- **Third Street.** The other two community cards turn, then the same Bet or Fold.
- **Reveal.** Every hand still in play turns over. A winner everyone else folded to is still turned
  over, board included.
- **Showdown.** A hand is exactly 5 cards: 2 hole + 3 board (6 with Extra Draw, best five scored).
  Ties all win.
- **Settlement: `OwnStake`.** The winners' caps are discarded. Every loser eats what they staked:
  the ante plus one cap per Street they bet on (up to 3); a folder eats what they had in when they folded.
- **Colorful pick.** The existing `PokerColorfulPickStage` rules: the winner names anyone `InMatch &&
  IsAlive`, themselves included; folders can be named; with several winners the one nearest the hand
  opener picks.
- **Eating.** One shared beat, as in Normal: the pick puts the Colorful cap on the victim's plate, then
  everybody eats their plate. A plate of ordinary caps is one mouthful; the Colorful is its own.
- **Mushrooms.** Every ordinary cap is +10 Death Rate, first taste or not. Colorful is +20, then the
  Death Roll against the new rate.
- **Survivor's Item.** The player named for the Colorful who survives its roll receives 1 random Item
  when the eating beat ends, if still conscious (weighted draw from the mode's database). A full
  inventory drops it and the player alone is told.
  Only the Colorful roll pays out; Half Dose and Shared Roll rolls do not.
- **Match.** Hands repeat until one conscious player is left, then `PokerMatchOverStage`, as in Normal.
- **Turn clocks.** None (`_turnDuration` -1), as in the other modes. Timeout actions are still set so a
  clock can be switched on later: first Street → Bet, second Street → Fold, Colorful pick → self.

## Items

- **Pool.** Thirteen Items in `PokerItemDatabase_Baccarat`, all weight 1. Show Together, Suit Count,
  Lock and Raise are left out of this database only; the other modes keep them.

  | Group | Asset | Name | Effect |
  |---|---|---|---|
  | Information | `PeekBoard` | Scry | See one random face-down board card; you cannot Fold this Street. |
  | Card | `SwapHand` | Swap | Trade one card with a chosen player; they choose which of theirs. |
  | Card | `SwapBoard` | Board Swap | Choose your card; a random board card is shown to everyone, then the two trade places. |
  | Card | `ExtraDraw` | Extra Draw | Draw one more card; you cannot Fold for the rest of the Hand. |
  | Card | `PlaceOnBoard` | Lay Down | Lay a chosen card face up on the board (the board grows by one) and draw a new one in its place. |
  | Card | `RandomSuit` | Repaint | A chosen card keeps its rank and takes a random other suit. |
  | Card | `CopyCard` | Copy | 35% (`_chance`) that a chosen card becomes a copy of another card in your hand. The table hears whether it took. |
  | Death rate | `HalfDose` | Half Dose | Halve your Death Rate, then Death Roll. |
  | Death rate | `SharedRoll` | Shared Roll | You and a chosen player Death Roll together. |
  | Death rate | `MushroomDose` | Spike | A chosen player's next 2 mushrooms (`_caps`) do nothing or hit twice as hard, drawn 50/50 (`_nullifyWeight`/`_doubleWeight`), Colorful roll included. Kept across Hands until eaten; the table is told which. |
  | Disturb | `PeekHand` | Peek | See one card of a chosen player, who is told which. |
  | Disturb | `PairLockItems` | No Items | You and a chosen player cannot play Items on the next Street. |
  | Disturb | `PairLockFold` | Chained | You and a chosen player cannot Fold on the next Street. |

  Joker cards are never offered to Repaint or Copy. Chosen own cards before the look are picked face down.
- **Handout.** 2 random Items to every player in the Match at every deal (`_itemsPerHand` 2), no
  loser bonus. The winner may name themselves for the Colorful to fish for the survivor's Item.
- **Inventory.** Capacity 5, 1 Item per Street, kept across Hands, cleared when the Match ends.
- **Next-Street Items.** No Items and Chained are hidden on the second Street, the last one (existing
  rule: what the rules forbid is hidden).

## Hand ranking

The mode has its own `PokerHandDatabase` (`PokerGameMode._handDatabase`, read by the showdown and the helper): the ten standard hands plus **Five of a Kind** on top.
Normal and Liar keep theirs, so their helper never shows a hand they cannot deal.

- **Jokers in scoring.** Each hand type counts Jokers as wild when it tests itself: N of a kind is
  `count[rank] + jokers ≥ N`, a straight is a 5-rank window missing no more ranks than there are
  Jokers, a flush is `count[suit] + jokers ≥ 5`, a full house fills trips first then the pair. Jokers
  take the highest rank that helps, so kickers are as high as the hand allows. With no Jokers every
  hand scores exactly as it does today.
- **Five of a Kind.** Ranked by its rank (AAAAA beats KKKKK); the same rank ties.
- **Ties.** Hands with Jokers and natural hands tie on equal score; natural is not preferred.

## Hand helper

Follows the mockup: Five of a Kind sits alone on top, crowned and numbered **0**, above the usual two
columns numbered 1 to 10. Its description is its own ("Five cards of the same value. Only Jokers make
it."); the mockup's line was Royal Flush's placeholder. Its example cards are a pair plus Jokers.
Other modes' helpers are unchanged.

## Architecture

- **Mode.** `GameModeType.BaccaratPoker = 5` ("Baccarat Poker"), a `GameModeDatabase` entry pointing at
  `GameMode_PokerBaccarat.prefab`, a variant of `GameMode_Poker` overriding `_rules`, `_sequence`,
  `_betItemDatabase`, `_handDatabase` and the item module's settings. The HUD is Normal's: the bet bar
  hides its kind picker on a Random street by itself. Configs under
  `Configs/Poker/Baccarat/`.
- **Sequence.** Waiting → Deal (hole 2, community 3, `_anteSize` 1) → CardLook (every card into the
  hand) → SecondStreet (turns 1 board card, Random kind, fold) → ThirdStreet (turns the other 2, same
  bet) → CardReveal (`_revealBoard`) →
  Showdown (`OwnStake`, `_revealUncontested`, own hand database) → ColorfulPick (gain 20) → Consume
  (ordinary gain 10/10) → MatchOver. Every named exit is re-pointed at this sequence's own stages.
- **Joker card.** `CardData` gains a Joker value that `IsValid` accepts and `IsJoker` names; its
  sprite is a separate entry on `PokerCardDatabase` and a `joker.png` in the `Cards` atlas folder
  (placeholder until art lands). Every reader of `Rank`/`Suit`/`DatabaseIndex` is audited for it.
- **Deck size.** Jokers per deck is table rule data (`PokerRuleSettings`), read by
  `PokerDeck.Rebuild`.
- **Wild scoring.** `PokerCardAnalysis` counts Jokers apart from ranks and suits (`WildCount`); each
  `PokerHandType` uses it. New `PokerHandFiveOfAKind`. `PokerHandExampleCard` can author a Joker.
- **Look into hand.** `PokerCardLookStage` gains an option to put every viewable card into every hand
  when it opens and move on once they are there, instead of waiting for picks.
- **Item handout.** `PokerItemModule` deals `_itemsPerHand` (2) at every deal as in Normal; the per-Match
  handout `_itemsPerMatch` stays available at 0. It also gains a Colorful-survivor reward (count) handed over when the consume stage ends to
  the player `PokerColorfulPickStage.FedClientId` names, if `IsAlive`. Notices `ItemReward` (public)
  and `ItemsLost` (private, when capacity turned items away).
- **Board of 3.** The board row lays out as many slots as the running deal puts on the table, centred,
  instead of a fixed 5.
- **Helper.** `UIPokerHandRankingList` lists the running mode's `HandDatabase` and places hands
  flagged `IsHouseHand` in a crowned row above the grid, numbered 0; the row is off at tables with none.

## Phases

Each phase is one commit, verified in Play mode on host and client.

1. **Mode shell** — enum, database entry, prefab variant, sequence and stage assets, bet-item database
   and effects, HUD variant; plays with the 52-card deck and standard hands.
2. **Look into hand, board of 3, item handout and survivor reward.**
3. **Jokers** — `CardData`, deck, art placeholder, wild scoring, Five of a Kind, Suit Count line.
4. **Helper** — house-hand row and the Baccarat hand database on the HUD variant.
