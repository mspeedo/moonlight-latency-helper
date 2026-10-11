using System;
using System.Reflection;
[assembly: AssemblyVersion("2.10.1.0")]
namespace HarmonyLib {
 public sealed class HarmonyMethod {
  public readonly MethodInfo method;
  public HarmonyMethod(MethodInfo value){method=value;}
 }
 public sealed class Harmony {
  public static MethodBase Original;
  public static HarmonyMethod Prefix,Postfix;
  public readonly string id;
  public Harmony(string guid){id=guid;}
  public MethodInfo Patch(MethodBase target, HarmonyMethod prefix=null, HarmonyMethod postfix=null,
       HarmonyMethod transpiler=null, HarmonyMethod finalizer=null) {
   Original=target; Prefix=prefix;Postfix=postfix; return target as MethodInfo;
  }
  public void UnpatchSelf() { Original=null; Prefix=null;Postfix=null; }
 }
}