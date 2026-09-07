using System;
using Greyline.Enemies;
using Greyline.Player;
using Greyline.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Greyline.Combat
{
    [DisallowMultipleComponent]
    public sealed class ContextualCombat : MonoBehaviour, IDamageInvulnerability
    {
        [SerializeField, Min(.1f)] private float approachDuration = .28f;
        [SerializeField, Min(.1f)] private float contactDelay = .58f;
        [SerializeField, Min(.1f)] private float recoveryDuration = .47f;
        [SerializeField, Min(.6f)] private float partnerDistance = .9f;
        [SerializeField, Min(.5f)] private float clinchDistance = .65f;
        [SerializeField, Min(1f)] private float unarmedDamage = 55f;
        [SerializeField, Min(1f)] private float surfaceDamage = 85f;
        private CombatHealth health;
        private PlayerCombat combat;
        private PlayerDodge dodge;
        private CombatDefense defense;
        private ThirdPersonPlayerMotor motor;
        private CharacterController controller;
        private CharacterAnimationDriver animationDriver;
        private CombatHealth victim;
        private EnvironmentalFinisher surface;
        private Vector3 actorStart, victimStart, victimStop, direction;
        private Quaternion actorRotation, victimRotation;
        private float startedAt, strikeStartedAt = -1f;
        private bool busy, contactApplied;
        private Collider pairedCollider;
        private bool pairWasIgnored;
        private float PartnerDistance => surface != null ? partnerDistance : clinchDistance;
        private readonly Collider[] candidates = new Collider[32];
        private readonly Collider[] surfaceCandidates = new Collider[32];
        private readonly RaycastHit[] sightHits = new RaycastHit[32];
        public bool IsBusy => busy;
        public bool IsInvulnerable => isActiveAndEnabled && IsBusy;
        public CombatHealth AvailableTarget { get; private set; }
        public CombatHealth Partner => victim;
        public string ActiveState => surface != null ? "FinisherPush" : "Finisher";
        public float ContactDelay => contactDelay;
        public float StrikeElapsed => strikeStartedAt < 0 ? 0 : Time.time - strikeStartedAt;
        public bool HasContact => contactApplied;
        public string Prompt { get; private set; }
        public event Action<string, Vector3> Finished;

        private void Awake()
        {
            health = GetComponent<CombatHealth>(); combat = GetComponent<PlayerCombat>();
            dodge = GetComponent<PlayerDodge>(); defense = GetComponent<CombatDefense>();
            motor = GetComponent<ThirdPersonPlayerMotor>(); controller = GetComponent<CharacterController>();
            animationDriver = GetComponent<CharacterAnimationDriver>();
        }

        private void Update()
        {
            if (Progression.ProductionSession.GameplayBlocked) return;
            if (health != null && health.IsDead) { EndSequence(); return; }
            if (busy) { TickSequence(); return; }
            RefreshTarget();
            if (Keyboard.current?.fKey.wasPressedThisFrame == true || Gamepad.current?.leftShoulder.wasPressedThisFrame == true)
                TryFinish();
        }

        private void TickSequence()
        {
            if (victim == null || (!contactApplied && victim.IsDead)) { EndSequence(); return; }
            if (strikeStartedAt < 0)
            {
                float phase = Mathf.Clamp01((Time.time - startedAt) / approachDuration);
                float blend = Mathf.SmoothStep(0, 1, phase);
                transform.rotation = Quaternion.Slerp(actorRotation, Quaternion.LookRotation(direction), blend);
                victim.transform.rotation = Quaternion.Slerp(victimRotation, Quaternion.LookRotation(-direction), blend);
                Vector3 goal = victimStart - direction * PartnerDistance;
                Vector3 desired = Vector3.Lerp(actorStart, goal, blend);
                controller.Move(Vector3.ProjectOnPlane(desired - transform.position, Vector3.up));
                if (phase < 1) return;
                if (Vector3.ProjectOnPlane(transform.position - goal, Vector3.up).magnitude > .15f || !HasClearContact(victim) ||
                    !animationDriver.RequestFinisher(ActiveState, contactDelay)) { EndSequence(); return; }
                strikeStartedAt = Time.time;
            }
            float elapsed = StrikeElapsed;
            animationDriver.SetFinisherProgress(elapsed, contactDelay, contactDelay + recoveryDuration);
            if (!contactApplied && surface != null)
            {
                Vector3 next = Vector3.Lerp(victimStart, victimStop, Mathf.SmoothStep(0, 1, elapsed / contactDelay));
                if (!ClearTravel(victim.transform.position, next, victim.transform, transform)) { EndSequence(); return; }
                victim.transform.position = next;
                controller.Move(Vector3.ProjectOnPlane(next - direction * PartnerDistance - transform.position, Vector3.up));
            }
            if (!contactApplied && elapsed >= contactDelay)
            {
                float amount = surface != null ? surfaceDamage : unarmedDamage;
                if (!HasClearContact(victim) || Vector3.Distance(transform.position, victim.transform.position) > PartnerDistance + .2f ||
                    victim.CurrentHealth > amount ||
                    (surface != null && !surface.IsAtContact(victim.transform.position))) { EndSequence(); return; }
                contactApplied = true;
                victim.GetComponent<EnemyAttack>()?.SetFinisherHeld(false);
                if (surface != null) victim.GetComponent<CombatHitReaction>()?.PrepareSurfaceDeath();
                victim.ApplyDamage(new DamageInfo(gameObject, surface != null ? "environment.finisher" : "finisher", 0,
                    amount, victim.transform.position + Vector3.up, direction, HitReactionType.Knockdown, 0f, .2f, CombatHitFlags.None));
                surface?.ResolveImpact();
                Finished?.Invoke(surface != null ? surface.Label : "CLINCH KNEE", victim.transform.position + Vector3.up);
            }
            if (elapsed >= contactDelay + recoveryDuration) EndSequence();
        }

        public void RefreshTarget()
        {
            AvailableTarget = null; Prompt = null;
            if (busy || (health != null && health.IsDead) || combat == null || combat.IsAttacking || combat.IsChargingHeavy ||
                (dodge != null && dodge.IsDodging) || (defense != null && defense.IsGuardBroken)) return;
            float closest = 2.7f * 2.7f;
            int count = Physics.OverlapSphereNonAlloc(transform.position, 2.7f, candidates, ~0, QueryTriggerInteraction.Ignore);
            // Plan selection uses a separate cast buffer, never overwriting this overlap's candidates.
            for (int i = 0; i < count; i++)
            {
                var candidate = candidates[i].GetComponentInParent<CombatHealth>();
                if (candidate == null || candidate == health || candidate.IsDead || candidate.GetComponent<EnemyAttack>() == null ||
                    candidate.HealthNormalized > .38f) continue;
                Vector3 offset = Vector3.ProjectOnPlane(candidate.transform.position - transform.position, Vector3.up);
                if (offset.sqrMagnitude >= closest || Vector3.Dot(transform.forward, offset.normalized) < .2f || !HasClearContact(candidate)) continue;
                if (!TryPlan(candidate, out var prop, out var stop, out var facing)) continue;
                AvailableTarget = candidate; surface = prop; victimStop = stop; direction = facing;
                closest = offset.sqrMagnitude;
            }
            if (AvailableTarget != null) Prompt = "F  " + (surface != null ? surface.Label : "CLINCH KNEE");
        }

        private bool TryPlan(CombatHealth target, out EnvironmentalFinisher prop, out Vector3 stop, out Vector3 facing)
        {
            prop = null; stop = target.transform.position;
            facing = Vector3.ProjectOnPlane(stop - transform.position, Vector3.up).normalized;
            float nearest = float.PositiveInfinity;
            Vector3 plannedStop = stop, plannedFacing = facing;
            int count = Physics.OverlapSphereNonAlloc(stop, 2f, surfaceCandidates, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var candidate = surfaceCandidates[i].GetComponentInParent<EnvironmentalFinisher>();
                if (candidate == null) continue;
                if (!candidate.TryGetContact(stop, out var endpoint, out var push)) continue;
                // A surface beside or behind the player does not become a remote damage bonus.
                if (Vector3.Dot(facing, push) < .75f || !ClearTravel(stop, endpoint, target.transform, transform)) continue;
                float distance = (endpoint - stop).sqrMagnitude;
                if (distance >= nearest) continue;
                prop = candidate; plannedStop = endpoint; plannedFacing = push; nearest = distance;
            }
            if (prop != null) { stop = plannedStop; facing = plannedFacing; }
            if (target.CurrentHealth > (prop != null ? surfaceDamage : unarmedDamage)) return false;
            float spacing = prop != null ? partnerDistance : clinchDistance;
            Vector3 actorGoal = target.transform.position - facing * spacing;
            return Vector3.Distance(transform.position, actorGoal) <= 2f &&
                ClearTravel(transform.position, actorGoal, transform, target.transform) &&
                ClearTravel(actorGoal, stop - facing * spacing, transform, target.transform);
        }

        public bool TryFinish()
        {
            if (Progression.ProductionSession.GameplayBlocked || busy || !isActiveAndEnabled || controller == null || !controller.enabled || !controller.isGrounded) return false;
            RefreshTarget();
            if (AvailableTarget == null || (motor != null && motor.IsCrouching && !motor.TryStandForAction())) return false;
            if (animationDriver == null || !animationDriver.RequestFinisher("FinisherApproach")) return false;
            victim = AvailableTarget; AvailableTarget = null; Prompt = null;
            actorStart = transform.position; victimStart = victim.transform.position;
            startedAt = Time.time; strikeStartedAt = -1; contactApplied = false; busy = true;
            defense?.SetGuardHeld(false); combat.CancelCurrentAction();
            actorRotation = transform.rotation; victimRotation = victim.transform.rotation;
            victim.GetComponent<DeterministicKnockback>()?.Cancel();
            victim.GetComponent<EnemyAttack>()?.SetFinisherHeld(true);
            pairedCollider = victim.GetComponent<Collider>();
            if (pairedCollider != null)
            {
                // Only the two participants may overlap for a close clinch; world collision stays live.
                pairWasIgnored = Physics.GetIgnoreCollision(controller, pairedCollider);
                Physics.IgnoreCollision(controller, pairedCollider, true);
            }
            return true;
        }

        private void EndSequence()
        {
            if (pairedCollider != null && controller != null)
            {
                if (victim != null && !victim.IsDead && controller.enabled)
                {
                    float spacing = Vector3.ProjectOnPlane(transform.position - victim.transform.position, Vector3.up).magnitude;
                    controller.Move(-direction * Mathf.Max(0, partnerDistance + .04f - spacing));
                }
                Physics.IgnoreCollision(controller, pairedCollider, pairWasIgnored);
            }
            pairedCollider = null;
            if (victim != null) victim.GetComponent<EnemyAttack>()?.SetFinisherHeld(false);
            if (busy) animationDriver?.EndFinisher();
            busy = false; victim = null; AvailableTarget = null; Prompt = null;
        }
        private void OnDisable() => EndSequence();

        private bool ClearTravel(Vector3 start, Vector3 end, Transform first, Transform second)
        {
            Vector3 delta = Vector3.ProjectOnPlane(end - start, Vector3.up);
            if (delta.magnitude < .001f) return true;
            var capsule = first.GetComponent<CapsuleCollider>();
            var character = first.GetComponent<CharacterController>();
            float radius = character != null ? character.radius : capsule != null ? capsule.radius : .38f;
            float height = character != null ? character.height : capsule != null ? capsule.height : 1.8f;
            Vector3 centre = character != null ? character.center : capsule != null ? capsule.center : Vector3.up * .9f;
            Vector3 bottom = start + centre - Vector3.up * (height * .5f - radius) + Vector3.up * .025f;
            Vector3 top = start + centre + Vector3.up * (height * .5f - radius);
            int count = Physics.CapsuleCastNonAlloc(bottom, top, radius - .01f,
                delta.normalized, sightHits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            if (count == sightHits.Length) return false;
            for (int i = 0; i < count; i++)
                if (!sightHits[i].transform.IsChildOf(first) && !sightHits[i].transform.IsChildOf(second)) return false;
            return true;
        }

        private bool HasClearContact(CombatHealth target)
        {
            Vector3 start = transform.position + Vector3.up;
            Vector3 offset = target.transform.position + Vector3.up - start;
            int count = Physics.RaycastNonAlloc(start, offset.normalized, sightHits, offset.magnitude, ~0, QueryTriggerInteraction.Ignore);
            if (count == sightHits.Length) return false;
            for (int i = 0; i < count; i++)
                if (!sightHits[i].transform.IsChildOf(transform) && !sightHits[i].transform.IsChildOf(target.transform)) return false;
            return true;
        }
    }
}
