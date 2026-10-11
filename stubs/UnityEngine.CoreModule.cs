using System.Reflection;
[assembly: AssemblyVersion("0.0.0.0")]
namespace UnityEngine {
 public class Object {
   private static int nextId;
   private int instanceId = ++nextId;
   public int GetInstanceID(){return instanceId;}
   public static bool operator ==(Object a,Object b){return object.ReferenceEquals(a,b);}
   public static bool operator !=(Object a,Object b){return !object.ReferenceEquals(a,b);}
   public override bool Equals(object o){return object.ReferenceEquals(this,o);}
   public override int GetHashCode(){return base.GetHashCode();}
 }
 public class GameObject : Object {}
 public class Component : Object {public GameObject gameObject {get{return new GameObject();}}}
 public class Behaviour:Component{}
 public class MonoBehaviour:Behaviour{}
 public struct Vector2 {public float x,y;public static Vector2 zero {get{return new Vector2();}}}
 public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}}
 public class Time {public static float realtimeSinceStartup {get;set;}}
}