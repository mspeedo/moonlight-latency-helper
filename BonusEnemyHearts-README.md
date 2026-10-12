# Bonus Enemy Hearts 1.0.5

Enter the Gungeon BepInEx 5 plugin. Mod the Gungeon API is used for console commands.

**Bonus drops:** 10% chance of an extra half-heart, 2% chance of an extra full heart per eligible enemy kill, with exactly one extra heart at most. Regular loot tables, ammo, money, keys, vanilla hearts and reward RNG are unchanged. Bosses are eligible; companions and harmless enemies are excluded.

## Installation
Exit the game. Remove the previous `BonusEnemyHearts.dll` and extract `BonusEnemyHearts/BonusEnemyHearts.dll` to `Enter the Gungeon/BepInEx/plugins/BonusEnemyHearts/`. Restart the game. Do not install the test stubs or ship an extra 0Harmony.dll (BepInEx supplies Harmony).

## Test
Open the Mod the Gungeon console:
```
heartdrop status
heartdrop full 0
heartdrop half 100
```
Status should say `hook=ACTIVE`. Kill enemies; `deaths`, `eligible`, `spawn_attempts`, `spawned` should increase. The drop should appear at their death location. `heartdrop test` attempts to spawn a half-heart at your character independently of the death hook.

To restore default rates: `heartdrop half 10`, then `heartdrop full 2`.

Other commands: `heartdrop on`, `heartdrop off`, `heartdrop status`, `heartdrop log on`, `heartdrop log off`.

## Bonus drop console notifications (new in 1.0.5)
Console notifications are ON by default. Every successful bonus-heart spawn prints a line like:

```
[Bonus Enemy Hearts] Bonus drop: half-heart | total=4 (half=3, full=1)
```

Full-heart drops print `Bonus drop: full heart` instead. These notifications are only emitted after `LootEngine.SpawnItem` returned a spawned object. To silence messages without changing the heart-drop probabilities, use `heartdrop log off`; to restore them use `heartdrop log on`. `heartdrop status` shows `log=ON`/`OFF` and separate half/full spawn counters. Logging state is session-only and resets to ON after relaunch.


## How it works
The mod installs a runtime Harmony prefix/postfix on `HealthHaver.Die(Vector2)`. It captures the enemy and its world position before the base game's death method, and then requests a separate bonus pickup after the original method. It does not overwrite or replace any original loot logic, nor require polling enemy lists. It uses its own System.Random and prevents duplicate drops from repeated Die calls on one HealthHaver. Runtime reflection avoids hard-binding to a specific HarmonyX version.

## Troubleshooting
`hook=INACTIVE` or `error=hook:...` indicates patch registration failed; inspect `BepInEx/LogOutput.log`. `deaths>0` and `eligible=0` suggests the game isn't associating AIActors correctly. `spawn_attempts>0` with `spawned=0` points to pickup spawning. The full in-game behavior cannot be verified by the CI mock.

## Changes
1.0.5: Print every successful bonus heart spawn to the MtG console, distinguish half/full hearts, keep running counters, and allow notifications to be toggled separately from the drop system. CI tests verify notifications and that muting does not affect drop spawning.
1.0.4: Replace room-scanning registration from 1.0.3 with global Harmony Death hook. Preserve corrected console registration. Add duplicate-death detection and more explicit diagnostics. Automated simulated kill tests included in CI.
