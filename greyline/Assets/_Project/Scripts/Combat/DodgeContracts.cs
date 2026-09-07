using UnityEngine;

namespace Greyline.Combat
{
    /// <summary>
    /// Minimal contract that lets a component veto incoming damage on its GameObject.
    /// A dodge i-frame window is the first implementer; enemies/bosses can reuse it later.
    /// </summary>
    public interface IDamageInvulnerability
    {
        bool IsInvulnerable { get; }
    }

    public static class DamageInvulnerability
    {
        /// <summary>
        /// True when any active <see cref="IDamageInvulnerability"/> on the target or its parents
        /// is currently blocking damage. Cheap no-op for targets that never opt in (e.g. dummies).
        /// </summary>
        public static bool IsActive(Component target)
        {
            if (target == null)
            {
                return false;
            }

            foreach (MonoBehaviour behaviour in target.GetComponentsInParent<MonoBehaviour>())
            {
                if (behaviour is IDamageInvulnerability invulnerability && invulnerability.IsInvulnerable)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
