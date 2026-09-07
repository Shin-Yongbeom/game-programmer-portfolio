using UnityEngine;

namespace Greyline.Minigames
{
    public sealed class CourierWatcher : MonoBehaviour
    {
        private enum MotionMode { Fixed, Rotate, Patrol }
        [SerializeField] private float viewDistance = 7f;
        [SerializeField, Range(1f, 179f)] private float viewAngle = 80f;
        [SerializeField] private Transform target;
        [SerializeField] private LayerMask obstructionMask = ~0;
        [SerializeField] private MotionMode motionMode = MotionMode.Fixed;
        [SerializeField] private float rotationDegreesPerSecond = 35f;
        [SerializeField] private float rotationAmplitude = 55f;
        [SerializeField] private Transform[] patrolPoints;
        [SerializeField] private float patrolSpeed = 1.2f;
        [SerializeField, Range(.1f, 1f)] private float sneakRangeMultiplier = .5f;

        private Vector3 startPosition;
        private Quaternion startRotation;
        private float motionTime;
        private int patrolIndex;

        private void Awake() { CaptureStartPose(); }

        public void Configure(Transform observedTarget, float distance, float angle)
        {
            target = observedTarget; viewDistance = distance; viewAngle = angle; motionMode = MotionMode.Fixed; CaptureStartPose();
        }

        public void ConfigureRotating(Transform observedTarget, float distance, float angle, float degreesPerSecond, float amplitude)
        {
            Configure(observedTarget, distance, angle); motionMode = MotionMode.Rotate;
            rotationDegreesPerSecond = degreesPerSecond; rotationAmplitude = amplitude;
        }

        public void ConfigurePatrol(Transform observedTarget, float distance, float angle, Transform[] points, float speed)
        {
            Configure(observedTarget, distance, angle); motionMode = MotionMode.Patrol;
            patrolPoints = points; patrolSpeed = speed; patrolIndex = 0;
        }

        public void Tick(float deltaTime)
        {
            motionTime += Mathf.Max(0f, deltaTime);
            if (motionMode == MotionMode.Rotate)
            {
                float yaw = Mathf.Sin(motionTime * rotationDegreesPerSecond * Mathf.Deg2Rad) * rotationAmplitude;
                transform.rotation = startRotation * Quaternion.Euler(0f, yaw, 0f);
            }
            else if (motionMode == MotionMode.Patrol && patrolPoints != null && patrolPoints.Length > 0)
            {
                Transform point = patrolPoints[Mathf.Clamp(patrolIndex, 0, patrolPoints.Length - 1)];
                if (point == null) return;
                transform.position = Vector3.MoveTowards(transform.position, point.position, patrolSpeed * Mathf.Max(0f, deltaTime));
                Vector3 direction = point.position - transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                if (Vector3.Distance(transform.position, point.position) <= .02f) patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
            }
        }

        public void ResetRuntime()
        {
            transform.SetPositionAndRotation(startPosition, startRotation);
            motionTime = 0f; patrolIndex = 0;
        }

        public bool CanSeeTarget() => CanSeeTarget(false);

        public bool CanSeeTarget(bool targetSneaking)
        {
            if (target == null) return false;
            float effectiveViewDistance = EffectiveViewDistance(viewDistance, targetSneaking, sneakRangeMultiplier);
            Vector3 offset = target.position - transform.position;
            if (offset.sqrMagnitude > effectiveViewDistance * effectiveViewDistance) return false;
            Vector3 flatOffset = Vector3.ProjectOnPlane(offset, Vector3.up);
            Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (flatOffset.sqrMagnitude <= Mathf.Epsilon || flatForward.sqrMagnitude <= Mathf.Epsilon) return false;
            if (Vector3.Angle(flatForward, flatOffset) > viewAngle * .5f) return false;
            Vector3 origin = transform.position + Vector3.up * .5f;
            Vector3 destination = target.position + Vector3.up * .5f;
            Vector3 direction = destination - origin;
            float distance = direction.magnitude;
            if (distance <= Mathf.Epsilon) return true;
            RaycastHit[] hits = Physics.RaycastAll(origin, direction / distance, distance, obstructionMask, QueryTriggerInteraction.Ignore);
            float nearestDistance = float.MaxValue;
            Transform nearest = null;
            foreach (RaycastHit hit in hits)
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                if (hit.distance < nearestDistance) { nearestDistance = hit.distance; nearest = hit.transform; }
            }
            return nearest == target || (nearest != null && nearest.IsChildOf(target));
        }

        private void CaptureStartPose()
        {
            startPosition = transform.position; startRotation = transform.rotation;
        }

        public static float EffectiveViewDistance(float baseDistance, bool targetSneaking, float sneakMultiplier)
            => targetSneaking ? baseDistance * Mathf.Clamp(sneakMultiplier, .1f, 1f) : baseDistance;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, viewDistance);
        }
    }
}
