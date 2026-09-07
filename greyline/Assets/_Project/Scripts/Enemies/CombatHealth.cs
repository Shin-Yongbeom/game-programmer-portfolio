using System;
using Greyline.Combat;
using UnityEngine;

namespace Greyline.Enemies
{
    public sealed class CombatHealth : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] private float maxHealth = 60f;
        [SerializeField] private float currentHealth;
        [SerializeField] private bool isDead;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsDead => isDead;
        public float HealthNormalized => maxHealth <= 0f ? 0f : currentHealth / maxHealth;

        /// <summary>Raised exactly once when this damageable enters its dead state.</summary>
        public event Action<DamageInfo> Died;
        public event Action<DamageInfo> Damaged;

        public void Configure(float health)
        {
            maxHealth = Mathf.Max(1f, health);
            currentHealth = maxHealth;
            isDead = false;
        }

        public void SetMaximumHealth(float value, bool heal)
        {
            maxHealth = Mathf.Max(1, value);
            currentHealth = heal ? maxHealth : Mathf.Min(currentHealth, maxHealth);
            if (heal) isDead = false;
        }

        private void Awake()
        {
            if (currentHealth <= 0f && !isDead)
            {
                currentHealth = maxHealth;
            }
        }

        public void ApplyDamage(DamageInfo damage)
        {
            if (isDead || damage.Amount <= 0f)
            {
                return;
            }

            if (DamageInvulnerability.IsActive(this))
            {
                return;
            }

            var filter = GetComponent<IDamageFilter>();
            if (filter != null && !filter.FilterDamage(damage, out damage)) return;
            if (damage.Amount <= 0f) return;

            currentHealth = Mathf.Max(0f, currentHealth - damage.Amount);
            IHitReactionReceiver reaction = GetComponent<IHitReactionReceiver>();
            reaction?.RequestHitReaction(damage);
            Damaged?.Invoke(damage);
            if (currentHealth <= 0f)
            {
                isDead = true;
                GetComponent<CombatHitReaction>()?.RequestDeath();
                Died?.Invoke(damage);
            }
        }
    }
}
