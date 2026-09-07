using System;
using System.Reflection;
using Greyline.Combat;
using Greyline.Enemies;
using UnityEngine;

namespace Greyline.World.EditorTools
{
    public static class ProductionDefenseValidation
    {
        public static void Validate()
        {
            GameObject root = new("DefenseValidation");
            try
            {
                GameObject player = new("Defender"); player.transform.SetParent(root.transform);
                CombatHealth health = player.AddComponent<CombatHealth>(); health.Configure(100);
                CombatDefense defense = player.AddComponent<CombatDefense>();
                typeof(CombatDefense).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(defense,null);
                GameObject attacker = new("Attacker"); attacker.transform.SetParent(root.transform); attacker.transform.position=Vector3.forward*2;
                attacker.AddComponent<CombatHealth>().Configure(100);
                var attack = attacker.AddComponent<EnemyAttack>();
                typeof(EnemyAttack).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(attack,null);
                int accepted=0; health.Damaged += _ => accepted++;
                defense.SetGuardHeld(true);
                DamageInfo Hit(float amount, CombatHitFlags flags=CombatHitFlags.None) => new(attacker,"validation",0,amount,Vector3.up,Vector3.back,HitReactionType.Light,0,0,flags);
                health.ApplyDamage(Hit(10));
                Require(health.CurrentHealth==100 && accepted==0 && defense.PostureNormalized>0,"Frontal guard must absorb contact without damage events.");
                attacker.transform.position=Vector3.back*2;
                health.ApplyDamage(Hit(10));
                Require(health.CurrentHealth==90 && accepted==1,"Rear attacks must bypass guard.");
                attacker.transform.position=Vector3.forward*2;
                var state=typeof(EnemyAttack).GetField("state",BindingFlags.Instance|BindingFlags.NonPublic);
                state.SetValue(attack,Enum.Parse(state.FieldType,"Telegraph"));
                health.ApplyDamage(Hit(10,CombatHitFlags.Counterable));
                Require(defense.CounterCount==1 && health.CurrentHealth==90 && attack.StateName=="Recovery" && attacker.GetComponent<CombatHealth>().CurrentHealth==92,"Counter window must reject damage and stagger the source exactly once.");
                health.ApplyDamage(Hit(65));
                Require(defense.IsGuardBroken && health.CurrentHealth==25,"Exhausted posture must break guard and accept damage.");
                health.ApplyDamage(Hit(40));
                Require(health.IsDead,"Guard break must allow lethal follow-up.");
                Require(!defense.SetGuardHeld(true),"Defeated players must not guard.");
                Debug.Log("PRODUCTION_DEFENSE_VALIDATION_OK: front/rear, counter, posture break, death, accepted-hit events.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static void Require(bool condition,string message) { if(!condition)throw new InvalidOperationException(message); }
    }
}
