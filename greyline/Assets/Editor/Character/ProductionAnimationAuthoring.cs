using System;
using System.Linq;
using Greyline.Core;
using UnityEditor;
using UnityEngine;

namespace Greyline.Character.EditorTools
{
    public static class ProductionAnimationAuthoring
    {
        public const string SetPath = "Assets/_Project/Animators/ProductionPresentation.asset";
        private const string Root = "Assets/_Project/Animators/";
        public static AnimationPresentationSet Build()
        {
            // Source landmarks are authored in frames (30 fps), measured in the source-pose report.
            // These are editable presentation data, separate from AttackDefinition's gameplay windows.
            var set = AssetDatabase.LoadAssetAtPath<AnimationPresentationSet>(SetPath);
            if (set == null) { set = ScriptableObject.CreateInstance<AnimationPresentationSet>(); AssetDatabase.CreateAsset(set, SetPath); }
            int kneeContact = MeasureKneeContact();
            set.Configure(new[] {
                Entry("HeavyPunchCombo", "HeavyPunchCombo", 18, 21, 23),
                Entry("ChargedUppercut", "Uppercut", 16, 23, 40),
                Entry("StrikeJab", "Jab", 14, 17, 18),
                Entry("StrikeHeavy", "HeavyPunchCombo", 18, 21, 23),
                Entry("StrikeSweep", "LegSweep", 23, 33, 52),
                Entry("Finisher", "KneeKickLead", kneeContact, kneeContact + 4, kneeContact + 22),
                Entry("FinisherPush", "Pushing", 20, 24, 40),
                Entry("DodgeForward", "DodgeForward", 0, 0, 46, .035f),
                Entry("DodgeBackward", "DodgeBackward", 0, 0, -1, .035f),
                Entry("DodgeLeft", "DodgeLeft", 0, 0, -1, .035f),
                Entry("DodgeRight", "DodgeRight", 0, 0, -1, .035f)
            });
            EditorUtility.SetDirty(set);
            Pose("GuardHold", "CenterBlock", 21f / 30f);
            Pose("ChargeRelaxed", "HeavyPunchCombo", 1f / 30f);
            Pose("ChargeReady", "HeavyPunchCombo", 7f / 30f);
            Pose("FinisherHeld", "HitReactionLight2", .18f);
            return set;
        }
        public static AnimationClip LoadPose(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + name + ".anim");
        private static int MeasureKneeContact()
        {
            AnimationClip clip = Source("KneeKickLead");
            GameObject probe = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(CharacterSandbox.VisualPrefabPath));
            try
            {
                Animator animator = probe.GetComponentInChildren<Animator>();
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                Transform[] knees = { animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg), animator.GetBoneTransform(HumanBodyBones.RightLowerLeg) };
                float reach = float.NegativeInfinity;
                int contact = 0;
                for (int frame = 0; frame <= Mathf.FloorToInt(clip.length * 30); frame++)
                {
                    clip.SampleAnimation(probe, frame / 30f);
                    foreach (Transform knee in knees)
                    {
                        Vector3 offset = probe.transform.InverseTransformDirection(knee.position - hips.position);
                        if (offset.y < -.25f || offset.z <= reach) continue;
                        reach = offset.z; contact = frame;
                    }
                }
                if (reach < .2f) throw new InvalidOperationException("Knee source does not contain a forward body-height strike.");
                Debug.Log($"FINISHER_KNEE_LANDMARK frame={contact} sourceSeconds={contact / 30f:0.000} reachFromHips={reach:0.000}m");
                return contact;
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); }
        }
        private static AnimationPresentationSet.Motion Entry(string state, string source, int contact, int follow, int end, float blend = .08f)
        {
            AnimationClip clip = Source(source);
            return new AnimationPresentationSet.Motion { state = state, clip = clip,
                contactSeconds = Mathf.Min(contact / 30f, clip.length), followThroughSeconds = Mathf.Min(follow / 30f, clip.length),
                endSeconds = end < 0 ? clip.length : Mathf.Min(end / 30f, clip.length), blendSeconds = blend };
        }
        private static AnimationClip Source(string name) => AssetDatabase.LoadAllAssetsAtPath(CharacterSandbox.Bundle.First(s=>s.State==name).AssetPath)
            .OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
        private static void Pose(string name, string source, float sourceTime)
        {
            AnimationClip original = Source(source);
            string path = Root + name + ".anim";
            AnimationClip pose = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (pose == null) { pose = new AnimationClip(); AssetDatabase.CreateAsset(pose, path); }
            EditorUtility.CopySerialized(original, pose);
            pose.name = name;
            foreach (var binding in AnimationUtility.GetCurveBindings(original))
            {
                float value = AnimationUtility.GetEditorCurve(original,binding).Evaluate(sourceTime);
                AnimationUtility.SetEditorCurve(pose,binding,AnimationCurve.Constant(0f,1f,value));
            }
            AnimationUtility.SetAnimationEvents(pose, Array.Empty<AnimationEvent>());
            var settings = AnimationUtility.GetAnimationClipSettings(pose);
            settings.startTime=0;settings.stopTime=1;settings.loopTime=true;
            AnimationUtility.SetAnimationClipSettings(pose,settings);
            EditorUtility.SetDirty(pose);
        }
    }
}
