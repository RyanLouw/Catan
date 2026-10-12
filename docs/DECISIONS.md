# Gameplay decisions

The supplied GAME_RULES.md is retained unchanged. Classic base-game behavior supplies building costs, finite resource/piece inventories, 25-card development deck composition, card play restrictions, production shortage handling, maritime ratios, graph scoring, and victory on one's own turn.

## Outstanding: all remaining players inactive

A decision was requested during implementation because the specification defines individual disconnect and surrender behavior but not what happens when no eligible connected player remains.

The current implementation is a reviewable proposal: disconnected players continue receiving their specified 10-second grace periods and skipped turns; if all players surrender, the game enters `Abandoned` with no winner and retains the board. There is no automatic win for the last remaining player. The alternative offered is to pause when all remaining players are disconnected, resuming on reconnection, while still abandoning without a winner when all surrender.

This proposal is explicitly pending the user's decision. The `Paused` contract value is reserved and currently unused. Do not call Stage 1's rule decisions finalized until this case is resolved. All other implemented mechanics and simulation results can be evaluated independently.

## Derived timer behavior

Mandatory setup choices use the 30-second mandatory-action timer. Roll-7 discards begin together and receive the same 30-second deadline; the next robber/victim phase receives its own 30 seconds. No action timer starts until dice effects complete.

Playing a Knight before the roll does not reset the original 15-second roll deadline. The engine resolves the Knight's mandatory sequence first and immediately performs an overdue roll afterwards. A Knight during the action phase does not reset its 90-second deadline; at expiration the engine supplies any outstanding mandatory robber/victim choices before ending the turn.

Disconnecting mid-turn does not reset existing deadlines. Only reaching a disconnected player's new turn starts the 10-second reconnection grace. Surrender during a mandatory roll-7 sequence completes the outstanding effects, then skips immediately; pieces remain on the board and continue blocking road traversal and placement.
