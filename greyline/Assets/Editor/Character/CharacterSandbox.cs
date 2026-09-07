using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Greyline.Character.EditorTools
{
    /// <summary>
    /// Builds the preview and playable character controllers, Humanoid imports and motion profiles.
    ///
    /// Execution policy: explicit menu / CLI only. No InitializeOnLoad, no reliance on auto refresh.
    /// This tool configures presentation assets only; it never changes combat rules or combo data.
    /// </summary>
    public static class CharacterSandbox
    {
        public const string ScenePath = "Assets/_Project/Scenes/CharacterSandbox.unity";
        public const string ControllerPath = "Assets/_Project/Animators/CharacterSandbox.controller";
        public const string CombatControllerPath = "Assets/_Project/Animators/PlayerCombat.controller";
        public const string SliceEnemyControllerPath = "Assets/_Project/Animators/SliceEnemyPresentation.controller";
        public const string ClipRoot = "Assets/_External/Animations/Mixamo";
        public const string VisualPrefabPath = "Assets/_Project/Prefabs/Characters/YBotCharacterVisual.prefab";

        private const string YBotObjectName = "Y Bot";
        private const float HumanReferenceHeight = 1.8f;

        /// <summary>
        /// The first Mixamo bundle verified on Y Bot. Order is the preview / clip-player order.
        /// "loop" marks clips that must import as looping (FightIdle only).
        /// "keepHeight" marks clips whose vertical root travel is meaningful for inspection (Jump, Landing).
        /// </summary>
        public static readonly ClipSpec[] Bundle =
        {
            new ClipSpec("StandingIdle", "Locomotion/Standing Idle.fbx", loop: true, keepHeight: false),
            new ClipSpec("Walking", "Locomotion/Walking.fbx", loop: true, keepHeight: false),
            new ClipSpec("Running", "Locomotion/Running.fbx", loop: true, keepHeight: false),
            new ClipSpec("FightIdle", "Combat/FightIdle.fbx", loop: true, keepHeight: false),
            new ClipSpec("Jump", "Locomotion/Jump.fbx", loop: false, keepHeight: false),
            new ClipSpec("Falling", "Locomotion/FallingIdle.fbx", loop: true, keepHeight: false),
            new ClipSpec("Landing", "Locomotion/Landing.fbx", loop: false, keepHeight: false),
            new ClipSpec("HitReactionLight2", "Combat/HitReactionLight2.fbx", loop: false, keepHeight: true),
            // Trailing Mixamo idle-return tail trimmed to the PlayerCombat gameplay budget
            // (Duration+RecoveryTime; see AttackDefinition/CharacterCombatValidator.ValidateAnimationBudgets).
            // Cut frame chosen from measured bone-velocity energy: a low-motion point right after
            // the strike settles, at or under budget. Tracked separately as a presentation gap.
            new ClipSpec("Jab", "Combat/Jab.fbx", loop: false, keepHeight: false, trimEndFrame: 18),
            // Impact is ~0.97s. The current 1.18s gameplay budget preserves contact and a short
            // follow-through; trim only the trailing idle-return tail beyond that budget.
            new ClipSpec("Cross", "Combat/Cross.fbx", loop: false, keepHeight: false, trimEndFrame: 35),
            new ClipSpec("HeavyPunchCombo", "Combat/HeavyPunchCombo.fbx", loop: false, keepHeight: false, trimEndFrame: 34),
            new ClipSpec("StraightPunch", "Combat/StraightPunch.fbx", loop: false, keepHeight: false, trimEndFrame: 28),
            new ClipSpec("FrontKick", "Combat/FrontKick.fbx", loop: false, keepHeight: false, trimEndFrame: 40),
            new ClipSpec("DodgeBackward", "Combat/Dodge/DodgeBackward.fbx", loop: false, keepHeight: true),
            new ClipSpec("DodgeLeft", "Combat/Dodge/DodgeLeft.fbx", loop: false, keepHeight: true),
            new ClipSpec("DodgeRight", "Combat/Dodge/DodgeRight.fbx", loop: false, keepHeight: true),
            new ClipSpec("DodgeForward", "Locomotion/RunningSlide.fbx", false, false),
            new ClipSpec("Uppercut", "Combat/Uppercut.fbx", false, false),
            new ClipSpec("CenterBlock", "Combat/CenterBlock.fbx", false, false),
            new ClipSpec("LegSweep", "Combat/LegSweep.fbx", false, false),
            new ClipSpec("KneeKickLead", "Combat/KneeKickLead.fbx", false, false),
            new ClipSpec("Pushing", "Combat/Pushing.fbx", false, false),
            new ClipSpec("CrouchIdle", "Locomotion/CrouchIdle.fbx", true, false),
            new ClipSpec("CrouchWalk", "Locomotion/CrouchWalk.fbx", true, false),
            new ClipSpec("BlockIdle", "Combat/BlockIdle.fbx", true, false),
            new ClipSpec("ChokeLift", "Combat/ChokeLift.fbx", false, false),
            new ClipSpec("SittingIdle", "Interaction/SittingIdle.fbx", true, false),
            new ClipSpec("StandToSit", "Interaction/StandToSit.fbx", false, false),
            new ClipSpec("SitToStand", "Interaction/SitToStand.fbx", false, false),
            new ClipSpec("Typing", "Interaction/Typing.fbx", true, false),
            new ClipSpec("PickUpObject", "Interaction/PickUpObject.fbx", false, false),
            new ClipSpec("Talking1", "Gesture/Talking1.fbx", true, false),
            new ClipSpec("Talking2", "Gesture/Talking2.fbx", true, false),
            new ClipSpec("Wave", "Gesture/Wave.fbx", false, false),
            new ClipSpec("LookAround", "Idle/LookAround.fbx", true, false),
            new ClipSpec("StandingVariation1", "Idle/StandingVariation1.fbx", true, false),
            new ClipSpec("StandingVariation2", "Idle/StandingVariation2.fbx", true, false),
            new ClipSpec("StrafeWalkLeft", "Locomotion/StrafeWalkLeft.fbx", true, false),
            new ClipSpec("StrafeWalkRight", "Locomotion/StrafeWalkRight.fbx", true, false),
            new ClipSpec("StrafeRunLeft", "Locomotion/StrafeRunLeft.fbx", true, false),
            new ClipSpec("StrafeRunRight", "Locomotion/StrafeRunRight.fbx", true, false),
            new ClipSpec("Turn90Left", "Locomotion/Turn90Left.fbx", false, false),
            new ClipSpec("Turn90Right", "Locomotion/Turn90Right.fbx", false, false),
            new ClipSpec("IdleTurn180", "Locomotion/IdleTurn180.fbx", false, false),
            new ClipSpec("VaultOverBox", "Locomotion/VaultOverBox.fbx", false, false),
            new ClipSpec("ClimbLedge", "Locomotion/ClimbLedge.fbx", false, false),
            // With root motion disabled the vertical fall must remain baked into the skeleton.
            new ClipSpec("KnockdownFaceUp", "Combat/Hit/KnockdownFaceUp.fbx", false, false),
            new ClipSpec("KnockdownFaceDown", "Combat/Hit/KnockdownFaceDown.fbx", false, false),
        };

        public readonly struct ClipSpec
        {
            public readonly string State;
            public readonly string RelativePath;
            public readonly bool Loop;
            public readonly bool KeepHeight;
            /// <summary>Source-take frame to end playback at, or -1 to keep the importer's auto-detected last frame.</summary>
            public readonly int TrimEndFrame;

            public ClipSpec(string state, string relativePath, bool loop, bool keepHeight, int trimEndFrame = -1)
            {
                State = state;
                RelativePath = relativePath;
                Loop = loop;
                KeepHeight = keepHeight;
                TrimEndFrame = trimEndFrame;
            }

            public string AssetPath => ClipRoot + "/" + RelativePath;
        }

        [MenuItem("Greyline/Character/Build or Refresh Character Sandbox")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Exit Play Mode before building the Character Sandbox.");
                return;
            }

            EnsureFolders();
            ConfigureMixamoImports();
            BuildController();
            BuildCombatController();
            BuildSliceEnemyPresentationController();
            BuildScene();
            AssetDatabase.SaveAssets();
            Validate();
        }

        [MenuItem("Greyline/Character/Build Slice Enemy Presentation Controller")]
        public static void BuildSliceEnemyPresentationControllerFromMenu()
        {
            EnsureFolders();
            BuildSliceEnemyPresentationController();
        }

        [MenuItem("Greyline/Character/Configure Mixamo Humanoid Imports")]
        public static void ConfigureMixamoImports()
        {
            foreach (ClipSpec spec in Bundle)
            {
                var importer = AssetImporter.GetAtPath(spec.AssetPath) as ModelImporter;
                if (importer == null)
                {
                    Debug.LogError($"Character Sandbox: FBX not found for import config: {spec.AssetPath}");
                    continue;
                }

                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.autoGenerateAvatarMappingIfUnspecified = true;
                importer.importAnimation = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.isReadable = false;
                importer.optimizeGameObjects = false;
                importer.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;

                ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
                if (clips.Length == 0)
                {
                    Debug.LogError($"Character Sandbox: no take in FBX: {spec.AssetPath}");
                    continue;
                }

                // One take per Mixamo file. Name the clip after the sandbox state.
                ModelImporterClipAnimation clip = clips[0];
                clip.name = spec.State;
                clip.loopTime = spec.Loop;
                clip.loopPose = spec.Loop;
                // Candidate presentation settings: bake idle/attack root orientation into the
                // pose so the Y Bot does not inherit the source's slight tilt.
                clip.lockRootRotation = true;
                clip.keepOriginalOrientation = false;
                // Extract authored travel, then discard it through applyRootMotion=false. Baking
                // travel into the skeleton would slide the visual away from its collision capsule.
                clip.lockRootPositionXZ = spec.State != "CrouchWalk" && !spec.State.StartsWith("Dodge") &&
                    spec.State != "LegSweep" && !spec.State.StartsWith("KnockdownFace") &&
                    spec.State != "KneeKickLead" && spec.State != "Pushing";
                // Based-on-body is the in-place candidate for attacks; gameplay movement remains
                // outside this sandbox and is not implied by these settings.
                clip.keepOriginalPositionXZ = false;
                clip.lockRootHeightY = !spec.KeepHeight;
                clip.keepOriginalPositionY = true;
                // NOTE: a Running loop-seam blend (loopBlend/loopBlendOrientation/
                // loopBlendPositionY/loopBlendPositionXZ) was attempted here but those fields
                // are not exposed on ModelImporterClipAnimation's public scripting API in
                // Unity 6000.3.23f1 (compile error, not a runtime choice) -- only reachable via
                // SerializedObject on the importer. Dropped for P0-2; log as tooling debt if the
                // Running loop seam still visibly pops; track as tooling debt.
                clip.maskType = ClipAnimationMaskType.None;

                if (spec.TrimEndFrame >= 0)
                {
                    clip.firstFrame = 0f;
                    clip.lastFrame = spec.TrimEndFrame;
                }

                importer.clipAnimations = new[] { clip };
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
                Debug.Log($"Character Sandbox: configured Humanoid import for {spec.State} ({spec.AssetPath}).");
            }
        }

        [MenuItem("Greyline/Character/Validate Character Sandbox")]
        public static void Validate()
        {
            int errors = 0;
            int warnings = 0;

            foreach (ClipSpec spec in Bundle)
            {
                if (!File.Exists(spec.AssetPath))
                {
                    errors += Error($"Missing FBX: {spec.AssetPath}");
                    continue;
                }

                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(spec.AssetPath);
                Avatar avatar = assets.OfType<Avatar>().FirstOrDefault();
                if (avatar == null || !avatar.isValid || !avatar.isHuman)
                {
                    errors += Error($"{spec.State}: no valid Humanoid Avatar on {spec.AssetPath}.");
                }

                AnimationClip clip = assets.OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                if (clip == null)
                {
                    errors += Error($"{spec.State}: no AnimationClip imported from {spec.AssetPath}.");
                    continue;
                }

                if (clip.length <= 0f)
                {
                    errors += Error($"{spec.State}: imported clip has zero length.");
                }

                if (spec.Loop && !clip.isLooping)
                {
                    errors += Error($"{spec.State}: expected a looping clip but Loop Time is off.");
                }

                if (!spec.Loop && clip.isLooping)
                {
                    warnings += Warn($"{spec.State}: clip is looping but was not expected to loop.");
                }
            }

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                errors += Error($"Preview controller missing: {ControllerPath}");
            }
            else
            {
                AnimatorStateMachine sm = controller.layers[0].stateMachine;
                HashSet<string> states = new(sm.states.Select(s => s.state.name));
                foreach (ClipSpec spec in Bundle)
                {
                    if (!states.Contains(spec.State))
                    {
                        errors += Error($"Preview controller is missing state '{spec.State}'.");
                    }
                    else if (sm.states.First(s => s.state.name == spec.State).state.motion == null)
                    {
                        errors += Error($"Preview controller state '{spec.State}' has no motion assigned.");
                    }
                }

                if (sm.defaultState == null || sm.defaultState.name != "FightIdle")
                {
                    warnings += Warn("Preview controller default state is not FightIdle.");
                }

                if (sm.anyStateTransitions.Length + CountTransitions(sm) > 0)
                {
                    warnings += Warn("Preview controller has transitions; sandbox expects a flat, transition-free controller.");
                }
            }

            AnimatorController combatController = AssetDatabase.LoadAssetAtPath<AnimatorController>(CombatControllerPath);
            if (combatController == null)
            {
                errors += Error($"Combat presentation controller missing: {CombatControllerPath}");
            }
            else
            {
                AnimatorStateMachine combatSm = combatController.layers[0].stateMachine;
                string[] requiredCombatStates =
                {
                    "StandingIdle", "Walking", "Running", "Jump", "Falling", "Landing",
                    "FightIdle", "Jab", "Cross", "HeavyPunchCombo", "StraightPunch", "FrontKick",
                    "DodgeBackward", "DodgeLeft", "DodgeRight", "HitReaction"
                };
                foreach (string stateName in requiredCombatStates)
                {
                    if (FindState(combatSm, stateName) == null)
                    {
                        errors += Error($"Combat presentation controller is missing state '{stateName}'.");
                    }
                }
                AnimatorState fightIdle = FindState(combatSm, "FightIdle");
                AnimatorState jab = FindState(combatSm, "Jab");
                AnimatorState cross = FindState(combatSm, "Cross");
                if (fightIdle == null || jab == null || cross == null)
                {
                    errors += Error("Combat presentation controller must contain FightIdle, Jab and Cross states.");
                }
                else
                {
                    if (combatSm.defaultState != FindState(combatSm, "StandingIdle"))
                    {
                        errors += Error("Combat presentation controller default state must be StandingIdle (locomotion entry).");
                    }

                    if (fightIdle.motion == null || jab.motion == null || cross.motion == null)
                    {
                        errors += Error("Combat presentation controller has a state without a motion.");
                    }

                    if (!ReturnsToState(jab, fightIdle) || !ReturnsToState(cross, fightIdle))
                    {
                        errors += Error("Jab and Cross must have an exit-time transition back to FightIdle.");
                    }
                }
            }

            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (sceneAsset == null)
            {
                errors += Error($"Character Sandbox scene missing: {ScenePath}");
            }
            else
            {
                Scene scene = SceneManager.GetSceneByPath(ScenePath);
                bool openedHere = !scene.isLoaded;
                if (openedHere)
                {
                    scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                }

                errors += RequireRoot(scene, "Ground");
                errors += RequireRoot(scene, "Key Light");
                errors += RequireRoot(scene, "Sandbox Camera");
                errors += RequireRoot(scene, YBotObjectName);
                errors += RequireRoot(scene, "ScaleRef_1.8m");

                GameObject ybot = FindRoot(scene, YBotObjectName);
                if (ybot != null)
                {
                    Animator animator = ybot.GetComponentInChildren<Animator>();
                    if (animator == null)
                    {
                        errors += Error("Y Bot has no Animator.");
                    }
                    else
                    {
                        if (animator.runtimeAnimatorController == null)
                        {
                            errors += Error("Y Bot Animator has no preview controller assigned.");
                        }

                        if (animator.applyRootMotion)
                        {
                            errors += Error("Y Bot Animator has applyRootMotion on; sandbox presentation is in-place.");
                        }

                        if (animator.avatar == null || !animator.avatar.isHuman)
                        {
                            errors += Error("Y Bot Animator has no valid Humanoid Avatar.");
                        }
                    }
                }

                // Combat rules stay out of this lane's sandbox.
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.GetComponentInChildren<CharacterController>() != null)
                    {
                        errors += Error("Character Sandbox must not contain a CharacterController; gameplay movement lives in DevCombat.");
                    }
                }

                if (openedHere)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            string summary = $"Character Sandbox validation complete. Errors: {errors}, Warnings: {warnings}.";
            if (errors == 0)
            {
                Debug.Log(summary);
            }
            else
            {
                Debug.LogError(summary);
            }
        }

        // Explicit Unity CLI -executeMethod gates (interactive Editor closed).
        public static void BuildFromCommandLine() => Build();

        public static void ValidateFromCommandLine() => Validate();

        // ---- Controller -------------------------------------------------

        private static void BuildController()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            AnimatorState fightIdle = null;
            HashSet<string> desiredStates = new(Bundle.Select(spec => spec.State));
            // Preserve existing state objects/fileIDs. Rebuilds only add missing states and remove
            // obsolete sandbox states, avoiding serialization churn while keeping the controller flat.
            foreach (ChildAnimatorState child in sm.states.ToArray())
            {
                if (!desiredStates.Contains(child.state.name))
                {
                    sm.RemoveState(child.state);
                }
            }

            foreach (ClipSpec spec in Bundle)
            {
                AnimationClip clip = LoadClip(spec);
                if (clip == null)
                {
                    Debug.LogError($"Character Sandbox: cannot assign missing clip '{spec.State}' to controller.");
                    continue;
                }

                AnimatorState state = sm.states
                    .Select(child => child.state)
                    .FirstOrDefault(candidate => candidate.name == spec.State);
                if (state == null)
                {
                    state = sm.AddState(spec.State);
                }
                state.motion = clip;
                state.writeDefaultValues = true;
                if (spec.State == "FightIdle")
                {
                    fightIdle = state;
                }
            }

            if (fightIdle != null)
            {
                sm.defaultState = fightIdle;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"Character Sandbox: preview controller written to {ControllerPath}.");
        }

        private static void BuildCombatController()
        {
            var presentation = ProductionAnimationAuthoring.Build();
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CombatControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(CombatControllerPath);
            }

            // Idempotent rebuild: find-or-update every parameter / state / transition so a repeat
            // build with no recipe change leaves PlayerCombat.controller byte-identical. Never
            // blanket remove-and-recreate — Unity mints a fresh fileID for each new sub-object, so
            // that reshuffles the whole asset every run.
            EnsureParameter(controller, "Speed", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "Grounded", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "VerticalVelocity", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "FallDistance", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "HitReaction", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "HeavyPunchCombo", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "StraightPunch", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "FrontKick", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Dodge", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Crouching", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Guarding", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "ChargeAmount", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "ActionRate", AnimatorControllerParameterType.Float);
            string[] desiredParameters =
            {
                "Speed", "Grounded", "VerticalVelocity", "FallDistance", "HitReaction",
                "HeavyPunchCombo", "StraightPunch", "FrontKick", "Dodge", "Crouching", "Guarding", "ChargeAmount", "ActionRate"
            };
            foreach (AnimatorControllerParameter parameter in controller.parameters)
            {
                if (!desiredParameters.Contains(parameter.name))
                {
                    controller.RemoveParameter(parameter);
                }
            }
            var parameters = controller.parameters;
            parameters.First(p=>p.name=="ActionRate").defaultFloat=1f;
            controller.parameters=parameters;

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            string[] desiredStates =
            {
                "StandingIdle", "Walking", "Running", "Jump", "Falling", "Landing", "FightIdle",
                "Jab", "Cross", "HeavyPunchCombo", "StraightPunch", "FrontKick",
                "DodgeBackward", "DodgeLeft", "DodgeRight", "DodgeForward", "ChargedUppercut", "HitReaction", "HeavyCharge", "CrouchIdle", "CrouchWalk", "BlockIdle", "Finisher", "FinisherPush", "FinisherApproach", "Death"
            };
            HashSet<string> desiredStateSet = new(desiredStates);
            foreach (ChildAnimatorState child in sm.states.ToArray())
            {
                if (!desiredStateSet.Contains(child.state.name))
                {
                    sm.RemoveState(child.state);
                }
            }

            Dictionary<string, AnimatorState> states = new();
            foreach (string stateName in desiredStates)
            {
                AnimatorState state = FindState(sm, stateName) ?? sm.AddState(stateName);

                ClipSpec spec = Bundle.FirstOrDefault(candidate => candidate.State == stateName);
                if (stateName == "HitReaction")
                {
                    spec = Bundle.First(candidate => candidate.State == "HitReactionLight2");
                }
                if (stateName == "HeavyCharge") spec = Bundle.First(candidate => candidate.State == "HeavyPunchCombo");
                if (stateName == "Finisher") spec = Bundle.First(candidate => candidate.State == "KneeKickLead");
                if (stateName == "FinisherPush") spec = Bundle.First(candidate => candidate.State == "Pushing");
                if (stateName == "FinisherApproach") spec = Bundle.First(candidate => candidate.State == "Walking");
                if (stateName == "Death") spec = Bundle.First(candidate => candidate.State == "KnockdownFaceUp");
                if (stateName == "ChargedUppercut") spec = Bundle.First(candidate => candidate.State == "Uppercut");
                if (stateName == "BlockIdle") spec = Bundle.First(candidate => candidate.State == "CenterBlock");

                AnimationClip clip = LoadClip(spec);
                if (clip != null)
                {
                    state.motion = clip;
                }
                var motion = presentation.Find(stateName);
                if (motion != null) state.motion = motion.clip;
                if (stateName == "BlockIdle") state.motion = ProductionAnimationAuthoring.LoadPose("GuardHold");

                // Keep the readable first half of the hit pose, but do not leave the
                // player locked in its recovery animation after impact.
                state.speed = stateName == "HitReaction" ? 1.25f : 1f;
                if (stateName == "FinisherApproach") state.speed = 1.5f;
                if (stateName == "Landing") state.speed = 4f;
                state.timeParameterActive = false;
                state.timeParameter = string.Empty;
                state.speedParameterActive = motion != null;
                state.speedParameter = motion != null ? "ActionRate" : string.Empty;
                state.writeDefaultValues = true;
                states[stateName] = state;
            }

            sm.defaultState = states["StandingIdle"];
            var layers = controller.layers;
            layers[0].iKPass = true;
            controller.layers = layers;
            // One posture state blends stride speed, avoiding AnyState restarts at the idle/walk threshold.
            BlendTree crouch = AssetDatabase.LoadAllAssetsAtPath(CombatControllerPath).OfType<BlendTree>()
                .FirstOrDefault(t => t.name == "Crouch Locomotion");
            if (crouch == null)
            {
                crouch = new BlendTree { name = "Crouch Locomotion" };
                AssetDatabase.AddObjectToAsset(crouch, controller);
            }
            crouch.blendType = BlendTreeType.Simple1D;
            crouch.blendParameter = "Speed";
            crouch.useAutomaticThresholds = false;
            crouch.children = new[] {
                new ChildMotion { motion = LoadClip(Bundle.First(s => s.State == "CrouchIdle")), threshold = 0f, timeScale = 1f },
                new ChildMotion { motion = LoadClip(Bundle.First(s => s.State == "CrouchWalk")), threshold = 1.25f, timeScale = 1f }
            };
            states["CrouchIdle"].motion = crouch;
            EditorUtility.SetDirty(crouch);
            BlendTree charge = AssetDatabase.LoadAllAssetsAtPath(CombatControllerPath).OfType<BlendTree>().FirstOrDefault(t=>t.name=="Charge Anticipation");
            if(charge==null){charge=new BlendTree{name="Charge Anticipation"};AssetDatabase.AddObjectToAsset(charge,controller);}
            charge.blendType=BlendTreeType.Simple1D;charge.blendParameter="ChargeAmount";charge.useAutomaticThresholds=false;
            charge.children=new[]{
                new ChildMotion{motion=ProductionAnimationAuthoring.LoadPose("ChargeRelaxed"),threshold=0,timeScale=1},
                new ChildMotion{motion=ProductionAnimationAuthoring.LoadPose("ChargeReady"),threshold=1,timeScale=1}};
            states["HeavyCharge"].motion=charge;EditorUtility.SetDirty(charge);

            // (source, destination) is unique across this controller, so it is the reuse key.
            // Edge() returns the existing transition for that pair or creates it once, and clears
            // its inline condition list; conditions/timing are then rewritten deterministically
            // (conditions serialize inline on the transition — rewriting them is not fileID churn).
            HashSet<(string, string)> keep = new();
            HashSet<string> keepAny = new();

            AnimatorStateTransition Edge(string from, string to)
            {
                keep.Add((from, to));
                AnimatorState source = states[from];
                AnimatorState destination = states[to];
                AnimatorStateTransition transition =
                    source.transitions.FirstOrDefault(candidate => candidate.destinationState == destination)
                    ?? source.AddTransition(destination);
                ResetConditions(transition);
                return transition;
            }

            void Air(string from)
            {
                AddCondition(Edge(from, "Jump"), AnimatorConditionMode.NotEqual, 0f, "Grounded");
                AddCondition(Edge(from, "Jump"), AnimatorConditionMode.Greater, .1f, "VerticalVelocity");
                AddCondition(Edge(from, "Falling"), AnimatorConditionMode.NotEqual, 0f, "Grounded");
                AddCondition(Edge(from, "Falling"), AnimatorConditionMode.Less, -2.5f, "VerticalVelocity");
                AddCondition(Edge(from, "Falling"), AnimatorConditionMode.Greater, 1.5f, "FallDistance");
            }

            void ExitEdge(string from, string to, float exitTime, float duration)
            {
                AnimatorStateTransition transition = Edge(from, to);
                transition.hasExitTime = true;
                transition.exitTime = exitTime;
                transition.duration = duration;
            }

            AddCondition(Edge("StandingIdle", "Walking"), AnimatorConditionMode.Greater, .1f, "Speed");
            AddCondition(Edge("StandingIdle", "Running"), AnimatorConditionMode.Greater, 3.4f, "Speed");
            Air("StandingIdle");
            AddCondition(Edge("Walking", "StandingIdle"), AnimatorConditionMode.Less, .1f, "Speed");
            AddCondition(Edge("Walking", "Running"), AnimatorConditionMode.Greater, 3.4f, "Speed");
            Air("Walking");
            AddCondition(Edge("Running", "StandingIdle"), AnimatorConditionMode.Less, .1f, "Speed");
            AddCondition(Edge("Running", "Walking"), AnimatorConditionMode.Less, 3.4f, "Speed");
            Air("Running");

            AddCondition(Edge("Jump", "Falling"), AnimatorConditionMode.NotEqual, 0f, "Grounded");
            AddCondition(Edge("Jump", "Falling"), AnimatorConditionMode.Less, -2.5f, "VerticalVelocity");
            AddCondition(Edge("Jump", "Falling"), AnimatorConditionMode.Greater, 1.5f, "FallDistance");
            AddCondition(Edge("Jump", "Landing"), AnimatorConditionMode.If, 0f, "Grounded");
            AddCondition(Edge("Falling", "Landing"), AnimatorConditionMode.If, 0f, "Grounded");
            ExitEdge("Landing", "StandingIdle", .55f, .08f);
            AddCondition(Edge("Landing", "Walking"), AnimatorConditionMode.Greater, .1f, "Speed");
            AddCondition(Edge("Landing", "Running"), AnimatorConditionMode.Greater, 3.4f, "Speed");

            AddCondition(Edge("FightIdle", "StandingIdle"), AnimatorConditionMode.Less, .1f, "Speed");
            AddCondition(Edge("FightIdle", "Walking"), AnimatorConditionMode.Greater, .1f, "Speed");
            Air("FightIdle");
            ExitEdge("Jab", "FightIdle", .82f, .08f);
            ExitEdge("Cross", "FightIdle", .94f, .08f);
            // Sampled charge/dodge states exit explicitly from the driver on their gameplay clocks.
            ExitEdge("StraightPunch", "FightIdle", .82f, .08f);
            ExitEdge("FrontKick", "FightIdle", .86f, .08f);
            ExitEdge("HitReaction", "StandingIdle", .45f, .06f);
            AddCondition(Edge("CrouchIdle", "StandingIdle"), AnimatorConditionMode.IfNot, 0f, "Crouching");
            AddCondition(Edge("CrouchWalk", "StandingIdle"), AnimatorConditionMode.IfNot, 0f, "Crouching");
            AddCondition(Edge("BlockIdle", "FightIdle"), AnimatorConditionMode.IfNot, 0f, "Guarding");

            foreach (string posture in new[] { "CrouchIdle", "BlockIdle" })
            {
                keepAny.Add(posture);
                AnimatorStateTransition transition = sm.anyStateTransitions.FirstOrDefault(t => t.destinationState == states[posture])
                    ?? sm.AddAnyStateTransition(states[posture]);
                ResetConditions(transition);
                transition.canTransitionToSelf = false;
                transition.duration = posture == "CrouchIdle" ? .22f : .1f;
                transition.hasFixedDuration = true;
                transition.AddCondition(AnimatorConditionMode.If, 0f, posture == "BlockIdle" ? "Guarding" : "Crouching");
                transition.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            }

            AddAnyTrigger(sm, "HeavyPunchCombo", "HeavyPunchCombo");
            AddAnyTrigger(sm, "StraightPunch", "StraightPunch");
            AddAnyTrigger(sm, "FrontKick", "FrontKick");
            AddAnyTrigger(sm, "Dodge", "DodgeBackward");

            keepAny.Add("HitReaction");
            keepAny.Add("HeavyPunchCombo");
            keepAny.Add("StraightPunch");
            keepAny.Add("FrontKick");
            keepAny.Add("DodgeBackward");
            AnimatorStateTransition hit =
                sm.anyStateTransitions.FirstOrDefault(candidate => candidate.destinationState == states["HitReaction"])
                ?? sm.AddAnyStateTransition(states["HitReaction"]);
            ResetConditions(hit);
            hit.canTransitionToSelf = false;
            AddCondition(hit, AnimatorConditionMode.If, 0f, "HitReaction");
            hit.duration = .05f;

            foreach (KeyValuePair<string, AnimatorState> entry in states)
            {
                foreach (AnimatorStateTransition transition in entry.Value.transitions.ToArray())
                {
                    string destination = transition.destinationState != null ? transition.destinationState.name : null;
                    if (destination == null || !keep.Contains((entry.Key, destination)))
                    {
                        entry.Value.RemoveTransition(transition);
                    }
                }
            }

            foreach (AnimatorStateTransition transition in sm.anyStateTransitions.ToArray())
            {
                if (transition.destinationState == null || !keepAny.Contains(transition.destinationState.name))
                {
                    sm.RemoveAnyStateTransition(transition);
                }
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            // Definitions travel with the character into every scene, including the legacy sandbox.
            foreach (string guid in AssetDatabase.FindAssets("t:CharacterDefinition"))
            {
                var definition = AssetDatabase.LoadAssetAtPath<Greyline.Core.CharacterDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (definition.AnimatorController != controller) continue;
                definition.ConfigurePresentation(presentation);
                EditorUtility.SetDirty(definition);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"Character Sandbox: player presentation controller written to {CombatControllerPath}.");
        }

        private static void AddAnyTrigger(AnimatorStateMachine stateMachine, string trigger, string stateName)
        {
            AnimatorState state = FindState(stateMachine, stateName);
            if (state == null)
            {
                return;
            }

            AnimatorStateTransition transition = stateMachine.anyStateTransitions
                .FirstOrDefault(candidate => candidate.destinationState == state)
                ?? stateMachine.AddAnyStateTransition(state);
            ResetConditions(transition);
            transition.canTransitionToSelf = false;
            transition.duration = .05f;
            transition.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        }

        private static void BuildSliceEnemyPresentationController()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(SliceEnemyControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(SliceEnemyControllerPath);
            }

            string[] triggers = { "Jab", "Stagger", "Death" };
            foreach (string trigger in triggers)
            {
                EnsureParameter(controller, trigger, AnimatorControllerParameterType.Trigger);
            }

            foreach (AnimatorControllerParameter parameter in controller.parameters)
            {
                if (!triggers.Contains(parameter.name))
                {
                    controller.RemoveParameter(parameter);
                }
            }

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            (string name, string path)[] recipe =
            {
                ("Idle", "Locomotion/Standing Idle.fbx"),
                ("Jab", "Combat/Jab.fbx"),
                ("Stagger", "Combat/HitReactionLight2.fbx"),
                ("Death", "Boss/Defeated.fbx"),
            };
            Dictionary<string, AnimatorState> states = new();
            foreach ((string name, string path) entry in recipe)
            {
                AnimatorState state = FindState(stateMachine, entry.name) ?? stateMachine.AddState(entry.name);
                AnimationClip clip = LoadClip(new ClipSpec(entry.name, entry.path, entry.name == "Idle", keepHeight: false));
                if (clip != null)
                {
                    state.motion = clip;
                }

                state.writeDefaultValues = true;
                states[entry.name] = state;
            }

            stateMachine.defaultState = states["Idle"];
            foreach (ChildAnimatorState child in stateMachine.states.ToArray())
            {
                if (!states.ContainsKey(child.state.name))
                {
                    stateMachine.RemoveState(child.state);
                }
            }

            HashSet<AnimatorStateTransition> keepAny = new();
            foreach (string trigger in triggers)
            {
                AnimatorStateTransition transition = stateMachine.anyStateTransitions
                    .FirstOrDefault(candidate => candidate.destinationState == states[trigger])
                    ?? stateMachine.AddAnyStateTransition(states[trigger]);
                ResetConditions(transition);
                transition.canTransitionToSelf = false;
                transition.duration = .06f;
                transition.AddCondition(AnimatorConditionMode.If, 0f, trigger);
                keepAny.Add(transition);
            }

            foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions.ToArray())
            {
                if (!keepAny.Contains(transition))
                {
                    stateMachine.RemoveAnyStateTransition(transition);
                }
            }

            HashSet<(string, string)> keepExit = new();
            ExitEdge(states["Jab"], states["Idle"], .82f, keepExit);
            ExitEdge(states["Stagger"], states["Idle"], .85f, keepExit);
            foreach (KeyValuePair<string, AnimatorState> entry in states)
            {
                foreach (AnimatorStateTransition transition in entry.Value.transitions.ToArray())
                {
                    string destination = transition.destinationState != null ? transition.destinationState.name : null;
                    if (destination == null || !keepExit.Contains((entry.Key, destination)))
                    {
                        entry.Value.RemoveTransition(transition);
                    }
                }
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"Character Sandbox: Slice enemy presentation controller written to {SliceEnemyControllerPath}.");
        }

        private static void ExitEdge(AnimatorState from, AnimatorState to, float exitTime, HashSet<(string, string)> keep)
        {
            keep.Add((from.name, to.name));
            AnimatorStateTransition transition = from.transitions
                .FirstOrDefault(candidate => candidate.destinationState == to)
                ?? from.AddTransition(to);
            ResetConditions(transition);
            transition.hasExitTime = true;
            transition.exitTime = exitTime;
            transition.duration = .06f;
        }

        private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            foreach (AnimatorControllerParameter existing in controller.parameters)
            {
                if (existing.name != name)
                {
                    continue;
                }

                if (existing.type == type)
                {
                    return;
                }

                controller.RemoveParameter(existing);
                break;
            }

            controller.AddParameter(name, type);
        }

        private static void ResetConditions(AnimatorStateTransition transition)
        {
            while (transition.conditions.Length > 0)
            {
                transition.RemoveCondition(transition.conditions[0]);
            }
        }

        private static void AddCondition(AnimatorStateTransition transition, AnimatorConditionMode mode, float threshold, string parameter)
        {
            transition.hasExitTime = false;
            transition.duration = .08f;
            transition.AddCondition(mode, threshold, parameter);
        }

        internal static AnimationClip LoadClip(ClipSpec spec)
        {
            return AssetDatabase.LoadAllAssetsAtPath(spec.AssetPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        }

        // ---- Diagnostics -------------------------------------------------

        /// <summary>
        /// Log-only, no-Unity-GUI-required check for "does this trimmed clip actually show the
        /// strike, or does it cut mid-motion / before the swing even lands". Samples the CURRENT
        /// imported clip (i.e. already reflecting any ClipSpec.TrimEndFrame) frame-by-frame via
        /// AnimationClip.SampleAnimation and tracks one representative bone's world-space speed.
        /// If speed is still rising (or high) at the very last sampled frame, the clip is being
        /// cut off before its motion completes - the same failure mode ValidateAnimationBudgets
        /// catches for the gameplay-timer side, checked here from the animation-authoring side.
        /// </summary>
        [MenuItem("Greyline/Character/Diagnose Combat Clip Motion")]
        public static void DiagnoseCombatClipMotion()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Character Sandbox: visual prefab missing at {VisualPrefabPath}.");
                return;
            }

            (string state, string bone)[] targets =
            {
                ("Jab", "RightHand"),
                ("Cross", "RightHand"),
                ("StraightPunch", "RightHand"),
                ("FrontKick", "RightFoot"),
                ("HeavyPunchCombo", "RightHand"),
            };

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                foreach ((string state, string boneName) in targets)
                {
                    ClipSpec spec = Bundle.FirstOrDefault(candidate => candidate.State == state);
                    AnimationClip clip = LoadClip(spec);
                    if (clip == null)
                    {
                        Debug.LogWarning($"[ClipDiag] {state}: clip asset not found at '{spec.AssetPath}'.");
                        continue;
                    }

                    Transform bone = instance.GetComponentsInChildren<Transform>(true)
                        .FirstOrDefault(t => t.name == boneName || t.name.EndsWith(":" + boneName));
                    if (bone == null)
                    {
                        Debug.LogWarning($"[ClipDiag] {state}: bone '{boneName}' not found on {prefab.name}.");
                        continue;
                    }

                    const float sampleRate = 60f;
                    int steps = Mathf.Max(2, Mathf.RoundToInt(clip.length * sampleRate));
                    Vector3 previousPosition = default;
                    float peakSpeed = 0f;
                    float peakTime = 0f;
                    float endSpeed = 0f;
                    for (int i = 0; i <= steps; i++)
                    {
                        float time = clip.length * i / steps;
                        clip.SampleAnimation(instance, time);
                        Vector3 position = bone.position;
                        if (i > 0)
                        {
                            float speed = (position - previousPosition).magnitude * sampleRate;
                            if (speed > peakSpeed)
                            {
                                peakSpeed = speed;
                                peakTime = time;
                            }

                            endSpeed = speed;
                        }

                        previousPosition = position;
                    }

                    float peakFraction = clip.length > 0f ? peakTime / clip.length : 0f;
                    string verdict = peakFraction >= 0.9f
                        ? "PEAK NEAR/AT CLIP END - likely still swinging when it cuts, cut too early"
                        : peakSpeed > 0.01f && endSpeed > peakSpeed * 0.5f
                            ? "STILL FAST AT CLIP END - motion has not settled, cut too early"
                            : "peak reached well before clip end and settling - trim point looks fine";
                    Debug.Log(
                        $"[ClipDiag] {state} ({boneName}): trimmed clip length {clip.length:0.000}s, " +
                        $"peak speed {peakSpeed:0.00} m/s at {peakTime:0.000}s ({peakFraction:P0} through clip), " +
                        $"end speed {endSpeed:0.00} m/s -> {verdict}",
                        bone);
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        public static void DiagnoseCombatClipMotionFromCommandLine() => DiagnoseCombatClipMotion();

        // ---- Scene -----------------------------------------------------

        private static void BuildScene()
        {
            if (File.Exists(ScenePath))
            {
                // The sandbox scene is already a checked-in product of this builder. Opening and
                // saving a newly-created replacement is what caused meaningless fileID churn.
                // Scene changes are intentionally explicit and belong in a separate scene change.
                Debug.Log($"Character Sandbox: preserved existing scene for idempotent build: {ScenePath}.");
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            ground.isStatic = true;

            GameObject sun = new GameObject("Key Light");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(45f, 150f, 0f);

            GameObject cameraObject = new GameObject("Sandbox Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 1.4f, -3.2f);
            cameraObject.transform.rotation = Quaternion.Euler(6f, 0f, 0f);

            GameObject scaleRef = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            scaleRef.name = "ScaleRef_1.8m";
            scaleRef.transform.localScale = new Vector3(0.4f, HumanReferenceHeight / 2f, 0.4f);
            scaleRef.transform.position = new Vector3(1.2f, HumanReferenceHeight / 2f, 0f);
            Object.DestroyImmediate(scaleRef.GetComponent<Collider>());

            // Thin strip at exactly y = 0 to eyeball foot planting and root drift.
            GameObject footLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
            footLine.name = "FootContactRef_y0";
            footLine.transform.localScale = new Vector3(2f, 0.002f, 2f);
            footLine.transform.position = Vector3.zero;
            Object.DestroyImmediate(footLine.GetComponent<Collider>());

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefabPath);
            GameObject ybot;
            if (prefab != null)
            {
                ybot = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                PrefabUtility.UnpackPrefabInstance(ybot, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }
            else
            {
                Debug.LogError($"Character Sandbox: visual prefab missing at {VisualPrefabPath}; spawning empty Y Bot placeholder.");
                ybot = new GameObject();
            }

            ybot.name = YBotObjectName;
            ybot.transform.position = Vector3.zero;
            ybot.transform.rotation = Quaternion.identity;

            Animator animator = ybot.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                animator = ybot.AddComponent<Animator>();
            }

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"Character Sandbox scene written to {ScenePath}.");
        }

        // ---- helpers -------------------------------------------------

        private static int CountTransitions(AnimatorStateMachine sm)
        {
            int n = 0;
            foreach (ChildAnimatorState child in sm.states)
            {
                n += child.state.transitions.Length;
            }

            return n;
        }

        private static AnimatorState FindState(AnimatorStateMachine sm, string name)
        {
            return sm.states.Select(child => child.state).FirstOrDefault(state => state.name == name);
        }

        private static bool ReturnsToState(AnimatorState source, AnimatorState destination)
        {
            return source.transitions.Any(transition => transition.hasExitTime && transition.destinationState == destination);
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Project", "Animators");
            EnsureFolder("Assets/_Project", "Scenes");
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static int RequireRoot(Scene scene, string name)
        {
            return FindRoot(scene, name) == null ? Error($"Character Sandbox is missing '{name}'.") : 0;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root;
                }
            }

            return null;
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
