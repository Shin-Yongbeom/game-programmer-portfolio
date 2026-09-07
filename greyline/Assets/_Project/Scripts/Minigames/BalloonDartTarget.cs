using UnityEngine;

namespace Greyline.Minigames
{
    public sealed class BalloonDartTarget : MonoBehaviour
    {
        [SerializeField] private BalloonType type = BalloonType.Normal;
        public BalloonType Type => type;
        public void Configure(BalloonType value) { type = value; gameObject.SetActive(true); }
        public void Pop() => gameObject.SetActive(false);

        private void OnValidate()
        {
            if (GetComponent<Collider>() == null) Debug.LogWarning("BalloonDartTarget needs a Collider for hit detection.", this);
        }
    }
}
