using System;
using System.Globalization;
using BepInEx;
using UnityEngine;

[BepInPlugin("local.bonusenemyhearts", "Bonus Enemy Hearts", "1.0.0")]
[BepInDependency("etgmodding.etg.mtgapi")]
public sealed class BonusEnemyHearts : BaseUnityPlugin
{
    private static readonly Random Roll = new Random();
    private static float halfChance = 10f;
    private static float fullChance = 2f;
    private static bool dropsEnabled = true;
    private bool commandsRegistered;

    private void Awake()
    {
        ETGMod.AIActor.OnPostStart += RegisterEnemy;
    }
    private void Start() { RegisterCommandsIfReady(); }
    private void Update()
    {
        if (!commandsRegistered) RegisterCommandsIfReady();
    }
    private void OnDestroy()
    {
        ETGMod.AIActor.OnPostStart -= RegisterEnemy;
    }
    private void RegisterCommandsIfReady()
    {
        if (ETGModConsole.Commands == null) return;
        try
        {
            var group = ETGModConsole.Commands.AddGroup("heartdrop", args => Status());
            group.AddUnit("half", args => SetChance(args, true));
            group.AddUnit("full", args => SetChance(args, false));
            group.AddUnit("on", args => { dropsEnabled = true; Status(); });
            group.AddUnit("off", args => { dropsEnabled = false; Status(); });
            group.AddUnit("status", args => Status());
            commandsRegistered = true;
            ETGModConsole.Log("[Bonus Enemy Hearts] Loaded. Try: heartdrop status", false);
        }
        catch (Exception e)
        {
            Logger.LogWarning("Couldn't register heartdrop commands: " + e);
            commandsRegistered = true;
        }
    }
    private static void RegisterEnemy(AIActor enemy)
    {
        if (enemy == null || enemy.healthHaver == null) return;
        if (enemy.IsHarmlessEnemy || enemy.CompanionOwner != null) return;
        enemy.healthHaver.OnDeath += direction => EnemyDied(enemy);
    }
    private static void EnemyDied(AIActor enemy)
    {
        if (!dropsEnabled || enemy == null) return;
        double chance = Roll.NextDouble() * 100.0;
        int pickupId = chance < fullChance ? 85 :
                       chance < fullChance + halfChance ? 73 : -1;
        if (pickupId < 0) return;
        try
        {
            var pickup = PickupObjectDatabase.GetById(pickupId);
            if (pickup == null) return;
            Vector2 pos = enemy.CenterPosition;
            LootEngine.SpawnItem(pickup.gameObject,
                new Vector3(pos.x, pos.y, 0f), Vector2.zero, 0f,
                false, true, false);
        }
        catch (Exception)
        {
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
            "% | full=" + fullChance.ToString(CultureInfo.InvariantCulture) + "%", false);
    }
}
