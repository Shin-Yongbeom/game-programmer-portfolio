using NUnit.Framework;

namespace Greyline.Minigames.Tests
{
    public sealed class JanggiBoardTests
    {
        [Test]
        public void InitialPositionHasLegalMovesForBothSides()
        {
            JanggiBoard board = JanggiBoard.CreateInitial();
            Assert.That(board.GetLegalMoves(JanggiSide.Blue), Is.Not.Empty);
            Assert.That(board.GetLegalMoves(JanggiSide.Red), Is.Not.Empty);
        }

        [Test]
        public void GeneralCannotMoveAlongUnmarkedPalaceDiagonal()
        {
            JanggiBoard board = new JanggiBoard();
            board.Set(3, 1, new JanggiPiece(JanggiPieceType.General, JanggiSide.Blue));
            board.Set(4, 8, new JanggiPiece(JanggiPieceType.General, JanggiSide.Red));

            Assert.That(HasMove(board, JanggiSide.Blue, 3, 1, 4, 2), Is.False);

            board.Set(3, 1, JanggiPiece.Empty);
            board.Set(4, 1, new JanggiPiece(JanggiPieceType.General, JanggiSide.Blue));
            Assert.That(HasMove(board, JanggiSide.Blue, 4, 1, 3, 0), Is.True);
        }

        [Test]
        public void CannonNeedsExactlyOneNonCannonScreen()
        {
            JanggiBoard board = new JanggiBoard();
            board.Set(0, 0, new JanggiPiece(JanggiPieceType.General, JanggiSide.Blue));
            board.Set(8, 9, new JanggiPiece(JanggiPieceType.General, JanggiSide.Red));
            board.Set(1, 4, new JanggiPiece(JanggiPieceType.Cannon, JanggiSide.Blue));
            board.Set(1, 5, new JanggiPiece(JanggiPieceType.Soldier, JanggiSide.Blue));
            board.Set(1, 6, new JanggiPiece(JanggiPieceType.Horse, JanggiSide.Red));

            Assert.That(HasMove(board, JanggiSide.Blue, 1, 4, 1, 6), Is.True);
            board.Set(1, 5, new JanggiPiece(JanggiPieceType.Cannon, JanggiSide.Red));
            Assert.That(HasMove(board, JanggiSide.Blue, 1, 4, 1, 6), Is.False);
        }

        [Test]
        public void CloneAndCaptureDoNotMutateOriginal()
        {
            JanggiBoard board = new JanggiBoard();
            board.Set(0, 0, new JanggiPiece(JanggiPieceType.General, JanggiSide.Blue));
            board.Set(8, 9, new JanggiPiece(JanggiPieceType.General, JanggiSide.Red));
            board.Set(0, 4, new JanggiPiece(JanggiPieceType.Chariot, JanggiSide.Blue));
            board.Set(0, 6, new JanggiPiece(JanggiPieceType.Horse, JanggiSide.Red));

            JanggiBoard clone = board.Clone();
            // TryMove takes flat board indices (y * Width + x), not raw (x, y) pairs.
            int from = 4 * JanggiBoard.Width + 0;
            int to = 6 * JanggiBoard.Width + 0;
            Assert.That(clone.TryMove(JanggiSide.Blue, from, to, out _), Is.True);
            Assert.That(board.Get(0, 4).type, Is.EqualTo(JanggiPieceType.Chariot));
            Assert.That(board.Get(0, 6).type, Is.EqualTo(JanggiPieceType.Horse));
        }

        private static bool HasMove(JanggiBoard board, JanggiSide side, int fromX, int fromY, int toX, int toY)
        {
            int from = fromY * JanggiBoard.Width + fromX;
            int to = toY * JanggiBoard.Width + toX;
            foreach (JanggiMove move in board.GetLegalMoves(side))
                if (move.from == from && move.to == to) return true;
            return false;
        }
    }
}
