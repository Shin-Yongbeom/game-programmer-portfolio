using UnityEngine;

namespace Greyline.Core
{
    [CreateAssetMenu(fileName = "CharacterDefinition", menuName = "Greyline/Characters/Character Definition")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private GameObject visualPrefab;
        [SerializeField] private RuntimeAnimatorController animatorController;
        [SerializeField] private AnimationPresentationSet presentationSet;
        [SerializeField, Min(0.01f)] private float moveSpeedMultiplier = 1f;

        public string Id => id;
        public string DisplayName => displayName;
        public GameObject VisualPrefab => visualPrefab;
        public RuntimeAnimatorController AnimatorController => animatorController;
        public AnimationPresentationSet PresentationSet => presentationSet;
        public float MoveSpeedMultiplier => moveSpeedMultiplier;

        public void ConfigurePresentation(AnimationPresentationSet set) => presentationSet = set;

        public void Configure(
            string characterId,
            string characterDisplayName,
            GameObject characterVisualPrefab,
            RuntimeAnimatorController characterAnimatorController,
            float speedMultiplier = 1f)
        {
            id = characterId;
            displayName = characterDisplayName;
            visualPrefab = characterVisualPrefab;
            animatorController = characterAnimatorController;
            moveSpeedMultiplier = Mathf.Max(0.01f, speedMultiplier);
        }
    }
}
