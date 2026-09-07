using Greyline.Enemies;
using UnityEngine;

namespace Greyline.Slice
{
    /// <summary>Small Slice-only camera impulse for readable contact without a VFX/SFX framework.</summary>
    [DisallowMultipleComponent]
    public sealed class SliceCombatFeedback : MonoBehaviour
    {
        [SerializeField] private CombatHealth playerHealth;
        [SerializeField] private CombatHealth enemyHealth;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Transform playerVisual;
        [SerializeField, Min(0f)] private float impulse = .045f;
        [SerializeField, Min(0f)] private float playerHitOffset = .06f;
        [SerializeField, Min(.01f)] private float duration = .09f;

        private float lastPlayerHealth;
        private float lastEnemyHealth;
        private Vector3 restLocalPosition;
        private Vector3 playerRestLocalPosition;
        private Quaternion playerRestLocalRotation;
        private float cameraImpulseEndsAt;
        private float playerHitEndsAt;

        public void Configure(CombatHealth player, CombatHealth enemy, Transform camera, Transform visual)
        {
            playerHealth = player;
            enemyHealth = enemy;
            cameraTransform = camera;
            playerVisual = visual;
            CacheState();
        }

        private void Awake() => CacheState();

        private void Update()
        {
            if (playerHealth != null && playerHealth.CurrentHealth < lastPlayerHealth)
            {
                playerHitEndsAt = Time.time + duration;
                cameraImpulseEndsAt = playerHitEndsAt;
            }

            if (enemyHealth != null && enemyHealth.CurrentHealth < lastEnemyHealth)
            {
                cameraImpulseEndsAt = Time.time + duration;
            }

            if (cameraTransform != null)
            {
                float remaining = cameraImpulseEndsAt - Time.time;
                float strength = remaining > 0f ? Mathf.Clamp01(remaining / duration) : 0f;
                cameraTransform.localPosition = restLocalPosition + Random.insideUnitSphere * (impulse * strength);
            }

            if (playerVisual != null)
            {
                float remaining = playerHitEndsAt - Time.time;
                float strength = remaining > 0f ? Mathf.Clamp01(remaining / duration) : 0f;
                playerVisual.localPosition = playerRestLocalPosition + Vector3.back * (playerHitOffset * strength);
                playerVisual.localRotation = Quaternion.Slerp(playerRestLocalRotation,
                    playerRestLocalRotation * Quaternion.Euler(-5f, 0f, 0f), strength);
            }

            if (playerHealth != null) lastPlayerHealth = playerHealth.CurrentHealth;
            if (enemyHealth != null) lastEnemyHealth = enemyHealth.CurrentHealth;
        }

        private void CacheState()
        {
            if (playerHealth != null) lastPlayerHealth = playerHealth.CurrentHealth;
            if (enemyHealth != null) lastEnemyHealth = enemyHealth.CurrentHealth;
            if (cameraTransform != null) restLocalPosition = cameraTransform.localPosition;
            if (playerVisual != null)
            {
                playerRestLocalPosition = playerVisual.localPosition;
                playerRestLocalRotation = playerVisual.localRotation;
            }
        }
    }
}
