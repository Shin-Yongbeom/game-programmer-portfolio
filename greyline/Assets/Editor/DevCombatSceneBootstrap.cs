using Greyline.CameraSystem;
using Greyline.Core;
using Greyline.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Greyline.EditorTools
{
    [InitializeOnLoad]
    public static class DevCombatSceneBootstrap
    {
        private const string ScenePath = "Assets/_Project/Scenes/DevCombat.unity";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string VolumeProfilePath = "Assets/_Project/Settings/DevCombatVolumeProfile.asset";

        static DevCombatSceneBootstrap()
        {
            EditorApplication.delayCall += CreateIfMissing;
            EditorApplication.delayCall += UpgradeToU2WhenEditable;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Greyline/Create Gate U1 Dev Combat Scene")]
        public static void CreateIfMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                return;
            }

            EnsureFolders();
            CreateScene();
        }

        [MenuItem("Greyline/Upgrade Gate U1 Scene to U2 Character Foundation")]
        public static void UpgradeToU2IfNeeded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.Log("Gate U2 scene upgrade will run after returning to Edit Mode.");
                return;
            }

            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (sceneAsset == null)
            {
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedForUpgrade = !scene.isLoaded;
            if (openedForUpgrade)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            GameObject player = FindRootObject(scene, "Player");
            if (player != null)
            {
                UpgradePlayer(player);
                EditorSceneManager.SaveScene(scene);
            }

            if (openedForUpgrade)
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            CharacterCombatValidator.ValidateAll();
        }

        private static void UpgradeToU2WhenEditable()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                UpgradeToU2IfNeeded();
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.delayCall += UpgradeToU2IfNeeded;
            }
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "_Project");
            string[] folders =
            {
                "Data", "Prefabs", "Scenes", "Scripts", "Settings", "Tests",
                "Scripts/Core", "Scripts/Player", "Scripts/Combat", "Scripts/Enemies", "Scripts/Camera"
            };

            foreach (string folder in folders)
            {
                string parent = "Assets/_Project";
                string[] parts = folder.Split('/');
                for (int i = 0; i < parts.Length; i++)
                {
                    EnsureFolder(parent, parts[i]);
                    parent += "/" + parts[i];
                }
            }

            EnsureFolder("Assets", "Editor");
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static void CreateScene()
        {
            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (inputActions == null)
            {
                Debug.LogError("Gate U1 scene was not created: InputSystem_Actions is missing.");
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateGround();
            CreateSun();
            CreateVolume();

            GameObject player = CreatePlayer(inputActions);
            Camera followCamera = CreateCamera(player.transform, inputActions);
            player.GetComponent<ThirdPersonPlayerMotor>().Configure(followCamera, inputActions);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Gate U1 scene created at " + ScenePath);
        }

        private static void CreateGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(5f, 1f, 5f);
        }

        private static void CreateSun()
        {
            GameObject sun = new GameObject("Directional Light");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void CreateVolume()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            }

            GameObject volumeObject = new GameObject("HDRP Global Volume");
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        private static GameObject CreatePlayer(InputActionAsset inputActions)
        {
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.tag = "Player";
            // Gameplay root sits at ground level; the controller center owns its height.
            player.transform.position = Vector3.zero;
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());

            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = new Vector3(0f, 1f, 0f);

            ThirdPersonPlayerMotor motor = player.AddComponent<ThirdPersonPlayerMotor>();
            motor.Configure(null, inputActions);
            UpgradePlayer(player);
            return player;
        }

        private static void UpgradePlayer(GameObject player)
        {
            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            Transform visualRoot = player.transform.Find("VisualRoot");
            if (visualRoot == null)
            {
                GameObject visualRootObject = new GameObject("VisualRoot");
                visualRootObject.transform.SetParent(player.transform, false);
                visualRoot = visualRootObject.transform;
            }

            Transform characterVisual = visualRoot.Find("CharacterVisual");
            if (characterVisual == null)
            {
                MeshFilter meshFilter = player.GetComponent<MeshFilter>();
                MeshRenderer meshRenderer = player.GetComponent<MeshRenderer>();
                GameObject visual = new GameObject("CharacterVisual", typeof(MeshFilter), typeof(MeshRenderer));
                visual.transform.SetParent(visualRoot, false);
                visual.GetComponent<MeshFilter>().sharedMesh = meshFilter != null ? meshFilter.sharedMesh : null;
                visual.GetComponent<MeshRenderer>().sharedMaterials = meshRenderer != null ? meshRenderer.sharedMaterials : null;
                Object.DestroyImmediate(meshFilter);
                Object.DestroyImmediate(meshRenderer);
            }

            CharacterPresentation presentation = player.GetComponent<CharacterPresentation>();
            if (presentation == null)
            {
                presentation = player.AddComponent<CharacterPresentation>();
            }
            presentation.Configure(visualRoot, presentation.Definition);

            CharacterAnimationDriver animationDriver = player.GetComponent<CharacterAnimationDriver>();
            if (animationDriver == null)
            {
                animationDriver = player.AddComponent<CharacterAnimationDriver>();
            }

            PlayerCombat playerCombat = player.GetComponent<PlayerCombat>();
            if (playerCombat == null)
            {
                playerCombat = player.AddComponent<PlayerCombat>();
            }
            playerCombat.Configure(inputActions, animationDriver);
        }

        private static GameObject FindRootObject(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == objectName)
                {
                    return root;
                }
            }

            return null;
        }

        private static Camera CreateCamera(Transform player, InputActionAsset inputActions)
        {
            GameObject cameraObject = new GameObject("Follow Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            Camera followCamera = cameraObject.GetComponent<Camera>();
            ThirdPersonOrbitCamera orbitCamera = cameraObject.AddComponent<ThirdPersonOrbitCamera>();
            orbitCamera.Configure(player, inputActions);
            return followCamera;
        }
    }
}
