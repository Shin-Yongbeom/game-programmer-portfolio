using System;
using System.Linq;
using Greyline.Character.EditorTools;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Greyline.World.EditorTools
{
    public static class ProductionPresentationValidation
    {
        public static void Validate()
        {
            var set=AssetDatabase.LoadAssetAtPath<Core.AnimationPresentationSet>(ProductionAnimationAuthoring.SetPath);
            if(set==null || set.Motions.Length!=11)throw new InvalidOperationException("Production presentation set is incomplete.");
            foreach(var motion in set.Motions)
            {
                if(motion.clip==null || motion.contactSeconds<0 || motion.followThroughSeconds<motion.contactSeconds ||
                    motion.endSeconds<=0 || motion.endSeconds<motion.followThroughSeconds || motion.endSeconds>motion.clip.length+.001f)
                    throw new InvalidOperationException("Invalid authored motion landmarks: "+motion.state);
            }
            foreach(string path in new[]{CharacterSandbox.CombatControllerPath,"Assets/_Project/Animators/DistrictEnemy.controller"})
            {
                var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if(controller.layers[0].stateMachine.states.Any(s=>s.state.timeParameterActive))
                    throw new InvalidOperationException("Production states must use natural playback, not time-parameter pose scrubbing: "+path);
                foreach(var state in controller.layers[0].stateMachine.states.Where(s=>set.Find(s.state.name)!=null))
                    if(!state.state.speedParameterActive || state.state.speedParameter!="ActionRate")
                        throw new InvalidOperationException("Timed motion missing native playback rate: "+state.state.name);
            }
            foreach(string pose in new[]{"GuardHold","ChargeRelaxed","ChargeReady","FinisherHeld"})
                if(ProductionAnimationAuthoring.LoadPose(pose)?.humanMotion!=true)throw new InvalidOperationException("Invalid authored hold pose: "+pose);
            foreach (string guid in AssetDatabase.FindAssets("t:CharacterDefinition"))
            {
                var definition = AssetDatabase.LoadAssetAtPath<Core.CharacterDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (AssetDatabase.GetAssetPath(definition.AnimatorController) == CharacterSandbox.CombatControllerPath &&
                    definition.PresentationSet != set)
                    throw new InvalidOperationException("Character definition is missing its presentation set: " + definition.name);
            }
            GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(CharacterSandbox.VisualPrefabPath));
            try
            {
                Animator animator = instance.GetComponentInChildren<Animator>();
                animator.applyRootMotion = false;
                foreach (string name in new[] { "Jab", "Uppercut", "HeavyPunchCombo", "CenterBlock", "LegSweep", "KneeKickLead", "Pushing", "DodgeForward", "DodgeLeft", "CrouchIdle", "CrouchWalk", "KnockdownFaceUp" })
                {
                    var spec = CharacterSandbox.Bundle.First(s => s.State == name);
                    AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(spec.AssetPath).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
                    if (!clip.humanMotion || clip.length <= 0) throw new InvalidOperationException("Invalid presentation clip: " + name);
                    Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                    Transform right = animator.GetBoneTransform(HumanBodyBones.RightHand);
                    Transform left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                    Transform foot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                    string samples = "";
                    for (int i = 0; i <= 10; i++)
                    {
                        // This diagnoses source poses, not controller evaluation. Live bone/IK and
                        // in-place displacement checks belong to ProductionCombatPlayQA.
                        clip.SampleAnimation(instance, clip.length * i / 10f);
                        Vector3 hand = instance.transform.InverseTransformPoint(right.position);
                        Vector3 leftHand = instance.transform.InverseTransformPoint(left.position);
                        Vector3 footPoint = instance.transform.InverseTransformPoint(foot.position);
                        samples += $" {i / 10f:0.0}:{hips.position.y-instance.transform.position.y:0.00}/{hand.y:0.00},{hand.z:0.00}/{leftHand.y:0.00},{leftHand.z:0.00}/{footPoint.y:0.00},{footPoint.z:0.00}";
                    }
                    Debug.Log($"PRESENTATION_POSE_PROBE {name} length={clip.length:0.000} fraction:hipsY/rightHandYZ/leftHandYZ/footYZ{samples}");
                }
            }
            finally { Object.DestroyImmediate(instance); }
            Debug.Log("PRODUCTION_PRESENTATION_VALIDATION_OK: authored landmarks, native playback rates, humanoid holds and source-pose samples.");
        }
    }
}
