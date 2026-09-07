using Greyline.Combat;
using UnityEngine;

namespace Greyline.Core
{
    /// <summary>Keep an unarmed clinch's hands on the live partner while the knee clip plays.</summary>
    [RequireComponent(typeof(Animator))]
    public sealed class FinisherPartnerIK : MonoBehaviour
    {
        private Animator animator;
        private ContextualCombat contextual;
        private void Awake()
        {
            animator = GetComponent<Animator>();
            contextual = GetComponentInParent<ContextualCombat>();
        }
        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 || !animator.isHuman || contextual == null || !contextual.IsBusy ||
                contextual.ActiveState != "Finisher" || contextual.Partner == null || contextual.HasContact) return;
            var partner = contextual.Partner.GetComponentInChildren<Animator>();
            if (partner == null || !partner.isHuman) return;
            // Begin only after approach; the hold releases at contact into the victim's knockdown.
            float weight = Mathf.Clamp01(contextual.StrikeElapsed / .15f) * .8f;
            Transform chest = partner.GetBoneTransform(HumanBodyBones.Chest);
            if (chest == null) return;
            Vector3 centre = chest.position + Vector3.up * .08f;
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, weight);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, weight);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, centre - transform.right * .18f);
            animator.SetIKPosition(AvatarIKGoal.RightHand, centre + transform.right * .18f);
            animator.SetLookAtWeight(weight * .5f);
            animator.SetLookAtPosition(chest.position);
        }
    }
}
