using UnityEngine;
using UnityEngine.UI;

namespace Greyline.UI
{
    /// <summary>Presentation-only contextual prompt. Interaction selection remains Systems-owned.</summary>
    public sealed class InteractionPromptView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Text label;

        public void Show(InteractionPromptData data)
        {
            if (data == null) return;
            if (label != null) label.text = string.IsNullOrWhiteSpace(data.targetLabel) ? data.actionLabel : $"{data.actionLabel}  {data.targetLabel}";
            if (group != null) { group.alpha = 1f; group.blocksRaycasts = false; }
        }
        public void Hide() { if (group != null) group.alpha = 0f; }
    }
}
