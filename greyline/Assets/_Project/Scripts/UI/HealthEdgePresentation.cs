using UnityEngine;
using UnityEngine.UI;

namespace Greyline.UI
{
    /// Health-state presentation only. It never listens for hit events.
    public sealed class HealthEdgePresentation : MonoBehaviour
    {
        [SerializeField] private Image edgeOverlay;
        [SerializeField] private CanvasGroup desaturationCandidate;
        [SerializeField] private float damagedMin = .76f;
        [SerializeField] private float lowMin = .51f;
        [SerializeField] private float criticalMin = .26f;
        [SerializeField] private float smoothing = 6f;
        private float targetAlpha;
        private float accessibilityIntensity = 1f;

        public void SetHealth(PlayerHealthSnapshot snapshot) => SetNormalizedHealth(snapshot.NormalizedHealth);

        /// <summary>QA/editor setup hook; gameplay still owns health values at runtime.</summary>
        public void Configure(Image overlay, CanvasGroup desaturation = null)
        {
            edgeOverlay = overlay;
            desaturationCandidate = desaturation;
            if (edgeOverlay != null)
            {
                Color color = edgeOverlay.color;
                color.a = 0f;
                edgeOverlay.color = color;
            }
        }

        public void SetNormalizedHealth(float normalizedHealth)
        {
            float health = Mathf.Clamp01(normalizedHealth);
            float baseAlpha = health >= damagedMin ? 0f : health >= lowMin ? .10f : health >= criticalMin ? .24f : .42f;
            targetAlpha = baseAlpha * accessibilityIntensity;
            if (desaturationCandidate != null)
                desaturationCandidate.alpha = health < lowMin ? Mathf.InverseLerp(1f, lowMin, health) * .18f : 0f;
        }

        public void SetAccessibilityIntensity(float intensity) => accessibilityIntensity = Mathf.Clamp(intensity, .5f, 2f);

        private void Update()
        {
            if (edgeOverlay == null) return;
            Color color = edgeOverlay.color;
            color.a = Mathf.MoveTowards(color.a, targetAlpha, Time.unscaledDeltaTime * smoothing);
            edgeOverlay.color = color;
        }
    }
}
