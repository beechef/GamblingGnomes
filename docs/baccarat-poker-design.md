# Baccarat Poker — design

Date: 2026-09-28
Status: built. Scoring verified in the editor; mode spawns and the helper reads it in solo Play mode; a two-player match is not yet played.

A fifth mode on `Gameplay_Poker`. It plays like the Normal mode (two Streets around a look, a
Colorful pick at the end of every Hand), but each player holds 2 cards and shares a 3-card board, the
deck carries 4 Jokers, and Items are handed out once per Match plus one for surviving a Colorful.
Words follow `CONTEXT.md` and `docs/items-design.md` (Match, Hand, Street, Bet, BetItem, Item).

## Rules

- **Deck.** 56 cards: the 52 plus 4 Jokers. All four Jokers share one picture. A Joker stands for any
  rank and any suit, including a card already in the hand. Other modes keep the 52-card deck.
- **Deal.** 2 hole cards per player, face down on the table, and 3 community cards, face down. No ante.
- **First Street.** Bet only, 1 cap of a random kind (the kinds are worth the same). No Fold, no
  All-in. Items may be used. Nobody has seen their cards yet.
- **Look.** Both hole cards go into every hand at once. Nothing to choose, so nothing to click.
- **Second Street.** Bet (1 random cap) or Fold. No All-in. Items may be used.
- **Reveal.** All 3 community cards turn at once, with every hand still in play. A winner everyone else
  folded to is still turned over, board included.
- **Showdown.** A hand is exactly 5 cards: 2 hole + 3 board (6 with Extra Draw, best five scored).
  Ties all win.
- **Settlement: `OwnStake`.** The winners' caps are discarded. Every loser eats the 2 caps they
  staked; a folder eats the 1 cap from the first Street.
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

- **Pool.** The Liar database's eleven Items, copied into the mode's own database.
- **Handout.** 2 random Items to every player in the Match at its first deal. No per-Hand handout,
  no loser bonus.
- **Inventory.** Capacity 5, 1 Item per Street, kept across Hands, cleared when the Match ends.
- **Next-Street Items.** Raise and Lock are hidden on the second Street, the last one (existing rule:
  what the rules forbid is hidden).
- **Suit Count.** Shows a separate Joker line; Jokers are not added to any suit.

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
- **Sequence.** Waiting → Deal (hole 2, community 3) → FirstStreet (Random kind, no fold) → CardLook
  (every card into the hand) → SecondStreet (Random kind, fold) → CardReveal (`_revealBoard`) →
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
- **Item handout.** `PokerItemModule` gains a per-Match starting handout (count) beside the per-Hand one
  (set to 0 here), and a Colorful-survivor reward (count) handed over when the consume stage ends to
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
