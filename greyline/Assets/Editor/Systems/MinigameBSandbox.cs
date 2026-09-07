using Greyline.Minigames;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Greyline.Systems.EditorTools
{
    /// <summary>Builds Courier and scripted Janggi into one isolated sandbox. Explicit use only.</summary>
    public static class MinigameBSandbox
    {
        public const string ScenePath = "Assets/_Project/Scenes/MinigameBSandbox.unity";
        private const string DataPath = "Assets/_Project/Data/MinigamesB";

        [MenuItem("Greyline/Systems/Build Minigame B Sandbox")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Exit Play Mode before building Minigame B Sandbox."); return; }
            EnsureFolder("Assets/_Project", "Data"); EnsureFolder("Assets/_Project/Data", "MinigamesB"); EnsureFolder("Assets/_Project", "Scenes");
            CourierRunConfig microSd = LoadOrCreate<CourierRunConfig>(DataPath + "/CourierRunConfig.asset");
            microSd.suspicionRecoverySeconds = 2f;
            EditorUtility.SetDirty(microSd);
            JanggiChallengeDefinition challenge01 = CreateChallenge(DataPath + "/JanggiChallenge01.asset", "janggi.challenge.01.mate", JanggiChallengeTarget.Checkmate, 3, "Find the palace mate.", "The chariot can close the last palace file.", PositionMate(), JanggiPieceType.Empty, Index(3, 7), Index(3, 8));
            JanggiChallengeDefinition challenge02 = CreateChallenge(DataPath + "/JanggiChallenge02.asset", "janggi.challenge.02.escape", JanggiChallengeTarget.EscapeCheck, 3, "Escape check.", "Move the General off the attacked file.", PositionEscapeCheck(), JanggiPieceType.Empty, Index(4, 1), Index(3, 1));
            JanggiChallengeDefinition challenge03 = CreateChallenge(DataPath + "/JanggiChallenge03.asset", "janggi.challenge.03.capture", JanggiChallengeTarget.CapturePiece, 3, "Win the exposed chariot.", "The open-file capture is safe.", PositionCaptureMajor(), JanggiPieceType.Chariot, Index(3, 3), Index(3, 4));
            JanggiChallengeDefinition challenge04 = CreateChallenge(DataPath + "/JanggiChallenge04.asset", "janggi.challenge.04.defense", JanggiChallengeTarget.Survive, 4, "Defend for four moves.", "Keep distance from the opposing chariot.", PositionDefense(), JanggiPieceType.Empty, Index(1, 1), Index(1, 2));
            JanggiChallengeDefinition challenge05 = CreateChallenge(DataPath + "/JanggiChallenge05.asset", "janggi.challenge.05.cannon", JanggiChallengeTarget.CannonTactic, 3, "Complete the cannon tactic.", "A screen lets the cannon reach the horse.", PositionCannon(), JanggiPieceType.Horse, Index(1, 2), Index(1, 4));
            JanggiChallengeDefinition challenge06 = CreateChallenge(DataPath + "/JanggiChallenge06.asset", "janggi.challenge.06.palace", JanggiChallengeTarget.PalaceTactic, 3, "Use the palace diagonal.", "A soldier can step diagonally inside the palace.", PositionPalace(), JanggiPieceType.Empty, Index(3, 1), Index(4, 2));
            JanggiChallengeDefinition challenge07 = CreateChallenge(DataPath + "/JanggiChallenge07.asset", "janggi.challenge.07.material", JanggiChallengeTarget.MaterialLead, 4, "Take the material lead.", "The exposed horse is worth the exchange.", PositionMaterial(), JanggiPieceType.Horse, Index(2, 2), Index(2, 3));
            JanggiChallengeDefinition challenge08 = CreateChallenge(DataPath + "/JanggiChallenge08.asset", "janggi.challenge.08.endurance", JanggiChallengeTarget.Survive, 5, "Hold the position for five moves.", "Use the soldiers to keep the file closed.", PositionDefense(), JanggiPieceType.Empty, Index(1, 1), Index(0, 1));

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateLight(); Camera camera = CreateCamera(); CreateGround();

            GameObject microRoot = new GameObject("Courier Run Game");
            GameObject player = CreateMock("Courier Mock", new Vector3(-5f, 1f, -2.5f), PrimitiveType.Capsule);
            player.transform.localScale = Vector3.one * .75f;
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            CharacterController controller = player.AddComponent<CharacterController>(); controller.height = 1.5f; controller.radius = .35f; controller.center = Vector3.zero;
            CourierSandboxMover mover = player.AddComponent<CourierSandboxMover>(); mover.Configure(3.2f, 1.6f);
            GameObject terminal = CreateMock("Puzzle Terminal", new Vector3(-3f, .5f, 2.2f), PrimitiveType.Cube);
            terminal.transform.localScale = new Vector3(.8f, 1f, .8f);
            GameObject exchange = CreateMock("Exchange Point", new Vector3(4f, .5f, 2f), PrimitiveType.Cube);
            exchange.transform.localScale = new Vector3(1.2f, 1f, 1.2f);
            GameObject exit = CreateMock("Exit Point", new Vector3(5.5f, .5f, -2.5f), PrimitiveType.Cube);
            exit.transform.localScale = new Vector3(1.2f, .25f, 1.2f);
            CreateCover("Cover A", new Vector3(-.8f, .75f, .5f), new Vector3(2.2f, 1.5f, .6f));
            CreateCover("Cover B", new Vector3(2.2f, .75f, -.9f), new Vector3(1.4f, 1.5f, .6f));
            CourierWatcher[] observers = new CourierWatcher[3];
            GameObject fixedGuard = CreateMock("Observer Fixed", new Vector3(-1.5f, 1f, 4f), PrimitiveType.Cube);
            FaceTowards(fixedGuard.transform, player.transform.position);
            observers[0] = fixedGuard.AddComponent<CourierWatcher>(); observers[0].Configure(player.transform, 6f, 75f);
            GameObject rotatingGuard = CreateMock("Observer Rotating", new Vector3(2.2f, 1f, 4f), PrimitiveType.Cube);
            rotatingGuard.transform.forward = Vector3.back;
            observers[1] = rotatingGuard.AddComponent<CourierWatcher>(); observers[1].ConfigureRotating(player.transform, 6f, 70f, 32f, 60f);
            GameObject patrolGuard = CreateMock("Observer Patrol", new Vector3(5f, 1f, 1.5f), PrimitiveType.Cube);
            Transform[] patrolPoints = new Transform[]
            {
                CreatePoint("Patrol Point A", new Vector3(5f, 1f, 1.5f)),
                CreatePoint("Patrol Point B", new Vector3(3.2f, 1f, 1.5f)),
                CreatePoint("Patrol Point C", new Vector3(3.2f, 1f, -1.5f))
            };
            observers[2] = patrolGuard.AddComponent<CourierWatcher>(); observers[2].ConfigurePatrol(player.transform, 5.5f, 70f, patrolPoints, 1.35f);
            microRoot.AddComponent<CourierRunGame>().Configure(microSd, observers, player.transform, exchange.transform, mover, exit.transform, terminal.transform);

            GameObject janggiRoot = new GameObject("Janggi Scripted Challenge Game");
            janggiRoot.AddComponent<JanggiGame>().Configure(challenge01, new[] { challenge01, challenge02, challenge03, challenge04, challenge05, challenge06, challenge07, challenge08 });
            Debug.Log("Created sample challenges: " + string.Join(", ", new[] { challenge01.challengeId, challenge02.challengeId, challenge03.challengeId, challenge04.challengeId, challenge05.challengeId, challenge06.challengeId, challenge07.challengeId, challenge08.challengeId }));

            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets();
            Debug.Log("Minigame B sandbox written to " + ScenePath + ". Janggi samples are data assets in " + DataPath + ".");
        }

        [MenuItem("Greyline/Systems/Validate Minigame B Sandbox")]
        public static void Validate()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) { Debug.LogError("Minigame B sandbox scene missing: " + ScenePath); return; }
            Scene scene = SceneManager.GetSceneByPath(ScenePath); bool openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            int errors = 0;
            if (Find<CourierRunGame>(scene) == null) errors++;
            if (Find<JanggiGame>(scene) == null) errors++;
            if (Find<CourierWatcher>(scene) == null) errors++;
            if (Find<CourierSandboxMover>(scene) == null) errors++;
            if (Find<CharacterController>(scene) == null) errors++;
            if (GameObject.Find("Puzzle Terminal") == null || GameObject.Find("Exchange Point") == null || GameObject.Find("Exit Point") == null) errors++;
            if (Object.FindObjectsByType<CourierWatcher>(FindObjectsSortMode.None).Length != 3) errors++;
            if (GameObject.Find("Observer Fixed") == null || GameObject.Find("Observer Rotating") == null || GameObject.Find("Observer Patrol") == null) errors++;
            if (GameObject.Find("Cover A") == null || GameObject.Find("Cover B") == null) errors++;
            CourierRunConfig microSd = AssetDatabase.LoadAssetAtPath<CourierRunConfig>(DataPath + "/CourierRunConfig.asset");
            string microSdError = microSd == null ? "missing" : string.Empty;
            bool microSdValid = microSd != null;
            if (microSdValid) microSdValid = microSd.Validate(out microSdError);
            if (!microSdValid) { errors++; Debug.LogError("Courier config invalid: " + microSdError); }
            foreach (string challengePath in new[] { "/JanggiChallenge01.asset", "/JanggiChallenge02.asset", "/JanggiChallenge03.asset", "/JanggiChallenge04.asset", "/JanggiChallenge05.asset", "/JanggiChallenge06.asset", "/JanggiChallenge07.asset", "/JanggiChallenge08.asset" })
            {
                JanggiChallengeDefinition definition = AssetDatabase.LoadAssetAtPath<JanggiChallengeDefinition>(DataPath + challengePath);
                string challengeError = definition == null ? "missing" : string.Empty;
                bool challengeValid = definition != null;
                if (challengeValid) challengeValid = definition.Validate(out challengeError);
                if (!challengeValid) { errors++; Debug.LogError("Janggi challenge invalid: " + challengePath + " " + challengeError); }
                else if (!HasImmediateGoalLine(definition)) { errors++; Debug.LogError("Janggi challenge has no shallow success line: " + challengePath); }
            }
            if (!RunJanggiSmokeTests()) errors++;
            if (errors == 0) Debug.Log("Minigame B sandbox validation complete. Errors: 0, Warnings: 0.");
            else Debug.LogError("Minigame B sandbox validation complete. Errors: " + errors + ", Warnings: 0.");
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
        }

        public static void BuildFromCommandLine() => Build();
        public static void ValidateFromCommandLine() => Validate();

        private static bool RunJanggiSmokeTests()
        {
            JanggiBoard initial = JanggiBoard.CreateInitial();
            if (initial.GetLegalMoves(JanggiSide.Blue).Count == 0 || initial.GetLegalMoves(JanggiSide.Red).Count == 0)
            { Debug.LogError("Janggi smoke test: initial position has no legal moves."); return false; }
            JanggiBoard palace = new JanggiBoard();
            palace.Set(4, 2, new JanggiPiece(JanggiPieceType.General, JanggiSide.Blue));
            palace.Set(4, 8, new JanggiPiece(JanggiPieceType.General, JanggiSide.Red));
            palace.Set(3, 0, new JanggiPiece(JanggiPieceType.Chariot, JanggiSide.Blue));
            palace.Set(4, 5, new JanggiPiece(JanggiPieceType.Soldier, JanggiSide.Blue));
            bool diagonalFound = false;
            foreach (JanggiMove move in palace.GetLegalMoves(JanggiSide.Blue)) if (move.from == 3 && move.to == 13) diagonalFound = true;
            if (!diagonalFound) { Debug.LogError("Janggi smoke test: palace diagonal chariot move missing."); return false; }
            JanggiBoard check = new JanggiBoard();
            check.Set(4, 1, new JanggiPiece(JanggiPieceType.General, JanggiSide.Blue));
            check.Set(4, 8, new JanggiPiece(JanggiPieceType.General, JanggiSide.Red));
            check.Set(4, 5, new JanggiPiece(JanggiPieceType.Chariot, JanggiSide.Red));
            if (!check.IsInCheck(JanggiSide.Blue)) { Debug.LogError("Janggi smoke test: check detection failed."); return false; }
            if (!RunRuleTests()) return false;
            Debug.Log("Janggi smoke tests passed.");
            return true;
        }

        private static bool RunRuleTests()
        {
            JanggiBoard board = SafeBoard();
            Put(board, 0, 4, JanggiPieceType.Chariot, JanggiSide.Blue);
            if (!HasMove(board, JanggiSide.Blue, 0, 4, 0, 7)) return RuleFailure("chariot movement");

            board = SafeBoard(); Put(board, 2, 4, JanggiPieceType.Horse, JanggiSide.Blue);
            if (!HasMove(board, JanggiSide.Blue, 2, 4, 4, 5)) return RuleFailure("horse movement");
            Put(board, 3, 4, JanggiPieceType.Soldier, JanggiSide.Blue);
            if (HasMove(board, JanggiSide.Blue, 2, 4, 4, 5)) return RuleFailure("horse blocking");

            board = SafeBoard(); Put(board, 1, 4, JanggiPieceType.Elephant, JanggiSide.Blue);
            if (!HasMove(board, JanggiSide.Blue, 1, 4, 4, 7)) return RuleFailure("elephant movement");
            Put(board, 2, 4, JanggiPieceType.Soldier, JanggiSide.Blue);
            if (HasMove(board, JanggiSide.Blue, 1, 4, 4, 7)) return RuleFailure("elephant blocking");

            board = SafeBoard(); Put(board, 1, 4, JanggiPieceType.Cannon, JanggiSide.Blue); Put(board, 1, 5, JanggiPieceType.Soldier, JanggiSide.Blue); Put(board, 1, 6, JanggiPieceType.Horse, JanggiSide.Red);
            if (!HasMove(board, JanggiSide.Blue, 1, 4, 1, 6)) return RuleFailure("cannon screen/capture");
            board.Set(1, 5, JanggiPiece.Empty);
            if (HasMove(board, JanggiSide.Blue, 1, 4, 1, 6)) return RuleFailure("cannon requires screen");
            board.Set(1, 5, new JanggiPiece(JanggiPieceType.Soldier, JanggiSide.Blue)); board.Set(1, 6, new JanggiPiece(JanggiPieceType.Cannon, JanggiSide.Red));
            if (HasMove(board, JanggiSide.Blue, 1, 4, 1, 6)) return RuleFailure("cannon cannot capture cannon");

            board = SafeBoard(); board.Set(0, 0, JanggiPiece.Empty); board.Set(4, 1, new JanggiPiece(JanggiPieceType.General, JanggiSide.Blue));
            if (!HasMove(board, JanggiSide.Blue, 4, 1, 3, 1) || HasMove(board, JanggiSide.Blue, 4, 1, 4, 3)) return RuleFailure("general palace restriction");
            board = SafeBoard(); Put(board, 3, 0, JanggiPieceType.Advisor, JanggiSide.Blue);
            if (!HasMove(board, JanggiSide.Blue, 3, 0, 4, 1) || HasMove(board, JanggiSide.Blue, 3, 0, 2, 0)) return RuleFailure("advisor palace restriction");
            board = SafeBoard(); Put(board, 3, 1, JanggiPieceType.Soldier, JanggiSide.Blue);
            if (!HasMove(board, JanggiSide.Blue, 3, 1, 4, 2)) return RuleFailure("soldier palace diagonal");

            board = SafeBoard(); Put(board, 3, 3, JanggiPieceType.Soldier, JanggiSide.Blue);
            if (!HasMove(board, JanggiSide.Blue, 3, 3, 3, 4) || !HasMove(board, JanggiSide.Blue, 3, 3, 2, 3) || HasMove(board, JanggiSide.Blue, 3, 3, 3, 2)) return RuleFailure("soldier movement");

            board = new JanggiBoard(); board.Set(4, 1, new JanggiPiece(JanggiPieceType.General, JanggiSide.Blue)); board.Set(8, 9, new JanggiPiece(JanggiPieceType.General, JanggiSide.Red)); Put(board, 4, 5, JanggiPieceType.Chariot, JanggiSide.Red);
            foreach (JanggiMove legal in board.GetLegalMoves(JanggiSide.Blue)) { JanggiBoard next = board.Clone(); next.Apply(legal); if (next.IsInCheck(JanggiSide.Blue)) return RuleFailure("legal move while in check"); }

            board = new JanggiBoard(); board.Set(0, 0, new JanggiPiece(JanggiPieceType.General, JanggiSide.Blue)); board.Set(4, 8, new JanggiPiece(JanggiPieceType.General, JanggiSide.Red)); Put(board, 3, 8, JanggiPieceType.Chariot, JanggiSide.Blue); Put(board, 4, 5, JanggiPieceType.Chariot, JanggiSide.Blue); Put(board, 5, 5, JanggiPieceType.Chariot, JanggiSide.Blue); Put(board, 1, 7, JanggiPieceType.Horse, JanggiSide.Blue);
            if (!board.IsCheckmate(JanggiSide.Red)) return RuleFailure("checkmate");

            board = SafeBoard(); Put(board, 0, 4, JanggiPieceType.Chariot, JanggiSide.Blue); Put(board, 0, 6, JanggiPieceType.Horse, JanggiSide.Red);
            JanggiMove capture = default(JanggiMove); bool foundCapture = false;
            foreach (JanggiMove move in board.GetLegalMoves(JanggiSide.Blue)) if (move.from == Index(0, 4) && move.to == Index(0, 6)) { capture = move; foundCapture = true; break; }
            if (!foundCapture) return RuleFailure("capture setup");
            JanggiBoard clone = board.Clone(); clone.Apply(capture);
            if (board.Get(0, 6).type != JanggiPieceType.Horse || clone.Get(0, 6).type != JanggiPieceType.Chariot) return RuleFailure("clone/capture restoration");
            return true;
        }

        private static JanggiBoard SafeBoard()
        {
            JanggiBoard board = new JanggiBoard(); board.Set(0, 0, new JanggiPiece(JanggiPieceType.General, JanggiSide.Blue)); board.Set(8, 9, new JanggiPiece(JanggiPieceType.General, JanggiSide.Red)); return board;
        }

        private static bool HasMove(JanggiBoard board, JanggiSide side, int fromX, int fromY, int toX, int toY)
        {
            foreach (JanggiMove move in board.GetLegalMoves(side)) if (move.from == Index(fromX, fromY) && move.to == Index(toX, toY)) return true;
            return false;
        }

        private static bool RuleFailure(string name) { Debug.LogError("Janggi rule smoke test failed: " + name); return false; }

        private static bool HasImmediateGoalLine(JanggiChallengeDefinition definition)
        {
            JanggiBoard board = JanggiBoard.FromEncoding(definition.initialBoard);
            JanggiSide side = definition.sideToMove;
            JanggiSide opponent = side == JanggiSide.Blue ? JanggiSide.Red : JanggiSide.Blue;
            foreach (JanggiMove move in board.GetLegalMoves(side))
            {
                JanggiPiece moving = board.Get(move.from);
                JanggiBoard next = board.Clone(); next.Apply(move);
                if (definition.target == JanggiChallengeTarget.Checkmate && next.IsCheckmate(opponent)) return true;
                if (definition.target == JanggiChallengeTarget.EscapeCheck && !next.IsInCheck(side)) return true;
                if (definition.target == JanggiChallengeTarget.CapturePiece && move.captured.type == definition.captureType) return true;
                if (definition.target == JanggiChallengeTarget.CannonTactic && moving.type == JanggiPieceType.Cannon && move.captured.type == definition.captureType) return true;
                if (definition.target == JanggiChallengeTarget.PalaceTactic && IsInsidePalace(move.to, side)) return true;
                if (definition.target == JanggiChallengeTarget.MaterialLead && MaterialScore(next, side) > MaterialScore(next, opponent) + 50) return true;
                if (definition.target == JanggiChallengeTarget.Survive) return true;
            }
            return false;
        }

        private static int MaterialScore(JanggiBoard board, JanggiSide side)
        {
            int score = 0;
            for (int i = 0; i < JanggiBoard.Width * JanggiBoard.Height; i++) if (!board.Get(i).IsEmpty && board.Get(i).side == side) score += PieceValue(board.Get(i).type);
            return score;
        }

        private static int PieceValue(JanggiPieceType type) => type switch
        {
            JanggiPieceType.Chariot => 130,
            JanggiPieceType.Cannon => 70,
            JanggiPieceType.Horse => 50,
            JanggiPieceType.Elephant => 30,
            JanggiPieceType.Advisor => 30,
            JanggiPieceType.Soldier => 20,
            _ => 0,
        };

        private static bool IsInsidePalace(int index, JanggiSide side)
        {
            int x = index % JanggiBoard.Width; int y = index / JanggiBoard.Width;
            return x >= 3 && x <= 5 && (side == JanggiSide.Blue ? y <= 2 : y >= 7);
        }

        private static JanggiChallengeDefinition CreateChallenge(string path, string id, JanggiChallengeTarget target, int limit, string objective, string hint, JanggiBoard position, JanggiPieceType captureType, int hintPiece, int hintDestination)
        {
            JanggiChallengeDefinition asset = AssetDatabase.LoadAssetAtPath<JanggiChallengeDefinition>(path);
            if (asset == null) { asset = ScriptableObject.CreateInstance<JanggiChallengeDefinition>(); AssetDatabase.CreateAsset(asset, path); }
            asset.challengeId = id; asset.initialBoard = Encode(position); asset.sideToMove = JanggiSide.Blue; asset.moveLimit = limit; asset.target = target; asset.captureType = captureType; asset.requiredCaptures = 1; asset.objective = objective; asset.hint = hint; asset.hintPieceIndex = hintPiece; asset.hintDestinationIndex = hintDestination; asset.rewardId = "reward." + id;
            EditorUtility.SetDirty(asset); return asset;
        }

        private static int[] Encode(JanggiBoard board)
        {
            int[] encoded = new int[JanggiBoard.Width * JanggiBoard.Height];
            for (int i = 0; i < encoded.Length; i++) encoded[i] = JanggiBoard.Encode(board.Get(i));
            return encoded;
        }

        private static JanggiBoard EmptyPosition(JanggiSide blue = JanggiSide.Blue, JanggiSide red = JanggiSide.Red)
        {
            JanggiBoard board = new JanggiBoard();
            board.Set(0, 0, new JanggiPiece(JanggiPieceType.General, blue));
            board.Set(8, 9, new JanggiPiece(JanggiPieceType.General, red));
            return board;
        }

        private static void Put(JanggiBoard board, int x, int y, JanggiPieceType type, JanggiSide side) { board.Set(x, y, new JanggiPiece(type, side)); }
        private static int Index(int x, int y) { return y * JanggiBoard.Width + x; }

        private static JanggiBoard PositionMate()
        {
            JanggiBoard board = EmptyPosition(); board.Set(8, 9, JanggiPiece.Empty); board.Set(4, 8, new JanggiPiece(JanggiPieceType.General, JanggiSide.Red));
            Put(board, 3, 7, JanggiPieceType.Chariot, JanggiSide.Blue); Put(board, 1, 7, JanggiPieceType.Horse, JanggiSide.Blue);
            foreach (Vector2Int cell in new[] { new Vector2Int(4, 7), new Vector2Int(5, 7), new Vector2Int(5, 8), new Vector2Int(4, 9), new Vector2Int(5, 9) }) Put(board, cell.x, cell.y, JanggiPieceType.Soldier, JanggiSide.Red);
            return board;
        }

        private static JanggiBoard PositionEscapeCheck()
        {
            JanggiBoard board = EmptyPosition(); board.Set(0, 0, JanggiPiece.Empty); board.Set(4, 1, new JanggiPiece(JanggiPieceType.General, JanggiSide.Blue)); Put(board, 4, 5, JanggiPieceType.Chariot, JanggiSide.Red); Put(board, 3, 0, JanggiPieceType.Advisor, JanggiSide.Blue); return board;
        }

        private static JanggiBoard PositionCaptureMajor()
        {
            JanggiBoard board = EmptyPosition(); Put(board, 3, 3, JanggiPieceType.Chariot, JanggiSide.Blue); Put(board, 3, 4, JanggiPieceType.Chariot, JanggiSide.Red); return board;
        }

        private static JanggiBoard PositionDefense()
        {
            JanggiBoard board = EmptyPosition(); Put(board, 1, 1, JanggiPieceType.Chariot, JanggiSide.Blue); Put(board, 7, 8, JanggiPieceType.Chariot, JanggiSide.Red); Put(board, 2, 3, JanggiPieceType.Soldier, JanggiSide.Blue); Put(board, 6, 6, JanggiPieceType.Soldier, JanggiSide.Red); return board;
        }

        private static JanggiBoard PositionCannon()
        {
            JanggiBoard board = EmptyPosition(); Put(board, 1, 2, JanggiPieceType.Cannon, JanggiSide.Blue); Put(board, 1, 3, JanggiPieceType.Soldier, JanggiSide.Blue); Put(board, 1, 4, JanggiPieceType.Horse, JanggiSide.Red); return board;
        }

        private static JanggiBoard PositionPalace()
        {
            JanggiBoard board = EmptyPosition(); board.Set(0, 0, JanggiPiece.Empty); board.Set(4, 1, new JanggiPiece(JanggiPieceType.General, JanggiSide.Blue)); Put(board, 3, 1, JanggiPieceType.Soldier, JanggiSide.Blue); return board;
        }

        private static JanggiBoard PositionMaterial()
        {
            JanggiBoard board = EmptyPosition(); Put(board, 2, 2, JanggiPieceType.Chariot, JanggiSide.Blue); Put(board, 2, 3, JanggiPieceType.Horse, JanggiSide.Red); return board;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); return asset;
        }

        private static GameObject CreateMock(string name, Vector3 position, PrimitiveType type) { GameObject go = GameObject.CreatePrimitive(type); go.name = name; go.transform.position = position; return go; }
        private static GameObject CreateCover(string name, Vector3 position, Vector3 scale) { GameObject go = CreateMock(name, position, PrimitiveType.Cube); go.transform.localScale = scale; return go; }
        private static Transform CreatePoint(string name, Vector3 position) { GameObject go = new GameObject(name); go.transform.position = position; return go.transform; }
        private static void FaceTowards(Transform value, Vector3 target) { Vector3 direction = target - value.position; direction.y = 0f; if (direction.sqrMagnitude > .001f) value.forward = direction.normalized; }
        private static Camera CreateCamera() { GameObject go = new GameObject("Main Camera", typeof(Camera)); go.tag = "MainCamera"; go.transform.SetPositionAndRotation(new Vector3(0f, 3f, -10f), Quaternion.Euler(10f, 0f, 0f)); return go.GetComponent<Camera>(); }
        private static void CreateLight() { GameObject go = new GameObject("Directional Light"); Light light = go.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.2f; go.transform.rotation = Quaternion.Euler(45f, -30f, 0f); }
        private static void CreateGround() { GameObject go = GameObject.CreatePrimitive(PrimitiveType.Plane); go.name = "Sandbox Ground"; go.transform.localScale = Vector3.one * 2f; }
        private static T Find<T>(Scene scene) where T : Component { foreach (GameObject root in scene.GetRootGameObjects()) { T found = root.GetComponentInChildren<T>(true); if (found != null) return found; } return null; }
        private static void EnsureFolder(string parent, string name) { if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name); }
    }
}
