using UnityEngine;
using UnityEngine.UI;

namespace Greyline.UI
{
    /// <summary>Read-only dialogue rendering. It does not advance dialogue or choose responses.</summary>
    public sealed class DialoguePresentationView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Text speakerName;
        [SerializeField] private Text bodyText;
        [SerializeField] private GameObject continueIndicator;
        [SerializeField] private bool reduceMotion;

        public void SetReduceMotion(bool value) => reduceMotion = value;
        public void Show(DialoguePresentationData data)
        {
            if (data == null) return;
            if (speakerName != null) speakerName.text = data.speakerName;
            if (bodyText != null) { bodyText.text = data.bodyText; bodyText.horizontalOverflow = HorizontalWrapMode.Wrap; bodyText.verticalOverflow = VerticalWrapMode.Overflow; }
            if (continueIndicator != null) continueIndicator.SetActive(data.showContinue);
            if (group != null) { group.alpha = 1f; group.blocksRaycasts = false; }
        }
        public void Hide() { if (group != null) group.alpha = reduceMotion ? 0f : 0f; }
    }
}
