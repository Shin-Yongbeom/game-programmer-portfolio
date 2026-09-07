using System;
using UnityEngine;

namespace Greyline.Core
{
    [CreateAssetMenu(menuName = "Greyline/Characters/Animation Presentation Set")]
    public sealed class AnimationPresentationSet : ScriptableObject
    {
        [Serializable]
        public sealed class Motion
        {
            public string state;
            public AnimationClip clip;
            [Min(0f)] public float contactSeconds;
            [Min(0f)] public float followThroughSeconds;
            [Min(0f)] public float endSeconds;
            [Min(0f)] public float blendSeconds = .08f;
        }
        [SerializeField] private Motion[] motions = Array.Empty<Motion>();
        public Motion[] Motions => motions;
        public void Configure(Motion[] values) => motions = values;
        public Motion Find(string state)
        {
            foreach (Motion motion in motions) if (motion.state == state) return motion;
            return null;
        }
    }

    /// <summary>Native Animator playback steered toward authored landmarks by rate, never by pose scrubbing.</summary>
    public static class AnimationPhasePlayback
    {
        private static readonly int Rate = Animator.StringToHash("ActionRate");
        public static void Begin(Animator animator, AnimationPresentationSet.Motion motion, float firstPhaseSeconds)
        {
            float firstLandmark = motion.contactSeconds > 0 ? motion.contactSeconds : motion.endSeconds;
            animator.SetFloat(Rate, firstLandmark / Mathf.Max(.01f, firstPhaseSeconds));
            animator.CrossFadeInFixedTime(motion.state, motion.blendSeconds, 0, 0f);
        }
        public static void Advance(Animator animator, AnimationPresentationSet.Motion motion, float landmark, float secondsRemaining)
        {
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(motion.state))
                state = animator.GetNextAnimatorStateInfo(0);
            if (!state.IsName(motion.state)) return;
            float animationTime = state.normalizedTime * motion.clip.length;
            // Stop at the boundary while gameplay owns recovery/cooldown; never replay a contact.
            animator.SetFloat(Rate, Mathf.Clamp((landmark - animationTime) / Mathf.Max(Time.deltaTime, secondsRemaining), 0f, 12f));
        }
    }
}
