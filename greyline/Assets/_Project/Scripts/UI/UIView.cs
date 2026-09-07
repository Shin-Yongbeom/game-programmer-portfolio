using System.Collections;
using UnityEngine;

namespace Greyline.UI
{
    /// Reusable visual view transition. It does not pause gameplay or own navigation state.
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UIView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform slideTarget;
        [SerializeField] private Vector2 hiddenOffset = new Vector2(0f, -18f);
        [SerializeField, Min(0.01f)] private float duration = ProductionUITokens.FadeSeconds;
        private Vector2 shownPosition;
        private Coroutine transition;
        private bool reduceMotion;

        private void Awake()
        {
            canvasGroup ??= GetComponent<CanvasGroup>();
            if (slideTarget != null) shownPosition = slideTarget.anchoredPosition;
        }

        public void Show() => SetVisible(true);
        public void Hide() => SetVisible(false);
        public void SetMotionReduction(bool value) => reduceMotion = value;

        public void SetVisible(bool visible)
        {
            if (transition != null) StopCoroutine(transition);
            transition = StartCoroutine(Transition(visible));
        }

        private IEnumerator Transition(bool visible)
        {
            if (visible) gameObject.SetActive(true);
            Vector2 from = slideTarget != null ? slideTarget.anchoredPosition : Vector2.zero;
            Vector2 to = visible ? shownPosition : shownPosition + hiddenOffset;
            if (reduceMotion)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                if (slideTarget != null) slideTarget.anchoredPosition = to;
                if (!visible) gameObject.SetActive(false);
                yield break;
            }
            float start = visible ? 0f : canvasGroup.alpha;
            float end = visible ? 1f : 0f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                canvasGroup.alpha = Mathf.Lerp(start, end, t);
                if (slideTarget != null) slideTarget.anchoredPosition = Vector2.Lerp(from, to, t);
                yield return null;
            }
            canvasGroup.alpha = end;
            if (slideTarget != null) slideTarget.anchoredPosition = to;
            if (!visible) gameObject.SetActive(false);
        }
    }
}
