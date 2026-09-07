# Context

Shared vocabulary for GamblingGnomes. Glossary only — no implementation detail, no spec.
When a term here disagrees with the code, one of the two is wrong; say so rather than picking silently.

## Modes

**Hold'em** — the poker variant the project shipped first: hole cards, community cards, betting
streets priced in Money, blinds and side pots, plus Cheating and the Report. Still playable.

**Mushroom Round** — the variant described in the design document. Five private cards per player,
no community cards, wagers paid in Mushrooms rather than Money, and losers eat what the winner
wagered. Not a separate game: a different sequence of Stages inside the same poker mode.

## Currencies

Three things a player can lose, and they are never interchangeable.

**Money** — the chip stake of Hold'em. Has denominations, is raised and called, forms the Pot.
Unused by the Mushroom Round.

**Blood** — a player's health, drawn on the body as fingers. Staked by the Report. Untouched by
Hallucination: a player at 100% Hallucination is out of the match with every finger intact.

**Hallucination** — a percentage from 0 to 100 that only ever rises, except where an Item lowers it.
Reaching 100 is Unconsciousness. Public: every player can read every other player's figure.

## The Mushroom Round

**Wager** — choosing one Mushroom type. It has no amount: a wager is one mushroom, never three of
them and never a sum. Each round asks for two, one before the cards and one after.

**Mushroom** — a wagered token and a thing that gets eaten. Four ordinary types plus Colorful.

**Colorful Mushroom** — not wagerable. The round's winner names one player — themselves included —
to eat it.

**Eating** — what a loser does at the end of a round. Raises the eater's Hallucination: a type they
have never eaten raises it more than one they have. Which types a player has eaten is public and
lasts the whole Match.

**Unconsciousness** — reaching 100 Hallucination, or losing the roll that eating a Colorful Mushroom
forces. Permanent for the Match. An unconscious player keeps their chair and their voice, and is
never dealt in again.

**Fold** — putting the cards down. A folder eats one mushroom: their own first Wager. A loser who did
not fold eats both of the winner's.

**Item** — a one-use effect kept in a player's own hidden inventory, handed out to losers and carried
between rounds. Played during the second Wager. What a player is holding is theirs alone to know.

**Effect** — what a level of Hallucination does to the world a player sees and hears. Attached to a
threshold rather than to a mushroom type: crossing a threshold picks one, and it keeps running
alongside every effect picked below it.

## The table

**Seat** — a chair at the table, assigned by the server when a player arrives. Not chosen, not left:
there are exactly as many chairs as the lobby holds players.

**Match** — one contest, run as a series of Rounds, ending when a single conscious player is left.
Hallucination, eaten-type history and inventories all belong to the Match and reset with it.

**Hand** — one deal, from the first Wager to the eating. The winner of a Hand wagers first in the next
one. The design document calls this a "round"; the code does not, and `ServerResetForHand` is the
reset that belongs to it.

**Round** — one betting street inside a Hand, and nothing larger. `ServerResetForRound` runs per
street, so anything that must survive from the first Wager to the second belongs to the Hand instead.

## Cheating and the Report

**Cheating** — an Ability that grants its holder something the table cannot see it grant.

**Report** — accusing another player of Cheating, staked in Blood. Disabled in the Mushroom Round;
the mechanism remains, and Hold'em still plays with it.
