using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Greyline.UI
{
    /// Controller-friendly focus helper. Input action ownership remains outside UI.
    public sealed class UIFocusController : MonoBehaviour
    {
        [SerializeField] private Selectable firstSelectable;
        [SerializeField] private Selectable fallbackSelectable;
        private GameObject lastSelected;

        public void FocusFirst()
        {
            Selectable target = firstSelectable != null ? firstSelectable : fallbackSelectable;
            SetSelected(target != null ? target.gameObject : null);
        }

        public void RememberCurrent()
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
                lastSelected = EventSystem.current.currentSelectedGameObject;
        }

        public void RestoreLast()
        {
            if (lastSelected != null && lastSelected.activeInHierarchy && lastSelected.GetComponent<Selectable>() != null)
                SetSelected(lastSelected);
            else
                FocusFirst();
        }

        public static void SetSelected(GameObject target)
        {
            if (EventSystem.current == null || target == null) return;
            EventSystem.current.SetSelectedGameObject(target);
        }
    }
}
