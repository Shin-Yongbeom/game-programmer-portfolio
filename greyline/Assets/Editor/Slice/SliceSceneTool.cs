using System;
using System.Linq;
using Greyline.CameraSystem;
using Greyline.Combat;
using Greyline.Core;
using Greyline.Dialogue;
using Greyline.Enemies;
using Greyline.Interaction;
using Greyline.Player;
using Greyline.Slice;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Greyline.EditorTools
{
    /// <summary>Small, explicit assembler for the first integrated gameplay slice.</summary>
    public static class SliceSceneTool
    {
        public const string ScenePath = "Assets/_Project/Scenes/Slice01.unity";
        private const string WorldScenePath = "Assets/_Project/Scenes/WorldSandbox.unity";
        private const string CombatScenePath = "Assets/_Project/Scenes/DevCombat.unity";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string CharacterDefinitionPath = "Assets/_Project/Data/Characters/YBotCharacterDefinition.asset";
        private const string SliceEnemyControllerPath = "Assets/_Project/Animators/SliceEnemyPresentation.controller";
        private const string StudentDialoguePath = "Assets/_Project/Data/Dialogue/Slice01StudentDialogue.asset";

        [MenuItem("Greyline/Slice/Build or Refresh Slice01")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Exit Play Mode before building Slice01.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                // Slice01 is already a checked-in product of this assembler. The assembly copies
                // its roots with Object.Instantiate / CreatePrimitive, so re-running it mints a
                // fresh fileID for every object and reshuffles the whole scene for no functional
                // gain. A repeat build is therefore a no-op that only re-validates. To rebuild
                // from changed sources, delete Assets/_Project/Scenes/Slice01.unity first — an
                // explicit, reviewable scene change.
                Debug.Log($"Slice01: preserved existing scene for idempotent build: {ScenePath}. Delete it to force a rebuild.");
                Validate();
                return;
            }

            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (inputActions == null)
            {
                Debug.LogError("Slice01 requires the shared InputSystem_Actions asset.");
                return;
            }

            Scene slice = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                EditorSceneManager.SaveScene(slice, ScenePath);
            }

            Scene world = OpenSource(WorldScenePath);
            Scene combat = OpenSource(CombatScenePath);

            try
            {
                ClearManagedRoots(slice);
                CopyCampus(world, slice);
                GameObject player = CopyPlayer(combat, slice, inputActions);
                BuildFlagsHost(slice);
                BuildInteraction(player, slice, inputActions);
                CombatHealth enemyHealth = BuildEnemy(slice, player.transform);
                BuildEncounterGate(slice, player, enemyHealth.GetComponent<EnemyAttack>());
                Transform camera = slice.GetRootGameObjects().First(root => root.name == "Slice01_Player").transform.Find("Follow Camera");
                Transform playerVisual = player.GetComponentInChildren<Animator>()?.transform;
                BuildCombatFeedback(slice, player, enemyHealth, camera, playerVisual);
                BuildProgressPresentation(slice, player, enemyHealth.transform);
                EditorSceneManager.MarkSceneDirty(slice);
                EditorSceneManager.SaveScene(slice, ScenePath);
            }
            finally
            {
                EditorSceneManager.CloseScene(world, true);
                EditorSceneManager.CloseScene(combat, true);
            }

            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("Slice01 assembled: campus -> NPC -> service alley -> enemy combat -> enemy-down flag.");
        }

        [MenuItem("Greyline/Slice/Validate Slice01")]
        public static void Validate()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                Debug.LogError("Slice01 scene missing: " + ScenePath);
                return;
            }

            Scene slice = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !slice.isLoaded;
            if (openedHere)
            {
                slice = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }
            int errors = 0;
            try
            {
                GameObject player = FindRoot(slice, "Slice01_Player")?.transform.Find("Player")?.gameObject;
                errors += Require(player != null, "Slice01 is missing its Player.");
                errors += Require(FindComponent<GameFlagsHost>(slice) != null, "Slice01 is missing its GameFlagsHost.");
                errors += Require(FindComponent<NpcConversation>(slice) != null, "Slice01 is missing its NPC conversation.");
                errors += Require(player != null && player.GetComponent<InteractionInput>() != null, "Slice01 Player is missing InteractionInput.");
                errors += Require(player != null && player.GetComponent<DistrictInteractionDriver>() != null, "Slice01 Player is missing the DistrictInteractionDriver.");
                errors += Require(player != null && player.GetComponent<SandboxInteractionTester>() == null, "Slice01 Player must not retain the legacy SandboxInteractionTester alongside DistrictInteractionDriver.");
                errors += Require(player != null && player.GetComponent<DialogueRunner>() != null, "Slice01 Player is missing the DialogueRunner.");

                EnemyAttack enemy = FindComponent<EnemyAttack>(slice);
                errors += Require(enemy != null && enemy.HasTarget, "Slice01 enemy has no attack target.");
                errors += Require(enemy != null && enemy.GetComponent<CombatHealth>() != null, "Slice01 enemy has no CombatHealth.");
                errors += Require(enemy != null && enemy.GetComponent<SliceEnemyDeathFlagBridge>() != null, "Slice01 enemy has no death-to-flag bridge.");
                SliceEncounterGate gate = FindComponent<SliceEncounterGate>(slice);
                errors += Require(gate != null, "Slice01 is missing its encounter gate.");
                errors += Require(gate != null && gate.GetComponent<Collider>() != null && gate.GetComponent<Collider>().isTrigger, "Slice01 encounter gate must be a trigger.");
                errors += Require(enemy != null && !enemy.enabled, "Slice01 enemy encounter must start dormant.");
                errors += Require(FindComponent<SliceProgressPresentation>(slice) != null, "Slice01 is missing progression presentation.");
                errors += Require(enemy != null && enemy.GetComponent<SliceEnemyCombatPresentation>() != null, "Slice01 enemy has no animated combat presentation.");
                errors += Require(FindRoot(slice, "Slice01_CombatFeedback") != null, "Slice01 is missing combat feedback.");
            }
            finally
            {
                if (openedHere && SceneManager.sceneCount > 1)
                {
                    EditorSceneManager.CloseScene(slice, true);
                }
            }

            string summary = $"Slice01 validation complete. Errors: {errors}.";
            if (errors == 0)
            {
                Debug.Log(summary);
            }
            else
            {
                Debug.LogError(summary);
            }
        }

        public static void BuildFromCommandLine() => Build();
        public static void ValidateFromCommandLine() => Validate();

        [MenuItem("Greyline/Slice/Refresh Existing Presentation")]
        public static void RefreshExistingPresentation()
        {
            AnimatorController enemyController = AssetDatabase.LoadAssetAtPath<AnimatorController>(SliceEnemyControllerPath);
            if (enemyController == null)
            {
                Debug.LogError("Build the Character Sandbox once before refreshing Slice01 enemy presentation.");
                return;
            }

            Scene slice = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SliceEnemyCombatPresentation presentation = FindComponent<SliceEnemyCombatPresentation>(slice);
            Animator enemyAnimator = presentation != null ? presentation.GetComponentInChildren<Animator>(true) : null;
            if (enemyAnimator == null)
            {
                Debug.LogError("Slice01 enemy visual Animator is missing.");
                return;
            }

            enemyAnimator.runtimeAnimatorController = enemyController;
            EditorSceneManager.MarkSceneDirty(slice);
            EditorSceneManager.SaveScene(slice, ScenePath);
            Debug.Log("Slice01: refreshed enemy presentation controller.");
        }

        /// <summary>
        /// Idempotent upgrade for the already-assembled Slice01: replaces the sandbox-only
        /// SandboxInteractionTester with the real DistrictInteractionDriver + DialogueRunner
        /// (lane/systems), and points the student NPC at an authored DialogueDefinition instead of
        /// its legacy Response[] fallback. Safe to re-run; it always reaches the same end state.
        /// </summary>
        [MenuItem("Greyline/Slice/Upgrade Systems Integration")]
        public static void UpgradeSystemsIntegration()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Exit Play Mode before upgrading Slice01.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                Debug.LogError("Build Slice01 before upgrading its systems integration.");
                return;
            }

            Scene slice = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject playerRoot = FindRoot(slice, "Slice01_Player");
            GameObject player = playerRoot != null ? playerRoot.transform.Find("Player")?.gameObject : null;
            InteractionInput input = player != null ? player.GetComponent<InteractionInput>() : null;
            if (player == null || input == null)
            {
                Debug.LogError("Slice01 Player or InteractionInput missing; build Slice01 first.");
                return;
            }

            SandboxInteractionTester legacyTester = player.GetComponent<SandboxInteractionTester>();
            if (legacyTester != null)
            {
                UnityEngine.Object.DestroyImmediate(legacyTester);
            }

            DialogueRunner runner = player.GetComponent<DialogueRunner>() ?? player.AddComponent<DialogueRunner>();
            DistrictInteractionDriver driver = player.GetComponent<DistrictInteractionDriver>() ?? player.AddComponent<DistrictInteractionDriver>();
            driver.Configure(input, runner);

            NpcConversation npc = FindComponent<NpcConversation>(slice);
            if (npc != null)
            {
                npc.Configure(
                    "slice.student",
                    "slice.student.greeted",
                    new[]
                    {
                        new NpcConversation.Response("The service alley leads around the hall.", "slice.student.greeted"),
                        new NpcConversation.Response("Be careful back there.")
                    },
                    BuildStudentDialogue(),
                    10);
            }

            EditorSceneManager.MarkSceneDirty(slice);
            EditorSceneManager.SaveScene(slice, ScenePath);
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("Slice01: upgraded to DistrictInteractionDriver + DialogueRunner systems integration.");
        }

        public static void UpgradeSystemsIntegrationFromCommandLine() => UpgradeSystemsIntegration();

        private static DialogueDefinition BuildStudentDialogue()
        {
            DialogueDefinition dialogue = AssetDatabase.LoadAssetAtPath<DialogueDefinition>(StudentDialoguePath);
            if (dialogue == null)
            {
                string directory = System.IO.Path.GetDirectoryName(StudentDialoguePath)?.Replace('\\', '/');
                if (!AssetDatabase.IsValidFolder(directory))
                {
                    AssetDatabase.CreateFolder("Assets/_Project/Data", "Dialogue");
                }

                dialogue = ScriptableObject.CreateInstance<DialogueDefinition>();
                AssetDatabase.CreateAsset(dialogue, StudentDialoguePath);
            }

            dialogue.Configure("slice.student", new[]
            {
                new DialogueLine("Student", "The service alley leads around the hall.", "slice.student.greeted"),
                new DialogueLine("Student", "Be careful back there.")
            });
            EditorUtility.SetDirty(dialogue);
            return dialogue;
        }

        private static Scene OpenSource(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                throw new InvalidOperationException("Slice01 source scene is missing: " + path);
            }

            return EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        }

        private static void CopyCampus(Scene source, Scene destination)
        {
            GameObject root = new GameObject("Slice01_World");
            SceneManager.MoveGameObjectToScene(root, destination);
            string[] sourceRoots = { "Ground", "Key Light", "Sky and Fog Volume", "World Grade Volume", "Massing" };
            foreach (string name in sourceRoots)
            {
                GameObject sourceRoot = FindRoot(source, name);
                if (sourceRoot == null)
                {
                    throw new InvalidOperationException("WorldSandbox is missing required root: " + name);
                }

                GameObject copy = UnityEngine.Object.Instantiate(sourceRoot, root.transform);
                copy.name = name;
            }
        }

        private static GameObject CopyPlayer(Scene source, Scene destination, InputActionAsset inputActions)
        {
            GameObject sourcePlayer = FindRoot(source, "Player");
            GameObject sourceCamera = FindRoot(source, "Follow Camera");
            if (sourcePlayer == null || sourceCamera == null)
            {
                throw new InvalidOperationException("DevCombat Player or Follow Camera is missing.");
            }

            GameObject root = new GameObject("Slice01_Player");
            SceneManager.MoveGameObjectToScene(root, destination);
            GameObject player = UnityEngine.Object.Instantiate(sourcePlayer, root.transform);
            player.name = "Player";
            player.transform.localPosition = new Vector3(0f, 0f, -42f);
            player.transform.localRotation = Quaternion.identity;

            GameObject cameraObject = UnityEngine.Object.Instantiate(sourceCamera, root.transform);
            cameraObject.name = "Follow Camera";
            Camera camera = cameraObject.GetComponent<Camera>();
            player.GetComponent<ThirdPersonPlayerMotor>().Configure(camera, inputActions);
            cameraObject.GetComponent<ThirdPersonOrbitCamera>().Configure(player.transform, inputActions);
            player.GetComponent<PlayerCombat>().Configure(inputActions, player.GetComponent<CharacterAnimationDriver>());
            player.GetComponent<PlayerDodge>().ConfigureInput(inputActions);
            return player;
        }

        private static void BuildFlagsHost(Scene destination)
        {
            GameObject host = new GameObject("Slice01_GameFlags");
            SceneManager.MoveGameObjectToScene(host, destination);
            host.AddComponent<GameFlagsHost>();
        }

        private static void BuildInteraction(GameObject player, Scene destination, InputActionAsset inputActions)
        {
            InteractionInput input = player.GetComponent<InteractionInput>() ?? player.AddComponent<InteractionInput>();
            input.Configure(inputActions);
            (player.GetComponent<SandboxInteractionTester>() ?? player.AddComponent<SandboxInteractionTester>()).Configure(input);

            GameObject npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npc.name = "Slice01_StudentNPC";
            SceneManager.MoveGameObjectToScene(npc, destination);
            npc.transform.SetPositionAndRotation(new Vector3(-12f, 1f, -28f), Quaternion.Euler(0f, 35f, 0f));
            npc.AddComponent<NpcConversation>().Configure(
                "slice.student",
                "slice.student.greeted",
                new[]
                {
                    new NpcConversation.Response("The service alley leads around the hall.", "slice.student.greeted"),
                    new NpcConversation.Response("Be careful back there.")
                });
        }

        private static CombatHealth BuildEnemy(Scene destination, Transform player)
        {
            GameObject enemy = new GameObject("Slice01_Enemy");
            SceneManager.MoveGameObjectToScene(enemy, destination);
            enemy.transform.SetPositionAndRotation(new Vector3(-28f, 0f, 24f), Quaternion.Euler(0f, 180f, 0f));
            enemy.layer = 0;

            CapsuleCollider collider = enemy.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 1f, 0f);
            collider.height = 2f;
            collider.radius = .45f;
            CharacterDefinition definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(CharacterDefinitionPath);
            if (definition == null || definition.VisualPrefab == null)
            {
                throw new InvalidOperationException("Slice01 requires the staged Y Bot character definition.");
            }

            GameObject visual = UnityEngine.Object.Instantiate(definition.VisualPrefab, enemy.transform);
            visual.name = "EnemyVisual";
            visual.transform.localPosition = Vector3.zero;
            Animator animator = visual.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                AnimatorController enemyController = AssetDatabase.LoadAssetAtPath<AnimatorController>(SliceEnemyControllerPath);
                if (enemyController == null)
                {
                    throw new InvalidOperationException("Slice01 requires SliceEnemyPresentation.controller. Build the Character Sandbox first.");
                }

                animator.runtimeAnimatorController = enemyController;
            }

            GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            indicator.name = "Attack Telegraph";
            indicator.transform.SetParent(enemy.transform, false);
            indicator.transform.localPosition = new Vector3(0f, 1.65f, .15f);
            indicator.transform.localScale = Vector3.one * .16f;
            UnityEngine.Object.DestroyImmediate(indicator.GetComponent<Collider>());

            CombatHealth health = enemy.AddComponent<CombatHealth>();
            health.Configure(55f);
            CombatHitReaction reaction = enemy.AddComponent<CombatHitReaction>();
            reaction.Configure(visual.transform);
            enemy.AddComponent<DeterministicKnockback>();
            EnemyAttack attack = enemy.AddComponent<EnemyAttack>();
            attack.Configure(player, 3.2f, .55f, .16f, .75f, 1.2f, 12f, indicator.GetComponent<Renderer>());
            attack.ConfigureMovement(8f, 1.7f, 2f, 720f);
            attack.ConfigureMelee(1, .9f, .75f, .1f);
            enemy.AddComponent<SliceEnemyCombatPresentation>().Configure(attack, animator);
            enemy.AddComponent<SliceEnemyDeathFlagBridge>();
            return health;
        }

        private static void BuildEncounterGate(Scene destination, GameObject player, EnemyAttack enemyAttack)
        {
            GameObject gateObject = new GameObject("Slice01_EncounterGate");
            SceneManager.MoveGameObjectToScene(gateObject, destination);
            gateObject.transform.position = new Vector3(-28f, 1.5f, 10f);
            BoxCollider trigger = gateObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(12f, 3f, 16f);
            enemyAttack.enabled = false;
            gateObject.AddComponent<SliceEncounterGate>().Configure(enemyAttack, player.transform, "slice.student.greeted");
        }

        private static void BuildCombatFeedback(Scene destination, GameObject player, CombatHealth enemy, Transform camera, Transform playerVisual)
        {
            GameObject feedback = new GameObject("Slice01_CombatFeedback");
            SceneManager.MoveGameObjectToScene(feedback, destination);
            CombatHealth playerHealth = player.GetComponent<CombatHealth>();
            feedback.AddComponent<SliceCombatFeedback>().Configure(playerHealth, enemy, camera, playerVisual);
        }

        private static void BuildProgressPresentation(Scene destination, GameObject player, Transform enemy)
        {
            GameObject root = new GameObject("Slice01_ProgressPresentation");
            SceneManager.MoveGameObjectToScene(root, destination);

            GameObject labelObject = new GameObject("Objective Label");
            labelObject.transform.SetParent(root.transform, false);
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.anchor = TextAnchor.UpperLeft;
            label.alignment = TextAlignment.Left;
            label.fontSize = 26;
            label.characterSize = .018f;
            label.fontStyle = FontStyle.Normal;

            GameObject markerObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            markerObject.name = "Completion Marker";
            markerObject.transform.SetParent(root.transform, false);
            markerObject.transform.position = enemy.position + Vector3.up * .2f;
            markerObject.transform.localScale = new Vector3(.35f, .08f, .35f);
            UnityEngine.Object.DestroyImmediate(markerObject.GetComponent<Collider>());

            root.AddComponent<SliceProgressPresentation>().Configure(
                label,
                markerObject.GetComponent<Renderer>(),
                player.transform);
        }

        private static void ClearManagedRoots(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name.StartsWith("Slice01_", StringComparison.Ordinal))
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static GameObject FindRoot(Scene scene, string name) => scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);

        private static T FindComponent<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static int Require(bool condition, string message)
        {
            if (condition)
            {
                return 0;
            }

            Debug.LogError(message);
            return 1;
        }
    }
}
