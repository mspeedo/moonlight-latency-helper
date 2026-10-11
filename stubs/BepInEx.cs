using System;using System.Reflection;using UnityEngine;
[assembly: AssemblyVersion("5.4.21.0")]
namespace BepInEx {
 [AttributeUsage(AttributeTargets.Class)] public sealed class BepInPlugin : Attribute{public BepInPlugin(string guid,string name,string version){}}
 public class BaseUnityPlugin : MonoBehaviour{public BepInEx.Logging.ManualLogSource Logger{get{return null;}}}
}
namespace BepInEx.Logging { public class ManualLogSource {public void LogWarning(object obj){} } }