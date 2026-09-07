using Greyline.Core;
using Greyline.Enemies;
using UnityEngine;

namespace Greyline.Slice
{
    /// <summary>
    /// Slice-only progression bridge. It observes the existing enemy death state without adding
    /// progression knowledge to combat runtime code.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SliceEnemyDeathFlagBridge : MonoBehaviour
    {
        private const string DefaultFlagKey = "slice.enemy_down";

        [SerializeField] private CombatHealth enemyHealth;
        [SerializeField] private string flagKey = DefaultFlagKey;

        private bool hasWrittenFlag;

        private void Awake()
        {
            enemyHealth ??= GetComponent<CombatHealth>();
        }

        private void Update()
        {
            if (hasWrittenFlag || enemyHealth == null || !enemyHealth.IsDead)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(flagKey))
            {
                Debug.LogError("SliceEnemyDeathFlagBridge requires a GameFlags key.", this);
                enabled = false;
                return;
            }

            GameFlagsHost host = GameFlagsHost.Current ?? FindFirstObjectByType<GameFlagsHost>();
            if (host == null)
            {
                return;
            }

            host.Flags.Set(flagKey, true);
            hasWrittenFlag = true;
        }
    }
}
