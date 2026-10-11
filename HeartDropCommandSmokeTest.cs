using System;
using System.Reflection;
public static class HeartDropCommandSmokeTest
{
    public static int Main()
    {
        try {
            var plugin = new BonusEnemyHearts();
            typeof(BonusEnemyHearts)
                .GetMethod("RegisterCommandsIfReady", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(plugin, null);
            var group = ETGModConsole.Commands.GetGroup("heartdrop");
            if (group == null) throw new Exception("heartdrop subgroup missing");
            group.Invoke("full", "0");
            group.Invoke("half", "100");
            group.Invoke("status");
            if (!ETGModConsole.LastLog.Contains("half=100% | full=0%"))
                throw new Exception("Subcommands failed, actual console output: " + ETGModConsole.LastLog);
            group.Invoke("half", "10");
            group.Invoke("full", "2");
            if (!ETGModConsole.LastLog.Contains("half=10% | full=2%"))
                throw new Exception("Could not restore defaults");
            Console.WriteLine("PASS: heartdrop full/half/status correctly invoke subgroup handlers.");
            return 0;
        } catch (Exception ex) {
            Console.Error.WriteLine("FAIL: " + ex);
            return 1;
        }
    }
}