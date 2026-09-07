using Greyline.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Greyline.Interaction
{
    /// <summary>District interaction endpoint that asks integration to start an already-owned minigame.</summary>
    [DisallowMultipleComponent]
    public sealed class MinigameEntry : MonoBehaviour
    {
        [SerializeField] private string entryId;
        [SerializeField] private string prompt = "Play";
        [SerializeField] private int priority;
        [SerializeField] private UnityEvent onEntered = new();

        public string EntryId => entryId;
        public string Prompt => prompt;
        public int Priority => priority;
        public bool CanInteract(GameFlags flags) => flags != null;
        public bool TryEnter(GameFlags flags)
        {
            if (!CanInteract(flags)) return false;
            onEntered.Invoke();
            return true;
        }
        public void Configure(string id, string promptText = "Play", int targetPriority = 0)
        {
            entryId = id;
            prompt = promptText;
            priority = targetPriority;
        }
    }
}
