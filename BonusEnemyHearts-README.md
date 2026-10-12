# Bonus Enemy Hearts 1.0.6

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

## Automatic status output (v1.0.6)

Console status output is enabled after bonus drops by default. After each **successfully spawned bonus heart**, the mod prints the **exact same full status line** that `heartdrop status` prints manually. No dedicated "Bonus drop" text is used.

Example (counters will vary):

```
[Bonus Enemy Hearts] ON | hook=ACTIVE | log=ON | half=10% | full=2% | deaths=26 | eligible=24 | duplicates=0 | spawn_attempts=3 | spawned=3 (half=2, full=1) | error=none
```

Use `heartdrop log off` to silence automatic status lines while retaining the same bonus-drop behavior, and `heartdrop log on` to enable them again. `heartdrop status` works regardless of the logging switch. Notifications apply to bonus hearts created by this mod, not unmodified vanilla heart drops. Rates and the working Harmony hook remain unchanged.

## How it works
The mod installs a runtime Harmony prefix/postfix on `HealthHaver.Die(Vector2)`. It captures the enemy and its world position before the base game's death method, and then requests a separate bonus pickup after the original method. It does not overwrite or replace any original loot logic, nor require polling enemy lists. It uses its own System.Random and prevents duplicate drops from repeated Die calls on one HealthHaver. Runtime reflection avoids hard-binding to a specific HarmonyX version.

## Troubleshooting
`hook=INACTIVE` or `error=hook:...` indicates patch registration failed; inspect `BepInEx/LogOutput.log`. `deaths>0` and `eligible=0` suggests the game isn't associating AIActors correctly. `spawn_attempts>0` with `spawned=0` points to pickup spawning. The full in-game behavior cannot be verified by the CI mock.

## Changes
1.0.6: On successful bonus-heart spawn, call the same Status() method as the manual command. No separate message type. Keep logging toggle and identical drop behavior. Tests verify the output matches exactly and is emitted only once per bonus heart.
1.0.5: Print every successful bonus heart spawn to the MtG console, distinguish half/full hearts, keep running counters, and allow notifications to be toggled separately from the drop system. CI tests verify notifications and that muting does not affect drop spawning.
1.0.4: Replace room-scanning registration from 1.0.3 with global Harmony Death hook. Preserve corrected console registration. Add duplicate-death detection and more explicit diagnostics. Automated simulated kill tests included in CI.
