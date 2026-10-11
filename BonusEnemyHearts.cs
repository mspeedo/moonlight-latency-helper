using System;
using System.Globalization;
using System.Collections.Generic;
using Dungeonator;
using BepInEx;
using UnityEngine;

[BepInPlugin("local.bonusenemyhearts", "Bonus Enemy Hearts", "1.0.3")]
public sealed class BonusEnemyHearts : BaseUnityPlugin
{
    private static readonly Random Roll = new Random();
    private const int HalfHeartPickupId = 73;
    private const int FullHeartPickupId = 85;
    private const float ScanIntervalSeconds = 0.10f;
    private static readonly HashSet<AIActor> SubscribedEnemies = new HashSet<AIActor>();
    private static int scanCount;
    private static int scannedEnemies;
    private static string lastError = "none";
    private float nextScanTime;
    private bool scanErrorLogged;
    private bool spawnErrorLogged;
    private static float halfChance = 10f;
    private static float fullChance = 2f;
    private static bool dropsEnabled = true;
    private bool commandsRegistered;
    private static int trackedEnemies;
    private static int enemyDeaths;
    private static int spawnAttempts;
    private static int bonusHeartsSpawned;
    private static BonusEnemyHearts instance;

    private void Awake()
    {
        instance = this;
        Logger.LogInfo("Bonus Enemy Hearts v1.0.3 loaded; scanning room enemies rather than depending on MtG OnPostStart.");
    }
    private void Start() { RegisterCommandsIfReady(); }
    private void Update()
    {
        if (!commandsRegistered) RegisterCommandsIfReady();
        if (Time.realtimeSinceStartup < nextScanTime) return;
        nextScanTime = Time.realtimeSinceStartup + ScanIntervalSeconds;
        ScanCurrentRoom();
    }
    private void OnDestroy()
    {
        instance = null;
        SubscribedEnemies.Clear();
    }
    private void RegisterCommandsIfReady()
    {
        if (ETGModConsole.Commands == null) return;
        try
        {
            ETGModConsole.Commands.AddGroup("heartdrop", args => Status());
            // IMPORTANT: AddGroup returns the PARENT, not the new subgroup.
            var group = ETGModConsole.Commands.GetGroup("heartdrop");
            if (group == null) throw new InvalidOperationException("heartdrop subgroup not found after AddGroup");
            group.AddUnit("half", args => SetChance(args, true));
            group.AddUnit("full", args => SetChance(args, false));
            group.AddUnit("on", args => { dropsEnabled = true; Status(); });
            group.AddUnit("off", args => { dropsEnabled = false; Status(); });
            group.AddUnit("status", args => Status());
            group.AddUnit("scan", args => { ScanCurrentRoom(); Status(); });
            group.AddUnit("test", args => SpawnTestHeart());
            commandsRegistered = true;
            Logger.LogInfo("Registered heartdrop commands.");
            ETGModConsole.Log("[Bonus Enemy Hearts] Loaded. Try: heartdrop status", false);
        }
        catch (Exception e)
        {
            Logger.LogWarning("Couldn't register heartdrop commands: " + e);
            commandsRegistered = true;
        }
    }
    private static void ScanCurrentRoom()
    {
        scanCount++;
        try
        {
            if (GameManager.Instance == null) return;
            PlayerController player = GameManager.Instance.PrimaryPlayer;
            if (player == null || player.CurrentRoom == null) return;
            // Room enemy enumeration works even when MtG's OnPostStart is never dispatched.
            List<AIActor> enemies = player.CurrentRoom.GetActiveEnemies((ActiveEnemyType)1);
            if (enemies == null) return;
            scannedEnemies += enemies.Count;
            for (int i = 0; i < enemies.Count; i++)
            {
                AIActor enemy = enemies[i];
                if (enemy == null || enemy.IsHarmlessEnemy || enemy.CompanionOwner != null) continue;
                HealthHaver health = enemy.healthHaver;
                if (health == null || health.IsDead || SubscribedEnemies.Contains(enemy)) continue;
                SubscribedEnemies.Add(enemy);
                health.OnPreDeath += direction => EnemyDied(enemy);
                trackedEnemies++;
            }
        }
        catch (Exception e)
        {
            lastError = "scan: " + e.GetType().Name + " " + e.Message;
            if (instance != null && !instance.scanErrorLogged)
            {
                instance.scanErrorLogged = true;
                instance.Logger.LogError("Enemy scan failed: " + e);
            }
        }
    }
    private static void EnemyDied(AIActor enemy)
    {
        enemyDeaths++;
        if (!dropsEnabled || enemy == null) return;
        double chance = Roll.NextDouble() * 100.0;
        int pickupId = chance < fullChance ? FullHeartPickupId :
                       chance < fullChance + halfChance ? HalfHeartPickupId : -1;
        if (pickupId < 0) return;
        try { SpawnPickup(pickupId, enemy.CenterPosition); }
        catch (Exception e)
        {
            lastError = "death: " + e.GetType().Name + " " + e.Message;
            if (instance != null && !instance.spawnErrorLogged)
            {
                instance.spawnErrorLogged = true;
                instance.Logger.LogError("Failed reading dead enemy position: " + e);
            }
        }
    }
    private static void SpawnPickup(int pickupId, Vector2 position)
    {
        spawnAttempts++;
        try
        {
            PickupObject pickup = PickupObjectDatabase.GetById(pickupId);
            if (pickup == null) throw new InvalidOperationException("Heart pickup ID " + pickupId + " not found");
            DebrisObject spawned = LootEngine.SpawnItem(pickup.gameObject,
                new Vector3(position.x, position.y, 0f), Vector2.zero, 0f,
                false, true, false);
            if (spawned == null) throw new InvalidOperationException("LootEngine.SpawnItem returned null for " + pickupId);
            bonusHeartsSpawned++;
        }
        catch (Exception e)
        {
            lastError = "spawn: " + e.GetType().Name + " " + e.Message;
            if (instance != null && !instance.spawnErrorLogged)
            {
                instance.spawnErrorLogged = true;
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
            SpawnPickup(HalfHeartPickupId, GameManager.Instance.PrimaryPlayer.CenterPosition);
            Status();
        }
        catch (Exception e)
        {
            lastError = "test: " + e.GetType().Name + " " + e.Message;
            if (instance != null) instance.Logger.LogError("Test heart spawn failed: " + e);
            Status();
        }
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
            ETGModConsole.Log("Combined chances cannot exceed 100%.", false);
            return;
        }
        if (half) halfChance = value; else fullChance = value;
        Status();
    }
    private static void Status()
    {
        ETGModConsole.Log("[Bonus Enemy Hearts] " + (dropsEnabled ? "ON" : "OFF") +
            " | half=" + halfChance.ToString(CultureInfo.InvariantCulture) +
            "% | full=" + fullChance.ToString(CultureInfo.InvariantCulture) +
            "% | scans=" + scanCount + " | seen=" + scannedEnemies +
            " | tracked=" + trackedEnemies + " | kills=" + enemyDeaths +
            " | spawn_attempts=" + spawnAttempts + " | spawned=" + bonusHeartsSpawned +
            " | error=" + lastError, false);
    }
}
