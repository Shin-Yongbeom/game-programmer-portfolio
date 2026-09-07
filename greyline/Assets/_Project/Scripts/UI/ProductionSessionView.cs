using System.Collections.Generic;
using Greyline.Interaction;
using Greyline.Progression;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Greyline.UI
{
    /// <summary>Live production pause/training UI; consumes session commands and catalog content.</summary>
    public sealed class ProductionSessionView : MonoBehaviour
    {
        private ProductionSession session;
        private GameObject menu;
        private Text summary, notice, prompt, menuNotice;
        private Button resume;
        private readonly Dictionary<string, Button> skillButtons = new();
        private readonly Dictionary<string, Text> skillLabels = new();
        private Font font;
        private bool wasOpen;
        private RectTransform viewport, scrollContent;
        private GameObject lastSelected;
        public bool IsMenuVisible => menu != null && menu.activeSelf;
        public int SkillButtonCount => skillButtons.Count;
        public Button ResumeButton => resume;
        public Button SkillButton(string id) => skillButtons.TryGetValue(id, out var button) ? button : null;
        private void Start()
        {
            session = GetComponent<ProductionSession>();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (EventSystem.current == null)
            {
                var events = new GameObject("Production UI Input", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            var canvasObject = new GameObject("Production Session Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            notice = Label(canvas.transform, "Session Status", 16, new Vector2(20, -16), new Vector2(710, 34));
            notice.rectTransform.anchorMin = notice.rectTransform.anchorMax = new Vector2(0, 1);
            notice.rectTransform.pivot = new Vector2(0, 1);
            prompt = Label(canvas.transform, "Interaction", 20, new Vector2(0, -126), new Vector2(800, 38));
            menu = new GameObject("Pause and Training", typeof(RectTransform), typeof(Image)); menu.transform.SetParent(canvas.transform, false);
            Stretch(menu.GetComponent<RectTransform>()); menu.GetComponent<Image>().color = new Color(.025f, .045f, .055f, .97f);
            Label(menu.transform, "Header", 30, new Vector2(0, 277), new Vector2(920, 48)).text = "FIELD TRAINING";
            summary = Label(menu.transform, "Training Balance", 19, new Vector2(0, 232), new Vector2(920, 48));
            var scroll = new GameObject("Training List", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            scroll.transform.SetParent(menu.transform, false);
            viewport = scroll.GetComponent<RectTransform>(); viewport.anchoredPosition = new Vector2(0, 5); viewport.sizeDelta = new Vector2(940, 380);
            scroll.GetComponent<Image>().color = new Color(0, 0, 0, .08f);
            var content = new GameObject("Skills", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)); content.transform.SetParent(scroll.transform, false);
            scrollContent = content.GetComponent<RectTransform>(); scrollContent.anchorMin = new Vector2(0, 1); scrollContent.anchorMax = Vector2.one;
            scrollContent.pivot = new Vector2(.5f, 1); scrollContent.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>(); layout.spacing = 8; layout.padding = new RectOffset(10, 10, 8, 8);
            layout.childControlWidth = true; layout.childForceExpandWidth = true; layout.childControlHeight = false; layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scrolling = scroll.GetComponent<ScrollRect>(); scrolling.content = scrollContent; scrolling.viewport = viewport;
            scrolling.horizontal = false; scrolling.movementType = ScrollRect.MovementType.Clamped; scrolling.scrollSensitivity = 25;
            for (int i = 0; i < session.Catalog.skills.Length; i++)
            {
                var skill = session.Catalog.skills[i]; string id = skill.id;
                var button = Button(content.transform, skill.title, Vector2.zero, new Vector2(920, 67), () => session.TryBuy(id));
                skillButtons[id] = button; skillLabels[id] = button.GetComponentInChildren<Text>();
            }
            menuNotice = Label(menu.transform, "Operation Status", 16, new Vector2(0, -199), new Vector2(920, 22));
            resume = Button(menu.transform, "RESUME", new Vector2(-242, -235), new Vector2(450, 48), () => session.SetMenuOpen(false));
            Button(menu.transform, "RETURN TO CHECKPOINT", new Vector2(242, -235), new Vector2(450, 48), session.RestartCheckpoint);
            Label(menu.transform, "Help", 17, new Vector2(0, -295), new Vector2(960, 52)).text =
                "Clear combat pockets to earn training. Upgrades are permanent.\nRest at a field station to recover health and save your return point.  Esc / Start: resume";
            menu.SetActive(false);
            session.Changed += Refresh;
            Refresh();
        }
        private void Update()
        {
            if (session == null) return;
            if (session.MenuOpen && Gamepad.current?.buttonEast.wasPressedThisFrame == true) session.SetMenuOpen(false);
            var interaction = session.Player.GetComponent<DistrictInteractionDriver>();
            prompt.text = !session.MenuOpen ? interaction?.CurrentPrompt ?? "" : "";
        }
        private void LateUpdate()
        {
            var selected = EventSystem.current?.currentSelectedGameObject;
            if (selected == lastSelected) return;
            lastSelected = selected;
            if (selected == null || scrollContent == null || !selected.transform.IsChildOf(scrollContent)) return;
            Canvas.ForceUpdateCanvases();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, selected.transform);
            float shift = bounds.min.y < viewport.rect.yMin ? viewport.rect.yMin - bounds.min.y :
                bounds.max.y > viewport.rect.yMax ? viewport.rect.yMax - bounds.max.y : 0;
            scrollContent.anchoredPosition += Vector2.up * shift;
        }
        private void Refresh()
        {
            if (menu == null) return;
            notice.text = $"TRAINING {session.Points}   /   {(session.Dirty ? "UNSAVED  /  " : "")}{session.Message}";
            menuNotice.text = (session.Dirty ? "UNSAVED  /  " : "") + session.Message;
            summary.text = $"{session.Points} POINTS   ·   RETURN: {session.CheckpointId.ToUpperInvariant()}" +
                (!session.CanRest() ? "   ·   TRAINING UNAVAILABLE DURING COMBAT" : "");
            foreach (var skill in session.Catalog.skills)
            {
                string state = session.HasSkill(skill.id) ? "TRAINED" : $"{skill.cost} PT";
                string required = string.IsNullOrEmpty(skill.prerequisite) ? "" : "  ·  Requires " + session.Catalog.FindSkill(skill.prerequisite)?.title;
                skillLabels[skill.id].text = $"{skill.title}   [{state}]\n{skill.description}{required}";
                skillButtons[skill.id].interactable = session.CanBuy(skill.id);
            }
            menu.SetActive(session.MenuOpen);
            if (session.MenuOpen && !wasOpen) EventSystem.current?.SetSelectedGameObject(resume.gameObject);
            if (!session.MenuOpen && wasOpen) EventSystem.current?.SetSelectedGameObject(null);
            wasOpen = session.MenuOpen;
        }
        private Text Label(Transform parent, string name, int size, Vector2 position, Vector2 dimensions)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Text)); obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = dimensions;
            var text = obj.GetComponent<Text>(); text.font = font; text.fontSize = size; text.color = ProductionUITokens.Text;
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false; text.supportRichText = false;
            return text;
        }
        private Button Button(Transform parent, string name, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>(); rect.anchoredPosition = position; rect.sizeDelta = size;
            var image = obj.GetComponent<Image>(); image.color = new Color(.15f, .22f, .23f);
            var button = obj.GetComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            var colors = button.colors; colors.highlightedColor = new Color(1, .83f, .45f); colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(.35f, .4f, .4f); button.colors = colors;
            var label = Label(obj.transform, "Label", 18, Vector2.zero, size - new Vector2(24, 4)); label.text = name;
            return button;
        }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private void OnDestroy() { if (session != null) session.Changed -= Refresh; }
    }
}
