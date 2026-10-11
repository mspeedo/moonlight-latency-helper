using System;
using System.Reflection;
using UnityEngine;
using Dungeonator;
public static class HeartDropFunctionalTest
{
    private static void Call(object plugin, string method)
    {
        MethodInfo fn = typeof(BonusEnemyHearts).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
        if (fn == null) throw new Exception("Missing method " + method);
        fn.Invoke(plugin, null);
    }
    private static void Require(bool ok, string message)
    {
        if (!ok) throw new Exception(message + "\n" + ETGModConsole.LastLog);
    }
    public static int Main()
    {
        try
        {
            GameManager.Instance = new GameManager { PrimaryPlayer = new PlayerController { CurrentRoom = new RoomHandler() } };
            var enemy = new AIActor { healthHaver = new HealthHaver(), CenterPosition = new Vector2 {x=1, y=2} };
            GameManager.Instance.PrimaryPlayer.CurrentRoom.Active.Add(enemy);
            var plugin = new BonusEnemyHearts();
            Call(plugin, "Awake");
            Call(plugin, "Start");
            var group = ETGModConsole.Commands.GetGroup("heartdrop");
            Require(group != null, "Command group missing");
            group.Invoke("full", "0");
            group.Invoke("half", "100");
            Time.realtimeSinceStartup = 1.0f;
            Call(plugin, "Update");
            group.Invoke("status");
            Require(ETGModConsole.LastLog.Contains("tracked=1"), "Enemy was not tracked");
            Time.realtimeSinceStartup = 1.5f;
            Call(plugin, "Update");
            Require(enemy.healthHaver.OnPreDeath != null, "Death handler missing");
            Require(enemy.healthHaver.OnPreDeath.GetInvocationList().Length == 1, "Duplicate death handlers");
            enemy.healthHaver.OnPreDeath(Vector2.zero);
            group.Invoke("status");
            Require(ETGModConsole.LastLog.Contains("kills=1"), "Death not recorded");
            Require(ETGModConsole.LastLog.Contains("spawn_attempts=1"), "Drop attempt missing");
            Require(ETGModConsole.LastLog.Contains("spawned=1"), "Bonus drop not recorded");
            Require(LootEngine.SpawnCounter == 1, "LootEngine spawn missing or duplicated");
            group.Invoke("test");
            Require(LootEngine.SpawnCounter == 2, "Manual test failed");
            group.Invoke("half", "10");
            group.Invoke("full", "2");
            Require(ETGModConsole.LastLog.Contains("half=10% | full=2%"), "Default reset failed");
            Console.WriteLine("PASS: live room scan -> death hook -> guaranteed heart spawn -> diagnostics -> manual test");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("FAIL: " + e);
            return 1;
        }
    }
}