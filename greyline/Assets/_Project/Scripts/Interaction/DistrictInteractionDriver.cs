using System;
using Greyline.Core;
using Greyline.Dialogue;
using UnityEngine;

namespace Greyline.Interaction
{
    /// <summary>
    /// First-district interaction route. It consumes InteractionInput, chooses exactly one nearby
    /// endpoint by priority then distance, and dispatches supported endpoints including field checkpoints.
    /// It deliberately does not modify Player or own an input action asset.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DistrictInteractionDriver : MonoBehaviour
    {
        [SerializeField] private InteractionInput interactionInput;
        [SerializeField] private DialogueRunner dialogueRunner;
        [SerializeField, Min(0f)] private float interactRadius = 4f;
        [SerializeField, Tooltip("Console log for target changes / interact routing / dialogue advance.")] private bool logStateChanges = true;

        public string CurrentPrompt { get; private set; }
        public Component CurrentTarget { get; private set; }
        public event Action<Component, string> TargetChanged;
        public event Action<Component> InteractionRouted;

        public void Configure(InteractionInput input, DialogueRunner runner = null)
        {
            interactionInput = input;
            dialogueRunner = runner;
        }

        private void OnEnable()
        {
            interactionInput ??= GetComponent<InteractionInput>();
            if (interactionInput != null) interactionInput.Interacted += OnInteracted;
            RefreshTarget();
        }
        private void OnDisable()
        {
            if (interactionInput != null) interactionInput.Interacted -= OnInteracted;
        }
        private void Update() => RefreshTarget();
        private void OnInteracted()
        {
            if (dialogueRunner != null && dialogueRunner.IsRunning)
            {
                if (logStateChanges)
                {
                    Debug.Log("[DistrictInteraction] Interact -> DialogueRunner.Continue()", this);
                }

                dialogueRunner.Continue();
            }
            else
            {
                if (logStateChanges)
                {
                    Debug.Log($"[DistrictInteraction] Interact pressed | currentTarget={(CurrentTarget != null ? CurrentTarget.name : "<none>")}", this);
                }

                TryInteractCurrent();
            }
        }

        public bool TryInteractCurrent()
        {
            GameFlags flags = ResolveFlags();
            if (Progression.ProductionSession.GameplayBlocked || dialogueRunner != null && dialogueRunner.IsRunning) return false;
            RefreshTarget();
            bool handled = CurrentTarget switch
            {
                ProductionCheckpoint checkpoint => checkpoint.TryUse(),
                NpcConversation npc when flags != null => npc.TryInteract(flags, dialogueRunner),
                Interactable interactable when flags != null => interactable.TryInteract(flags),
                MinigameEntry entry when flags != null => entry.TryEnter(flags),
                _ => false
            };
            if (handled)
            {
                if (logStateChanges)
                {
                    Debug.Log($"[DistrictInteraction] routed to {CurrentTarget.name}", this);
                }

                InteractionRouted?.Invoke(CurrentTarget);
            }
            else if (logStateChanges)
            {
                Debug.Log("[DistrictInteraction] interact had no eligible target in range", this);
            }

            RefreshTarget();
            return handled;
        }

        public void RefreshTarget()
        {
            GameFlags flags = ResolveFlags();
            Candidate best = default;
            var session = Progression.ProductionSession.Current;
            if (session != null && !Progression.ProductionSession.GameplayBlocked && session.CanRest())
                foreach (var checkpoint in FindObjectsByType<ProductionCheckpoint>(FindObjectsSortMode.None))
                    Consider(checkpoint, checkpoint.IsReachable(transform), 10, "E  REST & SAVE  /  " + checkpoint.Title, checkpoint.Id, ref best);
            if (flags != null)
            {
                foreach (NpcConversation npc in FindObjectsByType<NpcConversation>(FindObjectsSortMode.None)) Consider(npc, npc.CanInteract(flags), npc.Priority, npc.Dialogue != null ? "Talk" : "Speak", npc.ConversationId, ref best);
                foreach (MinigameEntry entry in FindObjectsByType<MinigameEntry>(FindObjectsSortMode.None)) Consider(entry, entry.CanInteract(flags), entry.Priority, entry.Prompt, entry.EntryId, ref best);
                foreach (Interactable interactable in FindObjectsByType<Interactable>(FindObjectsSortMode.None)) Consider(interactable, interactable.CanInteract(flags), interactable.Priority, interactable.Prompt, interactable.InteractionId, ref best);
            }
            if (CurrentTarget == best.target && CurrentPrompt == best.prompt) return;
            CurrentTarget = best.target;
            CurrentPrompt = best.prompt;
            if (logStateChanges)
            {
                Debug.Log($"[DistrictInteraction] target changed -> {(CurrentTarget != null ? CurrentTarget.name : "<none>")} ({CurrentPrompt})", this);
            }

            TargetChanged?.Invoke(CurrentTarget, CurrentPrompt);
        }

        private void Consider(Component target, bool eligible, int priority, string prompt, string id, ref Candidate best)
        {
            if (!eligible) return;
            float distance = Vector3.Distance(transform.position, target.transform.position);
            if (distance > interactRadius) return;
            if (best.target != null && (priority < best.priority
                || priority == best.priority && (distance > best.distance
                    || Mathf.Approximately(distance, best.distance) && string.CompareOrdinal(id, best.id) >= 0))) return;
            best = new Candidate { target = target, priority = priority, distance = distance, prompt = prompt, id = id };
        }
        private GameFlags ResolveFlags()
        {
            GameFlagsHost host = GameFlagsHost.Current != null ? GameFlagsHost.Current : FindFirstObjectByType<GameFlagsHost>();
            return host != null ? host.Flags : null;
        }
        private struct Candidate { public Component target; public int priority; public float distance; public string prompt; public string id; }
    }
}
