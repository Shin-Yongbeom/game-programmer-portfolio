using System;
using System.Collections.Generic;

namespace Greyline.Core
{
    /// <summary>
    /// Minimal global progression state: named boolean flags.
    ///
    /// Scope note: this is deliberately bool-only.
    /// The int/string generic store, event sink, and save serialization are out of scope until a
    /// vertical-slice requirement forces them. Quest / interaction one-shot state / dialogue branches
    /// all reduce to a flag being set, so a bool map is the smallest contract that proves the
    /// "Interactable -> state change" primitive.
    /// </summary>
    public sealed class GameFlags
    {
        private readonly Dictionary<string, bool> flags = new(StringComparer.Ordinal);

        /// <summary>Raised only when a flag's effective value actually changes.</summary>
        public event Action<string, bool> FlagChanged;

        public bool Get(string key)
        {
            return !string.IsNullOrWhiteSpace(key)
                   && flags.TryGetValue(key, out bool value)
                   && value;
        }

        /// <summary>Returns true when the stored value changed as a result of this call.</summary>
        public bool Set(string key, bool value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            if (Get(key) == value)
            {
                return false;
            }

            flags[key] = value;
            FlagChanged?.Invoke(key, value);
            return true;
        }

        public void Clear()
        {
            flags.Clear();
        }

        /// <summary>Read-only view for debug / validation. Not a save format.</summary>
        public IReadOnlyDictionary<string, bool> All => flags;
    }
}
