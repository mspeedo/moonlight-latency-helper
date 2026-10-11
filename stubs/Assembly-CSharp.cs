using System;using System.Reflection;using UnityEngine;
[assembly: AssemblyVersion("0.0.0.0")]
public class BraveBehaviour : MonoBehaviour {
 public HealthHaver healthHaver {get;set;}
 public AIActor aiActor {get;set;}
}
public class GameActor : BraveBehaviour { public Vector2 CenterPosition {get;set;} }
public class AIActor : GameActor { public bool IsHarmlessEnemy; public PlayerController CompanionOwner; }
public class PlayerController : GameActor {}
public class GameManager : MonoBehaviour {
 public static GameManager Instance {get;set;}
 public PlayerController PrimaryPlayer {get;set;}
}
public class HealthHaver : BraveBehaviour {
 public Action<Vector2> OnDeath; public Action<Vector2> OnPreDeath;
 public bool IsDead {get;set;}
 public void Die(Vector2 finalDamageDirection) {}
}
public class PickupObject : BraveBehaviour {}
public class DebrisObject : BraveBehaviour {}
public static class PickupObjectDatabase {
 public static PickupObject GetById(int id) {
  return id==73||id==85 ? new PickupObject() : null;
 }
}
public static class LootEngine {
 public static int SpawnCounter;
 public static DebrisObject SpawnItem(GameObject obj, Vector3 pos, Vector2 dir, float force, bool a, bool b, bool c) {
  SpawnCounter++; return new DebrisObject();
 }
}