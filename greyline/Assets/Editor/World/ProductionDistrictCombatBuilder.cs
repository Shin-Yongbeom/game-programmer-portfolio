using System;
using System.Linq;
using Greyline.Combat;
using Greyline.Enemies;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Greyline.World.EditorTools
{
    public static class ProductionDistrictCombatBuilder
    {
        public static void BuildFromCommandLine()
        {
            Character.EditorTools.CharacterSandbox.Build();
            ProductionDistrictBuilder.Build();
            AddCombat();
            ProductionPresentationValidation.Validate();
            Greyline.EditorTools.EnemyEncounterValidation.Validate();
            Greyline.EditorTools.CharacterCombatValidator.ValidateAll();
            ProductionDefenseValidation.Validate();
            Debug.Log("GREYLINE_BUILD_OK");
        }

        [MenuItem("Greyline/World/Assemble District Combat")]
        public static void AddCombat()
        {
            Scene scene = EditorSceneManager.OpenScene(ProductionDistrictBuilder.ScenePath, OpenSceneMode.Single);
            GameObject player = scene.GetRootGameObjects().First(r => r.name == "Player");
            player.GetComponent<Core.CharacterAnimationDriver>().ConfigurePresentation(AssetDatabase.LoadAssetAtPath<Core.AnimationPresentationSet>(Character.EditorTools.ProductionAnimationAuthoring.SetPath));
            if (player.GetComponent<CombatDefense>() == null) player.AddComponent<CombatDefense>();
            if (player.GetComponent<ContextualCombat>() == null) player.AddComponent<ContextualCombat>();
            player.GetComponent<CombatHealth>().Configure(160f);
            GameObject previous = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "District Combat");
            if (previous != null) Object.DestroyImmediate(previous);
            GameObject root = new("District Combat");
            root.AddComponent<DistrictCombatFeedback>().Configure(player.GetComponent<CombatHealth>());
            AnimatorController controller = BuildEnemyController();
            var content = ProductionSystemsBuilder.EnsureCatalog();
            foreach (var encounter in content.encounters) CreateEncounter(root.transform, encounter, player.transform, controller);
            foreach (Transform t in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>()))
            {
                bool crate = t.name == "Stacked Crate" && t.position.y < .8f;
                if (t.name != "Market Counter" && t.name != "Bench Seat" && t.name != "Monument Lower Plinth" && !crate) continue;
                var surface = t.GetComponent<EnvironmentalFinisher>() ?? t.gameObject.AddComponent<EnvironmentalFinisher>();
                surface.Configure(crate ? "CRATE SLAM" : t.name == "Market Counter" ? "MARKET COUNTER SLAM" :
                    t.name == "Monument Lower Plinth" ? "MONUMENT SLAM" : "BENCH TAKEDOWN");
            }
            int surfaces = scene.GetRootGameObjects().Sum(r => r.GetComponentsInChildren<EnvironmentalFinisher>().Length);
            Debug.Log($"DISTRICT_FINISHER_SURFACES count={surfaces} counter/bench/crate/monument");
            ProductionSystemsBuilder.Assemble(scene, player);
            EditorSceneManager.SaveScene(scene);
            var settings = EditorBuildSettings.scenes.Where(s => s.path != ProductionDistrictBuilder.ScenePath).ToList();
            settings.Insert(0, new EditorBuildSettingsScene(ProductionDistrictBuilder.ScenePath, true));
            EditorBuildSettings.scenes = settings.ToArray();
            AssetDatabase.SaveAssets();
            ProductionDistrictBuilder.Validate();
            Debug.Log($"DISTRICT_COMBAT_BUILD_OK: {content.encounters.Sum(e=>e.enemies.Length)} enemies, {content.encounters.Length} encounters, defense, finisher, feedback.");
        }

        private static void CreateEncounter(Transform parent, Progression.ProgressionCatalog.EncounterReward content, Transform player, AnimatorController controller)
        {
            GameObject group = new(content.sceneName + " Encounter");
            group.transform.SetParent(parent); group.transform.position = content.position;
            var coordinator = group.AddComponent<EnemyEncounterCoordinator>();
            group.AddComponent<Progression.PersistentEncounter>().Configure(content.id);
            coordinator.Configure(player, content.engagementRadius, content.leashRadius, content.turnGap);
            for (int i = 0; i < content.enemies.Length; i++)
            {
                var spawn=content.enemies[i]; var profile=spawn.profile; var role=profile.archetype;
                GameObject enemy = new(role + " " + (i+1));
                enemy.transform.SetParent(group.transform);
                enemy.transform.localPosition = spawn.offset;
                enemy.transform.rotation = Quaternion.Euler(0,spawn.yaw,0);
                CapsuleCollider collider = enemy.AddComponent<CapsuleCollider>();
                collider.height=1.8f; collider.center=Vector3.up*.9f; collider.radius=.38f;
                CombatHealth health = enemy.AddComponent<CombatHealth>();
                health.Configure(profile.health);
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Character.EditorTools.CharacterSandbox.VisualPrefabPath));
                visual.transform.SetParent(enemy.transform,false);
                visual.transform.localScale *= profile.visualScale;
                Animator animator=visual.GetComponentInChildren<Animator>(); animator.runtimeAnimatorController=controller; animator.applyRootMotion=false;
                enemy.AddComponent<CombatHitReaction>().Configure(visual.transform);
                enemy.AddComponent<DeterministicKnockback>();
                var attack = enemy.AddComponent<EnemyAttack>();
                attack.Configure(player,profile.attackRange,profile.telegraph,profile.active,profile.recovery,profile.cooldown,profile.damage);
                attack.ConfigureMovement(profile.detectionRange,profile.stopDistance,profile.moveSpeed,profile.turnSpeed);
                attack.ConfigureMelee(~0,.55f,.62f,.1f);
                attack.ConfigureEncounter(coordinator,role);
                enemy.AddComponent<DistrictEnemyPresentation>().Configure(animator,AssetDatabase.LoadAssetAtPath<Core.AnimationPresentationSet>(Character.EditorTools.ProductionAnimationAuthoring.SetPath));
            }
        }

        private static AnimatorController BuildEnemyController()
        {
            const string path="Assets/_Project/Animators/DistrictEnemy.controller";
            AnimatorController controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm=controller.layers[0].stateMachine;
            foreach(var parameter in controller.parameters.Where(p=>p.name=="AttackPose"))controller.RemoveParameter(parameter);
            if (!controller.parameters.Any(p => p.name == "ActionRate"))
                controller.AddParameter(new AnimatorControllerParameter{name="ActionRate",type=AnimatorControllerParameterType.Float,defaultFloat=1});
            foreach (var obsolete in sm.states.Where(s => s.state.name == "Telegraph" || s.state.name == "Strike").ToArray())
                sm.RemoveState(obsolete.state);
            (string state,string clip,float speed)[] recipes={
                ("Idle","Combat/FightIdle.fbx",1), ("Walk","Locomotion/Walking.fbx",1.2f),
                ("StrikeJab","Combat/Jab.fbx",1), ("StrikeHeavy","Combat/HeavyPunchCombo.fbx",1),
                ("StrikeSweep","Combat/LegSweep.fbx",1),
                ("Hit","Combat/HitReactionLight2.fbx",1.4f), ("Death","Combat/Hit/KnockdownFaceUp.fbx",1),
                ("DeathSurface","Combat/Hit/KnockdownFaceDown.fbx",1)};
            foreach(var recipe in recipes)
            {
                var state=sm.states.Select(s=>s.state).FirstOrDefault(s=>s.name==recipe.state)??sm.AddState(recipe.state);
                state.motion=AssetDatabase.LoadAllAssetsAtPath(Character.EditorTools.CharacterSandbox.ClipRoot+"/"+recipe.clip).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
                state.speed=recipe.speed;
                state.timeParameterActive = false;
                state.timeParameter = string.Empty;
                state.speedParameterActive = recipe.state.StartsWith("Strike", StringComparison.Ordinal);
                state.speedParameter = state.speedParameterActive ? "ActionRate" : string.Empty;
                if(recipe.state=="Idle")sm.defaultState=state;
            }
            var held = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "FinisherHeld") ?? sm.AddState("FinisherHeld");
            held.motion = Character.EditorTools.ProductionAnimationAuthoring.LoadPose("FinisherHeld");
            EditorUtility.SetDirty(controller);return controller;
        }
    }
}
