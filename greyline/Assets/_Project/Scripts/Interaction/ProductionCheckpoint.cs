using Greyline.Progression;
using UnityEngine;

namespace Greyline.Interaction
{
    public sealed class ProductionCheckpoint : MonoBehaviour
    {
        [SerializeField] private string id, title;
        [SerializeField] private Vector3 spawnPosition;
        [SerializeField] private float spawnYaw;
        public string Id => id;
        public string Title => title;
        public Vector3 SpawnPosition => spawnPosition;
        public float SpawnYaw => spawnYaw;
        private readonly RaycastHit[] hits = new RaycastHit[16];
        public void Configure(string key, string name, Vector3 spawn, float yaw = 0)
        { id = key; title = name; spawnPosition = spawn; spawnYaw = yaw; }
        public bool IsReachable(Transform actor)
        {
            if (!isActiveAndEnabled || Vector3.Distance(actor.position, transform.position) > 2.4f) return false;
            Vector3 start = actor.position + Vector3.up;
            Vector3 offset = transform.position + Vector3.up - start;
            int count = Physics.RaycastNonAlloc(start, offset.normalized, hits, offset.magnitude, ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            for (int i = 0; i < count; i++)
                if (!hits[i].transform.IsChildOf(actor) && !hits[i].transform.IsChildOf(transform)) return false;
            return true;
        }
        public bool TryUse() => ProductionSession.Current != null && ProductionSession.Current.TryCheckpoint(this);
    }
}
