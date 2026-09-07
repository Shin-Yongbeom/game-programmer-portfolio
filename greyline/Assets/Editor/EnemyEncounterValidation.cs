using System;
using System.Reflection;
using Greyline.Combat;
using Greyline.Enemies;
using UnityEditor;
using UnityEngine;

namespace Greyline.EditorTools
{
    /// <summary>Small deterministic rule checks. Play QA additionally observes real-time pacing and movement.</summary>
    public static class EnemyEncounterValidation
    {
        [MenuItem("Greyline/Validate Enemy Encounter Rules")]
        public static void ValidateFromCommandLine() => Validate();

        public static void Validate()
        {
            GameObject root = new GameObject("EnemyEncounterRuleValidation");
            root.transform.position = new Vector3(2000f, 0f, 2000f);
            try
            {
                Vector3 origin = root.transform.position;
                GameObject player = CreateBody(root.transform, "Target", origin, 1000f);
                EnemyEncounterCoordinator coordinator = root.AddComponent<EnemyEncounterCoordinator>();
                coordinator.Configure(player.transform, 10f, 14f, 0f);
                EnemyAttack a = CreateEnemy(root.transform, "First", player.transform, origin + new Vector3(0f, 0f, -1.5f), coordinator);
                EnemyAttack b = CreateEnemy(root.transform, "Second", player.transform, origin + new Vector3(1.4f, 0f, .7f), coordinator);
                EnemyAttack c = CreateEnemy(root.transform, "Third", player.transform, origin + new Vector3(-1.4f, 0f, .7f), coordinator);
                // Live approach stops inside MoveToward's 4 cm arrival tolerance. It must not
                // require reaching the exact stop distance before beginning its attack warning.
                a.transform.position = origin + Vector3.back * (a.ApproachStopDistance + .02f);
                SetState(a, "Approach");
                Physics.SyncTransforms();
                Tick(coordinator);
                Tick(a);
                Require(a.IsTelegraphing && coordinator.ActiveAttacker == a,
                    "An approaching enemy at stop distance plus 2 cm must acquire its turn and telegraph.");
                Tick(b);
                Tick(c);
                Require(a.IsTelegraphing && !b.IsTelegraphing && !c.IsTelegraphing && coordinator.ActiveAttacker == a,
                    "Only one of three enemies may telegraph or attack.");

                SetField(a, "stateStartedAt", Time.time - 10f);
                Tick(a);
                Require(a.IsActive, "The telegraph must advance to an active strike.");
                a.TryApplyMeleeHit();
                float afterHit = player.GetComponent<CombatHealth>().CurrentHealth;
                Require(afterHit < 1000f, "An unobstructed in-range strike must reach its player target.");
                a.TryApplyMeleeHit();
                Require(player.GetComponent<CombatHealth>().CurrentHealth == afterHit,
                    "Repeated contact callbacks must not apply a second hit.");
                Require(b.GetComponent<CombatHealth>().CurrentHealth == 100f && c.GetComponent<CombatHealth>().CurrentHealth == 100f,
                    "An enemy swing must not damage encounter allies.");
                a.ReceiveCounter(new CounterRequest(player, default));
                Require(a.StateName == "Recovery" && coordinator.ActiveAttacker == null,
                    "A successful counter must release the attack turn and open recovery.");
                Tick(b);
                Tick(c);
                Require(coordinator.ActiveAttacker == b && b.IsTelegraphing && !c.IsTelegraphing,
                    "The next waiting enemy must receive the released turn.");
                b.GetComponent<CombatHealth>().ApplyDamage(new DamageInfo(player, 200f, b.transform.position,
                    Vector3.forward, HitReactionType.Heavy, 0f, 0f));
                Tick(coordinator);
                Tick(c);
                Require(coordinator.ActiveAttacker == c, "A defeated attacker must not strand its attack turn.");
                Require(!(bool)typeof(EnemyAttack).GetProperty("IsReadyForPermission", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(b),
                    "A defeated enemy must not be permission-ready.");
                Require(!(bool)typeof(EnemyEncounterCoordinator).GetMethod("TryAcquire", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(coordinator, new object[] { b }),
                    "A defeated enemy must not acquire an attack turn.");
                SetState(b, "Active");
                SetField(b, "damageApplied", false);
                float beforeDefeatedContact = player.GetComponent<CombatHealth>().CurrentHealth;
                b.TryApplyMeleeHit();
                Require(player.GetComponent<CombatHealth>().CurrentHealth == beforeDefeatedContact,
                    "A defeated enemy must not deal melee damage even after a late animation contact.");

                player.transform.position = origin + Vector3.right * 20f;
                Tick(coordinator);
                Tick(c);
                Require(!coordinator.IsEngaged && coordinator.ActiveAttacker == null && !c.IsTelegraphing && !c.IsActive,
                    "Leaving the encounter leash must stop the attack cycle.");
                a.gameObject.SetActive(false);
                b.gameObject.SetActive(false);
                c.gameObject.SetActive(false);

                player.transform.position = origin;
                EnemyAttack boss = CreateEnemy(root.transform, "Boss", player.transform, origin + Vector3.back * 1.5f, null);
                boss.ConfigureEncounter(null, EnemyArchetype.Boss);
                boss.GetComponent<CombatHealth>().ApplyDamage(new DamageInfo(player, 60f, boss.transform.position,
                    Vector3.forward, HitReactionType.Heavy, 0f, 0f));
                Tick(boss);
                Require(boss.BossPhase == 2 && boss.StateName == "Recovery", "Boss half-health transition must open a phase-change recovery.");
                SetField(boss, "attackSequence", 2);
                Invoke(boss, "BeginAttack");
                Require(boss.IsUnblockable && !boss.CanBeCountered && boss.CurrentAttackId.EndsWith(".sweep"),
                    "Phase two's third pattern must expose an unblockable sweep warning.");

                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "OcclusionTestWall";
                wall.transform.SetParent(root.transform);
                wall.transform.position = origin + new Vector3(0f, 1f, -.75f);
                wall.transform.localScale = new Vector3(2f, 2f, .2f);
                Physics.SyncTransforms();
                Require(!(bool)Invoke(boss, "HasClearTargetLine"), "A solid wall must occlude melee targeting.");
                SetState(boss, "Active");
                SetField(boss, "damageApplied", false);
                float beforeWall = player.GetComponent<CombatHealth>().CurrentHealth;
                boss.TryApplyMeleeHit();
                Require(player.GetComponent<CombatHealth>().CurrentHealth == beforeWall, "Melee must not hit through the occluding wall.");

                MethodInfo travel = typeof(EnemyAttack).GetMethod("GetClearTravel", BindingFlags.Instance | BindingFlags.NonPublic);
                object[] args = { Vector3.forward, 1f, Vector3.zero };
                float clearTravel = (float)travel.Invoke(boss, args);
                Require(clearTravel >= 0f && clearTravel < .35f, "The enemy capsule must stop before the wall instead of crossing it.");
                Debug.Log("[EnemyEncounterValidation] PASS: stop-distance arrival, single attack turn, fair release, counter/death, defeated enemy denied permission/contact, one contact, allies, wall collision/occlusion, leash, boss phase two.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                Physics.SyncTransforms();
            }
        }

        private static GameObject CreateBody(Transform parent, string name, Vector3 position, float hitPoints)
        {
            GameObject body = new GameObject(name);
            body.transform.SetParent(parent);
            body.transform.position = position;
            CapsuleCollider collider = body.AddComponent<CapsuleCollider>();
            collider.center = Vector3.up;
            collider.height = 2f;
            collider.radius = .4f;
            body.AddComponent<CombatHealth>().Configure(hitPoints);
            return body;
        }
        private static EnemyAttack CreateEnemy(Transform parent, string name, Transform player, Vector3 position,
            EnemyEncounterCoordinator coordinator)
        {
            GameObject body = CreateBody(parent, name, position, 100f);
            body.transform.rotation = Quaternion.LookRotation(player.position - position);
            EnemyAttack attack = body.AddComponent<EnemyAttack>();
            attack.Configure(player, 2.4f, .5f, .2f, .5f, .5f, 10f);
            attack.ConfigureMovement(10f, 1.7f, 2f, 720f);
            attack.ConfigureMelee(1, .6f, .75f, .1f);
            attack.ConfigureEncounter(coordinator);
            Invoke(attack, "Awake");
            return attack;
        }
        private static void Tick(object target) => Invoke(target, "Update");
        private static object Invoke(object target, string method) => target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        private static void SetField(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void SetState(EnemyAttack target, string state)
        {
            FieldInfo field = typeof(EnemyAttack).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, Enum.Parse(field.FieldType, state));
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[EnemyEncounterValidation] " + message);
        }
    }
}
