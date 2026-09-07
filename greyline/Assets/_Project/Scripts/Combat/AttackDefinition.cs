using UnityEngine;

namespace Greyline.Combat
{
    [CreateAssetMenu(fileName = "AttackDefinition", menuName = "Greyline/Combat/Attack Definition")]
    public sealed class AttackDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string animationState;
        [SerializeField, Min(0f)] private float damage = 10f;
        [SerializeField, Min(0.01f)] private float duration = 0.6f;
        [SerializeField, Range(0f, 1f)] private float hitStartNormalized = 0.3f;
        [SerializeField, Range(0f, 1f)] private float hitEndNormalized = 0.55f;
        [SerializeField, Min(0f)] private float range = 1.5f;
        [SerializeField, Min(0f)] private float radius = 0.45f;
        [SerializeField] private float forwardOffset = 0.7f;
        [SerializeField, Min(0f)] private float recoveryTime = 0.15f;
        [SerializeField, Range(0f, 1f)] private float comboInputOpenNormalized = 0.4f;
        [SerializeField] private HitReactionType reactionType = HitReactionType.Light;
        [SerializeField] private CombatHitFlags hitFlags = CombatHitFlags.Counterable;
        [SerializeField, Min(0f)] private float knockbackDistance = 0.3f;
        [SerializeField, Min(0.01f)] private float knockbackDuration = 0.12f;
        [SerializeField] private AttackDefinition nextAttack;

        public string Id => id;
        public string AnimationState => animationState;
        public bool HasAnimationPresentation => !string.IsNullOrWhiteSpace(animationState);
        public int AnimationStateHash => HasAnimationPresentation ? Animator.StringToHash(animationState) : 0;
        public float Damage => damage;
        public float Duration => duration;
        public float HitStartNormalized => hitStartNormalized;
        public float HitEndNormalized => hitEndNormalized;
        public float Range => range;
        public float Radius => radius;
        public float ForwardOffset => forwardOffset;
        public float RecoveryTime => recoveryTime;
        public float ComboInputOpenNormalized => comboInputOpenNormalized;
        public HitReactionType ReactionType => reactionType;
        public CombatHitFlags HitFlags => hitFlags;
        public float KnockbackDistance => knockbackDistance;
        public float KnockbackDuration => knockbackDuration;
        public AttackDefinition NextAttack => nextAttack;

        public void Configure(
            string attackId,
            string stateName,
            float attackDamage,
            float attackDuration,
            float hitStart,
            float hitEnd,
            float attackRange,
            float attackRadius,
            float attackForwardOffset,
            float recovery,
            AttackDefinition next = null,
            float comboOpenNormalized = .4f,
            HitReactionType hitReactionType = HitReactionType.Light,
            float knockback = .3f,
            float knockbackTime = .12f)
        {
            id = attackId;
            animationState = stateName;
            damage = Mathf.Max(0f, attackDamage);
            duration = Mathf.Max(0.01f, attackDuration);
            hitStartNormalized = Mathf.Clamp01(hitStart);
            hitEndNormalized = Mathf.Clamp(hitEnd, hitStartNormalized, 1f);
            range = Mathf.Max(0f, attackRange);
            radius = Mathf.Max(0f, attackRadius);
            forwardOffset = attackForwardOffset;
            recoveryTime = Mathf.Max(0f, recovery);
            comboInputOpenNormalized = Mathf.Clamp01(comboOpenNormalized);
            reactionType = hitReactionType;
            knockbackDistance = Mathf.Max(0f, knockback);
            knockbackDuration = Mathf.Max(.01f, knockbackTime);
            nextAttack = next;
        }

        public bool IsInHitWindow(float normalizedTime)
        {
            return normalizedTime >= hitStartNormalized && normalizedTime <= hitEndNormalized;
        }
    }
}
