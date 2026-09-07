using System;
using Greyline.Core;
using UnityEngine;

namespace Greyline.Quest
{
    /// <summary>
    /// Minimal runtime quest progression for the first production slice.
    /// One active linear quest is enough to prove: interaction/combat/minigame result flag,
    /// next objective, and completion state. Save integration can capture the index later.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuestLog : MonoBehaviour
    {
        [SerializeField] private QuestDefinition quest;
        [SerializeField] private bool startOnEnable = true;

        private GameFlags flags;
        private int currentStepIndex = -1;
        private bool started;
        private bool completed;

        public QuestDefinition ActiveQuest => quest;
        public int CurrentStepIndex => currentStepIndex;
        public bool IsStarted => started;
        public bool IsCompleted => completed;
        public QuestStep CurrentStep => IsCurrentStepValid() ? quest.Steps[currentStepIndex] : null;

        public event Action<QuestDefinition, QuestStep> StepChanged;
        public event Action<QuestDefinition> QuestCompleted;

        public void Configure(QuestDefinition definition, GameFlags gameFlags = null)
        {
            Unsubscribe();
            quest = definition;
            flags = gameFlags;
            started = false;
            completed = false;
            currentStepIndex = -1;
            TryBindFlags();
        }

        public bool StartQuest()
        {
            if (quest == null || quest.Steps == null || quest.Steps.Count == 0 || started)
            {
                return false;
            }

            started = true;
            completed = false;
            currentStepIndex = 0;
            StepChanged?.Invoke(quest, CurrentStep);
            EvaluateCurrentStep();
            return true;
        }

        public void TickFromFlags()
        {
            EvaluateCurrentStep();
        }

        private void OnEnable()
        {
            TryBindFlags();
            if (startOnEnable)
            {
                StartQuest();
            }
        }

        private void OnDisable() => Unsubscribe();

        private void TryBindFlags()
        {
            if (flags == null)
            {
                GameFlagsHost host = GameFlagsHost.Current != null
                    ? GameFlagsHost.Current
                    : FindFirstObjectByType<GameFlagsHost>();
                flags = host != null ? host.Flags : null;
            }

            if (flags != null)
            {
                flags.FlagChanged -= OnFlagChanged;
                flags.FlagChanged += OnFlagChanged;
            }
        }

        private void Unsubscribe()
        {
            if (flags != null)
            {
                flags.FlagChanged -= OnFlagChanged;
            }
        }

        private void OnFlagChanged(string key, bool value)
        {
            if (value && IsCurrentStepValid() && key == CurrentStep.CompletionFlag)
            {
                Advance();
            }
        }

        private void EvaluateCurrentStep()
        {
            if (flags != null && IsCurrentStepValid() && flags.Get(CurrentStep.CompletionFlag))
            {
                Advance();
            }
        }

        private void Advance()
        {
            if (!IsCurrentStepValid())
            {
                return;
            }

            QuestStep finishedStep = CurrentStep;
            if (flags != null && !string.IsNullOrWhiteSpace(finishedStep.ResultFlag))
            {
                flags.Set(finishedStep.ResultFlag, true);
            }

            currentStepIndex++;
            if (currentStepIndex >= quest.Steps.Count)
            {
                completed = true;
                QuestCompleted?.Invoke(quest);
                return;
            }

            StepChanged?.Invoke(quest, CurrentStep);
            EvaluateCurrentStep();
        }

        private bool IsCurrentStepValid()
        {
            return quest != null
                   && quest.Steps != null
                   && currentStepIndex >= 0
                   && currentStepIndex < quest.Steps.Count
                   && CurrentStep != null
                   && !string.IsNullOrWhiteSpace(CurrentStep.CompletionFlag);
        }
    }
}
