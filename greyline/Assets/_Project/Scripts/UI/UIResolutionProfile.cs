using UnityEngine;
using UnityEngine.UI;

namespace Greyline.UI
{
    /// Keeps the root canvas predictable across PC aspect ratios without owning screen layout.
    [RequireComponent(typeof(Canvas))]
    public sealed class UIResolutionProfile : MonoBehaviour
    {
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
        [SerializeField, Range(0f, 1f)] private float matchWidthOrHeight = .5f;

        private void Awake()
        {
            CanvasScaler scaler = GetComponent<CanvasScaler>() ?? gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = matchWidthOrHeight;
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
        }
    }
}
