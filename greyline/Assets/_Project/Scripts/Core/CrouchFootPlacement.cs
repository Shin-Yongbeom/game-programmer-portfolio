using Greyline.Player;
using UnityEngine;

namespace Greyline.Core
{
    /// <summary>Reconcile the acquired high-hip crouch stride with the low crouch idle, preserving foot targets.</summary>
    [RequireComponent(typeof(Animator))]
    public sealed class CrouchFootPlacement : MonoBehaviour
    {
        private Animator animator;
        private ThirdPersonPlayerMotor motor;
        private float lowering;
        public float BodyLowering => lowering;
        private void Awake()
        {
            animator = GetComponent<Animator>();
            motor = GetComponentInParent<ThirdPersonPlayerMotor>();
        }
        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 || motor == null || !animator.isHuman) return;
            float target = motor.IsCrouching ? Mathf.Clamp01(animator.GetFloat("Speed") / 1.25f) * .34f : 0f;
            lowering = Mathf.Lerp(lowering, target, 1f - Mathf.Exp(-Time.deltaTime * 16f));
            if (lowering < .001f) return;
            // Cache the animated feet before lowering the body. Humanoid IK bends the knees to
            // keep those contacts; simply translating the visual would push the feet underground.
            Vector3 left = animator.GetIKPosition(AvatarIKGoal.LeftFoot);
            Vector3 right = animator.GetIKPosition(AvatarIKGoal.RightFoot);
            animator.bodyPosition -= Vector3.up * lowering;
            animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 1f);
            animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 1f);
            animator.SetIKPosition(AvatarIKGoal.LeftFoot, left);
            animator.SetIKPosition(AvatarIKGoal.RightFoot, right);
        }
    }
}
