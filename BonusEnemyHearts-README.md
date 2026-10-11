# Bonus Enemy Hearts 1.0.1
BepInEx 5 / ModTheGungeonAPI plugin for Enter the Gungeon.

Each killed hostile enemy including bosses independently gets a bonus roll for **10% half-heart** or **2% full-heart**. Up to one extra heart per kill; normal loot probabilities unchanged. Friendly companions and harmless enemies excluded.

## Install
Extract the ZIP and copy BonusEnemyHearts/BonusEnemyHearts.dll to the game's **BepInEx/plugins/BonusEnemyHearts/** directory. Restart the game.

## Console
`heartdrop status`, `heartdrop half 20`, `heartdrop full 5`, `heartdrop on`, `heartdrop off`.
Settings reset on restart. Half+full cannot exceed 100%.

## Notes
Needs BepInEx 5 and ModTheGungeonAPI; not compatible with the standalone classic ETGModule mod loader.
No change to vanilla drops or game RNG. Scripted boss deaths without ordinary AIActor death events may not grant bonus drops. Untested in game.

## v1.0.1
Fixed BepInEx plugin discovery error by removing an invalid dependency metadata attribute. Added BepInEx startup logs and one-time error logging for bonus drops. Compile-time validated against API stubs; not yet run in the game.
