using UnityEngine;

namespace Greyline.Enemies
{
    public sealed class DeterministicKnockback : MonoBehaviour
    {
        private Vector3 startPosition;
        private Vector3 targetPosition;
        private float duration;
        private float elapsed;
        private bool isActive;
        private readonly RaycastHit[] obstructionHits = new RaycastHit[24];
        public void Cancel() => isActive = false;

        public void Apply(Vector3 direction, float distance, float travelDuration)
        {
            Vector3 flatDirection = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            if (distance <= 0f || flatDirection.sqrMagnitude < .001f)
            {
                return;
            }

            startPosition = transform.position;
            int count = Physics.CapsuleCastNonAlloc(startPosition + Vector3.up * .45f,
                startPosition + Vector3.up * 1.35f, .37f, flatDirection, obstructionHits, distance,
                ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (obstructionHits[i].transform.IsChildOf(transform)) continue;
                distance = Mathf.Min(distance, Mathf.Max(0f, obstructionHits[i].distance - .04f));
            }
            targetPosition = startPosition + flatDirection * distance;
            duration = Mathf.Max(.01f, travelDuration);
            elapsed = 0f;
            isActive = true;
        }

        private void Update()
        {
            if (!isActive)
            {
                return;
            }

            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(startPosition, targetPosition, progress * progress * (3f - 2f * progress));
            isActive = progress < 1f;
        }
    }
}
