# Context

Shared vocabulary for GamblingGnomes. Glossary only — no implementation detail, no spec.
When a term here disagrees with the code, one of the two is wrong; say so rather than picking silently.

## Modes

**Hold'em** — the poker variant the project shipped first: hole cards, community cards, betting
streets priced in Money, blinds and side pots, plus Cheating and the Report. Still playable.

**Normal** — the variant described in the design document (`GameMode_Poker`). Five private cards per
player, no community cards, Bets paid in BetItems rather than Money, and losers eat what the winner
staked. Not a separate game: a different sequence of Stages inside the same poker mode.

**Liar's Poker** — two hole cards dealt straight into the hand, five community cards turned over four
Streets, one BetItem of a random kind per Bet, and everyone but the winner eats their own stake.

## Currencies

Three things a player can lose, and they are never interchangeable.

**Money** — the chip stake of Hold'em. Has denominations, is raised and called, forms the Pot.
Unused by Normal and Liar's Poker.

**Blood** — a player's health, drawn on the body as fingers. Staked by the Report. Untouched by
Hallucination: a player at 100% Hallucination is out of the match with every finger intact.

**Hallucination** — a percentage from 0 to 100 that only ever rises, except where an Item lowers it.
Reaching 100 is Unconsciousness. Public: every player can read every other player's figure. Items call
it the Death Rate.

## Betting

**Street** — one betting round inside a Hand: every player still in is asked once, in seat order.
Normal has two, Liar's Poker four. `ServerResetForRound` runs per Street, so anything that must survive
from one Street to the next belongs to the Hand instead. Formerly "Wager" and "Round".

**Bet** — the act of putting BetItems into the pot on your Turn. It has no amount the player picks: the
Street decides how many (its stake size) and the player at most picks the kind.

**BetItem** — a mushroom cap: the token a Bet stakes and the thing that gets eaten. Four ordinary kinds
plus Colorful.

**Pot Entry** — one line of the pot ledger: one BetItem, who it stands in front of, the Street it went
up on.

**Colorful Mushroom** — not bettable. In Normal, the Hand's winner names one player — themselves
included — to eat it.

**Eating** — what a loser does at the end of a Hand. Raises the eater's Hallucination: a kind they
have never eaten raises it more than one they have. Which kinds a player has eaten is public and
lasts the whole Match.

**Unconsciousness** — reaching 100 Hallucination, or losing a Death Roll (the roll eating a Colorful
Mushroom forces, or one an Item forces). Permanent for the Match. An unconscious player keeps their
chair and their voice, and is never dealt in again.

**Fold** — putting the cards down. In Normal a folder eats one BetItem: their own from the first
Street. A loser who did not fold eats a copy of everything the winner staked.

**Item** — a one-use card kept in a player's own inventory, dealt at the start of each Hand (more to
the previous Hand's losers) and played on the holder's own Turn, at most one per Street. How many a
player holds is public; which ones is theirs alone to know. Not a BetItem.

**Effect** — what a level of Hallucination does to the world a player sees and hears. Attached to a
threshold rather than to a mushroom type: crossing a threshold picks one, and it keeps running
alongside every effect picked below it.

## The table

**Seat** — a chair at the table, assigned by the server when a player arrives. Not chosen, not left:
there are exactly as many chairs as the lobby holds players.

**Match** — one contest, run as a series of Hands, ending when a single conscious player is left.
Hallucination, eaten-kind history and Item inventories all belong to the Match and reset with it.

**Hand** — one deal, through its Streets, to the eating. The winner of a Hand bets first in the next
one. The design document calls this a "round"; the code does not, and `ServerResetForHand` is the
reset that belongs to it.

**Turn** — one player being asked to act on a Street.

## Cheating and the Report

**Cheating** — an Ability that grants its holder something the table cannot see it grant.

**Report** — accusing another player of Cheating, staked in Blood. Disabled in Normal and Liar's Poker;
the mechanism remains, and Hold'em still plays with it.
