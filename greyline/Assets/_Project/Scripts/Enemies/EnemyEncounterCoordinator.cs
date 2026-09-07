using System.Collections.Generic;
using UnityEngine;

namespace Greyline.Enemies
{
    /// <summary>Owns a local encounter's attack turn and chase boundary. No global combat state.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyEncounterCoordinator : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField, Min(1f)] private float engagementRadius = 16f;
        [SerializeField, Min(1f)] private float leashRadius = 23f;
        [SerializeField, Min(0f)] private float attackGap = .35f;
        [SerializeField, Min(0f)] private float reengageDelay = 3f;
        private readonly List<EnemyAttack> members = new List<EnemyAttack>();
        private readonly List<EnemyAttack> waiting = new List<EnemyAttack>();
        private EnemyAttack activeAttacker;
        private bool engaged;
        private float nextAttackAt;
        private float reengageAt;

        public Transform Target => target;
        public EnemyAttack ActiveAttacker => activeAttacker;
        public bool IsEngaged => engaged;
        public float EngagementRadius => engagementRadius;
        public float LeashRadius => leashRadius;
        public int ActiveEnemyCount
        {
            get
            {
                int count = 0;
                foreach (EnemyAttack member in members)
                    if (member != null && member.isActiveAndEnabled && !member.IsDefeated) count++;
                return count;
            }
        }

        public void Configure(Transform attackTarget, float engagementRadius = 16f,
            float leashRadius = 23f, float attackGap = .35f)
        {
            target = attackTarget;
            this.engagementRadius = Mathf.Max(1f, engagementRadius);
            this.leashRadius = Mathf.Max(this.engagementRadius + 1f, leashRadius);
            this.attackGap = Mathf.Max(0f, attackGap);
        }

        private void Update()
        {
            if (activeAttacker != null && (!activeAttacker.isActiveAndEnabled || activeAttacker.IsDefeated))
                Release(activeAttacker);
            if (target == null || ActiveEnemyCount == 0) { Disengage(); return; }
            CombatHealth targetHealth = target.GetComponent<CombatHealth>();
            float distance = FlatDistanceSquared(transform.position, target.position);
            if ((targetHealth != null && targetHealth.IsDead) || distance > leashRadius * leashRadius)
            {
                if (engaged) reengageAt = Time.time + reengageDelay;
                Disengage();
                return;
            }
            if (!engaged && Time.time >= reengageAt && distance <= engagementRadius * engagementRadius)
                engaged = true;
        }

        private void OnDisable() => Disengage();
        internal void Register(EnemyAttack enemy)
        {
            if (enemy != null && !members.Contains(enemy)) members.Add(enemy);
        }
        internal void Unregister(EnemyAttack enemy)
        {
            Release(enemy);
            members.Remove(enemy);
            waiting.Remove(enemy);
        }
        internal bool TryAcquire(EnemyAttack enemy)
        {
            if (!isActiveAndEnabled || !engaged || enemy == null || !enemy.IsReadyForPermission) return false;
            if (activeAttacker == enemy) return true;
            if (!waiting.Contains(enemy)) waiting.Add(enemy);
            // FIFO prevents a close bruiser from permanently starving the flankers.
            waiting.RemoveAll(candidate => candidate == null || !candidate.IsReadyForPermission);
            if (activeAttacker != null || Time.time < nextAttackAt || waiting.Count == 0 || waiting[0] != enemy)
                return false;
            waiting.RemoveAt(0);
            activeAttacker = enemy;
            return true;
        }
        internal void Release(EnemyAttack enemy)
        {
            waiting.Remove(enemy);
            if (activeAttacker != enemy) return;
            activeAttacker = null;
            nextAttackAt = Time.time + attackGap;
        }
        internal Vector3 GetOrbitPoint(EnemyAttack enemy, float radius)
        {
            int index = Mathf.Max(0, members.IndexOf(enemy));
            float angle = (index * 360f / Mathf.Max(1, members.Count) + 25f) * Mathf.Deg2Rad;
            Vector3 center = target != null ? target.position : transform.position;
            return center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
        }
        private void Disengage()
        {
            engaged = false;
            activeAttacker = null;
            waiting.Clear();
        }
        private static float FlatDistanceSquared(Vector3 a, Vector3 b)
        {
            a.y = b.y;
            return (a - b).sqrMagnitude;
        }
    }
}
