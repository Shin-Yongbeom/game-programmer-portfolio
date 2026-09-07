using UnityEngine;
using Greyline.Core;

namespace Greyline.Enemies
{
    [DefaultExecutionOrder(100)]
    public sealed class DistrictEnemyPresentation : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private AnimationPresentationSet presentationSet;
        private EnemyAttack attack;
        private CombatHealth health;
        private CombatHitReaction reaction;
        private string previous;
        private float hitUntil;
        public string PresentedState => previous;
        public void Configure(Animator visual, AnimationPresentationSet set = null) { animator=visual;presentationSet=set; }
        private void Awake()
        {
            if (animator != null) animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            attack = GetComponent<EnemyAttack>(); health = GetComponent<CombatHealth>();
            reaction = GetComponent<CombatHitReaction>();
            if (health != null) health.Damaged += OnDamage;
        }
        private void OnDamage(Combat.DamageInfo hit) => hitUntil = Time.time + .3f;
        private void Update()
        {
            if (animator == null || attack == null || health == null) return;
            // CombatHitReaction owns death entry, final-pose hold and corpse placement.
            // Restarting Death here after the health event made its first pose play twice.
            if (health.IsDead)
            {
                reaction?.RequestDeath();
                previous = "Death";
                return;
            }
            if (attack.IsFinisherHeld)
            {
                if (previous != "FinisherHeld") animator.CrossFadeInFixedTime("FinisherHeld", .12f, 0, 0f);
                previous = "FinisherHeld";
                return;
            }
            bool attacking = attack.IsTelegraphing || attack.IsActive || attack.IsRecoveringAttack;
            string state = Time.time < hitUntil ? "Hit" : attacking ? attack.PresentationAttackState :
                attack.IsApproaching || attack.IsOrbiting || attack.IsReturning ? "Walk" : "Idle";
            if (attacking && state != "Hit")
            {
                var motion = presentationSet != null ? presentationSet.Find(state) : null;
                if (motion != null)
                {
                    if (state != previous) AnimationPhasePlayback.Begin(animator,motion,attack.PresentationPhaseRemaining);
                    float landmark=attack.IsTelegraphing ? motion.contactSeconds : attack.IsActive ? motion.followThroughSeconds : motion.endSeconds;
                    AnimationPhasePlayback.Advance(animator,motion,landmark,attack.PresentationPhaseRemaining);
                    previous=state;
                    return;
                }
            }
            if (state == previous) return;
            previous = state;
            animator.CrossFadeInFixedTime(state, .08f, 0, 0f);
        }
        private void OnDestroy() { if (health != null) health.Damaged -= OnDamage; }
    }
}
