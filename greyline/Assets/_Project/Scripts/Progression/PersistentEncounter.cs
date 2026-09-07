using System.Linq;
using Greyline.Combat;
using Greyline.Enemies;
using UnityEngine;

namespace Greyline.Progression
{
    public sealed class PersistentEncounter : MonoBehaviour
    {
        [SerializeField] private string encounterId;
        private CombatHealth[] enemies;
        public string Id => encounterId;
        public void Configure(string id) => encounterId = id;
        private void Start()
        {
            if (ProductionSession.Current == null) return;
            if (ProductionSession.Current.IsCleared(encounterId)) { gameObject.SetActive(false); return; }
            enemies = GetComponentsInChildren<EnemyAttack>().Select(e => e.GetComponent<CombatHealth>()).ToArray();
            foreach (var enemy in enemies) enemy.Died += OnDefeated;
        }
        private void OnDefeated(DamageInfo hit)
        {
            if (enemies.Length > 0 && enemies.All(e => e != null && e.IsDead))
                ProductionSession.Current?.CompleteEncounter(encounterId);
        }
        private void OnDestroy()
        {
            if (enemies != null) foreach (var enemy in enemies) if (enemy != null) enemy.Died -= OnDefeated;
        }
    }
}
