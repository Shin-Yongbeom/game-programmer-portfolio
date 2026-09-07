using UnityEngine;
using UnityEngine.InputSystem;

namespace Greyline.CameraSystem
{
    [RequireComponent(typeof(Camera))]
    public sealed class ThirdPersonOrbitCamera : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 targetOffset = new(0f, 1.5f, 0f);
        [SerializeField, Min(0.1f)] private float distance = 4.5f;
        [SerializeField] private float mouseSensitivity = 0.14f;
        [SerializeField] private float gamepadSensitivity = 120f;
        [SerializeField] private float minPitch = -30f;
        [SerializeField] private float maxPitch = 65f;
        [SerializeField, Min(.05f)] private float obstructionRadius = .22f;
        private readonly RaycastHit[] obstructionHits = new RaycastHit[32];
        private float currentDistance;
        private float currentPivotHeight;
        private float impulseStrength;
        private float impulseUntil;
        public float CurrentDistance => currentDistance;

        public void AddImpulse(float strength = .07f)
        {
            impulseStrength = Mathf.Max(impulseStrength, strength);
            impulseUntil = Time.unscaledTime + .16f;
        }

        private InputAction lookAction;
        private float yaw;
        private float pitch = 15f;

        public void Configure(Transform followTarget, InputActionAsset actions)
        {
            target = followTarget;
            inputActions = actions;
        }

        private void Awake()
        {
            currentPivotHeight = targetOffset.y;
            if (target == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                target = player != null ? player.transform : null;
            }

            if (inputActions != null)
            {
                lookAction = inputActions.FindAction($"{actionMapName}/Look", false);
            }

            Vector3 initialEuler = transform.rotation.eulerAngles;
            yaw = initialEuler.y;
            pitch = NormalizeAngle(initialEuler.x);
            currentDistance = distance;
        }

        private void OnEnable()
        {
            if (lookAction != null && !lookAction.enabled)
            {
                lookAction.Enable();
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (lookAction != null && lookAction.enabled)
            {
                lookAction.Disable();
            }
        }

        private void LateUpdate()
        {
            if (Progression.ProductionSession.GameplayBlocked) return;
            if (target == null || lookAction == null)
            {
                return;
            }

            if (Progression.ProductionSession.Current == null && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            Vector2 look = lookAction.ReadValue<Vector2>();
            float sensitivity = lookAction.activeControl != null && lookAction.activeControl.device is Gamepad
                ? gamepadSensitivity * Time.deltaTime
                : mouseSensitivity;

            yaw += look.x * sensitivity;
            pitch = Mathf.Clamp(pitch - look.y * sensitivity, minPitch, maxPitch);

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 offset = targetOffset;
            float desiredHeight = target.GetComponent<Player.ThirdPersonPlayerMotor>()?.IsCrouching == true ? Mathf.Min(offset.y, 1f) : offset.y;
            currentPivotHeight = Mathf.Lerp(currentPivotHeight, desiredHeight, 1f - Mathf.Exp(-Time.deltaTime / .16f));
            offset.y = currentPivotHeight;
            Vector3 pivot = target.position + offset;
            Vector3 back = -(rotation * Vector3.forward);
            float clearDistance = distance;
            int count = Physics.SphereCastNonAlloc(pivot, obstructionRadius, back, obstructionHits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform hit = obstructionHits[i].transform;
                if (hit.IsChildOf(target) || hit.GetComponentInParent<Combat.IDamageable>() != null) continue;
                clearDistance = Mathf.Min(clearDistance, Mathf.Max(.25f, obstructionHits[i].distance - .08f));
            }
            currentDistance = clearDistance < currentDistance ? clearDistance : Mathf.MoveTowards(currentDistance, clearDistance, Time.deltaTime * 5f);
            float remaining = Mathf.Clamp01((impulseUntil - Time.unscaledTime) / .16f);
            Vector3 impulse = rotation * new Vector3(Mathf.Sin(Time.unscaledTime * 99f), Mathf.Cos(Time.unscaledTime * 83f), 0f) * impulseStrength * remaining;
            if (remaining <= 0f) impulseStrength = 0f;
            transform.SetPositionAndRotation(pivot + back * currentDistance + impulse, rotation);
        }

        private static float NormalizeAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }
    }
}
