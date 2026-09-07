using System.Collections.Generic;
using Greyline.Enemies;
using Greyline.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Greyline.Combat
{
    /// <summary>
    /// Action-owned movement actions that sit beside PlayerCombat and PlayerDodge.
    ///
    /// The input names are optional on purpose: the current shared input asset does not
    /// contain Tackle, Slide, or Dash yet. Main integration can add those bindings later
    /// without changing the movement and collision rules in this component.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerCombatMobility : MonoBehaviour, IDamageInvulnerability
    {
        private enum MobilityState
        {
            Ready,
            Tackle,
            Slide,
        }

        [Header("Optional input actions")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string tackleActionName = "Tackle";
        [SerializeField] private string slideActionName = "Slide";

        [Header("Tackle")]
        [SerializeField, Min(0.01f)] private float tackleDistance = 3.2f;
        [SerializeField, Min(0.01f)] private float tackleDuration = 0.34f;
        [SerializeField, Min(0f)] private float tackleDamage = 28f;
        [SerializeField, Min(0f)] private float tackleHitRadius = 0.65f;
        [SerializeField, Min(0f)] private float tackleHitForwardOffset = 0.7f;
        [SerializeField, Min(0f)] private float tackleKnockbackDistance = 0.8f;

        [Header("Slide")]
        [SerializeField, Min(0.01f)] private float slideDistance = 2.1f;
        [SerializeField, Min(0.01f)] private float slideDuration = 0.42f;
        [SerializeField, Min(0f)] private float slideCooldown = 0.35f;
        [SerializeField, Min(0f)] private float slideDamage = 14f;
        [SerializeField, Min(0f)] private float slideHitRadius = 0.5f;
        [SerializeField, Min(0f)] private float slideHitForwardOffset = 0.55f;

        [Header("Targets")]
        [SerializeField] private LayerMask damageableLayers = 1;
        [SerializeField] private bool logStateChanges = true;

        private readonly Collider[] hitBuffer = new Collider[16];
        private CharacterController controller;
        private PlayerCombat playerCombat;
        private PlayerDodge dodge;
        private InputAction tackleAction;
        private InputAction slideAction;
        private MobilityState state;
        private Vector3 movementDirection;
        private float stateStartedAt;
        private float stateDistance;
        private float stateDuration;
        private float nextSlideAllowedAt;
        private bool tackleDamageApplied;

        public bool IsBusy => state != MobilityState.Ready;
        public bool IsTackling => state == MobilityState.Tackle;
        public bool IsSliding => state == MobilityState.Slide;
        public bool IsDashing => dodge != null && dodge.IsDodging;
        public bool IsInvulnerable => IsDashing;
        public float StateProgress => stateDuration <= 0f ? 1f : Mathf.Clamp01((Time.time - stateStartedAt) / stateDuration);

        public void ConfigureInput(InputActionAsset actions)
        {
            inputActions = actions;
            CacheActions();
        }

        public void ConfigureTargets(LayerMask layers)
        {
            damageableLayers = layers;
        }

        public bool RequestTackle(Vector3 direction)
        {
            if (!CanStart() || !TryGetPlanarDirection(direction, out Vector3 planarDirection))
            {
                return false;
            }

            Begin(MobilityState.Tackle, planarDirection, tackleDistance, tackleDuration);
            tackleDamageApplied = false;
            return true;
        }

        public bool RequestSlide(Vector3 direction)
        {
            if (!CanStart() || Time.time < nextSlideAllowedAt || !TryGetPlanarDirection(direction, out Vector3 planarDirection))
            {
                return false;
            }

            nextSlideAllowedAt = Time.time + slideCooldown;
            Begin(MobilityState.Slide, planarDirection, slideDistance, slideDuration);
            return true;
        }

        public bool RequestDash(Vector3 direction)
        {
            return dodge != null && dodge.RequestDodge(direction);
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            playerCombat = GetComponent<PlayerCombat>();
            dodge = GetComponent<PlayerDodge>();
            CacheActions();
        }

        private void OnEnable()
        {
            Enable(tackleAction);
            Enable(slideAction);
        }

        private void OnDisable()
        {
            Disable(tackleAction);
            Disable(slideAction);
        }

        private void Update()
        {
            if (tackleAction != null && tackleAction.WasPressedThisFrame()) RequestTackle(transform.forward);
            if (slideAction != null && slideAction.WasPressedThisFrame()) RequestSlide(transform.forward);
            if (state == MobilityState.Ready)
            {
                return;
            }

            if (playerCombat != null && playerCombat.IsAttacking)
            {
                End();
                return;
            }

            float progressBeforeMove = StateProgress;
            float speed = stateDistance / Mathf.Max(0.01f, stateDuration);
            CollisionFlags flags = controller.Move(movementDirection * speed * Time.deltaTime);

            if (state == MobilityState.Tackle && !tackleDamageApplied && progressBeforeMove >= 0.3f)
            {
                ApplyTackleHit();
            }

            if (state == MobilityState.Slide && !tackleDamageApplied && progressBeforeMove >= 0.35f)
            {
                ApplySlideHit();
            }

            if ((flags & CollisionFlags.Sides) != 0 || StateProgress >= 1f)
            {
                End();
            }
        }

        private bool CanStart()
        {
            return controller != null && state == MobilityState.Ready && (dodge == null || !dodge.IsDodging) &&
                (playerCombat == null || !playerCombat.IsAttacking);
        }

        private void Begin(MobilityState nextState, Vector3 direction, float distance, float duration)
        {
            state = nextState;
            movementDirection = direction;
            stateDistance = Mathf.Max(0f, distance);
            stateDuration = Mathf.Max(0.01f, duration);
            stateStartedAt = Time.time;
            if (logStateChanges)
            {
                Debug.Log($"[PlayerCombatMobility] {name} -> {state} | distance={stateDistance:0.00} duration={stateDuration:0.00}", this);
            }
        }

        private void End()
        {
            if (state == MobilityState.Ready)
            {
                return;
            }

            if (logStateChanges)
            {
                Debug.Log($"[PlayerCombatMobility] {name} -> Ready", this);
            }

            state = MobilityState.Ready;
            movementDirection = Vector3.zero;
            stateDistance = 0f;
            stateDuration = 0f;
            tackleDamageApplied = false;
        }

        public bool AnimationContact()
        {
            if (state == MobilityState.Tackle && !tackleDamageApplied)
            {
                ApplyTackleHit();
                return true;
            }

            if (state == MobilityState.Slide && !tackleDamageApplied)
            {
                ApplySlideHit();
                return true;
            }

            return false;
        }

        private void ApplyTackleHit()
        {
            tackleDamageApplied = true;
            Vector3 start = transform.position + Vector3.up * .9f + movementDirection * tackleHitForwardOffset;
            Vector3 end = start + movementDirection * tackleDistance * .45f;
            int count = Physics.OverlapCapsuleNonAlloc(start, end, tackleHitRadius, hitBuffer, damageableLayers, QueryTriggerInteraction.Collide);
            HashSet<int> hitIds = new();
            for (int index = 0; index < count; index++)
            {
                Collider hitCollider = hitBuffer[index];
                if (hitCollider == null)
                {
                    continue;
                }

                IDamageable target = FindDamageable(hitCollider.transform);
                Component targetComponent = target as Component;
                if (target == null || target.IsDead || targetComponent == null || targetComponent.transform.IsChildOf(transform) ||
                    !hitIds.Add(targetComponent.gameObject.GetInstanceID()))
                {
                    continue;
                }

                target.ApplyDamage(new DamageInfo(
                    gameObject,
                    tackleDamage,
                    hitCollider.ClosestPoint(start),
                    movementDirection,
                    HitReactionType.Heavy,
                    tackleKnockbackDistance,
                    0.2f));

                if (logStateChanges)
                {
                    Debug.Log($"[PlayerCombatMobility] tackle hit {targetComponent.name} for {tackleDamage:0}", this);
                }
            }
        }

        private void ApplySlideHit()
        {
            tackleDamageApplied = true;
            Vector3 start = transform.position + Vector3.up * .55f + movementDirection * slideHitForwardOffset;
            Vector3 end = start + movementDirection * slideDistance * .4f;
            int count = Physics.OverlapCapsuleNonAlloc(start, end, slideHitRadius, hitBuffer, damageableLayers, QueryTriggerInteraction.Collide);
            HashSet<int> hitIds = new();
            for (int index = 0; index < count; index++)
            {
                Collider hitCollider = hitBuffer[index];
                if (hitCollider == null) continue;
                IDamageable target = FindDamageable(hitCollider.transform);
                Component targetComponent = target as Component;
                if (target == null || target.IsDead || targetComponent == null || targetComponent.transform.IsChildOf(transform) ||
                    !hitIds.Add(targetComponent.gameObject.GetInstanceID())) continue;

                target.ApplyDamage(new DamageInfo(
                    gameObject,
                    slideDamage,
                    hitCollider.ClosestPoint(start),
                    movementDirection,
                    HitReactionType.Light,
                    0.25f,
                    0.12f));

                if (logStateChanges)
                {
                    Debug.Log($"[PlayerCombatMobility] slide hit {targetComponent.name} for {slideDamage:0}", this);
                }
            }
        }

        private bool TryGetPlanarDirection(Vector3 direction, out Vector3 planarDirection)
        {
            planarDirection = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            if (planarDirection.sqrMagnitude < 0.001f)
            {
                planarDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            }

            return planarDirection.sqrMagnitude >= 0.001f;
        }

        private void CacheActions()
        {
            tackleAction = FindOptionalAction(tackleActionName);
            slideAction = FindOptionalAction(slideActionName);
        }

        private InputAction FindOptionalAction(string actionName)
        {
            return inputActions == null || string.IsNullOrWhiteSpace(actionName)
                ? null
                : inputActions.FindAction($"{actionMapName}/{actionName}", false);
        }

        private static IDamageable FindDamageable(Transform root)
        {
            foreach (MonoBehaviour component in root.GetComponentsInParent<MonoBehaviour>())
            {
                if (component is IDamageable damageable)
                {
                    return damageable;
                }
            }

            return null;
        }

        private static void Enable(InputAction action)
        {
            if (action != null && !action.enabled) action.Enable();
        }

        private static void Disable(InputAction action)
        {
            if (action != null && action.enabled) action.Disable();
        }
    }
}
