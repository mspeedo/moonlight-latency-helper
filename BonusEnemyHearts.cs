using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using BepInEx;
using UnityEngine;

[BepInPlugin("local.bonusenemyhearts", "Bonus Enemy Hearts", "1.0.6")]
public sealed class BonusEnemyHearts : BaseUnityPlugin
{
    // Does not modify vanilla reward tables or the game's random generator.
    private const string PluginGuid = "local.bonusenemyhearts";
    private const int HalfHeartId = 73;
    private const int FullHeartId = 85;
    private static readonly System.Random Roll = new System.Random();
    private static readonly HashSet<int> ProcessedHealthIds = new HashSet<int>();

    private struct PendingDrop
    {
        public int ItemId;
        public Vector2 Position;
    }

    private static BonusEnemyHearts instance;
    private static float halfChance = 10f;
    private static float fullChance = 2f;
    private static bool dropsEnabled = true;
    private static bool dropConsoleMessages = true;
    private bool commandsRegistered;
    private bool hookActive;
    private object harmonyInstance;
    private static bool spawnErrorLogged;
    private static int allDeaths;
    private static int eligibleDeaths;
    private static int duplicateDeaths;
    private static int spawnAttempts;
    private static int bonusHeartsSpawned;
    private static int halfHeartsSpawned;
    private static int fullHeartsSpawned;
    private static string lastError = "none";

    private void Awake()
    {
        instance = this;
        try
        {
            InstallGlobalDeathHook();
            hookActive = true;
            Logger.LogInfo("Bonus Enemy Hearts v1.0.6: Harmony hook installed on HealthHaver.Die(Vector2).");
        }
        catch (Exception e)
        {
            hookActive = false;
            lastError = "hook: " + e.GetType().Name + " " + e.Message;
            Logger.LogError("Global enemy death hook could not be installed: " + e);
        }
    }

    private void Start() { RegisterCommandsIfReady(); }

    private void Update()
    {
        if (!commandsRegistered) RegisterCommandsIfReady();
    }

    private void OnDestroy()
    {
        // Only unpatch this plugin; never unpatch Harmony patches installed by other mods.
        if (harmonyInstance != null)
        {
            try
            {
                MethodInfo unpatch = harmonyInstance.GetType().GetMethod("UnpatchSelf", BindingFlags.Public | BindingFlags.Instance);
                if (unpatch != null) unpatch.Invoke(harmonyInstance, null);
            }
            catch (Exception e) { Logger.LogWarning("Harmony cleanup failed: " + e.Message); }
        }
        hookActive = false;
        instance = null;
        ProcessedHealthIds.Clear();
    }

    private void InstallGlobalDeathHook()
    {
        // BepInEx 5 ships 0Harmony. Resolve it at runtime to avoid depending on
        // one exact HarmonyX assembly version from the user's installation.
        Type harmonyType = Type.GetType("HarmonyLib.Harmony, 0Harmony", false);
        Type harmonyMethodType = Type.GetType("HarmonyLib.HarmonyMethod, 0Harmony", false);
        if (harmonyType == null || harmonyMethodType == null)
            throw new InvalidOperationException("BepInEx HarmonyX (0Harmony) was not found");

        MethodInfo die = typeof(HealthHaver).GetMethod("Die",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null, new Type[] { typeof(Vector2) }, null);
        if (die == null) throw new MissingMethodException("HealthHaver.Die(Vector2)");

        MethodInfo prefix = typeof(BonusEnemyHearts).GetMethod("DeathPrefix",
            BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo postfix = typeof(BonusEnemyHearts).GetMethod("DeathPostfix",
            BindingFlags.Static | BindingFlags.NonPublic);
        if (prefix == null || postfix == null)
            throw new MissingMethodException("Internal death patch methods not found");

        harmonyInstance = Activator.CreateInstance(harmonyType, new object[] { PluginGuid });
        object prefixMethod = Activator.CreateInstance(harmonyMethodType, new object[] { prefix });
        object postfixMethod = Activator.CreateInstance(harmonyMethodType, new object[] { postfix });

        MethodInfo patch = null;
        foreach (MethodInfo candidate in harmonyType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (candidate.Name != "Patch") continue;
            ParameterInfo[] p = candidate.GetParameters();
            if (p.Length < 3 || p[0].ParameterType != typeof(MethodBase) ||
                p[1].ParameterType != harmonyMethodType || p[2].ParameterType != harmonyMethodType)
                continue;
            patch = candidate;
            break;
        }
        if (patch == null) throw new MissingMethodException("Harmony.Patch(MethodBase, HarmonyMethod, HarmonyMethod...)");
        ParameterInfo[] parameters = patch.GetParameters();
        object[] patchArgs = new object[parameters.Length];
        patchArgs[0] = die;
        patchArgs[1] = prefixMethod;
        patchArgs[2] = postfixMethod;
        // Other HarmonyMethod parameters (transpiler, finalizer, etc.) remain null.
        patch.Invoke(harmonyInstance, patchArgs);
    }

    // Harmony invokes these for EVERY HealthHaver.Die, including bosses.
    // Prefix captures the living enemy position; postfix spawns the pickup
    // after the original Die implementation, without altering it.
    private static void DeathPrefix(HealthHaver __instance, out PendingDrop __state)
    {
        __state = new PendingDrop { ItemId = -1 };
        allDeaths++;
        try
        {
            if (__instance == null) return;
            AIActor enemy = __instance.aiActor;
            if (enemy == null || enemy.IsHarmlessEnemy || enemy.CompanionOwner != null) return;
            if (!ProcessedHealthIds.Add(__instance.GetInstanceID()))
            {
                duplicateDeaths++;
                return;
            }
            eligibleDeaths++;
            if (!dropsEnabled) return;

            double chance = Roll.NextDouble() * 100.0;
            int pickup = chance < fullChance ? FullHeartId :
                chance < fullChance + halfChance ? HalfHeartId : -1;
            if (pickup < 0) return;
            __state.ItemId = pickup;
            __state.Position = enemy.CenterPosition;
        }
        catch (Exception e)
        {
            lastError = "death: " + e.GetType().Name + " " + e.Message;
            if (instance != null) instance.Logger.LogError("Death hook error: " + e);
        }
    }

    private static void DeathPostfix(PendingDrop __state)
    {
        if (__state.ItemId >= 0)
            SpawnPickup(__state.ItemId, __state.Position);
    }

    private static void SpawnPickup(int pickupId, Vector2 position)
    {
        spawnAttempts++;
        try
        {
            PickupObject pickup = PickupObjectDatabase.GetById(pickupId);
            if (pickup == null)
                throw new InvalidOperationException("Heart pickup ID " + pickupId + " not found");
            DebrisObject spawned = LootEngine.SpawnItem(pickup.gameObject,
                new Vector3(position.x, position.y, 0f), Vector2.zero, 0f,
                false, true, false);
            if (spawned == null)
                throw new InvalidOperationException("LootEngine.SpawnItem returned null for " + pickupId);
            bonusHeartsSpawned++;
            if (pickupId == HalfHeartId) halfHeartsSpawned++;
            if (pickupId == FullHeartId) fullHeartsSpawned++;
            // Logging is isolated from the drop itself: console errors must not
            // interfere with vanilla death handling or bonus pickup spawning.
            if (dropConsoleMessages)
            {
                try
                {
                    // Reuse the EXACT same status formatter as 'heartdrop status'.
                    Status();
                }
                catch (Exception logError)
                {
                    if (instance != null)
                        instance.Logger.LogWarning("Bonus heart console notification failed: " + logError.Message);
                }
            }
        }
        catch (Exception e)
        {
            lastError = "spawn: " + e.GetType().Name + " " + e.Message;
            if (instance != null && !spawnErrorLogged)
            {
                spawnErrorLogged = true;
                instance.Logger.LogError("Bonus heart spawn failed: " + e);
            }
        }
    }

    private static void SpawnTestHeart()
    {
        try
        {
            if (GameManager.Instance == null || GameManager.Instance.PrimaryPlayer == null)
            {
                ETGModConsole.Log("[Bonus Enemy Hearts] Enter a dungeon room first.", false);
                return;
            }
            SpawnPickup(HalfHeartId, GameManager.Instance.PrimaryPlayer.CenterPosition);
        }
        catch (Exception e)
        {
            lastError = "test: " + e.GetType().Name + " " + e.Message;
            if (instance != null) instance.Logger.LogError("Heart test failed: " + e);
        }
        // A successful test drop already prints the status when notifications
        // are enabled; preserve explicit test feedback when logging is muted.
        if (!dropConsoleMessages) Status();
    }

    private void RegisterCommandsIfReady()
    {
        if (ETGModConsole.Commands == null) return;
        try
        {
            ETGModConsole.Commands.AddGroup("heartdrop", args => Status());
            var group = ETGModConsole.Commands.GetGroup("heartdrop");
            if (group == null)
                throw new InvalidOperationException("heartdrop command group was not found");
            group.AddUnit("half", args => SetChance(args, true));
            group.AddUnit("full", args => SetChance(args, false));
            group.AddUnit("on", args => { dropsEnabled = true; Status(); });
            group.AddUnit("off", args => { dropsEnabled = false; Status(); });
            group.AddUnit("status", args => Status());
            group.AddUnit("test", args => SpawnTestHeart());
            group.AddUnit("log", args => SetDropLogging(args));
            commandsRegistered = true;
            Logger.LogInfo("Registered heartdrop console commands.");
            ETGModConsole.Log("[Bonus Enemy Hearts] v1.0.6 loaded; use: heartdrop status", false);
        }
        catch (Exception e)
        {
            commandsRegistered = true;
            Logger.LogError("Couldn't register heartdrop commands: " + e);
        }
    }

    private static void SetDropLogging(string[] args)
    {
        if (args == null || args.Length == 0)
        {
            Status();
            return;
        }
        if (args.Length != 1 ||
            (args[0] != "on" && args[0] != "off"))
        {
            ETGModConsole.Log("Usage: heartdrop log on|off", false);
            return;
        }
        dropConsoleMessages = args[0] == "on";
        Status();
    }

    private static void SetChance(string[] args, bool half)
    {
        float value;
        if (args == null || args.Length != 1 ||
            !float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
            float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > 100f)
        {
            ETGModConsole.Log("Usage: heartdrop " + (half ? "half" : "full") + " <0..100>", false);
            return;
        }
        if (value + (half ? fullChance : halfChance) > 100f)
        {
            ETGModConsole.Log("Combined heart chances cannot exceed 100%.", false);
            return;
        }
        if (half) halfChance = value; else fullChance = value;
        Status();
    }

    private static void Status()
    {
        string hook = instance != null && instance.hookActive ? "ACTIVE" : "INACTIVE";
        ETGModConsole.Log("[Bonus Enemy Hearts] " + (dropsEnabled ? "ON" : "OFF") +
            " | hook=" + hook +
            " | log=" + (dropConsoleMessages ? "ON" : "OFF") +
            " | half=" + halfChance.ToString(CultureInfo.InvariantCulture) +
            "% | full=" + fullChance.ToString(CultureInfo.InvariantCulture) +
            "% | deaths=" + allDeaths + " | eligible=" + eligibleDeaths +
            " | duplicates=" + duplicateDeaths +
            " | spawn_attempts=" + spawnAttempts + " | spawned=" + bonusHeartsSpawned +
            " (half=" + halfHeartsSpawned + ", full=" + fullHeartsSpawned + ")" +
            " | error=" + lastError, false);
    }
}
