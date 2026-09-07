using Greyline.Core;
using UnityEngine;

namespace Greyline.Interaction
{
    /// <summary>
    /// SystemsSandbox-only driver for verifying Interactable/NpcConversation -> GameFlags contracts.
    ///
    /// Input isolation: the interaction trigger comes from <see cref="InteractionInput"/> (the shared
    /// <c>Player/Interact</c> action), not from a key this class names. The tester only owns the
    /// "find the nearest interactable and run it" gameplay logic; pressing the key is production input.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SandboxInteractionTester : MonoBehaviour
    {
        [SerializeField, Tooltip("Production input source for the Interact action.")]
        private InteractionInput interactionInput;
        [SerializeField, Min(0f), Tooltip("Interactables within this distance of the tester are eligible.")]
        private float interactRadius = 4f;
        [SerializeField, Tooltip("Log flag state on every interaction attempt.")]
        private bool verbose = true;

        public bool HasInteractionInput => interactionInput != null;

        /// <summary>Editor-tool / scene wiring entry point so scene setup stays code-owned.</summary>
        public void Configure(InteractionInput input) => interactionInput = input;

        private void OnEnable()
        {
            interactionInput ??= GetComponent<InteractionInput>();
            if (interactionInput != null)
            {
                interactionInput.Interacted += OnInteractPressed;
            }
        }

        private void OnDisable()
        {
            if (interactionInput != null)
            {
                interactionInput.Interacted -= OnInteractPressed;
            }
        }

        private void OnInteractPressed() => InteractNearest();

        [ContextMenu("Interact Nearest")]
        public bool InteractNearest()
        {
            GameFlags flags = GameFlagsHost.Current != null ? GameFlagsHost.Current.Flags : null;
            if (flags == null)
            {
                GameFlagsHost host = FindFirstObjectByType<GameFlagsHost>();
                flags = host != null ? host.Flags : null;
            }

            if (flags == null)
            {
                Debug.LogWarning("SandboxInteractionTester: no GameFlagsHost in the scene.", this);
                return false;
            }

            Interactable target = null;
            float bestDistance = interactRadius;
            foreach (Interactable candidate in FindObjectsByType<Interactable>(FindObjectsSortMode.None))
            {
                if (!candidate.CanInteract(flags))
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, candidate.transform.position);
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    target = candidate;
                }
            }

            NpcConversation npcTarget = null;
            foreach (NpcConversation candidate in FindObjectsByType<NpcConversation>(FindObjectsSortMode.None))
            {
                if (!candidate.CanInteract(flags))
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, candidate.transform.position);
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    target = null;
                    npcTarget = candidate;
                }
            }

            if (target == null && npcTarget == null)
            {
                Debug.Log("SandboxInteractionTester: nothing in range to interact with (or all consumed).", this);
                return false;
            }

            bool ran;
            if (npcTarget != null)
            {
                ran = npcTarget.TryInteract(flags);
            }
            else
            {
                ran = target.TryInteract(flags);
                Debug.Log(
                    ran
                        ? $"Interacted with '{target.InteractionId}' -> flag '{target.SetFlagKey}' = {flags.Get(target.SetFlagKey)}, consumed = {flags.Get(target.ConsumedFlagKey)}"
                        : $"Interaction with '{target.InteractionId}' was blocked.",
                    target);
            }

            if (verbose)
            {
                foreach (var pair in flags.All)
                {
                    Debug.Log($"  flag '{pair.Key}' = {pair.Value}");
                }
            }

            return ran;
        }
    }
}
