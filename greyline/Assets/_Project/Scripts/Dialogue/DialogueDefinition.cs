using System;
using System.Collections.Generic;
using UnityEngine;

namespace Greyline.Dialogue
{
    /// <summary>Small authored dialogue asset for one district beat. It is intentionally linear.</summary>
    [CreateAssetMenu(menuName = "Greyline/Dialogue/Dialogue Definition")]
    public sealed class DialogueDefinition : ScriptableObject
    {
        [SerializeField] private string dialogueId;
        [SerializeField] private DialogueLine[] lines = Array.Empty<DialogueLine>();

        public string DialogueId => dialogueId;
        public IReadOnlyList<DialogueLine> Lines => lines;

        public void Configure(string id, DialogueLine[] configuredLines)
        {
            dialogueId = id;
            lines = configuredLines ?? Array.Empty<DialogueLine>();
        }
    }

    [Serializable]
    public sealed class DialogueLine
    {
        [SerializeField] private string speaker;
        [SerializeField, TextArea(1, 4)] private string text;
        [SerializeField, Tooltip("Optional GameFlags key set when this line is advanced past.")] private string setFlag;

        public string Speaker => speaker;
        public string Text => text;
        public string SetFlag => setFlag;

        public DialogueLine(string lineSpeaker, string lineText, string flag = null)
        {
            speaker = lineSpeaker;
            text = lineText;
            setFlag = flag;
        }
    }
}
