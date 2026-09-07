using Greyline.Core;
using Greyline.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Greyline.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ThirdPersonPlayerMotor : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";

        [Header("References")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private CharacterAnimationDriver animationDriver;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float walkSpeed = 2.2f;
        [SerializeField, Min(0f)] private float runSpeed = 4.5f;
        [SerializeField, Min(0f)] private float rotationSharpness = 14f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.35f;
        [SerializeField] private float gravity = -25f;

        [Header("Crouch")]
        [SerializeField, Min(.1f)] private float crouchSpeed = 1.25f;
        [SerializeField, Min(.5f)] private float crouchHeight = 1.12f;

        private CharacterController controller;
        private PlayerCombat combat;
        private PlayerDodge dodge;
        private CombatDefense defense;
        private ContextualCombat contextualCombat;
        private IDamageable damageable;
        private InputAction moveAction;
        private InputAction jumpAction;
        private InputAction sprintAction;
        private InputAction crouchAction;
        private float verticalVelocity;
        private float airborneStartHeight;
        private float standingHeight;
        private Vector3 standingCenter;
        private readonly Collider[] standClearanceHits = new Collider[24];

        public bool IsGrounded => controller != null && controller.isGrounded;
        public float VerticalVelocity => verticalVelocity;
        public float PlanarSpeed { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsStandBlocked => IsCrouching && !HasStandingClearance();

        public void Configure(Camera followCamera, InputActionAsset actions)
        {
            cameraTransform = followCamera != null ? followCamera.transform : null;
            inputActions = actions;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            animationDriver ??= GetComponent<CharacterAnimationDriver>();
            combat = GetComponent<PlayerCombat>();
            dodge = GetComponent<PlayerDodge>();
            defense = GetComponent<CombatDefense>();
            contextualCombat = GetComponent<ContextualCombat>();
            damageable = GetComponent<IDamageable>();
            standingHeight = controller.height;
            standingCenter = controller.center;

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            CacheActions();
        }

        private void OnEnable()
        {
            EnableAction(moveAction);
            EnableAction(jumpAction);
            EnableAction(sprintAction);
            EnableAction(crouchAction);
        }

        private void OnDisable()
        {
            DisableAction(moveAction);
            DisableAction(jumpAction);
            DisableAction(sprintAction);
            DisableAction(crouchAction);
        }

        private void Update()
        {
            if (Progression.ProductionSession.GameplayBlocked) return;
            if (moveAction == null || cameraTransform == null)
            {
                return;
            }

            Vector2 input = moveAction.ReadValue<Vector2>();
            float inputMagnitude = Mathf.Clamp01(input.magnitude);
            Vector3 moveDirection = GetCameraRelativeDirection(input);

            bool isGrounded = controller.isGrounded;
            bool committed = (damageable != null && damageable.IsDead) ||
                (combat != null && combat.IsAttacking) ||
                (contextualCombat != null && contextualCombat.IsBusy) ||
                (defense != null && defense.IsGuardBroken);
            bool isDodging = dodge != null && dodge.IsDodging;
            bool charging = combat != null && combat.IsChargingHeavy;
            bool guarding = defense != null && defense.IsGuarding;
            bool wantsCrouch = crouchAction != null && crouchAction.IsPressed() &&
                isGrounded && !committed && !isDodging && !charging && !guarding;
            SetCrouching(wantsCrouch);
            if (isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            if (isGrounded)
            {
                airborneStartHeight = transform.position.y;
            }

            if (isGrounded && !committed && !isDodging && !charging && !guarding &&
                jumpAction != null && jumpAction.WasPressedThisFrame() && TryStandForAction())
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            verticalVelocity += gravity * Time.deltaTime;

            bool landingRecovery = animationDriver != null && animationDriver.IsLandingRecovery;
            float speed = landingRecovery || committed || isDodging ? 0f : GetMoveSpeed(inputMagnitude, isGrounded);
            if (charging) speed *= .35f;
            else if (guarding) speed *= .45f;
            Vector3 beforeMove = transform.position;
            controller.Move((moveDirection * speed + Vector3.up * verticalVelocity) * Time.deltaTime);
            PlanarSpeed = Time.deltaTime > 0f
                ? Vector3.ProjectOnPlane(transform.position - beforeMove, Vector3.up).magnitude / Time.deltaTime
                : 0f;
            bool groundedAfterMove = controller.isGrounded;
            float fallDistance = groundedAfterMove
                ? 0f
                : Mathf.Max(0f, airborneStartHeight - transform.position.y);
            animationDriver?.SetLocomotion(PlanarSpeed, groundedAfterMove, verticalVelocity, fallDistance);
            animationDriver?.SetCrouching(IsCrouching);

            if (!landingRecovery && !committed && !isDodging && moveDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
                float blend = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, blend);
            }
        }

        private void CacheActions()
        {
            if (inputActions == null)
            {
                Debug.LogError("Player motor requires InputSystem_Actions.", this);
                return;
            }

            moveAction = inputActions.FindAction($"{actionMapName}/Move", false);
            jumpAction = inputActions.FindAction($"{actionMapName}/Jump", false);
            sprintAction = inputActions.FindAction($"{actionMapName}/Sprint", false);
            crouchAction = inputActions.FindAction($"{actionMapName}/Crouch", false);
        }

        private Vector3 GetCameraRelativeDirection(Vector2 input)
        {
            Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            return Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
        }

        private float GetMoveSpeed(float inputMagnitude, bool isGrounded)
        {
            if (IsCrouching) return crouchSpeed * inputMagnitude;
            // Keyboard is deliberately discrete: WASD walks, Shift + WASD runs.
            // Analog magnitude still scales the selected speed for controller support.
            float selectedSpeed = isGrounded && sprintAction != null && sprintAction.IsPressed()
                ? runSpeed
                : walkSpeed;
            return selectedSpeed * inputMagnitude;
        }

        /// <summary>Standing combat and jump requests fail safely while below an obstruction.</summary>
        public bool TryStandForAction() => SetCrouching(false);

        public bool SetCrouching(bool crouching)
        {
            if (controller == null) return false;
            if (IsCrouching == crouching) return true;
            if (!crouching && !HasStandingClearance()) return false;

            float height = crouching
                ? Mathf.Clamp(crouchHeight, controller.radius * 2f, standingHeight)
                : standingHeight;
            // Keep the bottom of the capsule fixed: lowering it must not move feet through ground.
            controller.height = height;
            controller.center = standingCenter + Vector3.up * ((height - standingHeight) * .5f);
            IsCrouching = crouching;
            animationDriver?.SetCrouching(crouching);
            return true;
        }

        private bool HasStandingClearance()
        {
            if (controller == null) return false;
            Vector3 scale = transform.lossyScale;
            float radius = Mathf.Max(.01f, controller.radius - controller.skinWidth * .5f) *
                Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float halfSegment = Mathf.Max(0f, standingHeight * Mathf.Abs(scale.y) * .5f - radius);
            Vector3 center = transform.TransformPoint(standingCenter);
            int count = Physics.OverlapCapsuleNonAlloc(center - transform.up * halfSegment,
                center + transform.up * halfSegment, radius, standClearanceHits,
                Physics.AllLayers, QueryTriggerInteraction.Ignore);
            if (count == standClearanceHits.Length) return false;
            for (int index = 0; index < count; index++)
            {
                Collider hit = standClearanceHits[index];
                if (hit == null || hit.transform.IsChildOf(transform) ||
                    Physics.GetIgnoreLayerCollision(gameObject.layer, hit.gameObject.layer) ||
                    Physics.GetIgnoreCollision(controller, hit)) continue;
                return false;
            }

            return true;
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
    }
}
