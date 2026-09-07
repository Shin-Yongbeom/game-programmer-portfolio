using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Greyline.UI
{
    /// Interactive QA harness for the production UI shell. It is intentionally scene-local.
    public sealed class ProductionUIQASandboxController : MonoBehaviour
    {
        [SerializeField] private ProductionUIShell shell;
        [SerializeField] private UIAccessibilitySettings accessibility;
        [SerializeField] private HealthEdgePresentation healthEdge;
        [SerializeField] private Text status;
        private readonly float[] healthStates = { 1f, .70f, .40f, .15f };
        private int healthIndex;

        private void Awake()
        {
            shell = shell != null ? shell : FindObjectOfType<ProductionUIShell>();
            accessibility = accessibility != null ? accessibility : FindObjectOfType<UIAccessibilitySettings>();
            healthEdge = healthEdge != null ? healthEdge : FindObjectOfType<HealthEdgePresentation>();
            SetHealth(0);
            SetStatus("QA ONLY: H Health · C Contrast · M Motion · [ ] Intensity");
            Debug.Log("[ProductionUI QA] QA-only diagnostics ready. Production navigation is handled by the UI action map.", this);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || shell == null) return;
            if (keyboard.hKey.wasPressedThisFrame) SetHealth((healthIndex + 1) % healthStates.Length);
            if (keyboard.cKey.wasPressedThisFrame && accessibility != null) { accessibility.SetHighContrast(!accessibility.HighContrast); LogAction("C -> High Contrast " + (accessibility.HighContrast ? "ON" : "OFF")); }
            if (keyboard.mKey.wasPressedThisFrame && accessibility != null) { accessibility.SetReduceMotion(!accessibility.ReduceMotion); LogAction("M -> Reduce Motion " + (accessibility.ReduceMotion ? "ON" : "OFF")); }
            if (keyboard.leftBracketKey.wasPressedThisFrame && accessibility != null) { accessibility.SetHealthEdgeIntensity(accessibility.HealthEdgeIntensity - .25f); LogAction("[ -> Health Edge intensity " + accessibility.HealthEdgeIntensity.ToString("0.00")); }
            if (keyboard.rightBracketKey.wasPressedThisFrame && accessibility != null) { accessibility.SetHealthEdgeIntensity(accessibility.HealthEdgeIntensity + .25f); LogAction("] -> Health Edge intensity " + accessibility.HealthEdgeIntensity.ToString("0.00")); }
            UpdateStatus();
        }

        private void SetHealth(int index)
        {
            healthIndex = Mathf.Clamp(index, 0, healthStates.Length - 1);
            healthEdge?.SetNormalizedHealth(healthStates[healthIndex]);
            LogAction("H -> Health " + healthStates[healthIndex].ToString("0%"));
            UpdateStatus();
        }

        private void LogAction(string action)
        {
            string selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
                ? EventSystem.current.currentSelectedGameObject.name
                : "<none>";
            Debug.Log($"[ProductionUI QA] {action} | selected={selected}", this);
        }

        private void UpdateStatus()
        {
            if (status == null) return;
            string health = healthStates[healthIndex].ToString("0%") + " HP";
            string contrast = accessibility != null && accessibility.HighContrast ? "HC ON" : "HC OFF";
            string motion = accessibility != null && accessibility.ReduceMotion ? "RM ON" : "RM OFF";
            status.text = $"QA ONLY DIAGNOSTICS\nH Health: {health} · C Contrast: {contrast} · M Motion: {motion}\nProduction UI navigation uses the UI action map.";
        }

        private void SetStatus(string value)
        {
            if (status != null) status.text = value;
        }
    }
}
