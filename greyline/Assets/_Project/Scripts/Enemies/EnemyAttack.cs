using Greyline.Combat;
using UnityEngine;

namespace Greyline.Enemies
{
    public enum EnemyArchetype { Brawler, Bruiser, Boss }

    /// <summary>Readable melee enemy with encounter turns, collision-aware footwork and a local leash.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyAttack : MonoBehaviour, ICounterable
    {
        private enum AttackState { Idle, Approach, Orbit, Telegraph, Active, Recovery, Return }
        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField, Min(.1f)] private float detectionRange = 8f;
        [SerializeField, Min(.1f)] private float approachStopDistance = 2.7f;
        [SerializeField, Min(.1f)] private float attackRange = 3.2f;
        [SerializeField, Min(0f)] private float approachSpeed = 2f;
        [SerializeField, Min(0f)] private float turnSpeed = 720f;
        [SerializeField] private bool trackTargetDuringActive;
        [Header("Encounter")]
        [SerializeField] private EnemyEncounterCoordinator encounter;
        [SerializeField] private EnemyArchetype archetype;
        [SerializeField] private LayerMask obstructionLayers = ~0;
        [SerializeField, Min(.05f)] private float movementRadius = .42f;
        [SerializeField, Min(.5f)] private float movementHeight = 1.8f;
        [Header("Melee query candidate")]
        [SerializeField] private LayerMask damageableLayers = 1;
        [SerializeField, Min(0f)] private float hitForwardOffset = .9f;
        [SerializeField, Min(.01f)] private float hitRadius = .75f;
        [SerializeField, Min(0f)] private float hitLength = .8f;
        [SerializeField, Range(-1f, 1f)] private float minimumForwardDot = .1f;
        [Header("Timing candidates")]
        [SerializeField, Min(0f)] private float telegraphDuration = .55f;
        [SerializeField, Min(.01f)] private float activeDuration = .16f;
        [SerializeField, Min(0f)] private float recoveryDuration = .75f;
        [SerializeField, Min(0f)] private float cooldownDuration = 1.2f;
        [SerializeField] private bool useAnimationContactEvents;
        [Header("Damage")]
        [SerializeField, Min(0f)] private float damage = 12f;
        [Header("Presentation")]
        [SerializeField] private Renderer telegraphRenderer;
        [SerializeField] private bool logStateChanges;

        private AttackState state;
        private float stateStartedAt;
        private float interruptedUntil;
        private bool damageApplied;
        private IDamageable selfDamageable;
        private IDamageable observedTargetDamageable;
        private CombatHealth health;
        private CombatHitReaction hitReaction;
        private Color restColor;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private MaterialPropertyBlock indicatorProperties;
        private int attackSequence;
        private int bossPhase = 1;
        private bool heavyAttack;
        private bool sweepingAttack;
        private float cycleTelegraph;
        private float cycleRecovery;
        private float cycleDamage;
        private float cycleCooldown;
        private bool attackReachedActive;
        public bool IsFinisherHeld { get; private set; }
        public void SetFinisherHeld(bool value)
        {
            IsFinisherHeld = value;
            if (value) Interrupt(.5f);
        }
        private readonly Collider[] meleeHitBuffer = new Collider[16];
        private readonly RaycastHit[] sweepBuffer = new RaycastHit[32];
        private readonly RaycastHit[] sightBuffer = new RaycastHit[24];

        public bool HasTarget => target != null;
        public bool IsTelegraphing => state == AttackState.Telegraph;
        public bool IsActive => state == AttackState.Active;
        public bool IsRecoveringAttack => state == AttackState.Recovery && attackReachedActive;
        public string PresentationAttackState => sweepingAttack ? "StrikeSweep" : heavyAttack ? "StrikeHeavy" : "StrikeJab";
        public float RecoveryPoseProgress => Mathf.Clamp01((Time.time - stateStartedAt) / Mathf.Max(.01f, cycleRecovery));
        public float PresentationPhaseRemaining => Mathf.Max(0f,
            (IsRecoveringAttack ? cycleRecovery : GetCurrentStateDuration()) - (Time.time - stateStartedAt));
        public bool IsApproaching => state == AttackState.Approach;
        public bool IsOrbiting => state == AttackState.Orbit;
        public bool IsReturning => state == AttackState.Return;
        public bool IsDefeated => selfDamageable != null && selfDamageable.IsDead;
        public bool CanBeCountered => !IsDefeated && !sweepingAttack && (IsTelegraphing || IsActive);
        public bool IsUnblockable => sweepingAttack && (IsTelegraphing || IsActive);
        public int BossPhase => bossPhase;
        public EnemyArchetype Archetype => archetype;
        public EnemyEncounterCoordinator Encounter => encounter;
        public float DetectionRange => detectionRange;
        public float ApproachStopDistance => approachStopDistance;
        public float AttackRange => attackRange;
        public float ApproachSpeed => approachSpeed;
        public float TurnSpeed => turnSpeed;
        public LayerMask DamageableLayers => damageableLayers;
        public float HitForwardOffset => hitForwardOffset;
        public float HitRadius => hitRadius;
        public float HitLength => hitLength;
        public float MinimumForwardDot => minimumForwardDot;
        public float TelegraphDuration => telegraphDuration;
        public float ActiveDuration => activeDuration;
        public float RecoveryDuration => recoveryDuration;
        public float CooldownDuration => cooldownDuration;
        public float Damage => damage;
        public string StateName => state.ToString();
        public string CurrentAttackId => $"enemy.{archetype.ToString().ToLowerInvariant()}.{(sweepingAttack ? "sweep" : heavyAttack ? "heavy" : "jab")}";
        public float StateProgress => Mathf.Clamp01((Time.time - stateStartedAt) / Mathf.Max(.01f, GetCurrentStateDuration()));
        internal bool IsReadyForPermission => isActiveAndEnabled && !IsDefeated && !IsFinisherHeld && target != null &&
            (state == AttackState.Idle || state == AttackState.Approach || state == AttackState.Orbit) &&
            !IsReactingFromHit() && GetFlatDistanceSquared() <= Mathf.Pow(Mathf.Min(approachStopDistance, attackRange * .85f) + .04f, 2) && HasClearTargetLine();

        public void Configure(Transform attackTarget, float range, float telegraph, float active,
            float recovery, float cooldown, float attackDamage, Renderer indicator = null)
        {
            target = attackTarget;
            attackRange = Mathf.Max(.1f, range);
            telegraphDuration = Mathf.Max(0f, telegraph);
            activeDuration = Mathf.Max(.01f, active);
            recoveryDuration = Mathf.Max(0f, recovery);
            cooldownDuration = Mathf.Max(0f, cooldown);
            damage = Mathf.Max(0f, attackDamage);
            telegraphRenderer = indicator != null ? indicator : telegraphRenderer;
            CacheRestColor();
            SetIndicatorColor(restColor);
        }

        public void ConfigureMovement(float detectRange, float stopDistance, float moveSpeed, float rotationSpeed)
        {
            detectionRange = Mathf.Max(.1f, detectRange);
            approachStopDistance = Mathf.Max(.1f, stopDistance);
            approachSpeed = Mathf.Max(0f, moveSpeed);
            turnSpeed = Mathf.Max(0f, rotationSpeed);
        }

        public void ConfigureMelee(LayerMask layers, float forwardOffset, float radius, float forwardDot)
        {
            damageableLayers = layers;
            hitForwardOffset = Mathf.Max(0f, forwardOffset);
            hitRadius = Mathf.Max(.01f, radius);
            minimumForwardDot = Mathf.Clamp(forwardDot, -1f, 1f);
        }

        public void ConfigureEncounter(EnemyEncounterCoordinator coordinator, EnemyArchetype archetype = EnemyArchetype.Brawler)
        {
            if (encounter != null) encounter.Unregister(this);
            encounter = coordinator;
            this.archetype = archetype;
            if (encounter != null)
            {
                target = encounter.Target != null ? encounter.Target : target;
                encounter.Register(this);
            }
        }

        private void Awake()
        {
            CacheRestColor();
            selfDamageable = GetComponent<IDamageable>();
            health = GetComponent<CombatHealth>();
            if (health != null) health.Died += OnDeath;
            hitReaction = GetComponent<CombatHitReaction>();
            homePosition = transform.position;
            homeRotation = transform.rotation;
        }
        private void Start()
        {
            // Runtime builders position a newly-added component after Awake.
            homePosition = transform.position;
            homeRotation = transform.rotation;
        }
        private void OnEnable() { if (encounter != null) encounter.Register(this); }
        private void OnDeath(DamageInfo damage) => StopAttack();
        private void OnDestroy() { if (health != null) health.Died -= OnDeath; }
        private void OnDisable()
        {
            if (encounter != null) encounter.Unregister(this);
            state = AttackState.Idle;
            SetIndicatorColor(restColor);
        }

        private void Update()
        {
            selfDamageable ??= GetComponent<IDamageable>();
            if (target == null || IsDefeated) { StopAttack(); return; }
            if (IsFinisherHeld) return;
            observedTargetDamageable = FindDamageable(target);
            if (observedTargetDamageable != null && observedTargetDamageable.IsDead) { ReturnHome(); return; }
            if (archetype == EnemyArchetype.Boss && bossPhase == 1 && health != null && health.HealthNormalized <= .5f)
            {
                bossPhase = 2;
                Interrupt(1.4f);
                Debug.Log($"[EnemyAttack] {name} phase 2: alternating jab, heavy and unblockable sweep", this);
            }
            if (IsReactingFromHit())
            {
                if (state == AttackState.Telegraph || state == AttackState.Active) Interrupt(.65f);
                return;
            }
            bool detected = encounter != null
                ? encounter.isActiveAndEnabled && encounter.IsEngaged &&
                  FlatDistanceSquared(transform.position, encounter.transform.position) <= encounter.LeashRadius * encounter.LeashRadius
                : GetFlatDistanceSquared() <= detectionRange * detectionRange;
            if (!detected) { ReturnHome(); return; }

            // The last half of the warning commits facing, making lateral dodges dependable.
            if ((state != AttackState.Active || trackTargetDuringActive) &&
                (state != AttackState.Telegraph || StateProgress < .5f)) FaceTarget();
            switch (state)
            {
                case AttackState.Return: ChangeState(AttackState.Idle); break;
                case AttackState.Idle:
                case AttackState.Approach:
                case AttackState.Orbit: TickFootwork(); break;
                case AttackState.Telegraph:
                    if (!IsTargetInAttackRange() || !HasClearTargetLine()) ChangeState(AttackState.Recovery);
                    else if (HasStateElapsed(cycleTelegraph)) ChangeState(AttackState.Active);
                    break;
                case AttackState.Active:
                    if (!useAnimationContactEvents && !damageApplied) TryApplyMeleeHit();
                    if (HasStateElapsed(activeDuration)) ChangeState(AttackState.Recovery);
                    break;
                case AttackState.Recovery:
                    if (Time.time >= interruptedUntil && HasStateElapsed(cycleRecovery + cycleCooldown)) ChangeState(AttackState.Idle);
                    break;
            }
        }

        private void TickFootwork()
        {
            float distance = Mathf.Sqrt(GetFlatDistanceSquared());
            float stop = Mathf.Min(approachStopDistance, attackRange * .85f);
            // Use the same arrival tolerance as MoveToward. Otherwise footwork can park a few
            // millimetres outside stopDistance forever and never grant the attack turn.
            if (distance > stop + .04f)
            {
                // Waiting combatants hold distinct positions instead of pushing through the active attacker.
                if (encounter != null && encounter.ActiveAttacker != null && encounter.ActiveAttacker != this && distance < attackRange + 1.2f)
                {
                    ChangeState(AttackState.Orbit);
                    MoveToward(encounter.GetOrbitPoint(this, stop + .65f), approachSpeed * .65f);
                    return;
                }
                ChangeState(AttackState.Approach);
                MoveToward(target.position, approachSpeed * (archetype == EnemyArchetype.Bruiser ? .8f : 1f), stop);
                return;
            }
            if (distance > attackRange) return;
            if (!HasClearTargetLine())
            {
                ChangeState(AttackState.Approach);
                // A close obstacle still needs edge-seeking movement; stopping here strands pursuit.
                MoveToward(target.position, approachSpeed * .7f);
                return;
            }
            if (encounter == null || encounter.TryAcquire(this)) BeginAttack();
            else
            {
                ChangeState(AttackState.Orbit);
                MoveToward(encounter.GetOrbitPoint(this, stop + .65f), approachSpeed * .6f);
            }
        }

        private void BeginAttack()
        {
            attackReachedActive = false;
            attackSequence++;
            heavyAttack = archetype == EnemyArchetype.Bruiser || (archetype == EnemyArchetype.Boss && attackSequence % 2 == 0);
            sweepingAttack = archetype == EnemyArchetype.Boss && bossPhase == 2 && attackSequence % 3 == 0;
            cycleTelegraph = telegraphDuration * (sweepingAttack ? 1.8f : heavyAttack ? 1.45f : 1f);
            cycleRecovery = recoveryDuration * (heavyAttack || sweepingAttack ? 1.5f : 1f);
            cycleCooldown = cooldownDuration * (bossPhase == 2 ? .6f : 1f);
            cycleDamage = damage * (sweepingAttack ? 1.4f : heavyAttack ? 1.25f : 1f);
            ChangeState(AttackState.Telegraph);
        }
        public void ReceiveCounter(CounterRequest request) { if (CanBeCountered) Interrupt(1.15f); }
        public void Interrupt(float duration)
        {
            if (IsDefeated) return;
            attackReachedActive = false;
            interruptedUntil = Mathf.Max(interruptedUntil, Time.time + Mathf.Max(0f, duration));
            ChangeState(AttackState.Recovery);
        }
        private void StopAttack()
        {
            if (encounter != null) encounter.Release(this);
            ChangeState(AttackState.Idle);
        }
        private void ReturnHome()
        {
            if (encounter != null) encounter.Release(this);
            if (FlatDistanceSquared(transform.position, homePosition) > .09f)
            {
                ChangeState(AttackState.Return);
                FacePosition(homePosition);
                MoveToward(homePosition, approachSpeed);
            }
            else
            {
                ChangeState(AttackState.Idle);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, homeRotation, turnSpeed * Time.deltaTime);
            }
        }
        private float GetFlatDistanceSquared() => FlatDistanceSquared(transform.position, target.position);
        private bool IsTargetInAttackRange() => target != null && GetFlatDistanceSquared() <= attackRange * attackRange;
        private static float FlatDistanceSquared(Vector3 a, Vector3 b) { a.y = b.y; return (a - b).sqrMagnitude; }
        private bool IsReactingFromHit() => hitReaction != null && hitReaction.IsReacting;
        private void FaceTarget() => FacePosition(target.position);
        private void FacePosition(Vector3 position)
        {
            Vector3 direction = Vector3.ProjectOnPlane(position - transform.position, Vector3.up);
            if (direction.sqrMagnitude > .001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), turnSpeed * Time.deltaTime);
        }

        private void MoveToward(Vector3 destination, float speed, float stopDistance = 0f)
        {
            Vector3 offset = Vector3.ProjectOnPlane(destination - transform.position, Vector3.up);
            float distance = offset.magnitude;
            if (distance < stopDistance + .04f || speed <= 0f) return;
            Vector3 direction = offset / distance;
            float travel = Mathf.Min(speed * Time.deltaTime, distance - stopDistance);
            float allowed = GetClearTravel(direction, travel, out Vector3 normal);
            if (allowed < travel * .5f)
            {
                Vector3 slide = Vector3.ProjectOnPlane(direction, normal).normalized;
                if (slide.sqrMagnitude < .01f) slide = Vector3.Cross(Vector3.up, normal).normalized;
                float slideTravel = GetClearTravel(slide, travel, out _);
                if (slideTravel > allowed) { direction = slide; allowed = slideTravel; }
            }
            // Sub-millimetre frame steps are still real movement on a fast/unthrottled player.
            // Discarding every small step makes approach speed depend on rendering frame rate.
            if (allowed > .000001f) transform.position += direction * allowed;
        }
        private float GetClearTravel(Vector3 direction, float distance, out Vector3 normal)
        {
            normal = -direction;
            if (direction.sqrMagnitude < .01f) return 0f;
            Vector3 foot = transform.position + Vector3.up * (movementRadius + .08f);
            Vector3 head = transform.position + Vector3.up * Mathf.Max(movementRadius + .08f, movementHeight - movementRadius);
            int count = Physics.CapsuleCastNonAlloc(foot, head, movementRadius, direction, sweepBuffer,
                distance + .035f, obstructionLayers, QueryTriggerInteraction.Ignore);
            float allowed = distance;
            for (int i = 0; i < count; i++)
            {
                Collider obstacle = sweepBuffer[i].collider;
                if (obstacle == null || obstacle.transform.IsChildOf(transform) || sweepBuffer[i].normal.y > .65f) continue;
                float candidate = Mathf.Max(0f, sweepBuffer[i].distance - .035f);
                if (candidate < allowed) { allowed = candidate; normal = sweepBuffer[i].normal; }
            }
            return allowed;
        }
        private bool HasClearTargetLine()
        {
            if (target == null) return false;
            Vector3 start = transform.position + Vector3.up * .9f;
            Vector3 delta = target.position + Vector3.up * .9f - start;
            int count = Physics.RaycastNonAlloc(start, delta.normalized, sightBuffer, delta.magnitude,
                obstructionLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform obstacle = sightBuffer[i].collider.transform;
                if (obstacle.IsChildOf(transform) || obstacle.IsChildOf(target) || target.IsChildOf(obstacle)) continue;
                return false;
            }
            return true;
        }

        /// <summary>Timer and animation contact share one authority; only the designated target can be hit.</summary>
        public void TryApplyMeleeHit()
        {
            if (state != AttackState.Active || damageApplied || IsDefeated) return;
            damageApplied = true;
            if (!IsTargetInAttackRange() || !HasClearTargetLine()) return;
            Vector3 hitStart = transform.position + Vector3.up * .85f + transform.forward * hitForwardOffset;
            Vector3 hitEnd = hitStart + transform.forward * hitLength;
            float radius = hitRadius * (sweepingAttack ? 1.35f : 1f);
            int hitCount = Physics.OverlapCapsuleNonAlloc(hitStart, hitEnd, radius, meleeHitBuffer,
                damageableLayers, QueryTriggerInteraction.Collide);
            observedTargetDamageable = FindDamageable(target);
            for (int index = 0; index < hitCount; index++)
            {
                Collider hitCollider = meleeHitBuffer[index];
                if (hitCollider == null) continue;
                IDamageable candidate = FindDamageable(hitCollider.transform);
                Component component = candidate as Component;
                if (candidate == null || candidate != observedTargetDamageable || candidate.IsDead || component == null) continue;
                Vector3 offset = Vector3.ProjectOnPlane(component.transform.position - transform.position, Vector3.up);
                if (offset.sqrMagnitude < .001f || Vector3.Dot(transform.forward, offset.normalized) < (sweepingAttack ? -.15f : minimumForwardDot)) continue;
                candidate.ApplyDamage(new DamageInfo(gameObject, CurrentAttackId, 0, cycleDamage,
                    hitCollider.ClosestPoint(hitStart), offset.normalized,
                    heavyAttack || sweepingAttack ? HitReactionType.Heavy : HitReactionType.Light,
                    heavyAttack ? .4f : .15f, .15f,
                    sweepingAttack ? CombatHitFlags.Uninterruptible : CombatHitFlags.Counterable));
                if (logStateChanges) Debug.Log($"[EnemyAttack] {name} {CurrentAttackId} contact on {component.name}", this);
                return;
            }
        }
        public bool AnimationContact()
        {
            if (state != AttackState.Active || damageApplied) return false;
            TryApplyMeleeHit();
            return true;
        }
        private void ChangeState(AttackState nextState)
        {
            if (state == nextState) return;
            if ((state == AttackState.Telegraph || state == AttackState.Active) &&
                nextState != AttackState.Telegraph && nextState != AttackState.Active && encounter != null) encounter.Release(this);
            state = nextState;
            stateStartedAt = Time.time;
            if (nextState == AttackState.Telegraph)
            {
                damageApplied = false;
                SetIndicatorColor(sweepingAttack ? new Color(1f, .12f, .85f) : heavyAttack ? new Color(1f, .45f, .06f) : Color.yellow);
            }
            else if (nextState == AttackState.Active)
            {
                attackReachedActive = true;
                SetIndicatorColor(Color.red);
            }
            else if (nextState == AttackState.Recovery) SetIndicatorColor(new Color(.25f, .65f, .85f));
            else SetIndicatorColor(restColor);
            if (logStateChanges) Debug.Log($"[EnemyAttack] {name} -> {nextState}", this);
        }
        private bool HasStateElapsed(float duration) => Time.time >= stateStartedAt + duration;
        private float GetCurrentStateDuration() => state switch
        {
            AttackState.Telegraph => cycleTelegraph,
            AttackState.Active => activeDuration,
            AttackState.Recovery => Mathf.Max(cycleRecovery + cycleCooldown, interruptedUntil - stateStartedAt),
            _ => 1f,
        };
        private static IDamageable FindDamageable(Transform root) => root != null ? root.GetComponentInParent<IDamageable>() : null;
        private void CacheRestColor()
        {
            restColor = telegraphRenderer != null && telegraphRenderer.sharedMaterial != null
                ? telegraphRenderer.sharedMaterial.color : Color.white;
        }
        private void SetIndicatorColor(Color color)
        {
            if (telegraphRenderer == null) return;
            indicatorProperties ??= new MaterialPropertyBlock();
            telegraphRenderer.GetPropertyBlock(indicatorProperties);
            indicatorProperties.SetColor("_BaseColor", color);
            indicatorProperties.SetColor("_Color", color);
            telegraphRenderer.SetPropertyBlock(indicatorProperties);
        }
    }
}
