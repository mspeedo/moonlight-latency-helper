using System;
using System.Collections.Generic;
using System.Reflection;
[assembly: AssemblyVersion("1.0.0.0")]
public static class ETGMod {public static class AIActor {public static Action<global::AIActor> OnPostStart;}}
public class ConsoleCommandGroup {
 private readonly Dictionary<string, ConsoleCommandGroup> groups = new Dictionary<string, ConsoleCommandGroup>();
 private readonly Dictionary<string, Action<string[]>> commands = new Dictionary<string, Action<string[]>>();
 private readonly Action<string[]> groupAction;
 public ConsoleCommandGroup() {}
 public ConsoleCommandGroup(Action<string[]> action) {groupAction=action;}
 public ConsoleCommandGroup AddGroup(string name,Action<string[]> action){
  groups[name]=new ConsoleCommandGroup(action);
  return this; // Actual API: returns parent, NOT newly created subgroup!
 }
 public ConsoleCommandGroup GetGroup(string name) { ConsoleCommandGroup result; return groups.TryGetValue(name,out result)?result:null; }
 public ConsoleCommandGroup AddUnit(string name,Action<string[]> action) {commands[name]=action;return this;}
 public void Invoke(string name,params string[] args) {
  Action<string[]> action;
  if (commands.TryGetValue(name,out action)) {action(args);return;}
  if (groupAction!=null) {groupAction(args);return;}
  throw new Exception("Unknown command: " + name);
 }
}
namespace SGUI {public class SLabel {}}
public static class ETGModConsole {
 public static ConsoleCommandGroup Commands = new ConsoleCommandGroup();
 public static string LastLog;
 public static SGUI.SLabel Log(object text,bool isError){LastLog=text==null?"":text.ToString();Console.WriteLine(LastLog);return null;}
}
