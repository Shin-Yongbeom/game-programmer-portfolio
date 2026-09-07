using Greyline.Minigames;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Greyline.Systems.EditorTools
{
    /// <summary>Explicit builder for an isolated sandbox; never edits Slice01, Player, or shared input.</summary>
    public static class MinigameASandbox
    {
        public const string ScenePath = "Assets/_Project/Scenes/MinigameASandbox.unity";
        private const string DataPath = "Assets/_Project/Data/Minigames";

        [MenuItem("Greyline/Systems/Build Minigame A Sandbox")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Exit Play Mode before building the Minigame A Sandbox."); return; }
            EnsureFolder("Assets/_Project", "Data");
            EnsureFolder("Assets/_Project/Data", "Minigames");
            EnsureFolder("Assets/_Project", "Scenes");
            NeolttwigiConfig neolttwigi = LoadOrCreate<NeolttwigiConfig>(DataPath + "/NeolttwigiConfig.asset");
            BalloonDartsConfig darts = LoadOrCreate<BalloonDartsConfig>(DataPath + "/BalloonDartsConfig.asset");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateLight();
            Camera camera = CreateCamera();
            CreateGround();

            GameObject neolttwigiStage = new GameObject("Neolttwigi Stage");
            neolttwigiStage.transform.position = new Vector3(-3f, .25f, 0f);
            GameObject seesaw = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seesaw.name = "Neolttwigi Mock Board";
            seesaw.transform.SetParent(neolttwigiStage.transform, false);
            seesaw.transform.localScale = new Vector3(3.6f, .18f, .85f);
            GameObject player = CreateMock("Neolttwigi Player Mock", Vector3.zero, PrimitiveType.Capsule);
            GameObject npc = CreateMock("Neolttwigi NPC Mock", Vector3.zero, PrimitiveType.Capsule);
            player.transform.SetParent(neolttwigiStage.transform, false);
            npc.transform.SetParent(neolttwigiStage.transform, false);
            player.transform.localScale = Vector3.one * .55f;
            npc.transform.localScale = Vector3.one * .55f;
            player.transform.localPosition = new Vector3(-1.05f, .67f, 0f);
            npc.transform.localPosition = new Vector3(1.05f, .67f, 0f);
            GameObject neolttwigiRoot = new GameObject("Neolttwigi Game");
            neolttwigiRoot.AddComponent<NeolttwigiGame>().Configure(neolttwigi, player.transform, npc.transform);

            GameObject dartRoot = new GameObject("Balloon Darts Game");
            BalloonDartTarget[] targets = new BalloonDartTarget[9];
            for (int i = 0; i < targets.Length; i++)
            {
                GameObject balloon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                balloon.name = "Balloon " + i;
                balloon.transform.SetParent(dartRoot.transform, true);
                balloon.transform.position = new Vector3(2.3f + (i % 3) * 1.15f, 1.1f + (i / 3) * 1.1f, 0f);
                balloon.transform.localScale = Vector3.one * .65f;
                BalloonDartTarget target = balloon.AddComponent<BalloonDartTarget>();
                target.Configure(i == 4 ? BalloonType.HighValue : (i == 8 ? BalloonType.Penalty : BalloonType.Normal));
                targets[i] = target;
            }
            dartRoot.AddComponent<BalloonDartsGame>().Configure(darts, camera, targets);
            dartRoot.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Minigame A sandbox written to " + ScenePath);
        }

        [MenuItem("Greyline/Systems/Validate Minigame A Sandbox")]
        public static void Validate()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) { Debug.LogError("Minigame A sandbox scene missing: " + ScenePath); return; }
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            int errors = 0;
            if (Find<NeolttwigiGame>(scene) == null) errors++;
            if (Find<BalloonDartsGame>(scene) == null) errors++;
            if (Find<Camera>(scene) == null) errors++;
            BalloonDartTarget[] targets = Object.FindObjectsByType<BalloonDartTarget>(FindObjectsSortMode.None);
            if (targets.Length < 3) errors++;
            foreach (BalloonDartTarget target in targets) if (target.GetComponent<Collider>() == null) errors++;
            BalloonDartsConfig darts = AssetDatabase.LoadAssetAtPath<BalloonDartsConfig>(DataPath + "/BalloonDartsConfig.asset");
            NeolttwigiConfig neolttwigi = AssetDatabase.LoadAssetAtPath<NeolttwigiConfig>(DataPath + "/NeolttwigiConfig.asset");
            string dartsError = darts == null ? "missing" : string.Empty;
            bool dartsValid = darts != null;
            if (dartsValid) dartsValid = darts.Validate(out dartsError);
            if (!dartsValid) { errors++; Debug.LogError("Balloon Darts config invalid: " + dartsError); }
            string neolttwigiError = neolttwigi == null ? "missing" : string.Empty;
            bool neolttwigiValid = neolttwigi != null;
            if (neolttwigiValid) neolttwigiValid = neolttwigi.Validate(out neolttwigiError);
            if (!neolttwigiValid) { errors++; Debug.LogError("Neolttwigi config invalid: " + neolttwigiError); }
            if (errors == 0) Debug.Log("Minigame A sandbox validation complete. Errors: 0, Warnings: 0.");
            else Debug.LogError("Minigame A sandbox validation complete. Errors: " + errors + ", Warnings: 0.");
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
        }

        public static void BuildFromCommandLine() => Build();
        public static void ValidateFromCommandLine() => Validate();

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static GameObject CreateMock(string name, Vector3 position, PrimitiveType type)
        {
            GameObject go = GameObject.CreatePrimitive(type); go.name = name; go.transform.position = position; return go;
        }

        private static Camera CreateCamera()
        {
            GameObject go = new GameObject("Main Camera", typeof(Camera));
            go.tag = "MainCamera";
            go.transform.SetPositionAndRotation(new Vector3(0f, 2.8f, -10f), Quaternion.Euler(8f, 0f, 0f));
            Camera camera = go.GetComponent<Camera>(); camera.fieldOfView = 45f; return camera;
        }

        private static void CreateLight()
        {
            GameObject go = new GameObject("Directional Light");
            Light light = go.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.2f;
            go.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        }

        private static void CreateGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane); ground.name = "Sandbox Ground"; ground.transform.localScale = Vector3.one * 2f;
        }

        private static T Find<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects()) { T found = root.GetComponentInChildren<T>(true); if (found != null) return found; }
            return null;
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
