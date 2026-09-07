using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Greyline.CameraSystem;
using Greyline.Enemies;
using Greyline.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Greyline.World.EditorTools
{
    /// <summary>Domain-reload-safe real Play/physics/ScreenCapture check. CLI must omit -quit.</summary>
    [InitializeOnLoad]
    public static class ProductionDistrictPlayQA
    {
        private const string ActiveKey = "ProductionDistrictPlayQA.Active";
        private const string ExitKey = "ProductionDistrictPlayQA.ExitEditor";
        private const string FinishingKey = "ProductionDistrictPlayQA.Finishing";
        private const string PassedKey = "ProductionDistrictPlayQA.Passed";
        private enum Phase { Waiting, Settle, Walk, Additional, CaptureSettle, CaptureWait, Finished }

        [Serializable]
        public sealed class Report
        {
            public bool passed;
            public string startedUtc;
            public string finishedUtc;
            public string scene;
            public int routeWaypointsReached;
            public float walkedMetres;
            public float playSeconds;
            public float averageWalkFrameMs;
            public float slowestWalkFrameMs;
            public float simulationStepSeconds;
            public int walkFrames;
            public int captureWidth;
            public int captureHeight;
            public List<string> screenshots = new();
            public List<string> checks = new();
            public List<string> errors = new();
        }

        /// <summary>Optional extension called each frame after walking. Return true when checks finish.</summary>
        public static Func<Report, bool> AdditionalPlayChecks;
        public static Report CurrentReport => report;
        public static string OutputDirectory => Path.GetFullPath(Path.Combine(Application.dataPath, "../Artifacts/ProductionQA"));
        public static bool CaptureEnabled => SessionState.GetBool("ProductionDistrictPlayQA.Capture", false);

        private static Report report;
        private static Phase phase;
        private static Transform player;
        private static CharacterController controller;
        private static ThirdPersonPlayerMotor motor;
        private static ThirdPersonOrbitCamera orbit;
        private static Camera view;
        private static EnemyAttack[] enemies;
        private static bool[] enemyEnabled;
        private static Transform[] vantages;
        private static int waypoint, captureIndex, lastFrame, waitFrames;
        private static double startedAt, lastProgressAt, phaseStartedAt;
        private static Vector3 lastProgressPosition;
        private static string pendingCapture;
        private static float oldTimeScale, elapsedFrames;
        private static float oldCaptureDelta;
        private static double lastTickAt;
        private static float wallFrameDelta;
        private static bool oldRunInBackground;

        static ProductionDistrictPlayQA()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += Tick;
            if (SessionState.GetBool(ActiveKey, false) && SessionState.GetBool(FinishingKey, false) && !EditorApplication.isPlaying)
                EditorApplication.delayCall += CompleteEditorExit;
        }

        public static void RunFromCommandLine() => Start(true);
        public static void RunCombatFromCommandLine() => Start(true, true);
        public static void RunVisualFromCommandLine() => Start(true, false, true);

        [MenuItem("Greyline/World/Run District Play and Visual QA")]
        public static void RunInEditor() => Start(false);

        private static void Start(bool exitEditor, bool skipWalk = false, bool capture = false)
        {
            // Each automated run owns a fresh profile; human progress is never loaded or modified.
            Environment.SetEnvironmentVariable(Progression.ProductionSession.QaSaveRootVariable,
                Path.GetFullPath(Path.Combine(OutputDirectory, "Profiles", Guid.NewGuid().ToString("N"))));
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start district QA from Edit Mode.");
            Directory.CreateDirectory(OutputDirectory);
            EditorSceneManager.OpenScene(ProductionDistrictBuilder.ScenePath, OpenSceneMode.Single);
            if (!Application.isBatchMode) PrepareGameView();
            SessionState.SetBool(ExitKey, exitEditor);
            SessionState.SetBool(FinishingKey, false);
            SessionState.SetBool(PassedKey, false);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool("ProductionDistrictPlayQA.SkipWalk", skipWalk);
            SessionState.SetBool("ProductionDistrictPlayQA.Capture", capture);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) BeginPlay();
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                if (!SessionState.GetBool(FinishingKey, false))
                {
                    report ??= new Report();
                    report.errors.Add("Play Mode ended before QA completed.");
                    WriteReport(false);
                    SessionState.SetBool(FinishingKey, true);
                }
                EditorApplication.delayCall += CompleteEditorExit;
            }
        }

        private static void BeginPlay()
        {
            report = new Report { startedUtc = DateTime.UtcNow.ToString("o"), scene = ProductionDistrictBuilder.ScenePath };
            startedAt = phaseStartedAt = EditorApplication.timeSinceStartup;
            phase = Phase.Settle;
            waypoint = 1;
            captureIndex = 0;
            lastFrame = -1;
            waitFrames = 0;
            elapsedFrames = 0;
            oldTimeScale = Time.timeScale;
            oldCaptureDelta = Time.captureDeltaTime;
            oldRunInBackground = Application.runInBackground;
            Time.timeScale = 1;
            // Log-only batch QA is not a performance benchmark. A fixed simulation clock avoids
            // GPU/headless frame-rate differences changing input/contact scheduling.
            if (Application.isBatchMode) Time.captureDeltaTime = 1f / 60f;
            report.simulationStepSeconds = Time.captureDeltaTime;
            lastTickAt = startedAt;
            Application.runInBackground = true;
            Application.logMessageReceived += OnRuntimeLog;
            try
            {
                GameObject playerObject = GameObject.FindWithTag("Player");
                if (playerObject == null) throw new InvalidOperationException("Player is missing in Play Mode.");
                player = playerObject.transform;
                controller = player.GetComponent<CharacterController>();
                motor = player.GetComponent<ThirdPersonPlayerMotor>();
                view = Camera.main;
                if (controller == null || motor == null || view == null) throw new InvalidOperationException("Player controller, motor, or main camera is missing.");
                orbit = view.GetComponent<ThirdPersonOrbitCamera>();
                motor.enabled = false;
                enemies = Object.FindObjectsByType<EnemyAttack>(FindObjectsSortMode.None);
                enemyEnabled = new bool[enemies.Length];
                for (int i = 0; i < enemies.Length; i++) { enemyEnabled[i] = enemies[i].enabled; enemies[i].enabled = false; }
                Transform vantageRoot = GameObject.Find("QA Vantage Points")?.transform;
                if (vantageRoot == null || vantageRoot.childCount != 6) throw new InvalidOperationException("Six named QA vantage points are required.");
                vantages = new Transform[vantageRoot.childCount];
                for (int i = 0; i < vantages.Length; i++) vantages[i] = vantageRoot.GetChild(i);
                Array.Sort(vantages, (a, b) => string.CompareOrdinal(a.name, b.name));
                controller.enabled = false;
                player.position = ProductionDistrictBuilder.Spawn;
                controller.enabled = true;
                lastProgressPosition = player.position;
                lastProgressAt = startedAt;
                report.routeWaypointsReached = 1;
                report.checks.Add("Entered real Play Mode with CharacterController, motor, camera and six vantage points.");
            }
            catch (Exception exception) { Fail(exception.ToString()); }
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying || report == null || phase == Phase.Finished) return;
            if (EditorApplication.timeSinceStartup - startedAt > 360) { Fail("QA exceeded 360 seconds."); return; }
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            wallFrameDelta = (float)(EditorApplication.timeSinceStartup - lastTickAt);
            lastTickAt = EditorApplication.timeSinceStartup;
            if (report.errors.Count > 0) { Finish(false); return; }
            try
            {
                switch (phase)
                {
                    case Phase.Settle:
                        controller.Move(Vector3.down * .04f);
                        if (++waitFrames >= 45)
                        {
                            phase = SessionState.GetBool("ProductionDistrictPlayQA.SkipWalk", false) ? Phase.Additional : Phase.Walk;
                            if (phase == Phase.Additional) { RestoreEnemies(); report.checks.Add("Focused combat regression run; traversal intentionally skipped."); }
                            lastProgressAt = EditorApplication.timeSinceStartup;
                        }
                        break;
                    case Phase.Walk: Walk(); break;
                    case Phase.Additional:
                        if (AdditionalPlayChecks == null || AdditionalPlayChecks(report)) BeginCaptures();
                        break;
                    case Phase.CaptureSettle:
                        HoldCaptureCamera();
                        if (++waitFrames >= 18)
                        {
                            pendingCapture = Path.Combine(OutputDirectory, vantages[captureIndex].name + ".png");
                            // A stale prior artifact must never satisfy the current capture's completion check.
                            if (File.Exists(pendingCapture)) File.Delete(pendingCapture);
                            ScreenCapture.CaptureScreenshot(pendingCapture);
                            phase = Phase.CaptureWait;
                            phaseStartedAt = EditorApplication.timeSinceStartup;
                        }
                        break;
                    case Phase.CaptureWait:
                        HoldCaptureCamera();
                        if (CaptureComplete())
                        {
                            report.screenshots.Add(pendingCapture);
                            captureIndex++;
                            if (captureIndex == vantages.Length) Finish(true);
                            else { waitFrames = 0; phase = Phase.CaptureSettle; }
                        }
                        else if (EditorApplication.timeSinceStartup - phaseStartedAt > 25) Fail("Screenshot was not written: " + pendingCapture);
                        break;
                }
            }
            catch (Exception exception) { Fail(exception.ToString()); }
        }

        private static void Walk()
        {
            float dt = Mathf.Clamp(Time.deltaTime, .001f, .05f);
            Vector3 offset = ProductionDistrictBuilder.WalkRoute[waypoint] - player.position;
            offset.y = 0;
            if (offset.magnitude < .3f)
            {
                report.routeWaypointsReached++;
                waypoint++;
                if (waypoint == ProductionDistrictBuilder.WalkRoute.Length)
                {
                    report.checks.Add("Completed connected district loop through both repair-hall doorways using real standing collision.");
                    RestoreEnemies();
                    phase = Phase.Additional;
                    return;
                }
                offset = ProductionDistrictBuilder.WalkRoute[waypoint] - player.position;
                offset.y = 0;
            }
            Vector3 before = player.position;
            Vector3 displacement = Vector3.ClampMagnitude(offset, 9 * dt);
            controller.Move(displacement + Vector3.down * (9 * dt));
            if (displacement.sqrMagnitude > .0001f) player.rotation = Quaternion.LookRotation(displacement);
            Vector3 actual = player.position - before;
            actual.y = 0;
            report.walkedMetres += actual.magnitude;
            report.walkFrames++;
            elapsedFrames += wallFrameDelta;
            report.slowestWalkFrameMs = Mathf.Max(report.slowestWalkFrameMs, wallFrameDelta * 1000);
            if (player.position.y < -.8f || player.position.y > 1.3f) { Fail("Standing route fell or climbed unexpectedly at " + player.position); return; }
            if (Vector3.Distance(lastProgressPosition, player.position) > .5f)
            {
                lastProgressPosition = player.position;
                lastProgressAt = EditorApplication.timeSinceStartup;
            }
            else if (EditorApplication.timeSinceStartup - lastProgressAt > 4) Fail("Standing controller stalled toward waypoint " + waypoint + " at " + player.position);
        }

        private static void BeginCaptures()
        {
            if (!CaptureEnabled)
            {
                report.checks.Add("Screenshots disabled by default; visual quality and feel are assigned to human review.");
                Finish(true);
                return;
            }
            // The final combat regression reloads the scene through Backspace.
            // Optional captures must bind the replacement scene instead of destroyed objects.
            player = GameObject.FindWithTag("Player").transform;
            controller = player.GetComponent<CharacterController>();
            motor = player.GetComponent<ThirdPersonPlayerMotor>();
            view = Camera.main;
            orbit = view.GetComponent<ThirdPersonOrbitCamera>();
            Transform vantageRoot = GameObject.Find("QA Vantage Points").transform;
            vantages = new Transform[vantageRoot.childCount];
            for (int i = 0; i < vantages.Length; i++) vantages[i] = vantageRoot.GetChild(i);
            Array.Sort(vantages, (a, b) => string.CompareOrdinal(a.name, b.name));
            if (orbit != null) orbit.enabled = false;
            phase = Phase.CaptureSettle;
            waitFrames = 0;
            HoldCaptureCamera();
        }

        private static void HoldCaptureCamera()
        {
            view.transform.SetPositionAndRotation(vantages[captureIndex].position, vantages[captureIndex].rotation);
        }

        private static bool CaptureComplete()
        {
            if (!File.Exists(pendingCapture)) return false;
            var info = new FileInfo(pendingCapture);
            if (info.Length < 1024) return false;
            byte[] header = new byte[24];
            using (FileStream stream = File.Open(pendingCapture, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (stream.Read(header, 0, header.Length) != header.Length) return false;
                byte[] tail = new byte[12];
                stream.Seek(-12, SeekOrigin.End);
                if (stream.Read(tail, 0, tail.Length) != tail.Length || tail[4] != 73 || tail[5] != 69 || tail[6] != 78 || tail[7] != 68) return false;
            }
            if (header[0] != 137 || header[1] != 80 || header[2] != 78 || header[3] != 71) return false;
            report.captureWidth = BigEndian(header, 16);
            report.captureHeight = BigEndian(header, 20);
            if (report.captureWidth < 640 || report.captureHeight < 360) throw new InvalidOperationException("Game View capture is too small for visual QA.");
            return true;
        }

        private static int BigEndian(byte[] data, int offset) => data[offset] << 24 | data[offset + 1] << 16 | data[offset + 2] << 8 | data[offset + 3];
        private static void OnRuntimeLog(string message, string trace, LogType type)
        {
            if (report == null || phase == Phase.Finished) return;
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) report.errors.Add(message + "\n" + trace);
        }
        private static void Fail(string message) { report.errors.Add(message); Finish(false); }
        private static void RestoreEnemies()
        {
            if (enemies == null) return;
            for (int i = 0; i < enemies.Length; i++) if (enemies[i] != null) enemies[i].enabled = enemyEnabled[i];
        }

        private static void Finish(bool success)
        {
            if (phase == Phase.Finished) return;
            phase = Phase.Finished;
            Application.logMessageReceived -= OnRuntimeLog;
            RestoreEnemies();
            if (motor != null) motor.enabled = true;
            if (orbit != null) orbit.enabled = true;
            Time.timeScale = oldTimeScale;
            Time.captureDeltaTime = oldCaptureDelta;
            Application.runInBackground = oldRunInBackground;
            WriteReport(success && report.errors.Count == 0);
            SessionState.SetBool(FinishingKey, true);
            EditorApplication.isPlaying = false;
        }

        private static void WriteReport(bool passed)
        {
            report.passed = passed;
            report.finishedUtc = DateTime.UtcNow.ToString("o");
            report.playSeconds = (float)(EditorApplication.timeSinceStartup - startedAt);
            report.averageWalkFrameMs = report.walkFrames == 0 ? 0 : elapsedFrames / report.walkFrames * 1000;
            Directory.CreateDirectory(OutputDirectory);
            File.WriteAllText(Path.Combine(OutputDirectory, "district-play-qa.json"), JsonUtility.ToJson(report, true));
            SessionState.SetBool(PassedKey, passed);
            string marker = passed ? "PRODUCTION_DISTRICT_PLAY_QA_OK" : "PRODUCTION_DISTRICT_PLAY_QA_FAILED";
            Debug.Log(marker + $" route={report.routeWaypointsReached}/{ProductionDistrictBuilder.WalkRoute.Length} screenshots={report.screenshots.Count} report=" + OutputDirectory);
        }

        private static void CompleteEditorExit()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            bool passed = SessionState.GetBool(PassedKey, false);
            bool exit = SessionState.GetBool(ExitKey, false);
            Debug.Log($"PRODUCTION_QA_EDITOR_EXIT requested={exit} passed={passed}");
            Environment.SetEnvironmentVariable(Progression.ProductionSession.QaSaveRootVariable, null);
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetBool(FinishingKey, false);
            if (exit) EditorApplication.Exit(passed ? 0 : 1);
        }

        private static void PrepareGameView()
        {
            // Unity's public Game View API exposes no resolution selector. Keep this small optional adapter isolated.
            Assembly assembly = typeof(Editor).Assembly;
            Type gameViewType = assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null) return;
            EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
            gameView.position = new Rect(80, 80, 1280, 760);
            gameView.Focus();
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                Type sizesType = assembly.GetType("UnityEditor.GameViewSizes");
                Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                object sizes = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                Type groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
                object group = sizesType.GetMethod("GetGroup", flags).Invoke(sizes, new[] { Enum.Parse(groupType, "Standalone") });
                Type sizeType = assembly.GetType("UnityEditor.GameViewSize");
                Type sizeKind = assembly.GetType("UnityEditor.GameViewSizeType");
                object custom = Activator.CreateInstance(sizeType, flags, null,
                    new[] { Enum.Parse(sizeKind, "FixedResolution"), (object)1280, 720, "District Play QA" }, null);
                int total = (int)group.GetType().GetMethod("GetTotalCount", flags).Invoke(group, null);
                MethodInfo getSize = group.GetType().GetMethod("GetGameViewSize", flags);
                for (int index = 0; index < total; index++)
                {
                    object existing = getSize.Invoke(group, new object[] { index });
                    string label = sizeType.GetProperty("baseText", flags)?.GetValue(existing) as string;
                    if (label != "District Play QA") continue;
                    gameViewType.GetProperty("selectedSizeIndex", flags).SetValue(gameView, index);
                    return;
                }
                group.GetType().GetMethod("AddCustomSize", flags).Invoke(group, new[] { custom });
                gameViewType.GetProperty("selectedSizeIndex", flags).SetValue(gameView, total);
            }
            catch (Exception exception) { Debug.LogWarning("District QA uses the available Game View size: " + exception.Message); }
        }
    }
}
