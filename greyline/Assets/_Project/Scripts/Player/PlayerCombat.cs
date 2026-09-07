using System.Collections.Generic;
using Greyline.Combat;
using Greyline.Core;
using Greyline.Enemies;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Greyline.Player
{
    public sealed class PlayerCombat : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private AttackDefinition firstLightAttack;
        [SerializeField] private AttackDefinition heavyAttack;
        // Must exceed every attack's (Duration * ComboInputOpenNormalized): Light1 .168s, Light2
        // .205s, Light3 .286s. 0.2s was shorter than Light2/Light3's window-open time, so an early
        // (non-spammed) combo press frequently expired before the window opened - dropped combo.
        [SerializeField, Min(0f)] private float inputBufferDuration = 0.35f;
        [Header("Heavy charge")]
        [SerializeField] private bool enableHeavyCharge = true;
        [SerializeField, Min(0f)] private float heavyChargeMinDuration = 0.12f;
        [SerializeField, Min(0.01f)] private float heavyChargeMaxDuration = 0.85f;
        [SerializeField, Min(1f)] private float heavyChargeMaxDamageMultiplier = 1.6f;
        [SerializeField, Min(1f)] private float heavyChargeMaxKnockbackMultiplier = 1.35f;
        [SerializeField] private CharacterAnimationDriver animationDriver;
        [SerializeField] private PlayerDodge dodge;
        [SerializeField] private LayerMask damageableLayers = 1;
        [SerializeField, Min(0f)] private float targetAssistDistance = 3f;
        [SerializeField, Range(0f, 180f)] private float targetAssistAngle = 50f;
        [SerializeField, Range(0f, 90f)] private float targetAssistMaxYawDegrees = 25f;
        [SerializeField, Tooltip("Console log for attack start / combo queue / hit / combo end.")] private bool logStateChanges = true;

        private InputAction attackAction;
        private InputAction heavyAttackAction;
        private AttackDefinition currentAttack;
        private float attackElapsed;
        private float bufferedInputUntil = -1f;
        private float bufferedHeavyUntil = -1f;
        // Sticky (non-decaying) requests made while an attack is active. A short real-time buffer
        // is correct for "idle" input (forgiveness window before the character can react at all),
        // but a mid-combo request must survive until its own decision point regardless of how long
        // that takes - decaying it by a fixed duration is what dropped Light2/Light3 continuations
        // and buffered Heavy presses under normal (non-spammed) human timing.
        private bool comboRequested;
        private bool heavyRequestedDuringAttack;
        private float heavyChargeStartedAt = -1f;
        private float currentDamageMultiplier = 1f;
        private float trainingHeavyMultiplier = 1f;
        public float TrainingHeavyMultiplier => trainingHeavyMultiplier;
        public void SetTrainingHeavyMultiplier(float value) => trainingHeavyMultiplier = Mathf.Max(1, value);
        private float currentKnockbackMultiplier = 1f;
        private bool nextAttackQueued;
        private readonly Collider[] hitBuffer = new Collider[16];
        private readonly HashSet<int> hitTargetIds = new();
        private IDamageable selfDamageable;
        private bool deathHandled;
        private CombatDefense defense;
        private ContextualCombat contextual;
        private ThirdPersonPlayerMotor motor;
        private CombatHealth health;
        private float interruptedUntil;
        private readonly RaycastHit[] obstructionHits = new RaycastHit[24];

        public AttackDefinition CurrentAttack => currentAttack;
        public AttackDefinition FirstLightAttack => firstLightAttack;
        public AttackDefinition HeavyAttack => heavyAttack;
        public bool IsAttacking => currentAttack != null;
        public float NormalizedAttackTime => currentAttack == null ? 0f : Mathf.Clamp01(attackElapsed / currentAttack.Duration);
        public bool IsHitWindowOpen => currentAttack != null && currentAttack.IsInHitWindow(NormalizedAttackTime);
        public bool IsChargingHeavy => heavyChargeStartedAt >= 0f;
        public float HeavyChargeNormalized => !IsChargingHeavy
            ? 0f
            : Mathf.Clamp01((Time.time - heavyChargeStartedAt) / Mathf.Max(0.01f, heavyChargeMaxDuration));
        public LayerMask DamageableLayers => damageableLayers;
        public bool HasInputAsset => inputActions != null;

        public void Configure(InputActionAsset actions, CharacterAnimationDriver driver)
        {
            inputActions = actions;
            animationDriver = driver;
        }

        public void Configure(InputActionAsset actions, CharacterAnimationDriver driver, AttackDefinition firstAttack)
        {
            Configure(actions, driver);
            firstLightAttack = firstAttack;
        }

        public void ConfigureCombat(InputActionAsset actions, CharacterAnimationDriver driver, AttackDefinition firstAttack, LayerMask targetLayers)
        {
            Configure(actions, driver, firstAttack);
            damageableLayers = targetLayers;
        }

        public void ConfigureCombat(InputActionAsset actions, CharacterAnimationDriver driver, AttackDefinition firstAttack, AttackDefinition heavy, LayerMask targetLayers)
        {
            ConfigureCombat(actions, driver, firstAttack, targetLayers);
            heavyAttack = heavy;
        }

        private void Awake()
        {
            animationDriver ??= GetComponent<CharacterAnimationDriver>();
            dodge ??= GetComponent<PlayerDodge>();
            selfDamageable ??= GetComponent<IDamageable>();
            defense = GetComponent<CombatDefense>();
            contextual = GetComponent<ContextualCombat>();
            motor = GetComponent<ThirdPersonPlayerMotor>();
            health = GetComponent<CombatHealth>();
            if (health != null) health.Damaged += OnDamaged;
            if (inputActions != null)
            {
                attackAction = inputActions.FindAction($"{actionMapName}/Attack", false);
                heavyAttackAction = inputActions.FindAction($"{actionMapName}/HeavyAttack", false);
            }
        }

        private void OnEnable()
        {
            EnableAction(attackAction);
            EnableAction(heavyAttackAction);
        }

        private void OnDisable()
        {
            CancelCurrentAction();
            DisableAction(attackAction);
            DisableAction(heavyAttackAction);
        }

        private static void EnableAction(InputAction action)
        {
            if (action != null && !action.enabled)
            {
                action.Enable();
            }
        }

        private static void DisableAction(InputAction action)
        {
            if (action != null && action.enabled)
            {
                action.Disable();
            }
        }

        private void Update()
        {
            if (Progression.ProductionSession.GameplayBlocked) return;
            if (selfDamageable != null && selfDamageable.IsDead)
            {
                if (!deathHandled)
                {
                    deathHandled = true;
                    CancelCurrentAction();
                    currentAttack = null;
                    heavyChargeStartedAt = -1f;
                    bufferedInputUntil = 0f;
                    bufferedHeavyUntil = 0f;
                    comboRequested = false;
                    heavyRequestedDuringAttack = false;
                    nextAttackQueued = false;
                    if (logStateChanges)
                    {
                        Debug.Log("[PlayerCombat] player defeated -> attack/charge disabled", this);
                    }
                }

                return;
            }

            deathHandled = false;

            if (Time.time < interruptedUntil || (defense != null && defense.IsGuardBroken) || (contextual != null && contextual.IsBusy))
            {
                CancelCurrentAction();
                return;
            }

            if (attackAction != null && attackAction.WasPressedThisFrame())
            {
                RequestLightAttack();
            }

            if (heavyAttackAction != null && heavyAttackAction.WasPressedThisFrame())
            {
                BeginHeavyCharge();
            }

            if (enableHeavyCharge && heavyChargeStartedAt >= 0f && heavyAttackAction != null && heavyAttackAction.WasReleasedThisFrame())
            {
                ReleaseHeavyCharge();
            }

            if (IsChargingHeavy && dodge != null && dodge.IsDodging) CancelCurrentAction();
            animationDriver?.SetHeavyCharge(IsChargingHeavy, HeavyChargeNormalized);

            if (currentAttack == null)
            {
                if (dodge != null && dodge.IsDodging)
                {
                    return;
                }

                if (heavyAttack != null && (heavyRequestedDuringAttack || bufferedHeavyUntil >= Time.time))
                {
                    heavyRequestedDuringAttack = false;
                    bufferedHeavyUntil = 0f;
                    // Buffered / charge-disabled Heavy is an uncharged hit (1.0x). Only an actual
                    // hold through the release handler scales damage/knockback.
                    StartAttack(heavyAttack, 0f);
                    return;
                }

                if (!IsChargingHeavy && firstLightAttack != null && bufferedInputUntil >= Time.time)
                {
                    StartAttack(firstLightAttack, 0f);
                }

                return;
            }

            float previousElapsed = attackElapsed;
            attackElapsed += Time.deltaTime;
            animationDriver?.SetAttackProgress(currentAttack, attackElapsed);
            if (previousElapsed <= currentAttack.Duration * currentAttack.HitEndNormalized &&
                attackElapsed >= currentAttack.Duration * currentAttack.HitStartNormalized)
            {
                if (!useAnimationContactEvents)
                {
                    QueryAndApplyHits(currentAttack);
                }
            }

            if (!nextAttackQueued && currentAttack.NextAttack != null &&
                NormalizedAttackTime >= currentAttack.ComboInputOpenNormalized && comboRequested)
            {
                nextAttackQueued = true;
                comboRequested = false;
                if (logStateChanges)
                {
                    Debug.Log($"[PlayerCombat] next attack queued from '{currentAttack.Id}' at {NormalizedAttackTime:0.00}", this);
                }
            }

            if (attackElapsed < currentAttack.Duration + currentAttack.RecoveryTime)
            {
                return;
            }

            if (currentAttack.NextAttack != null && nextAttackQueued)
            {
                StartAttack(currentAttack.NextAttack, 0f);
                return;
            }

            if (logStateChanges)
            {
                Debug.Log($"[PlayerCombat] combo ended after '{currentAttack.Id}'", this);
            }

            // A press that arrived after the window closed (e.g. no NextAttack to chain into) never
            // got consumed; clear it here so it can't leak into a later, unrelated attack instance.
            comboRequested = false;
            currentAttack = null;
        }

        [Header("Animation contact")]
        [SerializeField] private bool useAnimationContactEvents;

        private void StartAttack(AttackDefinition attack, float chargeNormalized)
        {
            if (attack == null || !CanAct() || (motor != null && motor.IsCrouching && !motor.TryStandForAction()))
            {
                return;
            }

            currentAttack = attack;
            heavyChargeStartedAt = -1f;
            attackElapsed = 0f;
            bufferedInputUntil = -1f;
            bufferedHeavyUntil = -1f;
            nextAttackQueued = false;
            hitTargetIds.Clear();
            currentDamageMultiplier = attack == heavyAttack
                ? Mathf.Lerp(1f, heavyChargeMaxDamageMultiplier, Mathf.Clamp01(chargeNormalized)) * trainingHeavyMultiplier
                : 1f;
            currentKnockbackMultiplier = attack == heavyAttack
                ? Mathf.Lerp(1f, heavyChargeMaxKnockbackMultiplier, Mathf.Clamp01(chargeNormalized))
                : 1f;
            ApplyTargetAssist();
            animationDriver?.RequestAttack(attack, attack == heavyAttack && chargeNormalized >= .5f);

            if (logStateChanges)
            {
                bool hasState = !string.IsNullOrWhiteSpace(attack.AnimationState);
                Debug.Log(
                        $"[PlayerCombat] start '{attack.Id}' -> anim state '{(hasState ? animationDriver?.AttackPresentationState ?? attack.AnimationState : "<none: debug-only>")}', " +
                        $"budget {attack.Duration + attack.RecoveryTime:0.00}s charge={chargeNormalized:0.00}",
                        this);
            }
        }

        /// <summary>
        /// Optional Animation Event entry point. Timer-based contact remains the default
        /// until the corresponding imported clip and Animator state have been accepted.
        /// </summary>
        public void AnimationContact()
        {
            if (currentAttack != null && IsHitWindowOpen)
            {
                QueryAndApplyHits(currentAttack);
            }
        }

        private void QueryAndApplyHits(AttackDefinition attack)
        {
            Vector3 queryCenter = GetHitQueryCenter(attack);
            Vector3 queryStart = transform.position + Vector3.up * .9f + transform.forward * attack.ForwardOffset;
            Vector3 queryEnd = queryStart + transform.forward * attack.Range;
            int hitCount = Physics.OverlapCapsuleNonAlloc(
                queryStart,
                queryEnd,
                attack.Radius,
                hitBuffer,
                damageableLayers,
                QueryTriggerInteraction.Collide);
            for (int index = 0; index < hitCount; index++)
            {
                Collider hitCollider = hitBuffer[index];
                if (hitCollider == null)
                {
                    continue;
                }

                IDamageable target = FindDamageable(hitCollider);
                Component targetComponent = target as Component;
                if (target == null || target.IsDead || targetComponent == null || targetComponent.transform.IsChildOf(transform))
                {
                    continue;
                }
                if (!HasClearContact(targetComponent.transform)) continue;

                int targetId = targetComponent.gameObject.GetInstanceID();
                if (!hitTargetIds.Add(targetId))
                {
                    continue;
                }

                Vector3 point = hitCollider.ClosestPoint(queryCenter);
                Vector3 direction = Vector3.ProjectOnPlane(targetComponent.transform.position - transform.position, Vector3.up).normalized;
                if (direction.sqrMagnitude < .001f)
                {
                    direction = transform.forward;
                }

                target.ApplyDamage(new DamageInfo(
                    gameObject,
                    attack.Id,
                    0,
                    attack.Damage * currentDamageMultiplier,
                    point,
                    direction,
                    attack.ReactionType,
                    attack.KnockbackDistance * currentKnockbackMultiplier,
                    attack.KnockbackDuration,
                    attack.HitFlags));

                if (logStateChanges)
                {
                    Debug.Log(
                        $"[PlayerCombat] '{attack.Id}' hit {targetComponent.name} base={attack.Damage:0} final={attack.Damage * currentDamageMultiplier:0.0} kbMult={currentKnockbackMultiplier:0.00}" +
                        $"{(target.IsDead ? " (DEAD)" : string.Empty)}",
                        this);
                }
            }
        }

        private void ApplyTargetAssist()
        {
            int candidateCount = Physics.OverlapSphereNonAlloc(transform.position, targetAssistDistance, hitBuffer, damageableLayers, QueryTriggerInteraction.Collide);
            IDamageable bestTarget = null;
            float bestScore = float.MaxValue;
            for (int index = 0; index < candidateCount; index++)
            {
                IDamageable candidate = FindDamageable(hitBuffer[index]);
                Component component = candidate as Component;
                if (candidate == null || candidate.IsDead || component == null || component.transform.IsChildOf(transform) || !HasClearContact(component.transform))
                {
                    continue;
                }

                Vector3 flatDirection = Vector3.ProjectOnPlane(component.transform.position - transform.position, Vector3.up);
                if (flatDirection.sqrMagnitude < .001f)
                {
                    continue;
                }

                float angle = Vector3.Angle(transform.forward, flatDirection);
                if (angle > targetAssistAngle)
                {
                    continue;
                }

                float score = flatDirection.magnitude + angle * .02f;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestTarget = candidate;
                }
            }

            Component targetComponent = bestTarget as Component;
            if (targetComponent != null)
            {
                Vector3 direction = Vector3.ProjectOnPlane(targetComponent.transform.position - transform.position, Vector3.up).normalized;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction, Vector3.up), targetAssistMaxYawDegrees);
            }
        }

        private static IDamageable FindDamageable(Collider hitCollider)
        {
            foreach (MonoBehaviour component in hitCollider.GetComponentsInParent<MonoBehaviour>())
            {
                if (component is IDamageable damageable)
                {
                    return damageable;
                }
            }

            return null;
        }

        public void RequestLightAttack()
        {
            if (!CanAct()) return;
            if (IsChargingHeavy) CancelCurrentAction();
            if (currentAttack != null) comboRequested = true;
            else bufferedInputUntil = Time.time + inputBufferDuration;
        }

        public bool BeginHeavyCharge()
        {
            if (!CanAct() || (motor != null && motor.IsCrouching && !motor.TryStandForAction())) return false;
            if (enableHeavyCharge && currentAttack == null && (dodge == null || !dodge.IsDodging))
            {
                heavyChargeStartedAt = Time.time;
                bufferedInputUntil = -1f;
                return true;
            }
            if (currentAttack != null) heavyRequestedDuringAttack = true;
            else bufferedHeavyUntil = Time.time + inputBufferDuration;
            return false;
        }

        public void ReleaseHeavyCharge()
        {
            if (!IsChargingHeavy) return;
            float duration = Mathf.Clamp(Time.time - heavyChargeStartedAt, 0f, heavyChargeMaxDuration);
            heavyChargeStartedAt = -1f;
            if (currentAttack == null && (dodge == null || !dodge.IsDodging))
                StartAttack(heavyAttack, duration < heavyChargeMinDuration ? 0f : duration / heavyChargeMaxDuration);
        }

        public void CancelCurrentAction()
        {
            currentAttack = null;
            heavyChargeStartedAt = -1f;
            bufferedInputUntil = bufferedHeavyUntil = -1f;
            comboRequested = heavyRequestedDuringAttack = nextAttackQueued = false;
            animationDriver?.SetHeavyCharge(false, 0f);
        }

        private bool CanAct() => enabled && !Progression.ProductionSession.GameplayBlocked && (selfDamageable == null || !selfDamageable.IsDead) &&
            Time.time >= interruptedUntil && (defense == null || !defense.IsGuardBroken) && (contextual == null || !contextual.IsBusy);

        private void OnDamaged(DamageInfo damage)
        {
            CancelCurrentAction();
            interruptedUntil = Time.time + (damage.ReactionType == HitReactionType.Light ? .22f : .5f);
        }

        private void OnDestroy() { if (health != null) health.Damaged -= OnDamaged; }

        private bool HasClearContact(Transform target)
        {
            Vector3 start = transform.position + Vector3.up * .9f;
            Vector3 direction = target.position + Vector3.up * .9f - start;
            int count = Physics.RaycastNonAlloc(start, direction.normalized, obstructionHits, direction.magnitude, ~0, QueryTriggerInteraction.Ignore);
            if (count == obstructionHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Transform hit = obstructionHits[i].transform;
                if (!hit.IsChildOf(transform) && !hit.IsChildOf(target) && hit.GetComponentInParent<IDamageable>() == null) return false;
            }
            return true;
        }

        private Vector3 GetHitQueryCenter(AttackDefinition attack)
        {
            return transform.position + Vector3.up * .9f + transform.forward * (attack.ForwardOffset + attack.Range);
        }

        private void OnDrawGizmosSelected()
        {
            AttackDefinition attack = currentAttack != null ? currentAttack : firstLightAttack;
            if (attack == null)
            {
                return;
            }

            Gizmos.color = IsHitWindowOpen ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(GetHitQueryCenter(attack), attack.Radius);
        }
    }
}
