using Greyline.Player;
using Greyline.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Greyline.Combat
{
    /// <summary>
    /// Gameplay-driven dodge primitive. CharacterController displacement only - no Rigidbody/ragdoll.
    /// Dodge motion is additive on top of <see cref="ThirdPersonPlayerMotor"/>; the motor is not modified.
    /// Requests direction-matched presentation while gameplay owns distance and invulnerability.
    ///
    /// Dodge attack / perfect dodge / stamina / roll chaining are intentionally out of scope.
    /// This is also the single dash implementation: there is no separate Dash state.
    ///
    /// While dodging:
    /// - hit rule:    the i-frame window vetoes incoming damage via <see cref="IDamageInvulnerability"/>.
    /// - attack rule: <see cref="PlayerCombat"/> will not start a new attack while dodging; if an
    ///                attack is already active a dodge cannot start, and if one starts the dodge ends.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerDodge : MonoBehaviour, IDamageInvulnerability
    {
        [Header("Config (data-adjustable)")]
        [SerializeField, Min(0f)] private float dodgeDistance = 3.2f;
        [SerializeField, Min(0.01f)] private float dodgeDuration = 0.26f;
        [SerializeField, Min(0f)] private float invulnerabilityDuration = 0.2f;
        [SerializeField, Min(0f)] private float cooldown = 0.55f;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";

        [Header("References")]
        [SerializeField] private PlayerCombat playerCombat;

        [Header("Dev only")]
        [SerializeField, Tooltip("P key logs whether incoming damage would be blocked. Not a shipping binding.")]
        private bool enableIFrameProbe = true;
        [SerializeField] private bool logStateChanges = true;

        private CharacterController controller;
        private IDamageable selfDamageable;
        private InputAction dodgeAction;
        private InputAction moveAction;
        private CharacterAnimationDriver animationDriver;
        private Vector3 dodgeDirection;
        private float dodgeEndsAt;
        private float invulnerabilityEndsAt;
        private float nextDodgeAllowedAt;
        private bool isDodging;

        public bool IsDodging => isDodging;
        public bool HasInputAsset => inputActions != null;
        public bool IsInvulnerable => Time.time < invulnerabilityEndsAt;
        public bool IsOnCooldown => Time.time < nextDodgeAllowedAt;
        public float DodgeDistance => dodgeDistance;
        public float DodgeDuration => dodgeDuration;
        public float InvulnerabilityDuration => invulnerabilityDuration;
        public float Cooldown => cooldown;

        public void Configure(float distance, float duration, float invulnerability, float dodgeCooldown)
        {
            dodgeDistance = Mathf.Max(0f, distance);
            dodgeDuration = Mathf.Max(0.01f, duration);
            invulnerabilityDuration = Mathf.Max(0f, invulnerability);
            cooldown = Mathf.Max(0f, dodgeCooldown);
        }

        /// <summary>Editor-tool / scene wiring entry point for the shared input asset.</summary>
        public void ConfigureInput(InputActionAsset actions)
        {
            inputActions = actions;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            playerCombat ??= GetComponent<PlayerCombat>();
            selfDamageable ??= GetComponent<IDamageable>();
            animationDriver = GetComponent<CharacterAnimationDriver>();
            if (inputActions != null)
            {
                dodgeAction = inputActions.FindAction($"{actionMapName}/Dodge", false);
                moveAction = inputActions.FindAction($"{actionMapName}/Move", false);
            }
        }

        private void OnEnable()
        {
            if (dodgeAction != null && !dodgeAction.enabled)
            {
                dodgeAction.Enable();
            }
        }

        private void OnDisable()
        {
            if (dodgeAction != null && dodgeAction.enabled)
            {
                dodgeAction.Disable();
            }
        }

        private void Update()
        {
            if (Progression.ProductionSession.GameplayBlocked) return;
            if (selfDamageable != null && selfDamageable.IsDead)
            {
                if (isDodging)
                {
                    EndDodge();
                }

                return;
            }

            if (dodgeAction != null && dodgeAction.WasPressedThisFrame())
            {
                RequestDodge();
            }

            if (enableIFrameProbe && Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
            {
                bool blocked = DamageInvulnerability.IsActive(this);
                Debug.Log(
                    $"[PlayerDodge] TEST incoming damage would be {(blocked ? "BLOCKED (i-frames active)" : "APPLIED")} " +
                    $"| dodging={isDodging} onCooldown={IsOnCooldown}",
                    this);
            }

            if (!isDodging)
            {
                return;
            }

            if (Time.time >= dodgeEndsAt || (playerCombat != null && playerCombat.IsAttacking))
            {
                EndDodge();
                return;
            }

            float speed = dodgeDistance / dodgeDuration;
            controller.Move(dodgeDirection * speed * Time.deltaTime);
        }

        /// <summary>Starts a dodge if not already dodging, not on cooldown, and not mid-attack.</summary>
        public bool RequestDodge()
        {
            Vector2 input = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
            Transform camera = Camera.main != null ? Camera.main.transform : null;
            if (input.sqrMagnitude < .01f || camera == null) return RequestDodge(-transform.forward);
            Vector3 forward = Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(camera.right, Vector3.up).normalized;
            return RequestDodge(forward * input.y + right * input.x);
        }

        /// <summary>Starts the shared Dodge/Dash movement in the supplied planar direction.</summary>
        public bool RequestDodge(Vector3 direction)
        {
            if (Progression.ProductionSession.GameplayBlocked || isDodging || IsOnCooldown)
            {
                return false;
            }

            if (selfDamageable != null && selfDamageable.IsDead)
            {
                return false;
            }

            if (playerCombat != null && playerCombat.IsAttacking)
            {
                return false;
            }

            if (GetComponent<ContextualCombat>()?.IsBusy == true || GetComponent<CombatDefense>()?.IsGuardBroken == true)
                return false;
            var motor = GetComponent<ThirdPersonPlayerMotor>();
            if (motor != null && motor.IsCrouching && !motor.TryStandForAction()) return false;

            dodgeDirection = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            if (dodgeDirection.sqrMagnitude < 0.001f)
            {
                dodgeDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            }

            isDodging = true;
            dodgeEndsAt = Time.time + dodgeDuration;
            invulnerabilityEndsAt = Time.time + Mathf.Min(invulnerabilityDuration, dodgeDuration);
            nextDodgeAllowedAt = dodgeEndsAt + cooldown;
            playerCombat?.CancelCurrentAction();
            GetComponent<CombatDefense>()?.SetGuardHeld(false);
            animationDriver?.RequestDodge(dodgeDirection, dodgeDuration);

            if (logStateChanges)
            {
                Debug.Log(
                    $"[PlayerDodge] dodge start | dir={dodgeDirection} dist={dodgeDistance} dur={dodgeDuration} iframes={invulnerabilityDuration}",
                    this);
            }

            return true;
        }

        private void EndDodge()
        {
            if (!isDodging)
            {
                return;
            }

            isDodging = false;
            if (logStateChanges)
            {
                Debug.Log("[PlayerDodge] dodge end", this);
            }
        }
    }
}
