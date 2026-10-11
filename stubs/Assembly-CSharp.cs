using System;using System.Reflection;using UnityEngine;
[assembly: AssemblyVersion("0.0.0.0")]
public class BraveBehaviour: MonoBehaviour {public HealthHaver healthHaver {get{return null;}}}
public class GameActor:BraveBehaviour {public Vector2 CenterPosition {get{return new Vector2();}} }
public class AIActor:GameActor {public bool IsHarmlessEnemy;public PlayerController CompanionOwner;}
public class PlayerController:GameActor{}
public class HealthHaver:BraveBehaviour {public Action<Vector2> OnDeath;}
public class PickupObject:BraveBehaviour{}
public class DebrisObject:BraveBehaviour{}
public static class PickupObjectDatabase{public static PickupObject GetById(int id){return null;}}
public static class LootEngine{public static DebrisObject SpawnItem(GameObject o,Vector3 p,Vector2 d,float force,bool a,bool b,bool c){return null;}}