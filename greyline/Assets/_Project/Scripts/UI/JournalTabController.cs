using UnityEngine;
using UnityEngine.UI;

namespace Greyline.UI
{
    /// Four-tab archive shell. Panels are presentation only; data is supplied by callers.
    public sealed class JournalTabController : MonoBehaviour
    {
        [SerializeField] private Button[] tabButtons = new Button[4];
        [SerializeField] private GameObject[] tabPanels = new GameObject[4];
        [SerializeField] private int activeTab;

        public int ActiveTab => activeTab;

        private void Awake() => SelectTab(activeTab);

        public void SelectStory() => SelectTab(0);
        public void SelectPeople() => SelectTab(1);
        public void SelectTechniques() => SelectTab(2);
        public void SelectCollection() => SelectTab(3);

        public void SelectTab(int index)
        {
            if (tabPanels == null || index < 0 || index >= tabPanels.Length) return;
            activeTab = index;
            for (int i = 0; i < tabPanels.Length; i++)
            {
                if (tabPanels[i] != null) tabPanels[i].SetActive(i == activeTab);
                if (tabButtons != null && i < tabButtons.Length && tabButtons[i] != null)
                    tabButtons[i].interactable = i != activeTab;
            }
        }
    }
}
