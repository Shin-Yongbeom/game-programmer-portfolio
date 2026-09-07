using System;
using System.Linq;
using Greyline.Combat;
using Greyline.Core;
using Greyline.Enemies;
using Greyline.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

namespace Greyline.EditorTools
{
    public static class U4CombatSceneTool
    {
        private const string ScenePath = "Assets/_Project/Scenes/DevCombat.unity";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string CombatDataFolder = "Assets/_Project/Data/Combat";
        private const string Light1Path = CombatDataFolder + "/U4Light1.asset";
        private const string Light2Path = CombatDataFolder + "/U4Light2.asset";
        private const string Light3Path = CombatDataFolder + "/U4Light3.asset";
        private const string Kick1Path = CombatDataFolder + "/U4Kick1.asset";
        private const string Heavy1Path = CombatDataFolder + "/U4Heavy1.asset";

        [MenuItem("Greyline/U4/Build or Upgrade Combat Runtime")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Exit Play Mode before building U4 combat runtime.");
                return;
            }

            EnsureFolder("Assets/_Project/Data", "Combat");
            AttackDefinition light1 = LoadOrCreate<AttackDefinition>(Light1Path);
            AttackDefinition light2 = LoadOrCreate<AttackDefinition>(Light2Path);
            AttackDefinition light3 = LoadOrCreate<AttackDefinition>(Light3Path);
            AttackDefinition kick1 = LoadOrCreate<AttackDefinition>(Kick1Path);
            AttackDefinition heavy1 = LoadOrCreate<AttackDefinition>(Heavy1Path);
            // Light combo Light1 -> Light2 -> Light3 -> Kick, then Heavy as its own branch. State
            // names map 1:1 to the approved Mixamo presentation clips
            // (Jab/Cross/StraightPunch/FrontKick/HeavyPunchCombo). Values below are transcribed from
            // the committed .asset files so a rebuild is a zero-diff no-op.
            light1.Configure("u4_light_1", "Jab", 20f, .48f, .22f, .48f, 1.15f, .55f, .35f, .16f, light2, .35f, HitReactionType.Light, .28f, .12f);
            // Duration/hit-window/comboOpen widened from the original .54s/.25-.52/.38 values:
            // the Cross clip's real strike (RightHand extension) doesn't land until ~0.97s, so the
            // old budget applied damage before the fist moved and CrossFaded away well before impact.
            // Not trimmed because it would cut the pre-impact motion.
            // widened the gameplay budget to fit the untrimmed clip's real timing instead.
            light2.Configure("u4_light_2", "Cross", 24f, 1f, .82f, .98f, 1.2f, .56f, .38f, .18f, light3, .8f, HitReactionType.Light, .36f, .14f);
            light3.Configure("u4_light_3", "StraightPunch", 32f, .68f, .3f, .6f, 1.35f, .62f, .42f, .24f, kick1, .42f, HitReactionType.Heavy, .55f, .18f);
            // Duration/hit-window widened from the original .78s/.32-.56 values: bone-velocity
            // diagnostics (CharacterSandbox.DiagnoseCombatClipMotion) show the real foot-extension
            // peak lands at ~1.03s, well after the old .78s+.3s=1.08s budget's hit window (.25-.44s
            // absolute) - damage was applying long before the kick visually connected, and the clip
            // was cut with almost no follow-through right at the peak.
            kick1.Configure("u4_kick_1", "FrontKick", 42f, 1.1f, .85f, .98f, 1.4f, .58f, .5f, .25f, null, 1f, HitReactionType.Heavy, .7f, .2f);
            heavy1.Configure("u4_heavy_1", "HeavyPunchCombo", 46f, .82f, .34f, .6f, 1.4f, .7f, .45f, .34f, null, 1f, HitReactionType.Heavy, .95f, .22f);
            EditorUtility.SetDirty(light1);
            EditorUtility.SetDirty(light2);
            EditorUtility.SetDirty(light3);
            EditorUtility.SetDirty(kick1);
            EditorUtility.SetDirty(heavy1);

            Scene scene = EditorSceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !scene.isLoaded;
            if (openedHere)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            try
            {
                GameObject player = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Player");
                if (player == null)
                {
                    throw new InvalidOperationException("DevCombat is missing Player.");
                }

                PlayerCombat combat = player.GetComponent<PlayerCombat>();
                CharacterAnimationDriver animationDriver = player.GetComponent<CharacterAnimationDriver>();
                InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
                if (combat == null || animationDriver == null || inputActions == null)
                {
                    throw new InvalidOperationException("DevCombat Player is missing U4 runtime dependencies.");
                }

                combat.ConfigureCombat(inputActions, animationDriver, light1, heavy1, 1 << 0);
                EditorUtility.SetDirty(combat);

                PlayerDodge dodge = player.GetComponent<PlayerDodge>();
                if (dodge == null)
                {
                    dodge = player.AddComponent<PlayerDodge>();
                }

                dodge.Configure(3.2f, .26f, .2f, .55f);
                dodge.ConfigureInput(inputActions);
                EditorUtility.SetDirty(dodge);

                ConfigurePlayerHealth(player);
                BuildDummies(scene, player.transform);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (openedHere)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            AssetDatabase.SaveAssets();
            CharacterCombatValidator.ValidateAll();
            Debug.Log("U4 combat runtime assembled. Run U4 validation and Play test before acceptance.");
        }

        [MenuItem("Greyline/U4/Validate Combat Runtime")]
        public static void Validate()
        {
            CharacterCombatValidator.ValidateAll();
        }

        public static void BuildFromCommandLine()
        {
            Build();
        }

        public static void ValidateFromCommandLine()
        {
            Validate();
        }

        private static void ConfigurePlayerHealth(GameObject player)
        {
            CombatHealth health = player.GetComponent<CombatHealth>();
            if (health == null)
            {
                health = player.AddComponent<CombatHealth>();
            }

            health.Configure(100f);
            EditorUtility.SetDirty(health);

            Transform visual = player.transform.Find("VisualRoot/CharacterVisual");
            CombatHitReaction reaction = player.GetComponent<CombatHitReaction>();
            if (reaction == null)
            {
                reaction = player.AddComponent<CombatHitReaction>();
            }

            reaction.Configure(visual);
            EditorUtility.SetDirty(reaction);
        }

        private static void BuildDummies(Scene scene, Transform player)
        {
            GameObject dummies = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "CombatDummies");
            if (dummies == null)
            {
                dummies = new GameObject("CombatDummies");
                SceneManager.MoveGameObjectToScene(dummies, scene);
            }

            CreateOrUpdateDummy(dummies.transform, "CombatDummy_01", new Vector3(0f, 0f, 2.1f), 55f, player, true);
            CreateOrUpdateDummy(dummies.transform, "CombatDummy_02", new Vector3(-1.4f, 0f, 2.8f), 70f, player, false);
            CreateOrUpdateDummy(dummies.transform, "CombatDummy_03", new Vector3(1.5f, 0f, 3.2f), 85f, player, false);
        }

        private static void CreateOrUpdateDummy(Transform parent, string name, Vector3 position, float healthValue, Transform player, bool attacks)
        {
            GameObject root = parent.Find(name)?.gameObject;
            if (root == null)
            {
                root = new GameObject(name);
                root.transform.SetParent(parent, false);
            }

            root.transform.position = position;
            root.layer = 0;

            CapsuleCollider collider = root.GetComponent<CapsuleCollider>() ?? root.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 1f, 0f);
            collider.height = 2f;
            collider.radius = .45f;

            GameObject visual = root.transform.Find("Visual")?.gameObject;
            if (visual == null)
            {
                visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.name = "Visual";
                visual.transform.SetParent(root.transform, false);
            }

            visual.transform.localPosition = new Vector3(0f, 1f, 0f);
            Collider visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
            {
                UnityEngine.Object.DestroyImmediate(visualCollider);
            }

            CombatHealth combatHealth = root.GetComponent<CombatHealth>() ?? root.AddComponent<CombatHealth>();
            combatHealth.Configure(healthValue);
            CombatHitReaction reaction = root.GetComponent<CombatHitReaction>() ?? root.AddComponent<CombatHitReaction>();
            reaction.Configure(visual.transform);
            if (root.GetComponent<DeterministicKnockback>() == null)
            {
                root.AddComponent<DeterministicKnockback>();
            }

            EnemyAttack enemyAttack = root.GetComponent<EnemyAttack>();
            if (attacks)
            {
                enemyAttack ??= root.AddComponent<EnemyAttack>();
                enemyAttack.Configure(player, 3.2f, .55f, .16f, .75f, 1.2f, 12f, visual.GetComponent<Renderer>());
                enemyAttack.ConfigureMovement(8f, 1.7f, 2f, 720f);
                enemyAttack.ConfigureMelee(1, 0.9f, 0.75f, 0.1f);
                EditorUtility.SetDirty(enemyAttack);
            }
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
