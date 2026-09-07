using System;
using System.Collections.Generic;
using UnityEngine;

namespace Greyline.Quest
{
    /// <summary>
    /// Small, linear quest definition. A step is completed when its completion flag becomes true.
    /// Systems intentionally consumes the existing GameFlags contract; no quest-specific event
    /// framework is required for the first production slice.
    /// </summary>
    [CreateAssetMenu(menuName = "Greyline/Quest/Quest Definition")]
    public sealed class QuestDefinition : ScriptableObject
    {
        [SerializeField] private string questId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(1, 3)] private string summary;
        [SerializeField] private QuestStep[] steps = Array.Empty<QuestStep>();

        public string QuestId => questId;
        public string DisplayName => displayName;
        public string Summary => summary;
        public IReadOnlyList<QuestStep> Steps => steps;

        /// <summary>Editor-tool entry point for deterministic content assembly.</summary>
        public void Configure(string id, string name, string description, QuestStep[] configuredSteps)
        {
            questId = id;
            displayName = name;
            summary = description;
            steps = configuredSteps ?? Array.Empty<QuestStep>();
        }
    }

    [Serializable]
    public sealed class QuestStep
    {
        [SerializeField] private string stepId;
        [SerializeField, TextArea(1, 3)] private string objectiveText;
        [SerializeField, Tooltip("The GameFlags key that completes this step.")] private string completionFlag;
        [SerializeField, Tooltip("Optional flag set when this step completes.")] private string resultFlag;

        public string StepId => stepId;
        public string ObjectiveText => objectiveText;
        public string CompletionFlag => completionFlag;
        public string ResultFlag => resultFlag;

        public QuestStep(string id, string objective, string flag, string completedFlag = null)
        {
            stepId = id;
            objectiveText = objective;
            completionFlag = flag;
            resultFlag = completedFlag;
        }
    }
}
