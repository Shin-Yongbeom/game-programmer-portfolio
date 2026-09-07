using Greyline.Combat;
using UnityEngine;

namespace Greyline.Enemies
{
    public sealed class CombatHitReaction : MonoBehaviour, IHitReactionReceiver
    {
        [SerializeField] private Transform visual;
        [SerializeField, Min(0.01f)] private float reactionDuration = .16f;
        [SerializeField, Min(0f)] private float visualOffset = .08f;
        [SerializeField, Min(0f)] private float rotationDegrees = 10f;

        private Vector3 restLocalPosition;
        private Quaternion restLocalRotation;
        private Vector3 reactionLocalPosition;
        private Quaternion reactionLocalRotation;
        private float reactionEndsAt;
        private bool isDead;
        private Animator deathAnimator;
        private Transform[] supportBones;
        private readonly RaycastHit[] groundHits = new RaycastHit[24];
        private bool deathPoseHeld;
        private string deathState = "Death";
        public void PrepareSurfaceDeath() => deathState = "DeathSurface";
        public bool IsDeathPoseHeld => deathPoseHeld;

        public bool IsReacting => Time.time < reactionEndsAt;

        public void Configure(Transform visualRoot)
        {
            visual = visualRoot;
            CacheRestPose();
        }

        private void Awake()
        {
            CacheRestPose();
        }

        private void Update()
        {
            if (visual == null || isDead)
            {
                return;
            }

            float elapsed = reactionDuration - (reactionEndsAt - Time.time);
            if (elapsed >= reactionDuration)
            {
                visual.localPosition = restLocalPosition;
                visual.localRotation = restLocalRotation;
                return;
            }

            float blend = Mathf.Clamp01(elapsed / reactionDuration);
            visual.localPosition = Vector3.Lerp(reactionLocalPosition, restLocalPosition, blend);
            visual.localRotation = Quaternion.Slerp(reactionLocalRotation, restLocalRotation, blend);
        }

        public void RequestHitReaction(DamageInfo damage)
        {
            if (visual == null || isDead)
            {
                return;
            }

            Vector3 localDirection = transform.InverseTransformDirection(-damage.Direction.normalized);
            reactionLocalPosition = restLocalPosition + localDirection * visualOffset;
            reactionLocalRotation = restLocalRotation * Quaternion.Euler(localDirection.z * rotationDegrees, 0f, -localDirection.x * rotationDegrees);
            visual.localPosition = reactionLocalPosition;
            visual.localRotation = reactionLocalRotation;
            reactionEndsAt = Time.time + reactionDuration;
            GetComponent<DeterministicKnockback>()?.Apply(damage.Direction, damage.KnockbackDistance, damage.KnockbackDuration);
        }

        public void RequestDeath()
        {
            if (visual == null || isDead)
            {
                return;
            }

            isDead = true;
            if (GetComponent<EnemyAttack>() != null)
            {
                foreach (Collider collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            }
            visual.localPosition = restLocalPosition;
            visual.localRotation = restLocalRotation;
            Animator animator = visual.GetComponentInChildren<Animator>();
            deathAnimator = animator;
            if (animator != null && animator.HasState(0, Animator.StringToHash(deathState)))
            {
                foreach (AnimatorControllerParameter parameter in animator.parameters)
                    if (parameter.type == AnimatorControllerParameterType.Trigger) animator.ResetTrigger(parameter.nameHash);
                animator.CrossFadeInFixedTime(deathState, .08f, 0, 0f);
                if (animator.isHuman)
                    supportBones = new[] { animator.GetBoneTransform(HumanBodyBones.Hips),
                        animator.GetBoneTransform(HumanBodyBones.Chest), animator.GetBoneTransform(HumanBodyBones.Head),
                        animator.GetBoneTransform(HumanBodyBones.LeftFoot), animator.GetBoneTransform(HumanBodyBones.RightFoot),
                        animator.GetBoneTransform(HumanBodyBones.LeftHand), animator.GetBoneTransform(HumanBodyBones.RightHand) };
            }
            else visual.localRotation = restLocalRotation * Quaternion.Euler(0f, 0f, 85f);
        }

        private void LateUpdate()
        {
            if (!isDead || deathAnimator == null || supportBones == null) return;
            AnimatorStateInfo state = deathAnimator.GetCurrentAnimatorStateInfo(0);
            if (!state.IsName(deathState) || state.normalizedTime < .85f) return;
            // The fall is baked into the pose. Correct only the final visual contact, leaving the
            // actor/controller and authoritative collision movement at their original position.
            Transform lowest = null;
            foreach (Transform bone in supportBones)
                if (bone != null && (lowest == null || bone.position.y < lowest.position.y)) lowest = bone;
            if (lowest != null)
            {
                Vector3 origin = new(lowest.position.x, transform.position.y + 1f, lowest.position.z);
                int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, 3f, ~0, QueryTriggerInteraction.Ignore);
                float nearest = float.PositiveInfinity;
                float ground = 0f;
                for (int i = 0; i < count; i++)
                {
                    var hit = groundHits[i];
                    if (hit.transform.IsChildOf(transform) || hit.transform.GetComponentInParent<IDamageable>() != null || hit.normal.y < .5f) continue;
                    if (hit.distance < nearest) { nearest = hit.distance; ground = hit.point.y; }
                }
                if (!float.IsPositiveInfinity(nearest))
                {
                    float correction = Mathf.Clamp(ground + .07f - lowest.position.y, -.8f, .8f);
                    visual.position += Vector3.up * correction * (1f - Mathf.Exp(-Time.deltaTime * 18f));
                }
            }
            if (!deathPoseHeld && state.normalizedTime >= .99f)
            {
                deathAnimator.speed = 0f;
                deathPoseHeld = true;
            }
        }

        private void CacheRestPose()
        {
            if (visual == null)
            {
                return;
            }

            restLocalPosition = visual.localPosition;
            restLocalRotation = visual.localRotation;
        }
    }
}
