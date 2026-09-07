using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Greyline.Minigames
{
    /// <summary>Score attack using one raycast per throw; no projectile Rigidbody or shared input asset.</summary>
    public sealed class BalloonDartsGame : MonoBehaviour, IMinigameSession
    {
        [SerializeField] private BalloonDartsConfig config;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private BalloonDartTarget[] targets;
        [SerializeField] private LayerMask targetMask = ~0;
        [SerializeField] private bool startOnAwake;

        private Vector2 desiredAim = new Vector2(.5f, .5f);
        private Vector2 lockedBaseAim = new Vector2(.5f, .5f);
        private float elapsed;
        private float nextThrowAt;
        private float phaseStartedAt;
        private float lockedHorizontal;
        private ThrowPhase throwPhase;
        private int dartsRemaining;
        private int score;
        private int streak;
        private int bestStreak;
        private MinigameState state = MinigameState.Idle;

        public MinigameState State => state;
        public int Score => score;
        public int DartsRemaining => dartsRemaining;
        public event Action<MinigameRewardResult> SessionEnded;

        private void Awake() { if (startOnAwake) StartSession(); }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) ExitSession();
                if (keyboard.rKey.wasPressedThisFrame && state != MinigameState.Running) RestartSession();
                if (keyboard.enterKey.wasPressedThisFrame && state == MinigameState.Idle) StartSession();
                if (keyboard.spaceKey.wasPressedThisFrame && state == MinigameState.Running) ThrowDart();
                if (state == MinigameState.Running)
                {
                    Vector2 direction = Vector2.zero;
                    if (keyboard.leftArrowKey.isPressed) direction.x -= 1f;
                    if (keyboard.rightArrowKey.isPressed) direction.x += 1f;
                    if (keyboard.downArrowKey.isPressed) direction.y -= 1f;
                    if (keyboard.upArrowKey.isPressed) direction.y += 1f;
                    if (throwPhase == ThrowPhase.Targeting) desiredAim += direction * config.aimSpeed * Time.deltaTime;
                }
            }
            if (mouse != null && state == MinigameState.Running && throwPhase == ThrowPhase.Targeting)
                desiredAim = new Vector2(mouse.position.ReadValue().x / Screen.width, mouse.position.ReadValue().y / Screen.height);
            desiredAim = new Vector2(Mathf.Clamp01(desiredAim.x), Mathf.Clamp01(desiredAim.y));
            if (state != MinigameState.Running) return;
            elapsed += Time.deltaTime;
            if (dartsRemaining <= 0 || elapsed >= config.sessionSeconds) End(score >= config.minimumScoreToClear);
        }

        public void Configure(BalloonDartsConfig value, Camera camera, BalloonDartTarget[] balloonTargets)
        {
            config = value;
            aimCamera = camera;
            targets = balloonTargets;
        }

        public void StartSession()
        {
            if (config == null) config = CreateRuntimeDefault();
            elapsed = 0f;
            dartsRemaining = config.dartCount;
            score = 0;
            streak = 0;
            bestStreak = 0;
            desiredAim = new Vector2(.5f, .5f);
            lockedBaseAim = desiredAim;
            nextThrowAt = 0f;
            throwPhase = ThrowPhase.Targeting;
            state = MinigameState.Running;
            if (targets != null) foreach (BalloonDartTarget target in targets) if (target != null) target.gameObject.SetActive(true);
            Debug.Log($"[BalloonDarts] Started: {dartsRemaining} darts, clear score {config.minimumScoreToClear}.", this);
        }

        public void RestartSession() => StartSession();

        public void ExitSession()
        {
            if (state == MinigameState.Success || state == MinigameState.Failed || state == MinigameState.Exited) return;
            state = MinigameState.Exited;
            SessionEnded?.Invoke(new MinigameRewardResult(false, score, bestStreak, string.Empty));
        }

        private void ThrowDart()
        {
            if (throwPhase == ThrowPhase.Targeting)
            {
                lockedBaseAim = desiredAim;
                phaseStartedAt = elapsed;
                throwPhase = ThrowPhase.Horizontal;
                return;
            }

            if (throwPhase == ThrowPhase.Horizontal)
            {
                lockedHorizontal = ReleaseMarker();
                phaseStartedAt = elapsed;
                throwPhase = ThrowPhase.Vertical;
                return;
            }

            if (elapsed < nextThrowAt) return;
            dartsRemaining--;
            BalloonDartTarget hit = FindHitTarget();
            nextThrowAt = elapsed + config.throwCooldownSeconds;
            throwPhase = ThrowPhase.Targeting;
            if (hit == null) { streak = 0; Debug.Log("[BalloonDarts] Miss.", this); return; }
            hit.Pop();
            if (hit.Type == BalloonType.Penalty)
            {
                score = Mathf.Max(0, score - config.penaltyScore);
                streak = 0;
                Debug.Log($"[BalloonDarts] Penalty balloon: score {score}.", this);
                return;
            }
            score += hit.Type == BalloonType.HighValue ? config.highValueScore : config.normalScore;
            streak++;
            bestStreak = Mathf.Max(bestStreak, streak);
            Debug.Log($"[BalloonDarts] {hit.Type} hit: score {score}, streak {streak}.", this);
        }

        private BalloonDartTarget FindHitTarget()
        {
            if (aimCamera == null || targets == null) return null;
            Vector2 aim = EffectiveAim();
            Ray ray = aimCamera.ViewportPointToRay(new Vector3(aim.x, aim.y, 0f));
            RaycastHit[] hits = Physics.RaycastAll(ray, 100f, targetMask, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            BalloonDartTarget result = null;
            foreach (RaycastHit hit in hits)
            {
                BalloonDartTarget candidate = hit.collider.GetComponentInParent<BalloonDartTarget>();
                if (candidate != null && hit.distance < nearest && candidate.gameObject.activeInHierarchy)
                { nearest = hit.distance; result = candidate; }
            }
            return result;
        }

        private void End(bool completed)
        {
            state = completed ? MinigameState.Success : MinigameState.Failed;
            Debug.Log($"[BalloonDarts] Ended: {(completed ? "clear" : "failed")}, score {score}, best streak {bestStreak}.", this);
            SessionEnded?.Invoke(new MinigameRewardResult(completed, score, bestStreak, completed ? config.rewardKey : string.Empty));
        }

        private Vector2 EffectiveAim()
        {
            if (throwPhase == ThrowPhase.Targeting) return desiredAim;
            float horizontal = throwPhase == ThrowPhase.Horizontal ? ReleaseMarker() : lockedHorizontal;
            float vertical = throwPhase == ThrowPhase.Vertical ? ReleaseMarker() : 0f;
            Vector2 offset = new Vector2(horizontal, vertical) * config.maximumReleaseOffset;
            return new Vector2(Mathf.Clamp01(lockedBaseAim.x + offset.x), Mathf.Clamp01(lockedBaseAim.y + offset.y));
        }

        private float ReleaseMarker()
        {
            float cycle = (elapsed - phaseStartedAt) / config.releaseCycleSeconds;
            return Mathf.Sin(cycle * Mathf.PI * 2f);
        }

        private static BalloonDartsConfig CreateRuntimeDefault() => ScriptableObject.CreateInstance<BalloonDartsConfig>();

        private void OnGUI()
        {
            if (state == MinigameState.Idle) { GUI.Box(new Rect(24, 24, 350, 110), "BALLOON DARTS\nENTER: start"); return; }
            GUI.Box(new Rect(24, 24, 430, 155), $"BALLOON DARTS  [{state}]\nScore {score}   Streak {streak}   Best {bestStreak}\nDarts {dartsRemaining}\n{InstructionText()}\nR restart   ESC exit");
            Vector2 aim = EffectiveAim();
            GUI.Label(new Rect(Screen.width * aim.x - 10f, Screen.height * (1f - aim.y) - 10f, 24, 24), "+");
            if (state == MinigameState.Running && throwPhase != ThrowPhase.Targeting)
                DrawTimingMeter(throwPhase == ThrowPhase.Horizontal ? "HORIZONTAL: lock near center" : "VERTICAL: release near center", ReleaseMarker());
            if (state == MinigameState.Success || state == MinigameState.Failed || state == MinigameState.Exited) GUI.Box(new Rect(24, 190, 390, 50), "R: restart");
        }

        private string InstructionText()
        {
            switch (throwPhase)
            {
                case ThrowPhase.Horizontal: return "HORIZONTAL timing: SPACE near center";
                case ThrowPhase.Vertical: return "VERTICAL timing: SPACE near center to throw";
                default: return "MOUSE / ARROWS choose target, then SPACE lock target";
            }
        }

        private static void DrawTimingMeter(string label, float marker)
        {
            const float width = 280f;
            Rect rect = new Rect(24f, 190f, width, 36f);
            GUI.Box(rect, label);
            float markerX = rect.x + 10f + ((marker + 1f) * .5f) * (width - 20f);
            GUI.Label(new Rect(markerX - 4f, rect.y + 17f, 10f, 20f), "|");
            GUI.Label(new Rect(rect.x + width * .5f - 28f, rect.y + 17f, 56f, 20f), "[  ]");
        }

        private enum ThrowPhase { Targeting, Horizontal, Vertical }
    }
}
