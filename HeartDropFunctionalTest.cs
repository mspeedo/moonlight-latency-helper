using System;
using System.Reflection;
using UnityEngine;
using HarmonyLib;

public static class HeartDropFunctionalTest
{
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message + "\nConsole: " + ETGModConsole.LastLog);
    }
    private static void InvokeLifecycle(object plugin, string method)
    {
        typeof(BonusEnemyHearts).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(plugin,null);
    }
    private static void SimulateDeath(HealthHaver health)
    {
        Assert(Harmony.Prefix != null && Harmony.Postfix != null,"Harmony hooks are not registered");
        object[] prefixArgs = new object[]{health, null};
        Harmony.Prefix.method.Invoke(null, prefixArgs);
        // Production: Harmony calls HealthHaver.Die(Vector2) between these two methods.
        Harmony.Postfix.method.Invoke(null,new object[]{prefixArgs[1]});
    }
    public static int Main()
    {
        try
        {
            GameManager.Instance = new GameManager {
                PrimaryPlayer = new PlayerController { CenterPosition = new Vector2 {x=3,y=4} }
            };
            var plugin = new BonusEnemyHearts();
            InvokeLifecycle(plugin,"Awake");
            InvokeLifecycle(plugin,"Start");
            Assert(Harmony.Original != null &&
               Harmony.Original.DeclaringType == typeof(HealthHaver) &&
               Harmony.Original.Name == "Die","The global hook targets the wrong method");
            var commands = ETGModConsole.Commands.GetGroup("heartdrop");
            Assert(commands != null,"Console commands not registered");
            commands.Invoke("full","0");
            commands.Invoke("half","100");
            commands.Invoke("status");
            Assert(ETGModConsole.LastLog.Contains("hook=ACTIVE"),"Hook marked inactive");

            var enemy = new AIActor { CenterPosition = new Vector2 {x=1,y=2} };
            var enemyHealth = new HealthHaver { aiActor=enemy };
            int beforeFirstSpawnLogCount = ETGModConsole.LogCount;
            SimulateDeath(enemyHealth);
            Assert(ETGModConsole.LogCount == beforeFirstSpawnLogCount + 1,
                "Expected exactly one automatic status line after half-heart drop");
            string automaticStatus = ETGModConsole.LastLog;
            Assert(automaticStatus.StartsWith("[Bonus Enemy Hearts] ON | hook=ACTIVE | log=ON"),
                "Automatic message does not use regular status formatting");
            Assert(automaticStatus.Contains("spawned=1 (half=1, full=0)"),
                "Automatic status does not include half-heart counter");
            Assert(!automaticStatus.Contains("Bonus drop:"),
                "Old distinct drop message still emitted");
            commands.Invoke("status");
            Assert(ETGModConsole.LastLog == automaticStatus,
                "Automatic message differs from the regular heartdrop status");
            Assert(ETGModConsole.LastLog.Contains("deaths=1"),"Death hook not counted");
            Assert(ETGModConsole.LastLog.Contains("eligible=1"),"Enemy not eligible");
            Assert(ETGModConsole.LastLog.Contains("spawn_attempts=1"),"Spawn attempt not counted");
            Assert(ETGModConsole.LastLog.Contains("spawned=1"),"Heart not spawned");
            Assert(LootEngine.SpawnCounter == 1,"LootEngine not called");

            SimulateDeath(enemyHealth);
            commands.Invoke("status");
            Assert(ETGModConsole.LastLog.Contains("duplicates=1"),"Duplicate death not filtered");
            Assert(LootEngine.SpawnCounter == 1,"Duplicate death spawned bonus loot");

            SimulateDeath(new HealthHaver());
            Assert(LootEngine.SpawnCounter == 1,"Non-enemy HealthHaver got bonus drop");

            SimulateDeath(new HealthHaver {aiActor=new AIActor { IsHarmlessEnemy=true }});
            Assert(LootEngine.SpawnCounter == 1,"Harmless enemy got bonus drop");

            commands.Invoke("log","off");
            Assert(ETGModConsole.LastLog.Contains("log=OFF"),"Log-off status missing");
            string quietStatus = ETGModConsole.LastLog;
            SimulateDeath(new HealthHaver {aiActor=new AIActor()});
            Assert(LootEngine.SpawnCounter==2,"Log-off must not disable heart drops");
            Assert(ETGModConsole.LastLog==quietStatus,"Log-off still emitted an automatic message");

            commands.Invoke("log","on");
            Assert(ETGModConsole.LastLog.Contains("log=ON"),"Log-on status missing");
            commands.Invoke("half","0");
            commands.Invoke("full","100");
            int beforeFullSpawnLogCount = ETGModConsole.LogCount;
            SimulateDeath(new HealthHaver {aiActor=new AIActor()});
            Assert(ETGModConsole.LogCount == beforeFullSpawnLogCount + 1,
                "Expected exactly one automatic status line after full-heart drop");
            string automaticFullStatus = ETGModConsole.LastLog;
            Assert(automaticFullStatus.Contains("spawned=3 (half=2, full=1)"),
                "Full-heart counter missing in automatic status");
            commands.Invoke("status");
            Assert(ETGModConsole.LastLog == automaticFullStatus,
                "Automatic full-heart status differs from regular status");

            int beforeTestLogCount = ETGModConsole.LogCount;
            commands.Invoke("test");
            Assert(LootEngine.SpawnCounter==4,"Manual test failed");
            Assert(ETGModConsole.LogCount == beforeTestLogCount + 1,
                "Manual test should emit exactly one status for a successful drop");

            commands.Invoke("full","2");
            commands.Invoke("half","10");
            Assert(ETGModConsole.LastLog.Contains("half=10% | full=2%"),"Reset default failed");
            InvokeLifecycle(plugin,"OnDestroy");
            Assert(Harmony.Original == null,"Harmony patch not removed");
            Console.WriteLine("PASS: on each half/full bonus heart the exact regular status is printed once; log on/off preserves drops; Harmony hook and deduplication.");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("FAIL: "+e);
            return 1;
        }
    }
}