using System;
using UnityEngine;

namespace Greyline.UI
{
    public enum StoryArchiveState { Loading, Empty, Populated }

    /// <summary>UI read model; no gameplay state or progression rules are represented here.</summary>
    public sealed class StoryArchiveSnapshot
    {
        public StoryArchiveState State { get; private set; }
        public string CurrentQuestName { get; private set; }
        public string CurrentObjective { get; private set; }
        public string RecentEvent { get; private set; }

        public static StoryArchiveSnapshot Loading() => new StoryArchiveSnapshot { State = StoryArchiveState.Loading };
        public static StoryArchiveSnapshot Empty() => new StoryArchiveSnapshot { State = StoryArchiveState.Empty };
        public static StoryArchiveSnapshot Populated(string questName, string objective, string recentEvent) => new StoryArchiveSnapshot
        {
            State = StoryArchiveState.Populated,
            CurrentQuestName = questName,
            CurrentObjective = objective,
            RecentEvent = recentEvent
        };
    }

    /// Read-only UI-facing data. Gameplay systems remain the owners of these values.
    [Serializable]
    public sealed class StoryJournalData
    {
        public string chapter = "CHAPTER 01";
        public string currentSituation = "Placeholder situation text for the journal panel.";
        public string currentObjective = "Placeholder objective text.";
        public string[] recentRecords =
        {
            "Placeholder journal record A.",
            "Placeholder journal record B."
        };
    }

    [Serializable]
    public sealed class ObjectiveChangedData
    {
        public string title;
        public string detail;
    }

    [Serializable]
    public sealed class InteractionPromptData
    {
        public string actionLabel;
        public string targetLabel;
    }

    [Serializable]
    public sealed class DialoguePresentationData
    {
        public string speakerName;
        [TextArea(2, 8)] public string bodyText;
        public bool showContinue = true;
    }

    public readonly struct PlayerHealthSnapshot
    {
        public PlayerHealthSnapshot(float normalizedHealth) => NormalizedHealth = normalizedHealth;
        public float NormalizedHealth { get; }
    }
}
