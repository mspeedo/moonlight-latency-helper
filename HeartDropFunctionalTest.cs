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
            SimulateDeath(enemyHealth);
            commands.Invoke("status");
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

            commands.Invoke("test");
            Assert(LootEngine.SpawnCounter==2,"Manual test failed");

            commands.Invoke("half","10");
            commands.Invoke("full","2");
            Assert(ETGModConsole.LastLog.Contains("half=10% | full=2%"),"Reset default failed");
            InvokeLifecycle(plugin,"OnDestroy");
            Assert(Harmony.Original == null,"Harmony patch not removed");
            Console.WriteLine("PASS: global Harmony.Die patch registers, catches enemy deaths, spawns, deduplicates, filters, unloads.");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("FAIL: "+e);
            return 1;
        }
    }
}