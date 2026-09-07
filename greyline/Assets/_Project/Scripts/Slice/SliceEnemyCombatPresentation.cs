using Greyline.Enemies;
using UnityEngine;

namespace Greyline.Slice
{
    /// <summary>
    /// Slice-only visual bridge. EnemyAttack remains the gameplay authority; this component only
    /// requests the Slice-owned enemy states while EnemyAttack and CombatHealth remain gameplay
    /// authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SliceEnemyCombatPresentation : MonoBehaviour
    {
        private static readonly int JabHash = Animator.StringToHash("Jab");
        private static readonly int StaggerHash = Animator.StringToHash("Stagger");
        private static readonly int DeathHash = Animator.StringToHash("Death");

        [SerializeField] private EnemyAttack attack;
        [SerializeField] private CombatHealth health;
        [SerializeField] private Animator animator;
        private bool wasActive;
        private bool deathRequested;
        private float previousHealth;

        public void Configure(EnemyAttack enemyAttack, Animator enemyAnimator)
        {
            attack = enemyAttack;
            animator = enemyAnimator;
            health = GetComponent<CombatHealth>();
        }

        private void Awake()
        {
            attack ??= GetComponent<EnemyAttack>();
            health ??= GetComponent<CombatHealth>();
            previousHealth = health != null ? health.CurrentHealth : 0f;
        }

        private void Update()
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            if (health != null && health.IsDead)
            {
                if (!deathRequested)
                {
                    RequestState(DeathHash, "Death");
                    deathRequested = true;
                }

                return;
            }

            if (attack == null)
            {
                return;
            }

            bool isActive = attack.IsActive;
            if (isActive && !wasActive)
            {
                RequestState(JabHash, "Jab");
            }

            if (health != null && health.CurrentHealth < previousHealth)
            {
                RequestState(StaggerHash, "Stagger");
            }

            wasActive = isActive;
            previousHealth = health != null ? health.CurrentHealth : previousHealth;
        }

        private void RequestState(int stateHash, string trigger)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == trigger)
                {
                    animator.SetTrigger(parameter.nameHash);
                    return;
                }
            }

            // Existing scenes upgrade in place. Until their visual Animator is refreshed to the
            // Slice controller, retain the old direct Jab path instead of dropping attacks.
            animator.CrossFade(stateHash, .06f, 0, 0f);
        }
    }
}
