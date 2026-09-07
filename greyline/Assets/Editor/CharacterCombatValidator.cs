using System;
using System.Collections.Generic;
using Greyline.Combat;
using Greyline.Core;
using Greyline.CameraSystem;
using Greyline.Enemies;
using Greyline.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Greyline.EditorTools
{
    public static class CharacterCombatValidator
    {
        [MenuItem("Greyline/Validate Character and Combat Data")]
        public static void ValidateAll()
        {
            int errors = 0;
            int warnings = 0;
            errors += ValidateCharacters(ref warnings);
            errors += ValidateAttacks(ref warnings);
            errors += ValidateAnimationBudgets(ref warnings);
            errors += ValidateDevCombatScene();
            errors += ValidatePlayerInputActions();

            string summary = $"Character/Combat validation complete. Errors: {errors}, Warnings: {warnings}.";
            if (errors == 0)
            {
                Debug.Log(summary);
            }
            else
            {
                Debug.LogError(summary);
            }
        }

        private static int ValidateCharacters(ref int warnings)
        {
            int errors = 0;
            Dictionary<string, CharacterDefinition> ids = new(StringComparer.OrdinalIgnoreCase);
            foreach (string guid in AssetDatabase.FindAssets("t:CharacterDefinition"))
            {
                CharacterDefinition definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (definition == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(definition.Id))
                {
                    errors += LogError(definition, "CharacterDefinition id is required.");
                    continue;
                }

                if (!ids.TryAdd(definition.Id, definition))
                {
                    errors += LogError(definition, $"Duplicate CharacterDefinition id '{definition.Id}'.");
                }

                if (definition.VisualPrefab == null)
                {
                    warnings += LogWarning(definition, "No visual prefab assigned; fallback visual will be used.");
                }

                if (definition.AnimatorController == null)
                {
                    warnings += LogWarning(definition, "No Animator Controller assigned; animation requests will be ignored.");
                }
            }

            return errors;
        }

        private static int ValidateAttacks(ref int warnings)
        {
            int errors = 0;
            Dictionary<string, AttackDefinition> ids = new(StringComparer.OrdinalIgnoreCase);
            List<AttackDefinition> attacks = new();
            foreach (string guid in AssetDatabase.FindAssets("t:AttackDefinition"))
            {
                AttackDefinition attack = AssetDatabase.LoadAssetAtPath<AttackDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (attack == null)
                {
                    continue;
                }

                attacks.Add(attack);
                if (string.IsNullOrWhiteSpace(attack.Id))
                {
                    errors += LogError(attack, "AttackDefinition id is required.");
                }
                else if (!ids.TryAdd(attack.Id, attack))
                {
                    errors += LogError(attack, $"Duplicate AttackDefinition id '{attack.Id}'.");
                }

                if (string.IsNullOrWhiteSpace(attack.AnimationState))
                {
                    warnings += LogWarning(attack, "No animation presentation is assigned; U4 runtime will use debug-only combat presentation.");
                }

                if (attack.HitStartNormalized > attack.HitEndNormalized)
                {
                    errors += LogError(attack, "Hit window start must not be after hit window end.");
                }

                if (attack.Duration <= 0f)
                {
                    errors += LogError(attack, "Attack duration must be greater than zero.");
                }

                if (attack.Damage <= 0f)
                {
                    errors += LogError(attack, "Attack damage must be greater than zero.");
                }

                if (attack.Range <= 0f || attack.Radius <= 0f)
                {
                    errors += LogError(attack, "Attack range and radius must be greater than zero.");
                }

                if (attack.ComboInputOpenNormalized < 0f || attack.ComboInputOpenNormalized > 1f)
                {
                    errors += LogError(attack, "Combo input window must be normalized between zero and one.");
                }

                if (attack.KnockbackDistance < 0f || attack.KnockbackDuration <= 0f)
                {
                    errors += LogError(attack, "Knockback distance must be non-negative and duration must be greater than zero.");
                }
            }

            foreach (AttackDefinition attack in attacks)
            {
                if (HasCycle(attack))
                {
                    errors += LogError(attack, "Attack chain contains a cycle.");
                }
            }

            return errors;
        }

        /// <summary>
        /// The Animator CrossFades to the next attack's state at exactly Duration+RecoveryTime
        /// (PlayerCombat.Update/StartAttack), independent of the clip's own exit-time transition.
        /// If the assigned clip's real length exceeds that gameplay budget, every combo-chain
        /// transition visibly truncates the animation mid-pose before it finishes playing.
        /// </summary>
        private const string CombatControllerPath = "Assets/_Project/Animators/PlayerCombat.controller";

        private static int ValidateAnimationBudgets(ref int warnings)
        {
            int errors = 0;
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CombatControllerPath);
            if (controller == null || controller.layers.Length == 0)
            {
                return errors;
            }

            Dictionary<string, AnimationClip> clipsByState = new(StringComparer.Ordinal);
            foreach (ChildAnimatorState state in controller.layers[0].stateMachine.states)
            {
                if (state.state.motion is AnimationClip clip)
                {
                    clipsByState[state.state.name] = clip;
                }
            }

            foreach (string guid in AssetDatabase.FindAssets("t:AttackDefinition"))
            {
                AttackDefinition attack = AssetDatabase.LoadAssetAtPath<AttackDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (attack == null || string.IsNullOrWhiteSpace(attack.AnimationState))
                {
                    continue;
                }

                if (!clipsByState.TryGetValue(attack.AnimationState, out AnimationClip clip) || clip == null)
                {
                    continue;
                }

                float budget = attack.Duration + attack.RecoveryTime;
                if (clip.length > budget + 0.02f)
                {
                    warnings += LogWarning(attack,
                        $"'{attack.Id}' animation state '{attack.AnimationState}' clip is {clip.length:0.00}s but " +
                        $"Duration+RecoveryTime budget is only {budget:0.00}s - the CrossFade to the next state will " +
                        "cut the clip off before it finishes playing.");
                }
            }

            return errors;
        }

        private static int ValidateDevCombatScene()
        {
            const string scenePath = "Assets/_Project/Scenes/DevCombat.unity";
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedForValidation = !scene.isLoaded;
            if (openedForValidation)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }

            int errors = 0;
            GameObject player = FindRootObject(scene, "Player");
            if (player == null)
            {
                errors += LogError(null, "DevCombat is missing Player.");
            }
            else
            {
                errors += RequireComponent<CharacterController>(player, "CharacterController");
                errors += RequireComponent<ThirdPersonPlayerMotor>(player, "ThirdPersonPlayerMotor");
                errors += RequireComponent<PlayerCombat>(player, "PlayerCombat");
                errors += RequireComponent<CharacterAnimationDriver>(player, "CharacterAnimationDriver");
                errors += RequireComponent<CharacterPresentation>(player, "CharacterPresentation");
                errors += RequireComponent<PlayerDodge>(player, "PlayerDodge");
                errors += RequireComponent<CombatHealth>(player, "CombatHealth (DevCombat player health)");
                errors += RequireComponent<CombatHitReaction>(player, "CombatHitReaction (DevCombat player hit reaction)");

                PlayerDodge dodge = player.GetComponent<PlayerDodge>();
                if (dodge != null)
                {
                    if (dodge.DodgeDuration <= 0f)
                    {
                        errors += LogError(player, "PlayerDodge dodge duration must be greater than zero.");
                    }

                    if (dodge.DodgeDistance <= 0f)
                    {
                        errors += LogError(player, "PlayerDodge dodge distance must be greater than zero.");
                    }

                    if (dodge.InvulnerabilityDuration < 0f)
                    {
                        errors += LogError(player, "PlayerDodge invulnerability duration must be non-negative.");
                    }

                    if (dodge.InvulnerabilityDuration > dodge.DodgeDuration)
                    {
                        errors += LogError(player, "PlayerDodge invulnerability window must not exceed dodge duration.");
                    }

                    if (dodge.Cooldown < 0f)
                    {
                        errors += LogError(player, "PlayerDodge cooldown must be non-negative.");
                    }

                    if (!dodge.HasInputAsset)
                    {
                        errors += LogError(player, "PlayerDodge has no InputActionAsset assigned; the Dodge action cannot bind.");
                    }
                }

                PlayerCombat combat = player.GetComponent<PlayerCombat>();
                if (combat != null && combat.DamageableLayers.value == 0)
                {
                    errors += LogError(player, "PlayerCombat has no damageable layer filter.");
                }

                if (combat != null && !combat.HasInputAsset)
                {
                    errors += LogError(player, "PlayerCombat has no InputActionAsset assigned; Attack/HeavyAttack cannot bind.");
                }

                if (combat == null || combat.FirstLightAttack == null)
                {
                    errors += LogError(player, "PlayerCombat is missing its first light AttackDefinition.");
                }

                if (combat != null)
                {
                    AttackDefinition heavy = combat.HeavyAttack;
                    if (heavy == null)
                    {
                        errors += LogError(player, "PlayerCombat is missing its Heavy AttackDefinition.");
                    }
                    else
                    {
                        if (heavy.ReactionType != HitReactionType.Heavy)
                        {
                            errors += LogError(heavy, "Heavy attack must use the Heavy hit reaction type.");
                        }

                        AttackDefinition light = combat.FirstLightAttack;
                        if (light != null && heavy.Damage <= light.Damage)
                        {
                            errors += LogError(heavy, "Heavy attack damage must exceed the first light attack.");
                        }

                        if (light != null && heavy.KnockbackDistance <= light.KnockbackDistance)
                        {
                            errors += LogError(heavy, "Heavy attack knockback distance must exceed the first light attack.");
                        }
                    }
                }

                if (player.transform.Find("VisualRoot/CharacterVisual") == null)
                {
                    errors += LogError(player, "Player is missing VisualRoot/CharacterVisual fallback hierarchy.");
                }
            }

            GameObject dummies = FindRootObject(scene, "CombatDummies");
            if (dummies == null || dummies.transform.childCount == 0)
            {
                errors += LogError(dummies, "DevCombat is missing CombatDummies.");
            }
            else
            {
                int enemyAttackCount = 0;
                foreach (Transform dummy in dummies.transform)
                {
                    errors += RequireComponent<Collider>(dummy.gameObject, "Collider");
                    errors += RequireComponent<CombatHealth>(dummy.gameObject, "CombatHealth");
                    errors += RequireComponent<CombatHitReaction>(dummy.gameObject, "CombatHitReaction");
                    errors += RequireComponent<DeterministicKnockback>(dummy.gameObject, "DeterministicKnockback");

                    EnemyAttack enemyAttack = dummy.GetComponent<EnemyAttack>();
                    if (enemyAttack != null)
                    {
                        enemyAttackCount++;
                        if (!enemyAttack.HasTarget)
                        {
                            errors += LogError(dummy.gameObject, "EnemyAttack has no target.");
                        }

                        if (enemyAttack.AttackRange <= 0f || enemyAttack.Damage <= 0f)
                        {
                            errors += LogError(dummy.gameObject, "EnemyAttack range and damage must be greater than zero.");
                        }

                        if (enemyAttack.DetectionRange < enemyAttack.AttackRange ||
                            enemyAttack.ApproachStopDistance <= 0f ||
                            enemyAttack.ApproachStopDistance > enemyAttack.AttackRange ||
                            enemyAttack.ApproachSpeed < 0f || enemyAttack.TurnSpeed < 0f)
                        {
                            errors += LogError(dummy.gameObject, "EnemyAttack movement values are invalid.");
                        }

                        if (enemyAttack.DamageableLayers.value == 0 || enemyAttack.HitRadius <= 0f ||
                            enemyAttack.HitForwardOffset < 0f || enemyAttack.MinimumForwardDot < -1f ||
                            enemyAttack.MinimumForwardDot > 1f)
                        {
                            errors += LogError(dummy.gameObject, "EnemyAttack melee query values are invalid.");
                        }

                        if (enemyAttack.TelegraphDuration < 0f || enemyAttack.ActiveDuration <= 0f ||
                            enemyAttack.RecoveryDuration < 0f || enemyAttack.CooldownDuration < 0f)
                        {
                            errors += LogError(dummy.gameObject, "EnemyAttack timing values are invalid.");
                        }
                    }
                }

                if (enemyAttackCount != 1)
                {
                    errors += LogError(dummies, $"DevCombat must contain exactly one EnemyAttack primitive; found {enemyAttackCount}.");
                }
            }

            GameObject followCamera = FindRootObject(scene, "Follow Camera");
            if (followCamera == null || followCamera.GetComponent<ThirdPersonOrbitCamera>() == null)
            {
                errors += LogError(followCamera, "DevCombat is missing Follow Camera with ThirdPersonOrbitCamera.");
            }

            if (openedForValidation)
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            return errors;
        }

        private static int ValidatePlayerInputActions()
        {
            const string inputAssetPath = "Assets/InputSystem_Actions.inputactions";
            InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(inputAssetPath);
            if (asset == null)
            {
                return LogError(null, "InputSystem_Actions asset is missing at " + inputAssetPath + ".");
            }

            InputActionMap playerMap = asset.FindActionMap("Player", false);
            if (playerMap == null)
            {
                return LogError(asset, "InputSystem_Actions has no 'Player' action map.");
            }

            int errors = 0;
            // Promoted combat/interaction inputs must live in the Player map with a working binding.
            foreach (string actionName in new[] { "HeavyAttack", "Dodge", "Interact" })
            {
                InputAction action = playerMap.FindAction(actionName, false);
                if (action == null)
                {
                    errors += LogError(asset, $"Player map is missing the '{actionName}' action.");
                    continue;
                }

                if (action.bindings.Count == 0)
                {
                    errors += LogError(asset, $"Player/{actionName} has no bindings.");
                }

                foreach (InputBinding binding in action.bindings)
                {
                    if (!binding.isComposite && !binding.isPartOfComposite && string.IsNullOrEmpty(binding.effectivePath))
                    {
                        errors += LogError(asset, $"Player/{actionName} has a broken (empty-path) binding.");
                    }
                }
            }

            return errors;
        }

        private static bool HasCycle(AttackDefinition firstAttack)
        {
            HashSet<AttackDefinition> visited = new();
            for (AttackDefinition current = firstAttack; current != null; current = current.NextAttack)
            {
                if (!visited.Add(current))
                {
                    return true;
                }
            }

            return false;
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

        private static int RequireComponent<T>(GameObject gameObject, string componentName) where T : Component
        {
            return gameObject.GetComponent<T>() == null
                ? LogError(gameObject, $"DevCombat Player is missing {componentName}.")
                : 0;
        }

        private static int LogError(UnityEngine.Object context, string message)
        {
            Debug.LogError(message, context);
            return 1;
        }

        private static int LogWarning(UnityEngine.Object context, string message)
        {
            Debug.LogWarning(message, context);
            return 1;
        }
    }
}
