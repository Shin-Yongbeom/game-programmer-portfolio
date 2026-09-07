using System;
using System.Collections.Generic;
using Greyline.Core;
using Greyline.Dialogue;
using UnityEngine;

namespace Greyline.Interaction
{
    /// <summary>
    /// NPC interaction endpoint. It starts an assigned short linear dialogue, or retains the tiny
    /// legacy response fallback for existing sandbox content; it is not a graph editor.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NpcConversation : MonoBehaviour
    {
        [Serializable]
        public sealed class Response
        {
            [TextArea(1, 3)] public string text;
            public string flagKey;
            public bool flagValue = true;

            public Response(string responseText, string responseFlagKey = null, bool responseFlagValue = true)
            {
                text = responseText;
                flagKey = responseFlagKey;
                flagValue = responseFlagValue;
            }
        }

        [SerializeField] private string conversationId;
        [SerializeField] private string progressFlagKey;
        [SerializeField] private List<Response> responses = new();
        [SerializeField, Tooltip("Optional short linear dialogue. When assigned, it replaces legacy response logging.")]
        private DialogueDefinition dialogue;
        [SerializeField] private int priority = 10;

        public string ConversationId => conversationId;
        public string ProgressFlagKey => progressFlagKey;
        public int ResponseCount => responses.Count;
        public DialogueDefinition Dialogue => dialogue;
        public int Priority => priority;

        public bool CanInteract(GameFlags flags) => flags != null && (dialogue != null || responses.Count > 0);

        public bool TryInteract(GameFlags flags, DialogueRunner runner = null)
        {
            if (!CanInteract(flags))
            {
                return false;
            }

            if (dialogue != null)
            {
                return runner != null && runner.StartDialogue(dialogue, flags);
            }

            int responseIndex = flags.Get(progressFlagKey) ? Mathf.Min(1, responses.Count - 1) : 0;
            Response response = responses[responseIndex];
            if (!string.IsNullOrWhiteSpace(response.flagKey))
            {
                flags.Set(response.flagKey, response.flagValue);
            }

            Debug.Log($"NPC '{conversationId}': {response.text}", this);
            return true;
        }

        /// <summary>Editor-tool / scene wiring entry point for the tiny sandbox response set.</summary>
        public void Configure(string id, string stateFlagKey, IList<Response> configuredResponses, DialogueDefinition dialogueDefinition = null, int targetPriority = 10)
        {
            conversationId = id;
            progressFlagKey = stateFlagKey;
            responses.Clear();
            responses.AddRange(configuredResponses);
            dialogue = dialogueDefinition;
            priority = targetPriority;
        }
    }
}
