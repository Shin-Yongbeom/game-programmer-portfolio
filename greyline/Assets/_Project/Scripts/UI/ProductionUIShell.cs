using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Greyline.UI
{
    /// <summary>Sandbox-local UI navigation. Gameplay pause and scene flow remain integration-owned.</summary>
    public sealed class ProductionUIShell : MonoBehaviour
    {
        private enum Screen { None, Pause, Archive, Settings }
        private enum ArchiveTab { Story, People, Techniques, Collection }

        [SerializeField] private Canvas canvas;
        [SerializeField] private InputActionAsset uiActions;
        [SerializeField, Range(.75f, 1.5f)] private float uiScale = 1f;
        [SerializeField] private bool highContrast;
        [SerializeField] private bool reduceMotion;
        private readonly Stack<Screen> backStack = new();
        private readonly Dictionary<Screen, GameObject> focusMemory = new();
        private readonly List<Graphic> themedGraphics = new();
        private GameObject pausePanel, archivePanel, settingsPanel, modalPanel, archiveBody;
        private Text settingsSummary, modalBody;
        private Button storyTab, peopleTab, techniquesTab, collectionTab;
        private Screen currentScreen;
        private ArchiveTab currentTab;
        private InputAction cancelAction;
        private Coroutine transition;
        private StoryArchiveSnapshot storySnapshot = StoryArchiveSnapshot.Loading();

        public event Action MainMenuRequested;
        public bool IsVisible => currentScreen != Screen.None || (modalPanel != null && modalPanel.activeSelf);
        public float UIScale => uiScale;
        public bool HighContrast => highContrast;
        public bool ReduceMotion => reduceMotion;

        private void Awake() => EnsureBuilt();
        private void OnEnable() { EnsureBuilt(); BindCancel(); }
        private void OnDisable() { if (cancelAction != null) cancelAction.performed -= OnCancel; cancelAction = null; }

        public void SetActionAsset(InputActionAsset actions)
        {
            if (uiActions == actions) return;
            if (cancelAction != null) cancelAction.performed -= OnCancel;
            uiActions = actions; cancelAction = null;
            if (isActiveAndEnabled) BindCancel();
        }

        public void ShowPause() => NavigateTo(Screen.Pause, false);
        public void ShowJournal() => NavigateTo(Screen.Archive, currentScreen != Screen.None);
        public void ShowSettings() => NavigateTo(Screen.Settings, currentScreen != Screen.None);
        public void HideAll()
        {
            backStack.Clear(); SetScreen(Screen.None); HideModal();
            EventSystem.current?.SetSelectedGameObject(null);
        }
        public void SetUIScale(float value) { uiScale = Mathf.Clamp(value, .75f, 1.5f); ApplyAppearance(); }
        public void SetHighContrast(bool value) { highContrast = value; ApplyAppearance(); }
        public void SetReduceMotion(bool value) { reduceMotion = value; ApplyAppearance(); }
        public void PresentStory(StoryArchiveSnapshot snapshot)
        {
            storySnapshot = snapshot ?? StoryArchiveSnapshot.Empty();
            if (currentScreen == Screen.Archive && currentTab == ArchiveTab.Story) RenderArchiveBody();
        }

        private void BindCancel()
        {
            if (uiActions == null)
            {
                InputSystemUIInputModule module = FindFirstObjectByType<InputSystemUIInputModule>();
                uiActions = module != null ? module.actionsAsset : null;
            }
            cancelAction = uiActions != null ? uiActions.FindAction("UI/Cancel", false) : null;
            if (cancelAction != null) cancelAction.performed += OnCancel;
            else Debug.LogWarning("[ProductionUI] UI/Cancel action was not found; back navigation is unavailable.", this);
        }
        private void OnCancel(InputAction.CallbackContext _) => RequestBack();
        public void RequestBack()
        {
            if (modalPanel != null && modalPanel.activeSelf) { HideModal(); return; }
            if (currentScreen == Screen.None) { ShowPause(); return; }
            RememberFocus(currentScreen);
            if (backStack.Count > 0) SetScreen(backStack.Pop()); else SetScreen(Screen.None);
        }
        private void NavigateTo(Screen next, bool pushCurrent)
        {
            if (currentScreen == next) { RestoreFocus(next); return; }
            if (pushCurrent && currentScreen != Screen.None) { RememberFocus(currentScreen); backStack.Push(currentScreen); }
            SetScreen(next);
        }
        private void SetScreen(Screen next)
        {
            currentScreen = next;
            pausePanel.SetActive(next == Screen.Pause); archivePanel.SetActive(next == Screen.Archive); settingsPanel.SetActive(next == Screen.Settings);
            if (next == Screen.Archive) RenderArchiveBody();
            if (next == Screen.Settings) RefreshSettingsSummary();
            if (next != Screen.None) StartTransition(GetPanel(next));
            RestoreFocus(next);
        }
        private GameObject GetPanel(Screen screen) => screen == Screen.Pause ? pausePanel : screen == Screen.Archive ? archivePanel : settingsPanel;
        private void RememberFocus(Screen screen)
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null && selected.activeInHierarchy) focusMemory[screen] = selected;
        }
        private void RestoreFocus(Screen screen)
        {
            if (screen == Screen.None) return;
            if (focusMemory.TryGetValue(screen, out GameObject remembered) && remembered != null && remembered.activeInHierarchy) { EventSystem.current?.SetSelectedGameObject(remembered); return; }
            Selectable first = GetPanel(screen).GetComponentInChildren<Selectable>(true);
            if (first != null) EventSystem.current?.SetSelectedGameObject(first.gameObject);
        }

        private void EnsureBuilt()
        {
            if (pausePanel != null) return;
            canvas = canvas != null ? canvas : GetComponentInChildren<Canvas>(true);
            if (canvas == null) canvas = CreateCanvas();
            pausePanel = CreatePanel("Pause", canvas.transform); archivePanel = CreatePanel("Archive", canvas.transform);
            settingsPanel = CreatePanel("Settings", canvas.transform); modalPanel = CreatePanel("Modal", canvas.transform);
            BuildPause(); BuildArchive(); BuildSettings(); BuildModal(); HideAll(); ApplyAppearance();
        }
        private void BuildPause()
        {
            Transform content = CreateContent(pausePanel.transform, 560f);
            AddLabel(content, "PAUSED", 34, TextAnchor.MiddleCenter, 72f);
            AddButton(content, "RESUME", HideAll); AddButton(content, "ARCHIVE", ShowJournal); AddButton(content, "SETTINGS", ShowSettings);
            AddButton(content, "RETURN TO TITLE", ShowReturnToTitleBoundary);
        }
        private void BuildArchive()
        {
            Transform content = CreateContent(archivePanel.transform, 1120f); AddLabel(content, "ARCHIVE", 30, TextAnchor.MiddleLeft, 64f);
            GameObject tabs = new("Tabs"); tabs.transform.SetParent(content, false);
            HorizontalLayoutGroup layout = tabs.AddComponent<HorizontalLayoutGroup>(); layout.spacing = 8f; layout.childForceExpandWidth = true;
            storyTab = AddButton(tabs.transform, "STORY", () => SelectTab(ArchiveTab.Story));
            peopleTab = AddButton(tabs.transform, "PEOPLE", () => SelectTab(ArchiveTab.People));
            techniquesTab = AddButton(tabs.transform, "TECHNIQUES", () => SelectTab(ArchiveTab.Techniques));
            collectionTab = AddButton(tabs.transform, "COLLECTION", () => SelectTab(ArchiveTab.Collection));
            archiveBody = new GameObject("Archive Body"); archiveBody.transform.SetParent(content, false);
            LayoutElement element = archiveBody.AddComponent<LayoutElement>(); element.minHeight = 500f; element.preferredHeight = 500f; currentTab = ArchiveTab.Story;
        }
        private void BuildSettings()
        {
            Transform content = CreateContent(settingsPanel.transform, 720f); AddLabel(content, "SETTINGS", 30, TextAnchor.MiddleLeft, 64f);
            settingsSummary = AddLabel(content, string.Empty, 18, TextAnchor.UpperLeft, 100f);
            AddButton(content, "UI SCALE  −", () => SetUIScale(uiScale - .125f)); AddButton(content, "UI SCALE  +", () => SetUIScale(uiScale + .125f));
            AddButton(content, "HIGH CONTRAST", () => SetHighContrast(!highContrast)); AddButton(content, "REDUCE MOTION", () => SetReduceMotion(!reduceMotion));
            AddBoundaryLabel(content, "Display and audio are owned by platform and audio services; this sandbox does not change device settings.");
            AddBoundaryLabel(content, "Subtitles and input remapping require their Systems-owned runtime contracts before their views can bind.");
        }
        private void BuildModal()
        {
            Transform content = CreateContent(modalPanel.transform, 600f); AddLabel(content, "RETURN TO TITLE", 28, TextAnchor.MiddleCenter, 70f);
            modalBody = AddLabel(content, string.Empty, 18, TextAnchor.MiddleCenter, 110f); AddButton(content, "ACKNOWLEDGE", HideModal);
        }
        private void ShowReturnToTitleBoundary()
        {
            modalBody.text = "Scene transition is owned by game-flow integration. This sandbox will not load or discard a game session.";
            modalPanel.SetActive(true); StartTransition(modalPanel); Selectable first = modalPanel.GetComponentInChildren<Selectable>(true);
            if (first != null) EventSystem.current?.SetSelectedGameObject(first.gameObject); MainMenuRequested?.Invoke();
        }
        private void HideModal() { if (modalPanel == null) return; modalPanel.SetActive(false); RestoreFocus(currentScreen); }
        private void SelectTab(ArchiveTab tab)
        {
            currentTab = tab; RenderArchiveBody();
            Button focused = tab == ArchiveTab.Story ? storyTab : tab == ArchiveTab.People ? peopleTab : tab == ArchiveTab.Techniques ? techniquesTab : collectionTab;
            EventSystem.current?.SetSelectedGameObject(focused.gameObject);
        }
        private void RenderArchiveBody()
        {
            if (archiveBody == null) return;
            for (int i = archiveBody.transform.childCount - 1; i >= 0; i--) Destroy(archiveBody.transform.GetChild(i).gameObject);
            if (currentTab == ArchiveTab.Story) RenderStory(archiveBody.transform);
            else if (currentTab == ArchiveTab.People) RenderArchiveState(archiveBody.transform, "PEOPLE", "No person records have been discovered yet.");
            else if (currentTab == ArchiveTab.Techniques) RenderArchiveState(archiveBody.transform, "TECHNIQUES", "Technique records are locked until combat progression is available.");
            else RenderArchiveState(archiveBody.transform, "COLLECTION", "No collection records have been discovered yet.");
        }
        private void RenderStory(Transform parent)
        {
            if (storySnapshot.State == StoryArchiveState.Loading) { RenderArchiveState(parent, "STORY", "Loading current story record…"); return; }
            if (storySnapshot.State == StoryArchiveState.Empty) { RenderArchiveState(parent, "STORY", "No active story record is available."); return; }
            string recent = string.IsNullOrWhiteSpace(storySnapshot.RecentEvent) ? "No recent event has been recorded." : storySnapshot.RecentEvent;
            AddLabel(parent, "STORY", 22, TextAnchor.UpperLeft, 40f);
            AddLabel(parent, $"CURRENT QUEST\n{storySnapshot.CurrentQuestName}\n\nCURRENT OBJECTIVE\n{storySnapshot.CurrentObjective}\n\nRECENT EVENT\n{recent}", 20, TextAnchor.UpperLeft, 420f);
        }
        private void RenderArchiveState(Transform parent, string title, string message)
        {
            AddLabel(parent, title, 22, TextAnchor.UpperLeft, 40f);
            AddLabel(parent, message, 20, TextAnchor.UpperLeft, 160f);
        }
        private void RefreshSettingsSummary() { if (settingsSummary != null) settingsSummary.text = $"UI Scale: {uiScale:0%}\nHigh contrast: {(highContrast ? "On" : "Off")}\nReduce motion: {(reduceMotion ? "On" : "Off")}"; }
        private void ApplyAppearance()
        {
            if (canvas != null) canvas.transform.localScale = Vector3.one * uiScale;
            Color panel = highContrast ? new Color32(8, 12, 16, 245) : ProductionUITokens.Base;
            Color text = highContrast ? Color.white : ProductionUITokens.Text;
            foreach (Graphic graphic in themedGraphics) if (graphic != null) graphic.color = graphic is Image ? panel : text;
            foreach (Selectable selectable in canvas.GetComponentsInChildren<Selectable>(true))
            {
                ColorBlock colors = selectable.colors;
                colors.normalColor = highContrast ? new Color32(20, 28, 34, 255) : ProductionUITokens.PanelRaised;
                colors.highlightedColor = highContrast ? new Color32(80, 105, 120, 255) : new Color32(90, 65, 65, 255);
                colors.selectedColor = highContrast ? Color.yellow : ProductionUITokens.Accent;
                colors.pressedColor = Color.white;
                colors.colorMultiplier = 1f;
                selectable.colors = colors;
            }
            RefreshSettingsSummary();
        }
        private void StartTransition(GameObject panel) { if (transition != null) StopCoroutine(transition); transition = StartCoroutine(FadeIn(panel.GetComponent<CanvasGroup>())); }
        private IEnumerator FadeIn(CanvasGroup group)
        {
            if (group == null) yield break; if (reduceMotion) { group.alpha = 1f; yield break; } group.alpha = 0f;
            for (float elapsed = 0f; elapsed < ProductionUITokens.FadeSeconds; elapsed += Time.unscaledDeltaTime) { group.alpha = Mathf.Clamp01(elapsed / ProductionUITokens.FadeSeconds); yield return null; } group.alpha = 1f;
        }
        private static Canvas CreateCanvas()
        {
            GameObject go = new("Production UI Canvas"); Canvas c = go.AddComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = go.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = .5f; go.AddComponent<GraphicRaycaster>(); return c;
        }
        private GameObject CreatePanel(string name, Transform parent)
        {
            GameObject go = new(name); go.transform.SetParent(parent, false); RectTransform rect = go.AddComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            Image image = go.AddComponent<Image>(); themedGraphics.Add(image); go.AddComponent<CanvasGroup>(); return go;
        }
        private static Transform CreateContent(Transform parent, float width)
        {
            GameObject go = new("Content"); go.transform.SetParent(parent, false); RectTransform rect = go.AddComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(width, 0f);
            VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>(); layout.spacing = 14f; layout.padding = new RectOffset(44, 44, 40, 40); layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = go.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize; return go.transform;
        }
        private Text AddLabel(Transform parent, string value, int size, TextAnchor alignment, float height)
        {
            Text text = AddLabelStatic(parent, value, size, height); text.alignment = alignment; themedGraphics.Add(text); return text;
        }
        private static Text AddLabelStatic(Transform parent, string value, int size, float height)
        {
            GameObject go = new("Text"); go.transform.SetParent(parent, false); Text text = go.AddComponent<Text>(); text.text = value; text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = size; text.alignment = TextAnchor.UpperLeft; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
            LayoutElement element = go.AddComponent<LayoutElement>(); element.minHeight = height; element.preferredHeight = height; return text;
        }
        private void AddBoundaryLabel(Transform parent, string value) => AddLabel(parent, value, 15, TextAnchor.UpperLeft, 58f);
        private Button AddButton(Transform parent, string label, Action action)
        {
            GameObject go = new(label); go.transform.SetParent(parent, false); Image image = go.AddComponent<Image>(); image.color = ProductionUITokens.PanelRaised; Button button = go.AddComponent<Button>(); button.targetGraphic = image;
            ColorBlock colors = button.colors; colors.normalColor = ProductionUITokens.PanelRaised; colors.highlightedColor = new Color32(90, 65, 65, 255); colors.selectedColor = ProductionUITokens.Accent; colors.pressedColor = Color.white; colors.colorMultiplier = 1f; button.colors = colors; button.onClick.AddListener(() => action?.Invoke()); AddLabel(go.transform, label, 20, TextAnchor.MiddleCenter, 56f);
            LayoutElement element = go.AddComponent<LayoutElement>(); element.minHeight = 56f; element.preferredHeight = 56f; return button;
        }
    }
}
