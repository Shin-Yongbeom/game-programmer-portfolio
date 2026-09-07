using Greyline.Quest;
using UnityEngine;

namespace Greyline.UI
{
    /// <summary>Thin read-only bridge from the current QuestLog API into the Archive story view.</summary>
    [DisallowMultipleComponent]
    public sealed class QuestLogArchivePresenter : MonoBehaviour
    {
        [SerializeField] private ProductionUIShell shell;
        [SerializeField] private QuestLog questLog;

        private void OnEnable()
        {
            shell ??= FindFirstObjectByType<ProductionUIShell>();
            questLog ??= FindFirstObjectByType<QuestLog>();
            if (questLog == null) { shell?.PresentStory(StoryArchiveSnapshot.Empty()); return; }
            questLog.StepChanged += OnStepChanged;
            questLog.QuestCompleted += OnQuestCompleted;
            Refresh();
        }

        private void OnDisable()
        {
            if (questLog == null) return;
            questLog.StepChanged -= OnStepChanged;
            questLog.QuestCompleted -= OnQuestCompleted;
        }

        private void OnStepChanged(QuestDefinition _, QuestStep __) => Refresh();
        private void OnQuestCompleted(QuestDefinition _) => Refresh();

        private void Refresh()
        {
            if (shell == null || questLog == null || questLog.ActiveQuest == null || !questLog.IsStarted)
            {
                shell?.PresentStory(StoryArchiveSnapshot.Empty());
                return;
            }
            string objective = questLog.IsCompleted ? "Quest complete." : questLog.CurrentStep?.ObjectiveText;
            shell.PresentStory(StoryArchiveSnapshot.Populated(questLog.ActiveQuest.DisplayName, objective, null));
        }
    }
}
