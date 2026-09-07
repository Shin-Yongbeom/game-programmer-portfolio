using Greyline.Core;
using UnityEngine;

namespace Greyline.Slice
{
    /// <summary>
    /// Thin Slice01 presentation for the existing bool progression flags. It is deliberately
    /// scene-local: no objective model, quest graph, or new progression state is introduced.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SliceProgressPresentation : MonoBehaviour
    {
        private const string StudentMetFlag = "slice.student.greeted";
        private const string EnemyDownFlag = "slice.enemy_down";

        [SerializeField] private TextMesh statusLabel;
        [SerializeField] private Renderer completionMarker;
        [SerializeField] private Transform followTarget;

        private GameFlagsHost flagsHost;
        private bool loggedCompletion;

        public void Configure(TextMesh label, Renderer marker, Transform target)
        {
            statusLabel = label;
            completionMarker = marker;
            followTarget = target;
        }

        private void OnEnable()
        {
            flagsHost = GameFlagsHost.Current;
            ApplyHudStyle();
            Subscribe();
            UpdatePresentation();
        }

        private void Start()
        {
            if (flagsHost == null)
            {
                flagsHost = GameFlagsHost.Current ?? FindFirstObjectByType<GameFlagsHost>();
                Subscribe();
            }

            UpdatePresentation();
        }

        private void OnDisable()
        {
            if (flagsHost != null)
            {
                flagsHost.Flags.FlagChanged -= OnFlagChanged;
            }
        }

        private void LateUpdate()
        {
            Camera viewer = Camera.main;
            if (statusLabel != null && viewer != null)
            {
                transform.SetPositionAndRotation(
                    viewer.ViewportToWorldPoint(new Vector3(.035f, .94f, 3f)),
                    viewer.transform.rotation);
            }
        }

        private void ApplyHudStyle()
        {
            if (statusLabel == null)
            {
                return;
            }

            statusLabel.anchor = TextAnchor.UpperLeft;
            statusLabel.alignment = TextAlignment.Left;
            statusLabel.fontSize = 26;
            statusLabel.characterSize = .018f;
            statusLabel.fontStyle = FontStyle.Normal;
        }

        private void Subscribe()
        {
            if (flagsHost != null)
            {
                flagsHost.Flags.FlagChanged -= OnFlagChanged;
                flagsHost.Flags.FlagChanged += OnFlagChanged;
            }
        }

        private void OnFlagChanged(string key, bool value)
        {
            UpdatePresentation();
            if (key == EnemyDownFlag && value && !loggedCompletion)
            {
                loggedCompletion = true;
                Debug.Log("[Slice01] Service alley clear — Slice01 complete.", this);
            }
        }

        private void UpdatePresentation()
        {
            GameFlags flags = flagsHost != null ? flagsHost.Flags : null;
            bool studentMet = flags != null && flags.Get(StudentMetFlag);
            bool enemyDown = flags != null && flags.Get(EnemyDownFlag);

            if (statusLabel != null)
            {
                statusLabel.text = enemyDown
                    ? "SLICE COMPLETE"
                    : studentMet
                        ? "Follow the service alley"
                        : "Find the student";
                statusLabel.color = enemyDown ? new Color(0.35f, 1f, 0.45f) : Color.white;
                statusLabel.fontStyle = enemyDown ? FontStyle.Bold : FontStyle.Normal;
            }

            if (completionMarker != null)
            {
                MaterialPropertyBlock properties = new MaterialPropertyBlock();
                completionMarker.GetPropertyBlock(properties);
                Color color = enemyDown ? new Color(0.2f, 1f, 0.3f) : Color.gray;
                properties.SetColor("_BaseColor", color);
                properties.SetColor("_Color", color);
                completionMarker.SetPropertyBlock(properties);
            }
        }
    }
}
