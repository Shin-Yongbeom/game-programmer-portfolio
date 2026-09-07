using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace Greyline.World.EditorTools
{
    /// <summary>
    /// Isolated sandbox for reusable city-scale greybox: boulevard, apartments, monument and courtyard.
    /// Candidate A campus content belongs only to WorldSandbox.
    /// </summary>
    public static class DistrictSandbox
    {
        public const string ScenePath = "Assets/_Project/Scenes/DistrictSandbox.unity";
        private const string GradeProfilePath = "Assets/_Project/Settings/WorldSandboxGradeProfile.asset";
        private const string SkyAndFogProfilePath = "Assets/Settings/SkyandFogSettingsProfile.asset";
        private static readonly string[] ManagedPrefixes = { "Boulevard", "Apartment_", "Monument_", "Courtyard_" };

        [MenuItem("Greyline/World/Build or Refresh District Sandbox")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Exit Play Mode before building the District Sandbox.");
                return;
            }

            BuildScene();
            AssetDatabase.SaveAssets();
            Validate();
        }

        [MenuItem("Greyline/World/Validate District Sandbox")]
        public static void Validate()
        {
            int errors = 0;
            errors += AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null ? Error("District Sandbox scene missing: " + ScenePath) : 0;

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !scene.isLoaded;
            if (openedHere && AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            if (scene.IsValid())
            {
                errors += RequireRoot(scene, "Ground");
                errors += RequireRoot(scene, "Key Light");
                errors += RequireRoot(scene, "Sky and Fog Volume");
                errors += RequireRoot(scene, "World Grade Volume");
                errors += RequireRoot(scene, "Massing");
                errors += RequireRoot(scene, "ScaleRef_1.8m");
                errors += FindRoot(scene, "Player") != null ? Error("District Sandbox must not contain a Player.") : 0;
            }

            if (openedHere && scene.IsValid())
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            string summary = $"District visual baseline validation complete. Errors: {errors}, Warnings: 0.";
            if (errors == 0) Debug.Log(summary); else Debug.LogError(summary);
        }

        public static void BuildFromCommandLine() => Build();
        public static void ValidateFromCommandLine() => Validate();

        private static void BuildScene()
        {
            Scene scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            UpdateGround(scene);
            UpdateKeyLight(scene);
            UpdateVolume(scene, "Sky and Fog Volume", SkyAndFogProfilePath, 0f);
            UpdateVolume(scene, "World Grade Volume", GradeProfilePath, 1f);
            UpdateMassing(scene);
            UpdateScaleReference(scene);
            UpdateCamera(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("District Sandbox scene refreshed at " + ScenePath);
        }

        private static void UpdateMassing(Scene scene)
        {
            GameObject root = GetOrCreateRoot(scene, "Massing");
            HashSet<string> built = new HashSet<string>();
            built.Add(Block(root.transform, "Boulevard", new Vector3(0f, 0.05f, 0f), new Vector3(60f, 0.1f, 240f)));

            for (int i = 0; i < 6; i++)
            {
                float z = -95f + i * 38f;
                built.Add(Block(root.transform, $"Apartment_L_{i}", new Vector3(-45f, 16f, z), new Vector3(14f, 32f, 22f)));
                built.Add(Block(root.transform, $"Apartment_R_{i}", new Vector3(45f, 16f, z), new Vector3(14f, 32f, 22f)));
            }

            built.Add(Block(root.transform, "Monument_Plinth", new Vector3(0f, 1.5f, 110f), new Vector3(24f, 3f, 24f)));
            built.Add(Block(root.transform, "Monument_Shaft", new Vector3(0f, 32f, 110f), new Vector3(4f, 60f, 4f)));
            built.Add(Block(root.transform, "Courtyard_Floor", new Vector3(-70f, 0.05f, 0f), new Vector3(40f, 0.1f, 40f)));
            PruneStaleManaged(root.transform, built);
        }

        private static string Block(Transform parent, string name, Vector3 position, Vector3 size)
        {
            Transform existing = parent.Find(name);
            GameObject block = existing != null ? existing.gameObject : null;
            if (block == null || block.GetComponent<MeshFilter>() == null)
            {
                if (block != null) Object.DestroyImmediate(block);
                block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = name;
                block.transform.SetParent(parent, false);
            }

            block.transform.localPosition = position;
            block.transform.localScale = size;
            block.isStatic = true;
            return name;
        }

        private static void PruneStaleManaged(Transform parent, HashSet<string> keep)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                if (keep.Contains(child.name)) continue;
                foreach (string prefix in ManagedPrefixes)
                {
                    if (child.name.StartsWith(prefix))
                    {
                        Object.DestroyImmediate(child);
                        break;
                    }
                }
            }
        }

        private static void UpdateGround(Scene scene)
        {
            GameObject ground = GetOrCreatePrimitiveRoot(scene, "Ground", PrimitiveType.Plane);
            ground.transform.localScale = new Vector3(30f, 1f, 30f);
            ground.isStatic = true;
        }

        private static void UpdateKeyLight(Scene scene)
        {
            GameObject sun = GetOrCreateRoot(scene, "Key Light");
            Light light = sun.GetComponent<Light>();
            if (light == null)
            {
                light = sun.AddComponent<Light>();
            }

            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.intensity = 1.3f;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(38f, 150f, 0f);
        }

        private static void UpdateVolume(Scene scene, string name, string profilePath, float priority)
        {
            GameObject go = GetOrCreateRoot(scene, name);
            Volume volume = go.GetComponent<Volume>() ?? go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = priority;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        }

        private static void UpdateScaleReference(Scene scene)
        {
            GameObject capsule = GetOrCreatePrimitiveRoot(scene, "ScaleRef_1.8m", PrimitiveType.Capsule);
            capsule.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
            capsule.transform.position = new Vector3(0f, 0.9f, 0f);
            Collider collider = capsule.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
        }

        private static void UpdateCamera(Scene scene)
        {
            GameObject cameraObject = FindRoot(scene, "Sandbox Camera");
            if (cameraObject == null)
            {
                cameraObject = new GameObject("Sandbox Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
            }

            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 3f, -140f);
            cameraObject.transform.rotation = Quaternion.Euler(4f, 0f, 0f);
        }

        private static GameObject GetOrCreateRoot(Scene scene, string name)
        {
            GameObject go = FindRoot(scene, name) ?? new GameObject(name);
            if (go.scene != scene) SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        private static GameObject GetOrCreatePrimitiveRoot(Scene scene, string name, PrimitiveType primitive)
        {
            GameObject go = FindRoot(scene, name);
            if (go == null || go.GetComponent<MeshFilter>() == null)
            {
                if (go != null) Object.DestroyImmediate(go);
                go = GameObject.CreatePrimitive(primitive);
                go.name = name;
            }

            if (go.scene != scene) SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
            }

            return null;
        }

        private static int RequireRoot(Scene scene, string name) => FindRoot(scene, name) == null ? Error($"District Sandbox is missing '{name}'.") : 0;
        private static int Error(string message) { Debug.LogError(message); return 1; }
    }
}
