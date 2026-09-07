using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Greyline.UI;

namespace Greyline.EditorTools
{
    public static class ProductionUISandboxBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/ProductionUISandbox.unity";
        private const string PrefabPath = "Assets/_Project/Prefabs/UI/ProductionUIRoot.prefab";

        [MenuItem("Greyline/UI/Build or Refresh Production UI Sandbox")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Exit Play Mode before building the Production UI Sandbox.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            eventSystem.GetComponent<InputSystemUIInputModule>().actionsAsset = inputActions;

            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            root.name = "ProductionUIRoot";
            var shell = root.GetComponent<ProductionUIShell>();
            shell.SetActionAsset(inputActions);
            var accessibility = root.GetComponent<UIAccessibilitySettings>();
            root.AddComponent<QuestLogArchivePresenter>();

            var qaCanvasObject = new GameObject("QA Overlay", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var qaCanvas = qaCanvasObject.GetComponent<Canvas>();
            qaCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            qaCanvas.sortingOrder = 200;
            var scaler = qaCanvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var healthObject = new GameObject("Health Edge QA", typeof(RectTransform), typeof(Image), typeof(HealthEdgePresentation));
            healthObject.transform.SetParent(qaCanvas.transform, false);
            var healthRect = healthObject.GetComponent<RectTransform>();
            healthRect.anchorMin = Vector2.zero; healthRect.anchorMax = Vector2.one; healthRect.offsetMin = Vector2.zero; healthRect.offsetMax = Vector2.zero;
            var healthImage = healthObject.GetComponent<Image>();
            healthImage.color = new Color(0.75f, 0.05f, 0.05f, 0f);
            healthImage.raycastTarget = false;
            var health = healthObject.GetComponent<HealthEdgePresentation>();
            health.Configure(healthImage);

            var statusObject = new GameObject("QA Status", typeof(RectTransform), typeof(Text));
            statusObject.transform.SetParent(qaCanvas.transform, false);
            var statusRect = statusObject.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0f, 0f); statusRect.anchorMax = new Vector2(0f, 0f); statusRect.pivot = new Vector2(0f, 0f);
            statusRect.anchoredPosition = new Vector2(32f, 28f); statusRect.sizeDelta = new Vector2(720f, 110f);
            var status = statusObject.GetComponent<Text>();
            status.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); status.fontSize = 20; status.color = Color.white;
            status.horizontalOverflow = HorizontalWrapMode.Wrap; status.verticalOverflow = VerticalWrapMode.Overflow;

            var controllerObject = new GameObject("Production UI QA Controller");
            var controller = controllerObject.AddComponent<ProductionUIQASandboxController>();
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("shell").objectReferenceValue = shell;
            serialized.FindProperty("accessibility").objectReferenceValue = accessibility;
            serialized.FindProperty("healthEdge").objectReferenceValue = health;
            serialized.FindProperty("status").objectReferenceValue = status;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = controllerObject;
            Debug.Log("Production UI Sandbox built at " + ScenePath + ". Press Play to start QA.");
        }

        [MenuItem("Greyline/UI/Open Production UI Sandbox")]
        public static void Open() => EditorSceneManager.OpenScene(ScenePath);
    }
}
