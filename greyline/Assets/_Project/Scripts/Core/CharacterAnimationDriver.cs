using Greyline.Combat;
using UnityEngine;

namespace Greyline.Core
{
    public sealed class CharacterAnimationDriver : MonoBehaviour, IHitReactionReceiver
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int VerticalVelocityHash = Animator.StringToHash("VerticalVelocity");
        private static readonly int FallDistanceHash = Animator.StringToHash("FallDistance");
        private static readonly int HitReactionHash = Animator.StringToHash("HitReaction");
        private static readonly int FallingStateHash = Animator.StringToHash("Base Layer.Falling");
        private static readonly int CrouchingHash = Animator.StringToHash("Crouching");
        private static readonly int GuardingHash = Animator.StringToHash("Guarding");
        private static readonly int ChargeAmountHash = Animator.StringToHash("ChargeAmount");
        private static readonly int HeavyChargeStateHash = Animator.StringToHash("Base Layer.HeavyCharge");
        private static readonly int FightIdleStateHash = Animator.StringToHash("Base Layer.FightIdle");

        [SerializeField] private Animator animator;
        [SerializeField, Range(0f, 0.5f)] private float attackTransitionDuration = 0.08f;
        [SerializeField] private AnimationPresentationSet presentationSet;
        [SerializeField, Min(0f)] private float dodgePoseRecovery = .12f;

        private int previousStateHash;
        private float lastAirborneVerticalVelocity;
        private float lastFallDistance;
        private bool chargingHeavy;
        private bool hasCrouchingParameter;
        private bool hasGuardingParameter;
        private bool hasHeavyChargeParameter;
        private AnimationPresentationSet.Motion currentMotion;
        private float dodgeStartedAt = -100f;
        private float dodgeMotionDuration;
        private string dodgeState;

        public string AttackPresentationState { get; private set; }
        public void ConfigurePresentation(AnimationPresentationSet set) => presentationSet = set;

        public bool HasAnimator => animator != null && animator.runtimeAnimatorController != null;

        public void Configure(Animator characterAnimator)
        {
            animator = characterAnimator;
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                previousStateHash = 0;
                CachePresentationParameters();
            }
        }

        private void Awake()
        {
            if (animator != null) CachePresentationParameters();
        }

        private void CachePresentationParameters()
        {
            hasCrouchingParameter = false;
            hasGuardingParameter = false;
            hasHeavyChargeParameter = false;
            if (!HasAnimator) return;
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                hasCrouchingParameter |= parameter.nameHash == CrouchingHash;
                hasGuardingParameter |= parameter.nameHash == GuardingHash;
                hasHeavyChargeParameter |= parameter.nameHash == ChargeAmountHash;
            }
        }

        public void SetCrouching(bool crouching)
        {
            if (HasAnimator && hasCrouchingParameter) animator.SetBool(CrouchingHash, crouching);
        }

        public void SetGuarding(bool guarding)
        {
            if (HasAnimator && hasGuardingParameter)
            {
                animator.SetBool(GuardingHash, guarding);
            }
        }

        /// <summary>
        /// Blend authored anticipation poses. Holding a charge never advances into a strike.
        /// </summary>
        public void SetHeavyCharge(bool charging, float normalizedCharge)
        {
            if (!HasAnimator || !hasHeavyChargeParameter || !animator.HasState(0, HeavyChargeStateHash)) return;
            if (charging)
            {
                animator.SetFloat(ChargeAmountHash, Mathf.Clamp01(normalizedCharge), .08f, Time.deltaTime);
                if (!chargingHeavy)
                {
                    animator.CrossFadeInFixedTime(HeavyChargeStateHash, .12f, 0, 0f);
                }
            }
            else if (chargingHeavy)
            {
                AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
                AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
                if (current.fullPathHash == HeavyChargeStateHash || next.fullPathHash == HeavyChargeStateHash)
                {
                    animator.CrossFadeInFixedTime(FightIdleStateHash, .1f, 0, 0f);
                }
            }

            chargingHeavy = charging;
        }

        public bool IsLandingRecovery => HasAnimator &&
            animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Landing");

        public void SetLocomotion(float speed, bool isGrounded, float verticalVelocity, float fallDistance)
        {
            if (!HasAnimator)
            {
                return;
            }

            animator.SetFloat(SpeedHash, speed, .08f, Time.deltaTime);
            animator.SetBool(GroundedHash, isGrounded);
            // CharacterController keeps a small negative vertical velocity while planted.
            // Grounded locomotion must never be interpreted as an airborne descent.
            animator.SetFloat(VerticalVelocityHash, isGrounded ? 0f : verticalVelocity);
            animator.SetFloat(FallDistanceHash, isGrounded ? 0f : Mathf.Max(0f, fallDistance));

            lastAirborneVerticalVelocity = verticalVelocity;
            lastFallDistance = fallDistance;
            LogFallingEntry();
        }

        private void LogFallingEntry()
        {
            int currentStateHash = animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
            if (currentStateHash == FallingStateHash && previousStateHash != FallingStateHash)
            {
                Debug.Log(
                    $"[CharacterAnimation] Falling entered | vertical={lastAirborneVerticalVelocity:0.00}, descent={lastFallDistance:0.00}m",
                    this);
            }

            previousStateHash = currentStateHash;
        }

        public void RequestAttack(AttackDefinition attack, bool useChargedPresentation = false)
        {
            if (!HasAnimator || attack == null || string.IsNullOrWhiteSpace(attack.AnimationState))
            {
                return;
            }

            if (!animator.HasState(0, attack.AnimationStateHash))
            {
                Debug.LogWarning(
                    $"[CharacterAnimation] Missing Animator state '{attack.AnimationState}' for attack '{attack.Id}'. " +
                    "Gameplay may resolve without visible attack animation.",
                    this);
            }

            // Clear our charge latch without an idle transition between release and the strike.
            chargingHeavy = false;
            bool charged = useChargedPresentation && animator.HasState(0, Animator.StringToHash("ChargedUppercut"));
            AttackPresentationState = charged ? "ChargedUppercut" : attack.AnimationState;
            currentMotion = presentationSet != null ? presentationSet.Find(AttackPresentationState) : null;
            SetGuarding(false);
            if (currentMotion != null) AnimationPhasePlayback.Begin(animator, currentMotion, attack.Duration * attack.HitStartNormalized);
            else animator.CrossFadeInFixedTime(AttackPresentationState, attackTransitionDuration, 0, 0f);
        }

        public void SetAttackProgress(AttackDefinition attack, float elapsed)
        {
            if (!HasAnimator || currentMotion == null || attack == null) return;
            float contact = attack.Duration * attack.HitStartNormalized;
            float budget = attack.Duration + attack.RecoveryTime;
            AnimationPhasePlayback.Advance(animator, currentMotion,
                elapsed < contact ? currentMotion.contactSeconds : currentMotion.endSeconds,
                elapsed < contact ? contact - elapsed : budget - elapsed);
            if (elapsed >= budget - Time.deltaTime)
            {
                currentMotion = null;
                animator.CrossFadeInFixedTime(FightIdleStateHash, .08f, 0, 0f);
            }
        }

        public void RequestDodge(Vector3 worldDirection, float motionDuration)
        {
            if (!HasAnimator) return;
            Vector3 local = transform.InverseTransformDirection(worldDirection);
            dodgeState = Mathf.Abs(local.x) > Mathf.Abs(local.z) ?
                (local.x < 0 ? "DodgeLeft" : "DodgeRight") : (local.z < 0 ? "DodgeBackward" : "DodgeForward");
            if (!animator.HasState(0, Animator.StringToHash(dodgeState))) return;
            chargingHeavy = false;
            SetGuarding(false);
            SetCrouching(false);
            dodgeStartedAt = Time.time;
            dodgeMotionDuration = Mathf.Max(.01f, motionDuration);
            currentMotion = presentationSet != null ? presentationSet.Find(dodgeState) : null;
            if (currentMotion != null) AnimationPhasePlayback.Begin(animator, currentMotion, motionDuration + dodgePoseRecovery);
            else animator.CrossFadeInFixedTime(dodgeState, .035f, 0, 0f);
        }

        private void Update()
        {
            if (!HasAnimator || dodgeState == null) return;
            float elapsed = Time.time - dodgeStartedAt;
            float duration = dodgeMotionDuration + dodgePoseRecovery;
            if (currentMotion != null) AnimationPhasePlayback.Advance(animator, currentMotion, currentMotion.endSeconds, duration-elapsed);
            if (elapsed < duration) return;
            var current = animator.GetCurrentAnimatorStateInfo(0);
            if (current.IsName(dodgeState)) animator.CrossFadeInFixedTime(FightIdleStateHash, .08f, 0, 0f);
            dodgeState = null;
            currentMotion = null;
        }

        public bool RequestFinisher(string state = "Finisher", float contactDelay = .58f)
        {
            if (!HasAnimator || !animator.HasState(0, Animator.StringToHash(state))) return false;
            chargingHeavy = false;
            dodgeState = null;
            currentMotion = presentationSet != null ? presentationSet.Find(state) : null;
            if (currentMotion != null) AnimationPhasePlayback.Begin(animator, currentMotion, contactDelay);
            else animator.CrossFadeInFixedTime(state, .06f, 0, 0f);
            return true;
        }

        public void SetFinisherProgress(float elapsed, float contactDelay, float duration)
        {
            if (!HasAnimator || currentMotion == null) return;
            AnimationPhasePlayback.Advance(animator, currentMotion,
                elapsed < contactDelay ? currentMotion.contactSeconds : currentMotion.endSeconds,
                elapsed < contactDelay ? contactDelay - elapsed : duration - elapsed);
        }

        public void EndFinisher()
        {
            currentMotion = null;
            if (HasAnimator && GetComponent<IDamageable>()?.IsDead != true)
                animator.CrossFadeInFixedTime(FightIdleStateHash, .12f, 0, 0f);
        }

        public void RequestHitReaction(DamageInfo damage)
        {
            if (HasAnimator)
            {
                chargingHeavy = false;
                currentMotion = null;
                dodgeState = null;
                animator.SetTrigger(HitReactionHash);
            }
        }
    }
}
