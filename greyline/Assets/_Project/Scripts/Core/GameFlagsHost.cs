using UnityEngine;

namespace Greyline.Core
{
    /// <summary>
    /// Scene-level owner of the single <see cref="GameFlags"/> instance. One access point so
    /// interaction / quest / (later) save code never each spin up their own static manager.
    /// Place one in a sandbox or bootstrap scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameFlagsHost : MonoBehaviour
    {
        /// <summary>The most recently enabled host. Null when none is loaded.</summary>
        public static GameFlagsHost Current { get; private set; }

        public GameFlags Flags { get; } = new GameFlags();

        private void OnEnable()
        {
            if (Current != null && Current != this)
            {
                Debug.LogWarning($"Multiple GameFlagsHost instances; '{name}' overrides '{Current.name}'.", this);
            }

            Current = this;
        }

        private void OnDisable()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        [ContextMenu("Log All Flags")]
        private void LogAllFlags()
        {
            foreach (var pair in Flags.All)
            {
                Debug.Log($"flag '{pair.Key}' = {pair.Value}", this);
            }
        }
    }
}
