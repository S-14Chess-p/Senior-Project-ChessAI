using System.Text;

namespace ChessAI.Models
{
    [Serializable]
    public class Board
    {
        public Piece[][] Squares { get; set; } = new Piece[8][];

        public Board()
        {
            // Initialize each row in the jagged array
            for (int i = 0; i < 8; i++)
            {
                Squares[i] = new Piece[8];
            }
            InitializeBoard();
        }

        private void InitializeBoard()
        {
            /*
             * [0,0] is top-left corner from white's perspective(A8)
             * [0,7] is top-right corner from white's perspective (H8)
             * [7,0] is bottom-left corner from white's perspective (A1) 
             * [7,7] is bottom-right corner from white's perspective (H1)
            */
            // Initialize pawns
            for (int i = 0; i < 8; i++)
            {
                Squares[6][i] = new Pawn { IsWhite = true, Position = (6, i) };
                Squares[1][i] = new Pawn { IsWhite = false, Position = (1, i) };
            }

            // Initialize rooks
            Squares[7][0] = new Rook { IsWhite = true, Position = (7, 0) };//A1
            Squares[7][7] = new Rook { IsWhite = true, Position = (7, 7) };//H1
            Squares[0][0] = new Rook { IsWhite = false, Position = (0, 0) };//A8
            Squares[0][7] = new Rook { IsWhite = false, Position = (0, 7) };//H8

            // Initialize knights
            Squares[7][1] = new Knight { IsWhite = true, Position = (7, 1) };//B1
            Squares[7][6] = new Knight { IsWhite = true, Position = (7, 6) };//G1
            Squares[0][1] = new Knight { IsWhite = false, Position = (0, 1) };//B8
            Squares[0][6] = new Knight { IsWhite = false, Position = (0, 6) };//G8

            // Initialize bishops
            Squares[7][2] = new Bishop { IsWhite = true, Position = (7, 2) };//C1
            Squares[7][5] = new Bishop { IsWhite = true, Position = (7, 5) };//F1
            Squares[0][2] = new Bishop { IsWhite = false, Position = (0, 2) };//C8
            Squares[0][5] = new Bishop { IsWhite = false, Position = (0, 5) };//F8

            // Initialize queens
            Squares[7][3] = new Queen { IsWhite = true, Position = (7, 3) };//D1
            Squares[0][3] = new Queen { IsWhite = false, Position = (0, 3) };//D8

            // Initialize kings
            Squares[7][4] = new King { IsWhite = true, Position = (7, 4) };//E1
            Squares[0][4] = new King { IsWhite = false, Position = (0, 4) };//E8
        }

        // Clone method for deep copying the board
        public Board Clone()
        {
            var newBoard = new Board();

            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    if (Squares[row][col] != null)
                    {
                        newBoard.Squares[row][col] = Squares[row][col].Clone();
                    }
                    else
                    {
                        newBoard.Squares[row][col] = null;
                    }
                }
            }

            return newBoard;
        }

        public string GenerateFEN(bool isWhiteTurn, int halfMoveClock, int fullMoveNumber)
        {
            StringBuilder fen = new();

            // Piece positions
            for (int row = 0; row < 8; row++)
            {
                int emptyCount = 0;
                for (int col = 0; col < 8; col++)
                {
                    var piece = Squares[row][col];
                    if (piece == null)
                    {
                        emptyCount++;
                    }
                    else
                    {
                        if (emptyCount > 0)
                        {
                            fen.Append(emptyCount);
                            emptyCount = 0;
                        }

                        char pieceChar = piece switch
                        {
                            Pawn p => p.IsWhite ? 'P' : 'p',
                            Rook r => r.IsWhite ? 'R' : 'r',
                            Knight n => n.IsWhite ? 'N' : 'n',
                            Bishop b => b.IsWhite ? 'B' : 'b',
                            Queen q => q.IsWhite ? 'Q' : 'q',
                            King k => k.IsWhite ? 'K' : 'k',
                            _ => throw new Exception("Unknown piece type")
                        };
                        fen.Append(pieceChar);
                    }
                }

                if (emptyCount > 0)
                {
                    fen.Append(emptyCount);
                }

                if (row < 7)
                {
                    fen.Append('/');
                }
            }

            fen.Append(isWhiteTurn ? " w " : " b ");

            // Castling availability
            string castling = "";
            bool whiteKingside = Squares[7][4] is King whiteKing && !whiteKing.HasMoved && Squares[7][7] is Rook whiteRookH1 && !whiteRookH1.HasMoved;
            bool whiteQueenside = Squares[7][4] is King && !((King)Squares[7][4]).HasMoved && Squares[7][0] is Rook whiteRookA1 && !whiteRookA1.HasMoved;
            bool blackKingside = Squares[0][4] is King blackKing && !blackKing.HasMoved && Squares[0][7] is Rook blackRookH8 && !blackRookH8.HasMoved;
            bool blackQueenside = Squares[0][4] is King && !((King)Squares[0][4]).HasMoved && Squares[0][0] is Rook blackRookA8 && !blackRookA8.HasMoved;

            if (whiteKingside) castling += "K";
            if (whiteQueenside) castling += "Q";
            if (blackKingside) castling += "k";
            if (blackQueenside) castling += "q";

            fen.Append(string.IsNullOrEmpty(castling) ? "-" : castling);
            fen.Append(' ');

            // En passant target square
            string enPassant = "-";
            foreach (var rowPieces in Squares)
            {
                foreach (var p in rowPieces)
                {
                    if (p is Pawn pawn && pawn.EnPassantEligible)
                    {
                        enPassant = $"{(char)('a' + pawn.Position.Col)}{8 - pawn.Position.Row}";
                        break;
                    }
                }
                if (enPassant != "-") break;
            }
            fen.Append(enPassant);
            fen.Append(' ');

            // Halfmove clock and Fullmove number
            fen.Append($"{halfMoveClock} {fullMoveNumber}");

            return fen.ToString();
        }

        /// Generates the FEN string excluding HalfMoveClock and FullMoveNumber for repetition tracking.
        public string GenerateRepetitionFEN(bool isWhiteTurn)
        {
            StringBuilder fen = new();

            // Piece positions
            for (int row = 0; row < 8; row++)
            {
                int emptyCount = 0;
                for (int col = 0; col < 8; col++)
                {
                    var piece = Squares[row][col];
                    if (piece == null)
                    {
                        emptyCount++;
                    }
                    else
                    {
                        if (emptyCount > 0)
                        {
                            fen.Append(emptyCount);
                            emptyCount = 0;
                        }

                        char pieceChar = piece switch
                        {
                            Pawn p => p.IsWhite ? 'P' : 'p',
                            Rook r => r.IsWhite ? 'R' : 'r',
                            Knight n => n.IsWhite ? 'N' : 'n',
                            Bishop b => b.IsWhite ? 'B' : 'b',
                            Queen q => q.IsWhite ? 'Q' : 'q',
                            King k => k.IsWhite ? 'K' : 'k',
                            _ => throw new Exception("Unknown piece type")
                        };
                        fen.Append(pieceChar);
                    }
                }

                if (emptyCount > 0)
                {
                    fen.Append(emptyCount);
                }

                if (row < 7)
                {
                    fen.Append('/');
                }
            }

            fen.Append(isWhiteTurn ? " w " : " b ");

            // Castling availability
            string castling = "";
            bool whiteKingside = Squares[7][4] is King whiteKingForCastling && !whiteKingForCastling.HasMoved && Squares[7][7] is Rook whiteRookH1ForCastling && !whiteRookH1ForCastling.HasMoved;
            bool whiteQueenside = Squares[7][4] is King && !((King)Squares[7][4]).HasMoved && Squares[7][0] is Rook whiteRookA1ForCastling && !whiteRookA1ForCastling.HasMoved;
            bool blackKingside = Squares[0][4] is King blackKingForCastling && !blackKingForCastling.HasMoved && Squares[0][7] is Rook blackRookH8ForCastling && !blackRookH8ForCastling.HasMoved;
            bool blackQueenside = Squares[0][4] is King && !((King)Squares[0][4]).HasMoved && Squares[0][0] is Rook blackRookA8ForCastling && !blackRookA8ForCastling.HasMoved;

            if (whiteKingside) castling += "K";
            if (whiteQueenside) castling += "Q";
            if (blackKingside) castling += "k";
            if (blackQueenside) castling += "q";

            fen.Append(string.IsNullOrEmpty(castling) ? "-" : castling);
            fen.Append(' ');

            // En passant target square
            string enPassant = "-";
            foreach (var rowPieces in Squares)
            {
                foreach (var p in rowPieces)
                {
                    if (p is Pawn pawn && pawn.EnPassantEligible)
                    {
                        enPassant = $"{(char)('a' + pawn.Position.Col)}{8 - pawn.Position.Row}";
                        break;
                    }
                }
                if (enPassant != "-") break;
            }
            fen.Append(enPassant);


            return fen.ToString();
        }

        public bool IsEmpty(int row, int col)
        {
            return IsWithinBounds(row, col) && Squares[row][col] == null;
        }

        public bool IsEnemyPiece(int row, int col, bool isWhite)
        {
            return IsWithinBounds(row, col) && Squares[row][col]?.IsWhite == !isWhite;
        }

        public bool IsWithinBounds(int row, int col)
        {
            return row >= 0 && row < 8 && col >= 0 && col < 8;
        }

        public bool IsKingInCheck(bool isWhite, ILogger logger = null)
        {
            (int Row, int Col) = FindKingPosition(isWhite);
            bool isUnderAttack = IsSquareUnderAttack(Row, Col, !isWhite, out Piece attackingPiece);
            if (isUnderAttack && logger != null)
            {
                logger.LogInformation($"{(isWhite ? "White" : "Black")} King at ({Row}, {Col}) is in check by {(attackingPiece.IsWhite ? "White" : "Black")} {attackingPiece.GetType().Name} at ({attackingPiece.Position.Row}, {attackingPiece.Position.Col}).");
            }
            return isUnderAttack;
        }

        public bool IsSquareUnderAttack(int row, int col, bool byWhite, out Piece attackingPiece)
        {
            foreach (var pieceRow in Squares)
            {
                foreach (var piece in pieceRow)
                {
                    if (piece != null && piece.IsWhite == byWhite)
                    {
                        if (piece is King opponentKing)
                        {
                            // Handle the opponent's king separately
                            int[] offsets = { -1, 0, 1 };
                            foreach (int rowOffset in offsets)
                            {
                                foreach (int colOffset in offsets)
                                {
                                    if (rowOffset != 0 || colOffset != 0)
                                    {
                                        int newRow = opponentKing.Position.Row + rowOffset;
                                        int newCol = opponentKing.Position.Col + colOffset;
                                        if (IsWithinBounds(newRow, newCol))
                                        {
                                            if (newRow == row && newCol == col)
                                            {
                                                attackingPiece = opponentKing;
                                                return true;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else if (piece is Pawn pawn)
                        {
                            // Handle pawn attacks separately
                            int direction = pawn.IsWhite ? -1 : 1;
                            int attackRow = pawn.Position.Row + direction;
                            int[] attackCols = { pawn.Position.Col - 1, pawn.Position.Col + 1 };
                            foreach (int attackCol in attackCols)
                            {
                                if (IsWithinBounds(attackRow, attackCol))
                                {
                                    if (attackRow == row && attackCol == col)
                                    {
                                        attackingPiece = pawn;
                                        return true;
                                    }
                                }
                            }
                        }
                        else
                        {
                            var validMoves = piece.GetValidMovesIgnoringCheck(this);
                            if (validMoves.Any(move => move.Row == row && move.Col == col))
                            {
                                attackingPiece = piece;
                                return true;
                            }
                        }
                    }
                }
            }
            attackingPiece = null;
            return false;
        }

        public (int Row, int Col) FindKingPosition(bool isWhite)
        {
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    if (Squares[row][col] is King king && king.IsWhite == isWhite)
                    {
                        return (row, col);
                    }
                }
            }
            throw new Exception("King not found!");
        }

        public bool IsStalemate(bool isWhite)
        {
            // First, check if the king is in check. If it is, it's not a stalemate.
            if (IsKingInCheck(isWhite))
            {
                return false;
            }

            // Check if there are only kings left for both sides
            int whitePiecesCount = 0;
            int blackPiecesCount = 0;

            foreach (var row in Squares)
            {
                if (row != null)
                {
                    foreach (var piece in row)
                    {
                        if (piece != null)
                        {
                            if (piece.IsWhite)
                            {
                                whitePiecesCount++;
                            }
                            else
                            {
                                blackPiecesCount++;
                            }
                        }
                    }
                }
            }

            return !AreAnyMovesAvailable(isWhite);
        }

        public bool IsInsufficientMaterial()
        {
            List<Piece> whitePieces = [];
            List<Piece> blackPieces = [];

            foreach (var row in Squares)
            {
                foreach (var piece in row)
                {
                    if (piece != null)
                    {
                        if (piece.IsWhite)
                        {
                            whitePieces.Add(piece);
                        }
                        else
                        {
                            blackPieces.Add(piece);
                        }
                    }
                }
            }

            // Remove kings from the lists
            whitePieces.RemoveAll(p => p is King);
            blackPieces.RemoveAll(p => p is King);

            // King vs King
            if (whitePieces.Count == 0 && blackPieces.Count == 0)
            {
                return true;
            }

            // King and Bishop or Knight vs King
            if ((whitePieces.Count == 1 && (whitePieces[0] is Bishop || whitePieces[0] is Knight) && blackPieces.Count == 0) ||
                (blackPieces.Count == 1 && (blackPieces[0] is Bishop || blackPieces[0] is Knight) && whitePieces.Count == 0))
            {
                return true;
            }

            // King and Bishop vs. King and Bishop with bishops on the same color
            if (whitePieces.Count == 1 && blackPieces.Count == 1 &&
                whitePieces[0] is Bishop bishop && blackPieces[0] is Bishop bishopBlack)
            {
                var whiteBishop = bishop;
                var blackBishop = bishopBlack;

                bool whiteBishopOnLightSquare = (whiteBishop.Position.Row + whiteBishop.Position.Col) % 2 == 0;
                bool blackBishopOnLightSquare = (blackBishop.Position.Row + blackBishop.Position.Col) % 2 == 0;

                if (whiteBishopOnLightSquare == blackBishopOnLightSquare)
                {
                    return true;
                }
            }

            return false;
        }

        public bool AreAnyMovesAvailable(bool isWhite)
        {
            // Iterate through all squares on the board
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    // Check if the piece belongs to the player
                    if (Squares[row][col] is Piece piece && piece.IsWhite == isWhite)
                    {
                        // Get valid moves for the piece
                        var validMoves = piece.GetValidMoves(this);
                        foreach (var move in validMoves)
                        {
                            // Clone the board to simulate the move
                            var boardClone = this.Clone();
                            var pieceClone = boardClone.Squares[piece.Position.Row][piece.Position.Col];
                            var capturedPiece = boardClone.Squares[move.Row][move.Col];

                            // Simulate the move on the cloned board
                            boardClone.Squares[pieceClone.Position.Row][pieceClone.Position.Col] = null;
                            boardClone.Squares[move.Row][move.Col] = pieceClone;
                            pieceClone.Position = move;

                            // Handle En Passant capture in simulation
                            if (pieceClone is Pawn pawnClone)
                            {
                                // En Passant capture
                                if (capturedPiece == null && Math.Abs(move.Col - piece.Position.Col) == 1)
                                {
                                    int capturedPawnRow = isWhite ? move.Row + 1 : move.Row - 1;
                                    var enPassantPawn = boardClone.Squares[capturedPawnRow][move.Col];
                                    if (enPassantPawn is Pawn enPassantPawnClone && enPassantPawnClone.EnPassantEligible)
                                    {
                                        boardClone.Squares[capturedPawnRow][move.Col] = null;
                                    }
                                }
                            }

                            // Check if own king is in check
                            bool isInCheck = boardClone.IsKingInCheck(isWhite);

                            if (!isInCheck)
                            {
                                return true; // Found a valid move
                            }
                        }
                    }
                }
            }
            return false; // No moves available for any of the player's pieces
        }
    }
}