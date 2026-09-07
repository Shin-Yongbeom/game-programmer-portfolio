using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Greyline.Minigames
{
    /// <summary>Deterministic timing prototype; no Rigidbody, locomotion, or shared action asset.</summary>
    public sealed class NeolttwigiGame : MonoBehaviour, IMinigameSession
    {
        [SerializeField] private NeolttwigiConfig config;
        [SerializeField] private Transform playerMock;
        [SerializeField] private Transform npcMock;
        [SerializeField] private bool startOnAwake;

        private float elapsed;
        private float nextBeat;
        private int misses;
        private int score;
        private int streak;
        private int bestStreak;
        private MinigameState state = MinigameState.Idle;

        public MinigameState State => state;
        public int Score => score;
        public int Streak => streak;
        public int BestStreak => bestStreak;
        public event Action<MinigameRewardResult> SessionEnded;

        private void Awake() { if (startOnAwake) StartSession(); }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) ExitSession();
                if (keyboard.rKey.wasPressedThisFrame && state != MinigameState.Running) RestartSession();
                if (keyboard.enterKey.wasPressedThisFrame && state == MinigameState.Idle) StartSession();
                if (keyboard.spaceKey.wasPressedThisFrame && state == MinigameState.Running) TryTimingInput();
            }

            if (state != MinigameState.Running) return;
            elapsed += Time.deltaTime;
            UpdateMockTransforms();
            if (elapsed > nextBeat + config.goodWindow) RegisterMiss();
            if (elapsed >= config.sessionSeconds && state == MinigameState.Running) End(true);
        }

        public void Configure(NeolttwigiConfig value, Transform player, Transform npc)
        {
            config = value;
            playerMock = player;
            npcMock = npc;
        }

        public void StartSession()
        {
            if (config == null) config = CreateRuntimeDefault();
            elapsed = 0f;
            nextBeat = config.BeatSecondsAt(0f);
            misses = 0;
            score = 0;
            streak = 0;
            bestStreak = 0;
            state = MinigameState.Running;
            Debug.Log($"[Neolttwigi] started | duration={config.sessionSeconds:0.0}s, miss limit={config.missesToFail}");
        }

        public void RestartSession() => StartSession();

        public void ExitSession()
        {
            if (state == MinigameState.Success || state == MinigameState.Failed || state == MinigameState.Exited) return;
            state = MinigameState.Exited;
            SessionEnded?.Invoke(new MinigameRewardResult(false, score, bestStreak, string.Empty));
        }

        private void TryTimingInput()
        {
            float delta = Mathf.Abs(elapsed - nextBeat);
            if (delta <= config.perfectWindow)
            {
                score += 100 + streak * 5;
                streak++;
                bestStreak = Mathf.Max(bestStreak, streak);
                AdvanceBeat();
                Debug.Log($"[Neolttwigi] perfect | score={score}, streak={streak}");
            }
            else if (delta <= config.goodWindow)
            {
                score += 50 + streak * 2;
                streak++;
                bestStreak = Mathf.Max(bestStreak, streak);
                AdvanceBeat();
                Debug.Log($"[Neolttwigi] good | score={score}, streak={streak}");
            }
            else RegisterMiss();
        }

        private void RegisterMiss()
        {
            misses++;
            streak = 0;
            AdvanceBeat();
            Debug.Log($"[Neolttwigi] miss | misses={misses}/{config.missesToFail}");
            if (misses >= config.missesToFail) End(false);
        }

        private void AdvanceBeat() => nextBeat = AdvanceBeatValue(nextBeat, elapsed, config.goodWindow, config.BeatSecondsAt);

        // Always lands past the current goodWindow, even if a frame hitch (or a huge miss gap)
        // would otherwise require several beats to catch up. Without this, a single stall could
        // leave nextBeat behind for multiple following frames, each independently satisfying the
        // miss condition in Update() and registering one miss per frame for what was really one
        // lapse. Exposed as a pure function so the catch-up behavior is unit-testable without
        // driving Time.deltaTime through the Unity player loop.
        public static float AdvanceBeatValue(float nextBeat, float elapsed, float goodWindow, Func<float, float> beatSecondsAt)
        {
            nextBeat += Mathf.Max(.05f, beatSecondsAt(elapsed));
            while (nextBeat + goodWindow < elapsed) nextBeat += Mathf.Max(.05f, beatSecondsAt(elapsed));
            return nextBeat;
        }

        private void UpdateMockTransforms()
        {
            float interval = Mathf.Max(.1f, config.BeatSecondsAt(elapsed));
            float localBeat = Mathf.Repeat(elapsed / interval, 1f);
            float bounce = Mathf.Sin(localBeat * Mathf.PI) * .8f;
            if (playerMock != null) playerMock.localPosition = new Vector3(-1.05f, .67f + bounce, 0f);
            if (npcMock != null) npcMock.localPosition = new Vector3(1.05f, .67f + Mathf.Max(0f, .8f - bounce), 0f);
        }

        private void End(bool completed)
        {
            state = completed ? MinigameState.Success : MinigameState.Failed;
            Debug.Log($"[Neolttwigi] {(completed ? "complete" : "failed")} | score={score}, best streak={bestStreak}, misses={misses}");
            SessionEnded?.Invoke(new MinigameRewardResult(completed, score, bestStreak, completed ? config.rewardKey : string.Empty));
        }

        private static NeolttwigiConfig CreateRuntimeDefault() => ScriptableObject.CreateInstance<NeolttwigiConfig>();

        private void OnGUI()
        {
            if (state == MinigameState.Idle) { GUI.Box(new Rect(24, 24, 340, 110), "NEOLTTWIGI\nENTER: start"); return; }
            GUI.Box(new Rect(24, 24, 390, 155), $"NEOLTTWIGI  [{state}]\nScore {score}   Streak {streak}   Best {bestStreak}\nMisses {misses}/{config.missesToFail}\nSPACE timing   R restart   ESC exit");
            if (state == MinigameState.Success || state == MinigameState.Failed || state == MinigameState.Exited) GUI.Box(new Rect(24, 190, 390, 50), "R: restart");
        }
    }
}
