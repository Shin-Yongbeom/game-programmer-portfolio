using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Greyline.UI
{
    /// Non-permanent, non-central objective update. Call Show when the objective changes.
    public sealed class ObjectiveNotificationView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Text title;
        [SerializeField] private Text detail;
        [SerializeField] private bool reduceMotion;
        private Coroutine routine;

        public void Show(ObjectiveChangedData data)
        {
            if (data == null) return;
            if (title != null) title.text = "NEW OBJECTIVE";
            if (detail != null) detail.text = string.IsNullOrWhiteSpace(data.detail) ? data.title : $"{data.title}\n{data.detail}";
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Play());
        }

        public void SetReduceMotion(bool value) => reduceMotion = value;

        private IEnumerator Play()
        {
            if (group == null) yield break;
            if (reduceMotion)
            {
                group.alpha = 1f;
                yield return new WaitForSecondsRealtime(ProductionUITokens.ObjectiveLifetime);
                group.alpha = 0f;
                yield break;
            }
            yield return Fade(0f, 1f, ProductionUITokens.FadeSeconds);
            yield return new WaitForSecondsRealtime(ProductionUITokens.ObjectiveLifetime);
            yield return Fade(1f, 0f, ProductionUITokens.FadeSeconds);
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            group.alpha = to;
        }
    }
}
