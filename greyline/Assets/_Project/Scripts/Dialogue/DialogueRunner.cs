using System;
using Greyline.Core;
using UnityEngine;

namespace Greyline.Dialogue
{
    /// <summary>Runtime state machine for a single linear <see cref="DialogueDefinition"/>.</summary>
    [DisallowMultipleComponent]
    public sealed class DialogueRunner : MonoBehaviour
    {
        [SerializeField, Tooltip("Console log for dialogue start / line advance / completion.")] private bool logStateChanges = true;

        private GameFlags flags;
        private DialogueDefinition activeDialogue;
        private int currentLineIndex = -1;

        public DialogueDefinition ActiveDialogue => activeDialogue;
        public int CurrentLineIndex => currentLineIndex;
        public bool IsRunning => activeDialogue != null && currentLineIndex >= 0 && currentLineIndex < activeDialogue.Lines.Count;
        public DialogueLine CurrentLine => IsRunning ? activeDialogue.Lines[currentLineIndex] : null;
        public bool CanContinue => IsRunning;

        /// <summary>UI reads CurrentLine after this fires; null means the conversation closed.</summary>
        public event Action<DialogueDefinition, DialogueLine> DialogueStateChanged;
        public event Action<DialogueDefinition> DialogueCompleted;

        public void Configure(GameFlags gameFlags) => flags = gameFlags;

        public bool StartDialogue(DialogueDefinition definition, GameFlags gameFlags = null)
        {
            if (definition == null || definition.Lines == null || definition.Lines.Count == 0)
            {
                return false;
            }

            flags = gameFlags ?? ResolveFlags();
            activeDialogue = definition;
            currentLineIndex = 0;
            if (logStateChanges)
            {
                Debug.Log($"[DialogueRunner] start '{definition.DialogueId}' line 0/{definition.Lines.Count}: {CurrentLine.Speaker}: {CurrentLine.Text}", this);
            }

            DialogueStateChanged?.Invoke(activeDialogue, CurrentLine);
            return true;
        }

        /// <summary>Applies the optional current-line flag, then exposes the next line or completes.</summary>
        public bool Continue()
        {
            if (!IsRunning)
            {
                return false;
            }

            DialogueLine finished = CurrentLine;
            if (flags != null && !string.IsNullOrWhiteSpace(finished.SetFlag))
            {
                flags.Set(finished.SetFlag, true);
            }

            currentLineIndex++;
            if (currentLineIndex < activeDialogue.Lines.Count)
            {
                if (logStateChanges)
                {
                    Debug.Log($"[DialogueRunner] '{activeDialogue.DialogueId}' advance -> line {currentLineIndex}/{activeDialogue.Lines.Count}: {CurrentLine.Speaker}: {CurrentLine.Text}", this);
                }

                DialogueStateChanged?.Invoke(activeDialogue, CurrentLine);
                return true;
            }

            DialogueDefinition completedDialogue = activeDialogue;
            if (logStateChanges)
            {
                Debug.Log($"[DialogueRunner] '{completedDialogue.DialogueId}' completed", this);
            }

            activeDialogue = null;
            currentLineIndex = -1;
            DialogueStateChanged?.Invoke(completedDialogue, null);
            DialogueCompleted?.Invoke(completedDialogue);
            return true;
        }

        public void StopDialogue()
        {
            if (activeDialogue == null) return;
            DialogueDefinition stopped = activeDialogue;
            activeDialogue = null;
            currentLineIndex = -1;
            DialogueStateChanged?.Invoke(stopped, null);
        }

        private GameFlags ResolveFlags()
        {
            GameFlagsHost host = GameFlagsHost.Current != null ? GameFlagsHost.Current : FindFirstObjectByType<GameFlagsHost>();
            return host != null ? host.Flags : null;
        }
    }
}
