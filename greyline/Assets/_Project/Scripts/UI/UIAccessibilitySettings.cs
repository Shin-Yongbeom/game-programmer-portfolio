using UnityEngine;

namespace Greyline.UI
{
    /// Runtime-only accessibility options. Persistence belongs to the existing settings owner.
    public sealed class UIAccessibilitySettings : MonoBehaviour
    {
        [SerializeField, Range(.5f, 2f)] private float healthEdgeIntensity = 1f;
        [SerializeField] private bool highContrast;
        [SerializeField] private bool reduceMotion;
        [SerializeField] private HealthEdgePresentation healthEdge;
        [SerializeField] private UIView[] animatedViews;
        [SerializeField] private UIThemeStyle[] themedControls;
        [SerializeField] private ProductionUIShell shell;

        public float HealthEdgeIntensity => healthEdgeIntensity;
        public bool HighContrast => highContrast;
        public bool ReduceMotion => reduceMotion;

        public void SetHealthEdgeIntensity(float value) { healthEdgeIntensity = Mathf.Clamp(value, .5f, 2f); Apply(); }
        public void SetHighContrast(bool value) { highContrast = value; Apply(); }
        public void SetReduceMotion(bool value) { reduceMotion = value; Apply(); }

        public void Apply()
        {
            shell ??= FindFirstObjectByType<ProductionUIShell>();
            if (shell != null)
            {
                shell.SetHighContrast(highContrast);
                shell.SetReduceMotion(reduceMotion);
            }
            if (healthEdge != null) healthEdge.SetAccessibilityIntensity(healthEdgeIntensity);
            if (animatedViews != null)
                foreach (UIView view in animatedViews) if (view != null) view.SetMotionReduction(reduceMotion);
            if (themedControls != null)
                foreach (UIThemeStyle style in themedControls) if (style != null) style.Apply(highContrast);
        }
    }
}
