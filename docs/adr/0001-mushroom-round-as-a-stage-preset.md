# 1. The Mushroom Round is a stage preset, not a second game mode

Date: 2026-09-07

## Status

Accepted

## Context

The design document describes a game whose rules differ from the Hold'em mode already shipped:
five private cards instead of hole cards plus a board, wagers paid in mushroom types instead of
chip amounts, and a hallucination bar instead of a pot to win. Read cold, it looks like a different
game that happens to reuse a poker hand ranking.

Three ways to build it were on the table:

1. Replace Hold'em. The betting streets, blinds and side pots become dead code.
2. A second `IGameMode` beside `PokerGameMode`, sharing only the player, seat and hand evaluator.
3. A new `PokerStageSequence` preset inside `PokerGameMode`, plus the stages it needs.

## Decision

Option 3. The Mushroom Round is a sequence of stages, and Hold'em keeps its own.

## Consequences

The stage machine, the turn loop, the replicated table state, the seat register and the whole HUD
are written once and serve both. A rule that differs between the two is a serialized value on a
stage asset or a module toggle, not a branch in shared code — which is the shape the stages were
already built for.

The cost is that `PokerGameData` and `PokerPlayerData` carry state one of the two variants never
uses: `Bet`, `TotalBet` and the side-pot layers mean nothing to a mushroom wager, and
`HallucinationRate` means nothing to Hold'em. That is real weight, and it is the price of not
duplicating everything else.

Option 2 was rejected for the size of that duplication: two data classes, two HUDs and two copies
of every fix made to either. Option 1 was rejected because the new rules are unplayed and Hold'em
works; deleting a running game to make room for an untested one is a bet nobody needed to place.

Reversing this later means lifting the mushroom stages into their own mode — the stages themselves
would survive, the shared data class would have to be split.
