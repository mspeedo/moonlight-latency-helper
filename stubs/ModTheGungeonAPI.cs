using System;using System.Reflection;
[assembly: AssemblyVersion("1.0.0.0")]
public static class ETGMod {public static class AIActor {public static Action<global::AIActor> OnPostStart;}}
public class ConsoleCommandGroup {
 public ConsoleCommandGroup AddGroup(string command,Action<string[]> action){return null;}
 public ConsoleCommandGroup AddUnit(string command,Action<string[]> action){return null;}
}
namespace SGUI {public class SLabel {}}
public static class ETGModConsole {
 public static ConsoleCommandGroup Commands;
 public static SGUI.SLabel Log(object text,bool isError){return null;}
}