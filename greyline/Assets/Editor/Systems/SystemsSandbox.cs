using Greyline.Core;
using Greyline.Interaction;
using Greyline.Quest;
using Greyline.Save;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Greyline.Systems.EditorTools
{
    /// <summary>
    /// GameSystems lane tooling. Owns an isolated sandbox scene that verifies the smallest production
    /// progression chain: interaction/NPC state changes, flag-driven quest steps, and save round-trip.
    ///
    /// Execution policy: explicit menu / CLI only. No InitializeOnLoad,
    /// no reliance on auto asset refresh. Record the Console result after every run.
    /// </summary>
    public static class SystemsSandbox
    {
        public const string ScenePath = "Assets/_Project/Scenes/SystemsSandbox.unity";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string QuestDataPath = "Assets/_Project/Data/Quest/SystemsSandboxQuest.asset";

        [MenuItem("Greyline/Systems/Build or Refresh Systems Sandbox")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Exit Play Mode before building the Systems Sandbox.");
                return;
            }

            EnsureFolder("Assets/_Project", "Scenes");
            if (IsSceneCurrent())
            {
                Debug.Log("Systems Sandbox already matches the builder contract; scene unchanged.");
                Validate();
                return;
            }

            BuildScene();
            AssetDatabase.SaveAssets();
            Validate();
        }

        [MenuItem("Greyline/Systems/Validate Systems Sandbox")]
        public static void Validate()
        {
            int errors = 0;
            int warnings = 0;

            errors += ValidateRuntimeContract(ref warnings);
            errors += ValidateScene(ref warnings);

            string summary = $"Systems sandbox validation complete. Errors: {errors}, Warnings: {warnings}.";
            if (errors == 0)
            {
                Debug.Log(summary);
            }
            else
            {
                Debug.LogError(summary);
            }
        }

        // Explicit Unity CLI -executeMethod gates.
        public static void BuildFromCommandLine() => Build();

        public static void ValidateFromCommandLine() => Validate();

        // ---- Runtime contract self-test (no scene, detached objects) ------

        private static int ValidateRuntimeContract(ref int warnings)
        {
            int errors = 0;
            var probe = new GameObject("§SystemsSandboxProbe") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var flags = new GameFlags();

                Interactable interactable = probe.AddComponent<Interactable>();
                interactable.Configure("probe.lever", "probe.flag", oneShot: true);

                if (flags.Get("probe.flag"))
                {
                    errors += Error("Contract: flag started true before any interaction.");
                }

                if (!interactable.TryInteract(flags) || !flags.Get("probe.flag"))
                {
                    errors += Error("Contract: first interaction did not set the flag true.");
                }

                if (!flags.Get(interactable.ConsumedFlagKey))
                {
                    errors += Error("Contract: one-shot interactable did not record its consumed flag.");
                }

                if (interactable.TryInteract(flags))
                {
                    errors += Error("Contract: one-shot interactable was consumed a second time.");
                }

                var npc = new GameObject("§StudentNpcProbe").AddComponent<NpcConversation>();
                npc.Configure(
                    "probe.student",
                    "probe.student.greeted",
                    new[]
                    {
                        new NpcConversation.Response("Hey. New around here?", "probe.student.greeted"),
                        new NpcConversation.Response("You will get used to the campus.")
                    });

                if (!npc.TryInteract(flags) || !flags.Get("probe.student.greeted"))
                {
                    errors += Error("Contract: NPC first response did not set its progress flag.");
                }

                if (!npc.TryInteract(flags))
                {
                    errors += Error("Contract: NPC second response did not run.");
                }

                Object.DestroyImmediate(npc.gameObject);

                GameFlagsSaveData captured = GameFlagsSaveData.Capture(flags);
                captured.schemaVersion = GameFlagsSaveService.CurrentSchemaVersion;
                string json = JsonUtility.ToJson(captured);
                GameFlagsSaveData restored = JsonUtility.FromJson<GameFlagsSaveData>(json);
                flags.Clear();
                restored.Restore(flags);

                if (!flags.Get("probe.flag") || !flags.Get(interactable.ConsumedFlagKey))
                {
                    errors += Error("Contract: GameFlags save round-trip did not restore the interaction and consumed flags.");
                }

                if (restored.schemaVersion != GameFlagsSaveService.CurrentSchemaVersion)
                {
                    errors += Error("Contract: save schema version did not round-trip.");
                }

                if (interactable.TryInteract(flags))
                {
                    errors += Error("Contract: restored one-shot interactable was not still consumed.");
                }

                QuestDefinition questDefinition = ScriptableObject.CreateInstance<QuestDefinition>();
                questDefinition.Configure(
                    "systems.sandbox.quest",
                    "Systems Sandbox Quest",
                    "Verify flag-driven quest progression.",
                    new[]
                    {
                        new QuestStep("meet_student", "Meet the student", "probe.student.greeted", "probe.quest.student_met"),
                        new QuestStep("pull_lever", "Pull the lever", "probe.flag", "probe.quest.complete")
                    });
                var questObject = new GameObject("§QuestProbe");
                QuestLog questLog = questObject.AddComponent<QuestLog>();
                questLog.Configure(questDefinition, flags);
                if (!questLog.StartQuest() || questLog.CurrentStepIndex != 0)
                {
                    errors += Error("Contract: quest did not start on its first step.");
                }

                flags.Set("probe.student.greeted", true);
                if (questLog.CurrentStepIndex != 1 || !flags.Get("probe.quest.student_met"))
                {
                    errors += Error("Contract: quest did not advance after the first objective flag.");
                }

                flags.Set("probe.flag", true);
                if (!questLog.IsCompleted || !flags.Get("probe.quest.complete"))
                {
                    errors += Error("Contract: quest did not complete after the final objective flag.");
                }

                Object.DestroyImmediate(questObject);
                Object.DestroyImmediate(questDefinition);
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }

            return errors;
        }

        // ---- Scene ------------------------------------------------------

        private static int ValidateScene(ref int warnings)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                return Error("Systems Sandbox scene missing: " + ScenePath + " (run Build or Refresh).");
            }

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !scene.isLoaded;
            if (openedHere)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            int errors = 0;

            if (FindComponent<GameFlagsHost>(scene) == null)
            {
                errors += Error("Systems Sandbox has no GameFlagsHost.");
            }

            if (FindComponent<SandboxInteractionTester>(scene) == null)
            {
                errors += Error("Systems Sandbox has no SandboxInteractionTester.");
            }

            if (FindComponent<GameFlagsSaveService>(scene) == null)
            {
                errors += Error("Systems Sandbox has no GameFlagsSaveService.");
            }

            QuestLog questLog = FindComponent<QuestLog>(scene);
            if (questLog == null)
            {
                errors += Error("Systems Sandbox has no QuestLog.");
            }
            else if (questLog.ActiveQuest == null || questLog.ActiveQuest.Steps == null || questLog.ActiveQuest.Steps.Count < 2)
            {
                errors += Error("Systems Sandbox QuestLog has no two-step quest definition.");
            }

            InteractionInput interactionInput = FindComponent<InteractionInput>(scene);
            if (interactionInput == null)
            {
                errors += Error("Systems Sandbox has no InteractionInput (production Interact action adapter).");
            }
            else if (!interactionInput.HasResolvedAction)
            {
                errors += Error("Systems Sandbox InteractionInput could not resolve the Player/Interact action from the shared input asset.");
            }

            var interactables = Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None);
            if (interactables.Length == 0)
            {
                errors += Error("Systems Sandbox has no Interactable.");
            }

            NpcConversation[] npcConversations = Object.FindObjectsByType<NpcConversation>(FindObjectsSortMode.None);
            if (npcConversations.Length == 0)
            {
                errors += Error("Systems Sandbox has no NpcConversation.");
            }

            foreach (NpcConversation conversation in npcConversations)
            {
                if (conversation.ResponseCount == 0)
                {
                    errors += Error($"NPC '{conversation.name}' has no responses.");
                }
            }

            foreach (Interactable interactable in interactables)
            {
                if (string.IsNullOrWhiteSpace(interactable.SetFlagKey))
                {
                    warnings += Warn($"Interactable '{interactable.name}' sets no flag; nothing to verify.");
                }

                if (interactable.Once && string.IsNullOrWhiteSpace(interactable.ConsumedFlagKey))
                {
                    errors += Error($"Interactable '{interactable.name}' is one-shot but resolves no consumed flag (set an interactionId).");
                }
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Player")
                {
                    errors += Error("Systems Sandbox must not contain a Player; interaction input integration is a later shared-input task.");
                }
            }

            if (openedHere)
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            return errors;
        }

        private static void BuildScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var light = new GameObject("Directional Light");
            Light lightComponent = light.AddComponent<Light>();
            lightComponent.type = LightType.Directional;
            lightComponent.intensity = 1f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var cameraObject = new GameObject("Main Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 2f, -6f), Quaternion.Euler(12f, 0f, 0f));

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";

            var flagsHost = new GameObject("Systems Flags Host");
            flagsHost.AddComponent<GameFlagsHost>();
            flagsHost.AddComponent<GameFlagsSaveService>();

            QuestDefinition quest = CreateOrUpdateSandboxQuest();
            QuestLog questLog = flagsHost.AddComponent<QuestLog>();
            SerializedObject questLogSerialized = new SerializedObject(questLog);
            questLogSerialized.FindProperty("quest").objectReferenceValue = quest;
            questLogSerialized.ApplyModifiedPropertiesWithoutUndo();

            var lever = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lever.name = "Lever";
            lever.transform.position = new Vector3(1.5f, 0.5f, 0f);
            lever.AddComponent<Interactable>().Configure("sandbox.lever", "sandbox.lever.pulled", oneShot: true, promptText: "Pull lever");

            var student = GameObject.CreatePrimitive(PrimitiveType.Cube);
            student.name = "Student NPC";
            student.transform.position = new Vector3(0.75f, 0.5f, 0f);
            student.AddComponent<NpcConversation>().Configure(
                "sandbox.student",
                "sandbox.student.greeted",
                new[]
                {
                    new NpcConversation.Response("Hey. New around here?", "sandbox.student.greeted"),
                    new NpcConversation.Response("You will get used to the campus.")
                });

            var tester = new GameObject("Interaction Tester");
            tester.transform.position = Vector3.zero;
            InteractionInput interactionInput = tester.AddComponent<InteractionInput>();
            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (inputActions == null)
            {
                Debug.LogError("Systems Sandbox: InputSystem_Actions asset missing at " + InputActionsPath);
            }
            interactionInput.Configure(inputActions);
            tester.AddComponent<SandboxInteractionTester>().Configure(interactionInput);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("Systems Sandbox scene written to " + ScenePath);
        }

        private static bool IsSceneCurrent()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                return false;
            }

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !scene.isLoaded;
            if (openedHere)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            GameFlagsHost host = FindComponent<GameFlagsHost>(scene);
            GameFlagsSaveService saveService = FindComponent<GameFlagsSaveService>(scene);
            SandboxInteractionTester tester = FindComponent<SandboxInteractionTester>(scene);
            Interactable lever = FindComponentByName<Interactable>(scene, "Lever");
            NpcConversation student = FindComponentByName<NpcConversation>(scene, "Student NPC");
            InteractionInput input = FindComponent<InteractionInput>(scene);
            QuestLog questLog = FindComponent<QuestLog>(scene);
            QuestDefinition quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(QuestDataPath);
            bool current = host != null
                           && saveService != null
                           && tester != null
                           && input != null
                           && questLog != null
                           && questLog.ActiveQuest == quest
                           && quest != null
                           && quest.Steps.Count == 2
                           && lever != null
                           && student != null
                           && student.GetComponent<MeshFilter>() != null
                           && student.ResponseCount == 2
                           && student.ProgressFlagKey == "sandbox.student.greeted"
                           && lever.Once
                           && lever.SetFlagKey == "sandbox.lever.pulled"
                           && lever.ConsumedFlagKey == "sandbox.lever.consumed";

            if (openedHere)
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            return current;
        }

        // ---- helpers --------------------------------------------------

        private static T FindComponent<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static T FindComponentByName<T>(Scene scene, string objectName) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (T component in root.GetComponentsInChildren<T>(true))
                {
                    if (component.name == objectName)
                    {
                        return component;
                    }
                }
            }

            return null;
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static QuestDefinition CreateOrUpdateSandboxQuest()
        {
            EnsureFolder("Assets/_Project", "Data");
            EnsureFolder("Assets/_Project/Data", "Quest");

            QuestDefinition quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(QuestDataPath);
            if (quest == null)
            {
                quest = ScriptableObject.CreateInstance<QuestDefinition>();
                AssetDatabase.CreateAsset(quest, QuestDataPath);
            }

            quest.Configure(
                "systems.sandbox.quest",
                "Systems Sandbox Quest",
                "Verify flag-driven quest progression.",
                new[]
                {
                    new QuestStep("meet_student", "Meet the student", "sandbox.student.greeted", "sandbox.quest.student_met"),
                    new QuestStep("pull_lever", "Pull the lever", "sandbox.lever.pulled", "sandbox.quest.complete")
                });
            EditorUtility.SetDirty(quest);
            return quest;
        }

        private static int Error(string message)
        {
            Debug.LogError(message);
            return 1;
        }

        private static int Warn(string message)
        {
            Debug.LogWarning(message);
            return 1;
        }
    }
}
