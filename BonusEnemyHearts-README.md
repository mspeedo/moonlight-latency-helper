# Bonus Enemy Hearts 1.0.3

BepInEx 5 / ModTheGungeonAPI add-on for Enter the Gungeon.

Each eligible enemy kill (including ordinary room bosses) independently rolls an **additional** 10% half-heart or 2% full-heart. At most one extra heart per kill. Vanilla loot tables, currencies, weapons and normal drops are unchanged.

## Installation
1. Exit the game.
2. Delete the old BonusEnemyHearts.dll, then extract BonusEnemyHearts/BonusEnemyHearts.dll from this ZIP to the game's BepInEx/plugins/BonusEnemyHearts/ folder.
3. Start Enter the Gungeon and open the MtG console.

## Configuration / testing
- `heartdrop status`: show settings, room scans, enemies detected, deaths and item spawns.
- `heartdrop full 0`, then `heartdrop half 100`: force a bonus half-heart on every eligible enemy kill.
- `heartdrop scan`: force a room-enemy scan and display its counters.
- `heartdrop test`: spawn one half-heart at your character's location to test the pickup pipeline independently of death tracking.
- `heartdrop off` / `heartdrop on`: turn the extra heart system off/on.
- `heartdrop half 10`, then `heartdrop full 2`: restore defaults.

## Diagnostic counters
`scans` = polling cycles, `seen` = summed enemy-list entries observed across scans,
`tracked` = unique enemies with death handlers attached,
`kills` = pre-death events received,
`spawn_attempts` = bonus pickup spawn requests,
`spawned` = non-null LootEngine spawned objects,
`error` = most recent scan/spawn error or none.

## v1.0.3 fix
MtGAPI's exposed AIActor.OnPostStart field was not firing in the user's game (tracked=0).
This build **no longer uses the MtG hook**. It scans the current room approximately 10 times/second
and subscribes to HealthHaver.OnPreDeath for each distinct living enemy. Bosses enumerated as
active enemies are included. Updates to bonus chances still use the console from v1.0.2.

Build passed compile-time validation against API stubs; real in-game behavior needs verification.
The mod never changes vanilla loot tables or vanilla random rolls.
