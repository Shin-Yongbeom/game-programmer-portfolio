using Greyline.Core;
using UnityEngine;

namespace Greyline.Interaction
{
    /// <summary>
    /// Smallest interaction primitive: interacting with this object sets one <see cref="GameFlags"/>
    /// flag to true. Optionally one-shot, tracked by a second "consumed" flag so a used object is
    /// never re-triggered (and, once a save system exists, that state persists for free).
    ///
    /// Deliberately not wired to Input, a player controller, or an interaction router yet
    /// (Docs/SYSTEMS_QUEST_INTERACTION_SAVE.md). Those need the shared input asset / Player prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Interactable : MonoBehaviour
    {
        [SerializeField] private string interactionId;
        [SerializeField, Tooltip("Flag set to true when this object is interacted with.")]
        private string setFlagKey;
        [SerializeField, Tooltip("If true, this object can only be interacted with once.")]
        private bool once = true;
        [SerializeField, Tooltip("Flag that records consumption. Blank -> '<interactionId>.consumed'.")]
        private string consumedFlagOverride;
        [SerializeField, Tooltip("Prompt copy for a future interaction UI. Unused by the sandbox.")]
        private string prompt = "Interact";
        [SerializeField, Tooltip("Higher values win when targets are equally close.")]
        private int priority;

        public string InteractionId => interactionId;
        public string SetFlagKey => setFlagKey;
        public bool Once => once;
        public string Prompt => prompt;
        public int Priority => priority;

        public string ConsumedFlagKey =>
            string.IsNullOrWhiteSpace(consumedFlagOverride)
                ? (string.IsNullOrWhiteSpace(interactionId) ? null : interactionId + ".consumed")
                : consumedFlagOverride;

        public bool CanInteract(GameFlags flags)
        {
            if (flags == null)
            {
                return false;
            }

            string consumed = ConsumedFlagKey;
            return !(once && consumed != null && flags.Get(consumed));
        }

        /// <summary>Runs the interaction. Returns false when it was blocked (already consumed).</summary>
        public bool TryInteract(GameFlags flags)
        {
            if (!CanInteract(flags))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(setFlagKey))
            {
                flags.Set(setFlagKey, true);
            }

            if (once)
            {
                string consumed = ConsumedFlagKey;
                if (consumed != null)
                {
                    flags.Set(consumed, true);
                }
            }

            return true;
        }

        /// <summary>Editor-tool / validator entry point so scene wiring stays code-owned.</summary>
        public void Configure(string id, string flagKey, bool oneShot = true, string consumedOverride = null, string promptText = "Interact", int targetPriority = 0)
        {
            interactionId = id;
            setFlagKey = flagKey;
            once = oneShot;
            consumedFlagOverride = consumedOverride;
            prompt = promptText;
            priority = targetPriority;
        }
    }
}
