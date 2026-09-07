using UnityEngine;

namespace Greyline.Combat
{
    /// <summary>A solid prop that can stop a short, unobstructed push from its near side.</summary>
    public sealed class EnvironmentalFinisher : MonoBehaviour
    {
        [SerializeField] private string label = "COUNTER SLAM";
        [SerializeField, Min(.5f)] private float reach = 1.8f;
        public string Label => label;
        public int ImpactCount { get; private set; }
        public bool CanUse(Vector3 enemyPosition) => TryGetContact(enemyPosition, out _, out _);
        public void Configure(string title, float range = 1.8f) { label = title; reach = range; }

        public bool TryGetContact(Vector3 enemyPosition, out Vector3 stop, out Vector3 direction)
        {
            stop = enemyPosition;
            direction = Vector3.zero;
            var solid = GetComponent<Collider>();
            if (solid == null || !solid.enabled || solid.isTrigger) return false;
            Vector3 point = solid.ClosestPoint(enemyPosition + Vector3.up * .55f);
            Vector3 offset = Vector3.ProjectOnPlane(point - enemyPosition, Vector3.up);
            if (offset.magnitude < .44f || offset.magnitude > reach) return false;
            direction = offset.normalized;
            stop = enemyPosition + direction * (offset.magnitude - .44f);
            return true;
        }

        public bool IsAtContact(Vector3 enemyPosition)
        {
            var solid = GetComponent<Collider>();
            if (solid == null || !solid.enabled) return false;
            Vector3 point = solid.ClosestPoint(enemyPosition + Vector3.up * .55f);
            return Vector3.ProjectOnPlane(point - enemyPosition, Vector3.up).magnitude <= .56f;
        }

        public void ResolveImpact() => ImpactCount++;
    }
}
