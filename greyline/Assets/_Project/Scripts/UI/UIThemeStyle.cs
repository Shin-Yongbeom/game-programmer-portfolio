using UnityEngine;
using UnityEngine.UI;

namespace Greyline.UI
{
    /// Applies the project's restrained charcoal/red state language to uGUI controls.
    public sealed class UIThemeStyle : MonoBehaviour
    {
        [SerializeField] private Graphic graphic;
        [SerializeField] private Selectable selectable;
        [SerializeField] private bool useAccentForSelected = true;

        private void Awake()
        {
            graphic ??= GetComponent<Graphic>();
            selectable ??= GetComponent<Selectable>();
            Apply(false);
        }

        public void Apply() => Apply(false);

        public void Apply(bool highContrast)
        {
            if (graphic != null) graphic.color = highContrast ? new Color32(45, 52, 57, 255) : ProductionUITokens.PanelRaised;
            if (selectable == null) return;
            ColorBlock colors = selectable.colors;
            colors.normalColor = highContrast ? new Color32(45, 52, 57, 255) : ProductionUITokens.PanelRaised;
            colors.highlightedColor = highContrast ? new Color32(110, 72, 72, 255) : new Color32(63, 48, 49, 255);
            colors.selectedColor = useAccentForSelected ? (highContrast ? new Color32(190, 90, 90, 255) : new Color32(106, 51, 53, 255)) : colors.normalColor;
            colors.pressedColor = ProductionUITokens.Accent;
            colors.disabledColor = new Color32(45, 49, 50, 255);
            colors.colorMultiplier = 1f;
            selectable.colors = colors;
        }
    }
}
