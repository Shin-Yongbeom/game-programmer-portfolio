using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Greyline.Minigames
{
    /// <summary>Small state machine only: observers, suspicion, one timed sequence, exchange, exit.</summary>
    public sealed class CourierRunGame : MonoBehaviour
    {
        [SerializeField] private CourierRunConfig config;
        [SerializeField] private CourierWatcher[] observers;
        [SerializeField] private Transform playerMock;
        [SerializeField] private Transform exchangeMock;
        [SerializeField] private Transform exitMock;
        [SerializeField] private Transform puzzleTerminal;
        [SerializeField] private CourierSandboxMover sandboxMover;

        private CourierState state = CourierState.Inspect;
        private float puzzleTime;
        private float exchangeTime;
        private int suspicion;
        private int puzzleStep;
        private int retries;
        private float detectionPulse;
        private float suspicionRecoveryTimer;
        private bool playerSneaking;
        private string lastMessage = "Check the file and exchange target.";
        private Vector3 exchangeStartPosition;
        private bool hasExchangeStartPosition;
        private Vector3 playerStartPosition;
        private bool hasPlayerStartPosition;

        public CourierState State => state;
        private int DetectionThreshold => Mathf.Max(1, config == null ? 3 : config.detectionThreshold);
        public SuspicionLevel Suspicion => suspicion >= DetectionThreshold ? SuspicionLevel.Detected : suspicion > 0 ? SuspicionLevel.Suspicious : SuspicionLevel.Normal;
        public event Action<CourierResult> SessionEnded;

        private void Awake() { CaptureStartPositions(); }

        public void Configure(CourierRunConfig value, CourierWatcher[] valueObservers, Transform player, Transform exchange)
        {
            Configure(value, valueObservers, player, exchange, null, null, null);
        }

        public void Configure(CourierRunConfig value, CourierWatcher[] valueObservers, Transform player, Transform exchange, CourierSandboxMover mover, Transform exit)
        {
            Configure(value, valueObservers, player, exchange, mover, exit, null);
        }

        public void Configure(CourierRunConfig value, CourierWatcher[] valueObservers, Transform player, Transform exchange, CourierSandboxMover mover, Transform exit, Transform terminal)
        {
            config = value; observers = valueObservers; playerMock = player; exchangeMock = exchange; sandboxMover = mover; exitMock = exit; puzzleTerminal = terminal; CaptureStartPositions();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) Exit();
                if (keyboard.rKey.wasPressedThisFrame) Restart();
                if (keyboard.enterKey.wasPressedThisFrame && state == CourierState.Inspect) BeginSneak();
                if (keyboard.eKey.wasPressedThisFrame && state == CourierState.Sneak) TryBeginPuzzle();
                if (keyboard.spaceKey.wasPressedThisFrame && state == CourierState.Exchange) TryBeginEscape();
                if (keyboard.spaceKey.wasPressedThisFrame && state == CourierState.Escape) TryComplete();
                if (state == CourierState.Puzzle)
                {
                    int nodeCount = config == null ? 5 : Mathf.Clamp(config.nodeCount, 1, 5);
                    for (int i = 0; i < nodeCount; i++) if (keyboard[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) SelectNode(i + 1);
                }
            }
            if (state == CourierState.Sneak || state == CourierState.Puzzle || state == CourierState.Exchange || state == CourierState.Escape)
            {
                if (sandboxMover != null && state != CourierState.Puzzle) sandboxMover.Tick(state == CourierState.Sneak);
                playerSneaking = sandboxMover != null && sandboxMover.IsCrouching;
                foreach (CourierWatcher observer in observers ?? Array.Empty<CourierWatcher>()) if (observer != null) observer.Tick(Time.deltaTime);
                detectionPulse -= Time.deltaTime;
                if (detectionPulse <= 0f) UpdateDetection();
                suspicionRecoveryTimer -= Time.deltaTime;
                if (suspicionRecoveryTimer <= 0f && suspicion > 0)
                {
                    suspicion--;
                    suspicionRecoveryTimer = config == null ? 2f : config.suspicionRecoverySeconds;
                    lastMessage = "The patrol lost your trail.";
                }
            }
            if (state == CourierState.Puzzle)
            {
                puzzleTime -= Time.deltaTime;
                if (puzzleTime <= 0f) PuzzleMistake("Time expired.");
            }
            if (state == CourierState.Exchange)
            {
                exchangeTime += Time.deltaTime;
                if (exchangeTime >= 12f) Fail("The exchange window closed.");
            }
            if (Suspicion == SuspicionLevel.Detected && IsActiveState()) Fail("A guard noticed the movement.");
        }

        public void Restart()
        {
            state = CourierState.Inspect; puzzleTime = 0f; exchangeTime = 0f; suspicion = 0; puzzleStep = 0; retries = 0; detectionPulse = 0f; suspicionRecoveryTimer = 0f; playerSneaking = false; lastMessage = "Check the file and exchange target.";
            if (hasExchangeStartPosition && exchangeMock != null) exchangeMock.position = exchangeStartPosition;
            if (hasPlayerStartPosition && playerMock != null) playerMock.position = playerStartPosition;
            foreach (CourierWatcher observer in observers ?? Array.Empty<CourierWatcher>()) if (observer != null) observer.ResetRuntime();
            Debug.Log("[Courier] Reset: press Enter to begin the route.", this);
        }

        public void Start() => Restart();
        public void Exit() { if (state == CourierState.Success || state == CourierState.Failed || state == CourierState.Exited) return; state = CourierState.Exited; SessionEnded?.Invoke(new CourierResult(false, suspicion, string.Empty)); }

        private void UpdateDetection()
        {
            detectionPulse = .5f;
            if (observers == null || playerMock == null) return;
            foreach (CourierWatcher observer in observers) if (observer != null && observer.CanSeeTarget(playerSneaking))
            {
                suspicion = Mathf.Min(suspicion + 1, DetectionThreshold);
                suspicionRecoveryTimer = config == null ? 2f : config.suspicionRecoverySeconds;
                lastMessage = "A guard is looking this way.";
                break;
            }
        }

        private void BeginSneak()
        {
            state = CourierState.Sneak;
            lastMessage = "Reach the marked terminal and press E.";
            Debug.Log("[Courier] Route started.", this);
        }

        private void TryBeginPuzzle()
        {
            if (config == null) { lastMessage = "Courier config is missing."; Debug.LogError("[Courier] Cannot open puzzle: config missing.", this); return; }
            if (puzzleTerminal == null || playerMock == null) { lastMessage = "Puzzle terminal is unavailable."; Debug.LogError("[Courier] Cannot open puzzle: terminal or player missing.", this); return; }
            float distance = FlatDistance(puzzleTerminal);
            if (distance > config.terminalRadius)
            {
                lastMessage = $"Move closer to the puzzle terminal ({distance:0.0}m / {config.terminalRadius:0.0}m).";
                Debug.Log($"[Courier] Terminal denied: {distance:0.00}m away (needs {config.terminalRadius:0.00}m).", this);
                return;
            }
            state = CourierState.Puzzle; puzzleTime = config.puzzleSeconds; puzzleStep = 0; lastMessage = "Terminal connected. Enter the listed node order.";
            Debug.Log("[Courier] Terminal connected: puzzle started.", this);
        }

        private void SelectNode(int node)
        {
            if (config.correctSequence == null || puzzleStep >= config.correctSequence.Length) return;
            if (node == config.correctSequence[puzzleStep])
            {
                puzzleStep++;
                if (puzzleStep >= Mathf.Min(config.sequenceLength, config.correctSequence.Length))
                {
                    state = CourierState.Exchange; exchangeTime = 0f; lastMessage = "Reach the exchange point. SPACE: exchange.";
                    Debug.Log("[Courier] Puzzle solved: reach exchange point.", this);
                }
                else lastMessage = "Correct. Continue.";
            }
            else PuzzleMistake("Wrong node. Suspicion increased.");
        }

        private void PuzzleMistake(string message)
        {
            int mistakeCost = config == null ? 1 : Mathf.Max(1, config.suspicionPerMistake);
            suspicion = Mathf.Min(suspicion + mistakeCost, DetectionThreshold);
            suspicionRecoveryTimer = config == null ? 2f : config.suspicionRecoverySeconds;
            puzzleStep = 0; retries++; puzzleTime = config == null ? 10f : config.puzzleSeconds; lastMessage = message + " Retry the sequence.";
            if (exchangeMock != null) exchangeMock.position += Vector3.right * .25f;
            Debug.LogWarning($"[Courier] Puzzle mistake: suspicion {suspicion}/{DetectionThreshold}, retry {retries}.", this);
        }

        private void TryBeginEscape()
        {
            if (exchangeMock == null || playerMock == null) { lastMessage = "Exchange point is unavailable."; return; }
            float radius = config == null ? 1.25f : config.exchangeRadius;
            if (!WithinRadius(exchangeMock, radius)) { lastMessage = $"Move closer to the exchange point ({FlatDistance(exchangeMock):0.0}m / {radius:0.0}m)."; return; }
            state = CourierState.Escape; lastMessage = "Exchange complete. Reach the exit. SPACE: leave the area.";
            Debug.Log("[Courier] Exchange completed: reach exit.", this);
        }

        private void TryComplete()
        {
            if (exitMock == null || playerMock == null) { lastMessage = "Exit point is unavailable."; return; }
            float radius = config == null ? 1.25f : config.exitRadius;
            if (!WithinRadius(exitMock, radius)) { lastMessage = $"Reach the marked exit ({FlatDistance(exitMock):0.0}m / {radius:0.0}m)."; return; }
            Complete();
        }

        private void Complete() { state = CourierState.Success; Debug.Log($"[Courier] Complete: suspicion {suspicion}.", this); SessionEnded?.Invoke(new CourierResult(true, suspicion, config == null ? string.Empty : config.rewardKey)); }
        private void Fail(string message) { if (state == CourierState.Success || state == CourierState.Failed) return; state = CourierState.Failed; lastMessage = message; Debug.LogWarning($"[Courier] Failed: {message}", this); SessionEnded?.Invoke(new CourierResult(false, suspicion, string.Empty)); }

        private void OnGUI()
        {
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * SandboxUiScale());
            GUI.Box(new Rect(24, 24, 520, 270), $"MICROSD SMUGGLE  [{state}]\nFile: {(config == null ? "-" : config.fileName)}   Target: {(config == null ? "-" : config.exchangeTarget)}\nSuspicion: {Suspicion} ({suspicion})   Retries: {retries}\nPuzzle: {puzzleStep}/{(config == null ? 3 : config.sequenceLength)}   Time: {Mathf.Max(0f, puzzleTime):0.0}\n{lastMessage}\n{PuzzleNodeText()}\nWASD/arrows move   CTRL sneak   E terminal   1-5 node\nSPACE exchange/exit   R restart   ESC exit");
            GUI.matrix = previousMatrix;
        }

        private static float SandboxUiScale() => Mathf.Clamp(Screen.height / 720f, 1.5f, 1.75f);

        private void CaptureExchangePosition()
        {
            if (exchangeMock == null) return;
            exchangeStartPosition = exchangeMock.position;
            hasExchangeStartPosition = true;
        }

        private void CaptureStartPositions()
        {
            CaptureExchangePosition();
            if (playerMock == null) return;
            playerStartPosition = playerMock.position;
            hasPlayerStartPosition = true;
        }

        private bool WithinRadius(Transform point, float radius)
        {
            return FlatDistance(point) <= radius;
        }

        private float FlatDistance(Transform point)
        {
            if (point == null || playerMock == null) return float.PositiveInfinity;
            Vector3 offset = point.position - playerMock.position;
            offset.y = 0f;
            return offset.magnitude;
        }

        private string PuzzleNodeText()
        {
            if (state != CourierState.Puzzle || config == null) return string.Empty;
            string[] labels = config.puzzleLabels ?? Array.Empty<string>();
            string nodes = "Nodes: ";
            for (int i = 0; i < config.nodeCount; i++) nodes += $"[{i + 1} {(i < labels.Length ? labels[i] : "NODE")}] ";
            return nodes + $"\nInput: {puzzleStep}/{Mathf.Min(config.sequenceLength, config.correctSequence.Length)}";
        }

        private bool IsActiveState()
        {
            return state == CourierState.Sneak || state == CourierState.Puzzle || state == CourierState.Exchange || state == CourierState.Escape;
        }
    }
}
