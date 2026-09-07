using UnityEngine;

namespace Greyline.Core
{
    public sealed class CharacterPresentation : MonoBehaviour
    {
        private const float VisualFootContactOffset = -0.08f;

        [SerializeField] private CharacterDefinition definition;
        [SerializeField] private Transform visualRoot;

        public CharacterDefinition Definition => definition;
        public Transform VisualRoot => visualRoot;

        private void Awake()
        {
            ApplyVisualRootOffset();
            if (definition != null)
            {
                ApplyDefinition(definition);
            }
        }

        public void Configure(Transform root, CharacterDefinition characterDefinition = null)
        {
            visualRoot = root;
            definition = characterDefinition;
            ApplyVisualRootOffset();
        }

        public void ApplyDefinition(CharacterDefinition characterDefinition)
        {
            definition = characterDefinition;
            if (visualRoot == null || definition == null || definition.VisualPrefab == null)
            {
                return;
            }

            for (int childIndex = visualRoot.childCount - 1; childIndex >= 0; childIndex--)
            {
                Destroy(visualRoot.GetChild(childIndex).gameObject);
            }

            GameObject instantiatedVisual = Instantiate(definition.VisualPrefab, visualRoot);
            instantiatedVisual.name = "CharacterVisual";

            Animator animator = instantiatedVisual.GetComponentInChildren<Animator>();
            if (animator != null && definition.AnimatorController != null)
            {
                animator.runtimeAnimatorController = definition.AnimatorController;
                if (GetComponent<Greyline.Player.ThirdPersonPlayerMotor>() != null)
                {
                    animator.gameObject.AddComponent<CrouchFootPlacement>();
                    animator.gameObject.AddComponent<FinisherPartnerIK>();
                }
            }

            CharacterAnimationDriver animationDriver = GetComponent<CharacterAnimationDriver>();
            if (animationDriver != null)
            {
                animationDriver.Configure(animator);
                animationDriver.ConfigurePresentation(definition.PresentationSet);
            }

            // Presentation swaps destroy CharacterVisual; reactions must follow the stable parent.
            GetComponent<Greyline.Enemies.CombatHitReaction>()?.Configure(visualRoot);
        }

        private void ApplyVisualRootOffset()
        {
            if (visualRoot == null)
            {
                return;
            }

            Vector3 position = visualRoot.localPosition;
            position.y = VisualFootContactOffset;
            visualRoot.localPosition = position;
        }
    }
}
