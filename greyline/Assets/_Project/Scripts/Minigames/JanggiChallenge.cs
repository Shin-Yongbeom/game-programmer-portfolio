using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Greyline.Minigames
{
    public enum JanggiSide { Blue = 0, Red = 1 }
    public enum JanggiPieceType { Empty = 0, General = 1, Advisor = 2, Chariot = 3, Cannon = 4, Horse = 5, Elephant = 6, Soldier = 7 }
    public enum JanggiChallengeTarget { Checkmate, MaterialLead, Survive, EscapeCheck, CapturePiece, CannonTactic, PalaceTactic }

    [Serializable]
    public struct JanggiPiece
    {
        public JanggiPieceType type;
        public JanggiSide side;
        public bool IsEmpty => type == JanggiPieceType.Empty;
        public JanggiPiece(JanggiPieceType type, JanggiSide side) { this.type = type; this.side = side; }
        public static JanggiPiece Empty => new JanggiPiece(JanggiPieceType.Empty, JanggiSide.Blue);
    }

    [Serializable]
    public struct JanggiMove
    {
        public int from;
        public int to;
        public JanggiPiece captured;
        public JanggiMove(int from, int to, JanggiPiece captured) { this.from = from; this.to = to; this.captured = captured; }
    }

    /// <summary>Small rules-only board. Coordinates are x=0..8, y=0..9; pieces live on intersections.</summary>
    public sealed class JanggiBoard
    {
        public const int Width = 9;
        public const int Height = 10;
        private readonly JanggiPiece[] cells;

        public JanggiBoard() { cells = new JanggiPiece[Width * Height]; for (int i = 0; i < cells.Length; i++) cells[i] = JanggiPiece.Empty; }
        private JanggiBoard(JanggiPiece[] source) { cells = (JanggiPiece[])source.Clone(); }
        public JanggiPiece Get(int x, int y) => InBounds(x, y) ? cells[ToIndex(x, y)] : JanggiPiece.Empty;
        public JanggiPiece Get(int index) => index >= 0 && index < cells.Length ? cells[index] : JanggiPiece.Empty;
        public void Set(int x, int y, JanggiPiece piece) { if (InBounds(x, y)) cells[ToIndex(x, y)] = piece; }
        public JanggiBoard Clone() => new JanggiBoard(cells);

        public static JanggiBoard CreateInitial()
        {
            JanggiBoard board = new JanggiBoard();
            board.Set(4, 1, new JanggiPiece(JanggiPieceType.General, JanggiSide.Blue));
            board.Set(4, 8, new JanggiPiece(JanggiPieceType.General, JanggiSide.Red));
            foreach (int x in new[] { 0, 8 }) { board.Set(x, 0, new JanggiPiece(JanggiPieceType.Chariot, JanggiSide.Blue)); board.Set(x, 9, new JanggiPiece(JanggiPieceType.Chariot, JanggiSide.Red)); }
            foreach (int x in new[] { 3, 5 }) { board.Set(x, 0, new JanggiPiece(JanggiPieceType.Advisor, JanggiSide.Blue)); board.Set(x, 9, new JanggiPiece(JanggiPieceType.Advisor, JanggiSide.Red)); }
            board.Set(1, 0, new JanggiPiece(JanggiPieceType.Elephant, JanggiSide.Blue)); board.Set(7, 0, new JanggiPiece(JanggiPieceType.Elephant, JanggiSide.Blue));
            board.Set(1, 9, new JanggiPiece(JanggiPieceType.Elephant, JanggiSide.Red)); board.Set(7, 9, new JanggiPiece(JanggiPieceType.Elephant, JanggiSide.Red));
            board.Set(2, 0, new JanggiPiece(JanggiPieceType.Horse, JanggiSide.Blue)); board.Set(6, 0, new JanggiPiece(JanggiPieceType.Horse, JanggiSide.Blue));
            board.Set(2, 9, new JanggiPiece(JanggiPieceType.Horse, JanggiSide.Red)); board.Set(6, 9, new JanggiPiece(JanggiPieceType.Horse, JanggiSide.Red));
            board.Set(1, 2, new JanggiPiece(JanggiPieceType.Cannon, JanggiSide.Blue)); board.Set(7, 2, new JanggiPiece(JanggiPieceType.Cannon, JanggiSide.Blue));
            board.Set(1, 7, new JanggiPiece(JanggiPieceType.Cannon, JanggiSide.Red)); board.Set(7, 7, new JanggiPiece(JanggiPieceType.Cannon, JanggiSide.Red));
            for (int x = 0; x < Width; x += 2) { board.Set(x, 3, new JanggiPiece(JanggiPieceType.Soldier, JanggiSide.Blue)); board.Set(x, 6, new JanggiPiece(JanggiPieceType.Soldier, JanggiSide.Red)); }
            return board;
        }

        public static int[] CreateInitialEncoding()
        {
            JanggiBoard board = CreateInitial();
            int[] encoded = new int[Width * Height];
            for (int i = 0; i < encoded.Length; i++) encoded[i] = Encode(board.Get(i));
            return encoded;
        }

        public static JanggiBoard FromEncoding(int[] encoded)
        {
            JanggiBoard board = new JanggiBoard();
            if (encoded == null) return board;
            for (int i = 0; i < Mathf.Min(encoded.Length, board.cells.Length); i++) board.cells[i] = Decode(encoded[i]);
            return board;
        }

        public static int Encode(JanggiPiece piece) => piece.IsEmpty ? 0 : ((int)piece.side + 1) * 10 + (int)piece.type;
        public static JanggiPiece Decode(int value)
        {
            if (value == 0) return JanggiPiece.Empty;
            int type = value % 10;
            int side = value / 10 - 1;
            if (type <= 0 || type > (int)JanggiPieceType.Soldier || side < 0 || side > 1) return JanggiPiece.Empty;
            return new JanggiPiece((JanggiPieceType)type, (JanggiSide)side);
        }

        public List<JanggiMove> GetLegalMoves(JanggiSide side)
        {
            List<JanggiMove> legal = new List<JanggiMove>();
            foreach (JanggiMove move in GeneratePseudoMoves(side))
            {
                JanggiBoard next = Clone();
                next.Apply(move);
                if (!next.IsInCheck(side)) legal.Add(move);
            }
            legal.Sort((a, b) => a.from != b.from ? a.from.CompareTo(b.from) : a.to.CompareTo(b.to));
            return legal;
        }

        public bool TryMove(JanggiSide side, int from, int to, out JanggiMove selected)
        {
            foreach (JanggiMove move in GetLegalMoves(side)) if (move.from == from && move.to == to) { Apply(move); selected = move; return true; }
            selected = default(JanggiMove); return false;
        }

        public void Apply(JanggiMove move) { cells[move.to] = cells[move.from]; cells[move.from] = JanggiPiece.Empty; }

        public bool IsInCheck(JanggiSide side)
        {
            int general = FindGeneral(side);
            if (general < 0) return true;
            foreach (JanggiMove move in GeneratePseudoMoves(Opposite(side))) if (move.to == general) return true;
            if (GeneralsFace()) return true;
            return false;
        }

        public bool IsCheckmate(JanggiSide side) => IsInCheck(side) && GetLegalMoves(side).Count == 0;

        private IEnumerable<JanggiMove> GeneratePseudoMoves(JanggiSide side)
        {
            for (int index = 0; index < cells.Length; index++)
            {
                JanggiPiece piece = cells[index];
                if (piece.IsEmpty || piece.side != side) continue;
                int x = index % Width; int y = index / Width;
                switch (piece.type)
                {
                    case JanggiPieceType.Chariot:
                        foreach (JanggiMove move in Lines(x, y, side, false)) yield return move;
                        foreach (JanggiMove move in PalaceLines(x, y, side, false)) yield return move;
                        break;
                    case JanggiPieceType.Cannon:
                        foreach (JanggiMove move in Lines(x, y, side, true)) yield return move;
                        foreach (JanggiMove move in PalaceLines(x, y, side, true)) yield return move;
                        break;
                    case JanggiPieceType.Horse: foreach (JanggiMove move in HorseMoves(x, y, side)) yield return move; break;
                    case JanggiPieceType.Elephant: foreach (JanggiMove move in ElephantMoves(x, y, side)) yield return move; break;
                    case JanggiPieceType.General:
                    case JanggiPieceType.Advisor: foreach (JanggiMove move in PalaceMoves(x, y, side, piece.type == JanggiPieceType.General)) yield return move; break;
                    case JanggiPieceType.Soldier: foreach (JanggiMove move in SoldierMoves(x, y, side)) yield return move; break;
                }
            }
        }

        private IEnumerable<JanggiMove> Lines(int x, int y, JanggiSide side, bool cannon)
        {
            foreach (Vector2Int d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            {
                bool screen = false;
                for (int step = 1; step < 10; step++)
                {
                    int nx = x + d.x * step; int ny = y + d.y * step;
                    if (!InBounds(nx, ny)) break;
                    JanggiPiece target = Get(nx, ny);
                    if (!cannon)
                    {
                        if (target.IsEmpty) yield return Move(x, y, nx, ny);
                        else { if (target.side != side) yield return Move(x, y, nx, ny); break; }
                    }
                    else if (!screen)
                    {
                        if (target.type == JanggiPieceType.Cannon) break;
                        if (!target.IsEmpty) screen = true;
                    }
                    else
                    {
                        if (target.IsEmpty) yield return Move(x, y, nx, ny);
                        else { if (target.side != side && target.type != JanggiPieceType.Cannon) yield return Move(x, y, nx, ny); break; }
                    }
                }
            }
        }

        private IEnumerable<JanggiMove> HorseMoves(int x, int y, JanggiSide side)
        {
            foreach (Vector2Int d in new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) })
            {
                if (!Get(x + d.x, y + d.y).IsEmpty) continue;
                foreach (Vector2Int diagonal in new[] { new Vector2Int(d.y, d.x), new Vector2Int(-d.y, -d.x) })
                { int nx = x + d.x + diagonal.x; int ny = y + d.y + diagonal.y; if (CanLand(nx, ny, side)) yield return Move(x, y, nx, ny); }
            }
        }

        private IEnumerable<JanggiMove> PalaceLines(int x, int y, JanggiSide side, bool cannon)
        {
            foreach (Vector2Int d in new[] { new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1) })
            {
                bool screen = false;
                for (int step = 1; step < 3; step++)
                {
                    int nx = x + d.x * step; int ny = y + d.y * step;
                    if (!InPalace(nx, ny, side) || !OnPalaceDiagonal(x + d.x * (step - 1), y + d.y * (step - 1), nx, ny)) break;
                    JanggiPiece target = Get(nx, ny);
                    if (!cannon)
                    {
                        if (target.IsEmpty) yield return Move(x, y, nx, ny);
                        else { if (target.side != side) yield return Move(x, y, nx, ny); break; }
                    }
                    else if (!screen)
                    {
                        if (target.type == JanggiPieceType.Cannon) break;
                        if (!target.IsEmpty) screen = true;
                    }
                    else
                    {
                        if (target.IsEmpty) yield return Move(x, y, nx, ny);
                        else { if (target.side != side && target.type != JanggiPieceType.Cannon) yield return Move(x, y, nx, ny); break; }
                    }
                }
            }
        }

        private IEnumerable<JanggiMove> ElephantMoves(int x, int y, JanggiSide side)
        {
            foreach (Vector2Int d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            {
                int ax = x + d.x; int ay = y + d.y; int bx = x + d.x * 2; int by = y + d.y * 2;
                int nx = x + d.x * 3; int ny = y + d.y * 3;
                if (Get(ax, ay).IsEmpty && Get(bx, by).IsEmpty && CanLand(nx, ny, side)) yield return Move(x, y, nx, ny);
            }
        }

        private IEnumerable<JanggiMove> PalaceMoves(int x, int y, JanggiSide side, bool general)
        {
            foreach (Vector2Int d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right, new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1) })
            {
                int nx = x + d.x; int ny = y + d.y;
                if (!InPalace(nx, ny, side)) continue;
                // Generals and advisors may use only the palace's marked diagonals.
                // The general used to bypass this check, which allowed arbitrary
                // diagonal moves from the non-central palace intersections.
                if (Mathf.Abs(d.x) + Mathf.Abs(d.y) == 2 && !OnPalaceDiagonal(x, y, nx, ny)) continue;
                if (CanLand(nx, ny, side)) yield return Move(x, y, nx, ny);
            }
        }

        private IEnumerable<JanggiMove> SoldierMoves(int x, int y, JanggiSide side)
        {
            int forward = side == JanggiSide.Blue ? 1 : -1;
            foreach (Vector2Int d in new[] { new Vector2Int(0, forward), Vector2Int.left, Vector2Int.right })
            { int nx = x + d.x; int ny = y + d.y; if (CanLand(nx, ny, side)) yield return Move(x, y, nx, ny); }
            int palaceY = side == JanggiSide.Blue ? 0 : 9;
            if (y == palaceY || y == palaceY + forward)
                foreach (int nx in new[] { x - 1, x + 1 }) if (InPalace(nx, y + forward, side) && OnPalaceDiagonal(x, y, nx, y + forward) && CanLand(nx, y + forward, side)) yield return Move(x, y, nx, y + forward);
        }

        private bool CanLand(int x, int y, JanggiSide side) => InBounds(x, y) && (Get(x, y).IsEmpty || Get(x, y).side != side);
        private JanggiMove Move(int fx, int fy, int tx, int ty) => new JanggiMove(ToIndex(fx, fy), ToIndex(tx, ty), Get(tx, ty));
        private int FindGeneral(JanggiSide side) { for (int i = 0; i < cells.Length; i++) if (cells[i].type == JanggiPieceType.General && cells[i].side == side) return i; return -1; }
        private bool GeneralsFace()
        {
            int blue = FindGeneral(JanggiSide.Blue); int red = FindGeneral(JanggiSide.Red);
            if (blue < 0 || red < 0 || blue % Width != red % Width) return false;
            int start = Mathf.Min(blue, red) + Width;
            int end = Mathf.Max(blue, red);
            for (int index = start; index < end; index += Width) if (!cells[index].IsEmpty) return false;
            return true;
        }
        private static JanggiSide Opposite(JanggiSide side) => side == JanggiSide.Blue ? JanggiSide.Red : JanggiSide.Blue;
        private static int ToIndex(int x, int y) => y * Width + x;
        private static bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;
        private static bool InPalace(int x, int y, JanggiSide side) => x >= 3 && x <= 5 && (side == JanggiSide.Blue ? y <= 2 : y >= 7);
        private static bool OnPalaceDiagonal(int x1, int y1, int x2, int y2) => (x1 == 4 && (y1 == 1 || y1 == 8)) || (x2 == 4 && (y2 == 1 || y2 == 8)) || (x1 == 3 && x2 == 5) || (x1 == 5 && x2 == 3);
    }

    [Serializable]
    public sealed class JanggiChallengeDefinition : ScriptableObject
    {
        public string challengeId = "janggi.challenge.01";
        public int[] initialBoard = new int[JanggiBoard.Width * JanggiBoard.Height];
        public JanggiSide sideToMove = JanggiSide.Blue;
        public int moveLimit = 6;
        public JanggiChallengeTarget target = JanggiChallengeTarget.Checkmate;
        public JanggiPieceType captureType = JanggiPieceType.Empty;
        public int requiredCaptures = 1;
        public string objective = "Checkmate the opponent.";
        public string hint = "Look for a forcing check.";
        public int hintPieceIndex = -1;
        public int hintDestinationIndex = -1;
        public string rewardId = "";

        public bool Validate(out string error)
        {
            if (initialBoard == null || initialBoard.Length != JanggiBoard.Width * JanggiBoard.Height)
            { error = "initialBoard must contain exactly 90 entries."; return false; }
            int blueGenerals = 0; int redGenerals = 0;
            foreach (int encoded in initialBoard)
            {
                if (encoded != 0 && JanggiBoard.Decode(encoded).IsEmpty)
                { error = "initialBoard contains an invalid encoded piece."; return false; }
                if (encoded == JanggiBoard.Encode(new JanggiPiece(JanggiPieceType.General, JanggiSide.Blue))) blueGenerals++;
                if (encoded == JanggiBoard.Encode(new JanggiPiece(JanggiPieceType.General, JanggiSide.Red))) redGenerals++;
            }
            if (blueGenerals != 1 || redGenerals != 1) { error = "initialBoard must contain exactly one General per side."; return false; }
            if (moveLimit < 1) { error = "moveLimit must be at least 1."; return false; }
            if (requiredCaptures < 1) { error = "requiredCaptures must be at least 1."; return false; }
            if ((target == JanggiChallengeTarget.CapturePiece || target == JanggiChallengeTarget.CannonTactic) && captureType == JanggiPieceType.Empty)
            { error = "CapturePiece and CannonTactic challenges need a captureType."; return false; }
            JanggiBoard board = JanggiBoard.FromEncoding(initialBoard);
            if (board.GetLegalMoves(sideToMove).Count == 0) { error = "sideToMove has no legal moves."; return false; }
            error = string.Empty;
            return true;
        }
    }

    public sealed class JanggiGame : MonoBehaviour
    {
        [SerializeField] private JanggiChallengeDefinition challenge;
        [SerializeField] private JanggiChallengeDefinition[] challengeOptions;
        private JanggiBoard board;
        private JanggiSide playerSide;
        private int selected = -1;
        private int moveCount;
        private List<JanggiMove> legalMoves = new List<JanggiMove>();
        private int failedAttempts;
        private int hintLevel;
        private int playerCaptures;
        private bool lastMoveWasCannonCapture;
        private bool lastMoveEnteredPalace;
        private string message = "ENTER: start challenge";
        private MinigameState state = MinigameState.Idle;

        public MinigameState State => state;
        public void Configure(JanggiChallengeDefinition value) { challenge = value; challengeOptions = null; }
        public void Configure(JanggiChallengeDefinition value, JanggiChallengeDefinition[] options) { challenge = value; challengeOptions = options; }

        private void Update()
        {
            Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame) { state = MinigameState.Exited; message = "Exited."; }
            if (state == MinigameState.Idle && challengeOptions != null)
                for (int i = 0; i < Mathf.Min(8, challengeOptions.Length); i++) if (keyboard[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame && challengeOptions[i] != null) { challenge = challengeOptions[i]; message = challenge.objective; }
            if (keyboard.rKey.wasPressedThisFrame && state != MinigameState.Running) StartChallenge();
            if (keyboard.enterKey.wasPressedThisFrame && state == MinigameState.Idle) StartChallenge();
            if (keyboard.hKey.wasPressedThisFrame && state == MinigameState.Running) ShowHint();
        }

        public void StartChallenge()
        {
            if (challenge == null) { state = MinigameState.Failed; message = "Challenge data is missing."; return; }
            if (!challenge.Validate(out string error)) { state = MinigameState.Failed; message = error; return; }
            board = JanggiBoard.FromEncoding(challenge.initialBoard);
            playerSide = challenge.sideToMove; moveCount = 0; selected = -1; failedAttempts = 0; hintLevel = 0; playerCaptures = 0; lastMoveWasCannonCapture = false; lastMoveEnteredPalace = false; RefreshLegalMoves(); state = MinigameState.Running; message = challenge.objective;
        }

        private void OnGUI()
        {
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * SandboxUiScale());
            if (state == MinigameState.Idle)
            {
                float panelLeft = Screen.width / SandboxUiScale() - 460f;
                GUI.Box(new Rect(panelLeft, 24, 390, 140), "JU-CHE JANGGI\n1-8: select challenge   ENTER: start\n" + (challenge == null ? "No challenge selected." : challenge.challengeId));
                GUI.matrix = previousMatrix;
                return;
            }
            float size = Mathf.Min(48f, (Screen.height - 120f) / 10f);
            float left = Mathf.Max(560f, Screen.width / SandboxUiScale() - 440f);
            float top = 145f;
            GUI.Box(new Rect(left - 20f, 10, 440, 610), $"JU-CHE JANGGI  [{state}]\n{message}\nMove {moveCount}/{(challenge == null ? 0 : challenge.moveLimit)}   H: hint   R: restart");
            if (board != null)
            {
                for (int y = 0; y < JanggiBoard.Height; y++) for (int x = 0; x < JanggiBoard.Width; x++)
                {
                    int index = y * JanggiBoard.Width + x;
                    string label = PieceLabel(board.Get(index));
                    if (GUI.Button(new Rect(left + x * size, top + y * size, size - 2, size - 2), label)) SelectOrMove(index);
                    if (selected == index) GUI.Box(new Rect(left + x * size, top + y * size, size - 2, size - 2), "*");
                    else if (selected >= 0 && IsLegalDestination(index)) GUI.Box(new Rect(left + x * size + 12f, top + y * size + 12f, size - 26f, size - 26f), "·");
                    if (hintLevel >= 2 && index == challenge.hintPieceIndex) GUI.Box(new Rect(left + x * size, top + y * size, size - 2, size - 2), "?");
                    if (hintLevel >= 3 && index == challenge.hintDestinationIndex) GUI.Box(new Rect(left + x * size + 8f, top + y * size + 8f, size - 18f, size - 18f), "+");
                }
            }
            GUI.matrix = previousMatrix;
        }

        private static float SandboxUiScale() => Mathf.Clamp(Mathf.Min(Screen.height / 720f, Screen.width / 1000f), .85f, 1.5f);

        private void SelectOrMove(int index)
        {
            if (state != MinigameState.Running || board == null) return;
            if (selected < 0) { if (!board.Get(index).IsEmpty && board.Get(index).side == playerSide) selected = index; return; }
            if (index == selected) { selected = -1; return; }
            JanggiMove move = default(JanggiMove);
            bool legal = false;
            foreach (JanggiMove candidate in legalMoves) if (candidate.from == selected && candidate.to == index) { move = candidate; legal = true; break; }
            if (legal)
            {
                JanggiPiece movingPiece = board.Get(move.from);
                lastMoveWasCannonCapture = movingPiece.type == JanggiPieceType.Cannon && move.captured.type != JanggiPieceType.Empty;
                lastMoveEnteredPalace = IsInsidePalace(move.to, playerSide);
                if (move.captured.type == challenge.captureType) playerCaptures++;
                board.Apply(move); selected = -1; moveCount++;
                if (CheckTarget()) return;
                if (moveCount >= challenge.moveLimit) { state = MinigameState.Failed; message = "Move limit reached."; return; }
                RefreshLegalMoves();
                if (CheckTarget()) return;
                if (board.IsCheckmate(playerSide)) { state = MinigameState.Failed; message = "The challenge is lost."; }
            }
            else { failedAttempts++; hintLevel = Mathf.Max(hintLevel, failedAttempts >= 2 ? 2 : 0); selected = -1; message = "Illegal move."; }
        }

        private bool CheckTarget()
        {
            JanggiSide opponent = playerSide == JanggiSide.Blue ? JanggiSide.Red : JanggiSide.Blue;
            if (challenge.target == JanggiChallengeTarget.Checkmate && board.IsCheckmate(opponent)) { state = MinigameState.Success; message = "Challenge clear."; return true; }
            if (challenge.target == JanggiChallengeTarget.MaterialLead && Material(board, playerSide) > Material(board, opponent) + 50) { state = MinigameState.Success; message = "Material objective clear."; return true; }
            if (challenge.target == JanggiChallengeTarget.Survive && moveCount >= challenge.moveLimit) { state = MinigameState.Success; message = "Survived the challenge."; return true; }
            if (challenge.target == JanggiChallengeTarget.EscapeCheck && !board.IsInCheck(playerSide)) { state = MinigameState.Success; message = "Escaped check."; return true; }
            if (challenge.target == JanggiChallengeTarget.CapturePiece && playerCaptures >= challenge.requiredCaptures) { state = MinigameState.Success; message = "Target piece captured."; return true; }
            if (challenge.target == JanggiChallengeTarget.CannonTactic && lastMoveWasCannonCapture) { state = MinigameState.Success; message = "Cannon tactic complete."; return true; }
            if (challenge.target == JanggiChallengeTarget.PalaceTactic && lastMoveEnteredPalace) { state = MinigameState.Success; message = "Palace tactic complete."; return true; }
            return false;
        }

        private bool IsLegalDestination(int destination)
        {
            foreach (JanggiMove move in legalMoves) if (move.from == selected && move.to == destination) return true;
            return false;
        }

        private void RefreshLegalMoves()
        {
            legalMoves = board == null ? new List<JanggiMove>() : board.GetLegalMoves(playerSide);
        }

        private void ShowHint()
        {
            hintLevel = Mathf.Min(3, hintLevel + 1);
            if (hintLevel == 1 || challenge.hintPieceIndex < 0) message = challenge.hint;
            else if (hintLevel == 2) message = challenge.hint + " Check the highlighted piece.";
            else message = challenge.hint + " Check the highlighted destination.";
        }

        private static bool IsInsidePalace(int index, JanggiSide side)
        {
            int x = index % JanggiBoard.Width; int y = index / JanggiBoard.Width;
            return x >= 3 && x <= 5 && (side == JanggiSide.Blue ? y <= 2 : y >= 7);
        }

        private static int Material(JanggiBoard value, JanggiSide side)
        {
            int total = 0; for (int i = 0; i < 90; i++) if (!value.Get(i).IsEmpty && value.Get(i).side == side) total += PieceValue(value.Get(i).type); return total;
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

        private static string PieceLabel(JanggiPiece piece)
        {
            if (piece.IsEmpty) return "";
            string[] labels = { "", "王", "士", "車", "包", "馬", "象", "卒" };
            return (piece.side == JanggiSide.Blue ? "楚" : "漢") + labels[(int)piece.type];
        }
    }
}
