using ChessAI.Models;
using ChessAI.Models.AIs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Diagnostics;

namespace ChessAI.Controllers
{
    public class HomeController(ILogger<HomeController> logger) : Controller
    {
        private readonly ILogger<HomeController> _logger = logger;

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var dm = Request.Cookies["dm"];
            if (dm == null)
            {
                // Set dark mode cookie to true
                Response.Cookies.Append("dm", "true", new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    Path = "/"
                });
                ViewBag.DarkMode = true;
            }
            else
            {
                ViewBag.DarkMode = dm == "true";
            }
            base.OnActionExecuting(context);
        }

        public IActionResult Index()
        {
            var dm = Request.Cookies["dm"];
            ViewBag.DarkMode = dm == "true";
            return View();
        }

        public IActionResult Rules()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Play()
        {
            // Test session availability
            HttpContext.Session.SetString("Test", "Session is working");
            var testValue = HttpContext.Session.GetString("Test");
            if (testValue == null)
            {
                return Content("Session is not working");
            }

            // Retrieve the game from the session
            var game = HttpContext.Session.GetObjectFromJson<Game>("Game");
            if (game == null)
            {
                game = new Game();
                HttpContext.Session.SetObjectAsJson("Game", game);
            }

            // Retrieve game mode
            var gameMode = HttpContext.Session.GetString("GameMode") ?? "LocalPvP";

            // Pass SelectedAI to the view
            var selectedAI = HttpContext.Session.GetString("SelectedAI");
            ViewBag.SelectedAI = selectedAI;

            // Create a ViewModel to pass both game and game mode
            var viewModel = new PlayViewModel
            {
                Game = game,
                GameMode = gameMode,
                SelectedAI = selectedAI,
                IsWhiteTurn = game.IsWhiteTurn,
                IsWhiteKingInCheck = game.Board.IsKingInCheck(true),
                IsBlackKingInCheck = game.Board.IsKingInCheck(false),
                GameResult = game.GameResult
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RestartGame()
        {
            var gameMode = HttpContext.Session.GetString("GameMode") ?? "LocalPvP";
            var selectedAI = HttpContext.Session.GetString("SelectedAI");
            var newGame = new Game();
            HttpContext.Session.SetObjectAsJson("Game", newGame);

            var response = new RestartResponse
            {
                Success = true,
                Game = newGame,
                GameMode = gameMode,
                SelectedAI = selectedAI,
                IsWhiteTurn = newGame.IsWhiteTurn,
                IsWhiteKingInCheck = newGame.Board.IsKingInCheck(true),
                IsBlackKingInCheck = newGame.Board.IsKingInCheck(false),
                GameResult = newGame.GameResult,
                HalfMoveClock = newGame.HalfMoveClock,
                FullMoveNumber = newGame.FullMoveNumber
            };

            return Json(response);
        }

        public class RestartResponse
        {
            public bool Success { get; set; }
            public Game Game { get; set; }
            public string GameMode { get; set; }
            public string SelectedAI { get; set; }
            public bool IsWhiteTurn { get; set; }
            public bool IsWhiteKingInCheck { get; set; }
            public bool IsBlackKingInCheck { get; set; }
            public bool IsGameOver { get; set; }
            public string GameResult { get; set; }
            public int HalfMoveClock { get; set; }
            public int FullMoveNumber { get; set; }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ResignGame()
        {
            var game = HttpContext.Session.GetObjectFromJson<Game>("Game");
            if (game == null)
            {
                _logger.LogWarning("Game not found in session.");
                return BadRequest("Game not found.");
            }

            if (game.IsGameOver)
            {
                _logger.LogWarning("Resign attempted on an already over game.");
                return BadRequest("Game is already over.");
            }

            // Determine which player is resigning based on the turn
            bool resigningIsWhite = game.IsWhiteTurn;
            game.IsGameOver = true;

            if (resigningIsWhite)
            {
                game.GameResult = "Black wins by resignation";
            }
            else
            {
                game.GameResult = "White wins by resignation";
            }

            HttpContext.Session.SetObjectAsJson("Game", game);

            var response = new GameResultResponse
            {
                IsGameOver = true,
                GameResult = game.GameResult,
                IsWhiteTurn = game.IsWhiteTurn,
                IsWhiteKingInCheck = game.Board.IsKingInCheck(true),
                IsBlackKingInCheck = game.Board.IsKingInCheck(false)
            };

            return Json(response);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MakeMove([FromBody] MoveRequest move)
        {
            var game = HttpContext.Session.GetObjectFromJson<Game>("Game");
            if (game == null)
            {
                _logger.LogWarning("Game not found in session.");
                return BadRequest("Game not found.");
            }

            // Prevent any moves if the game is over
            if (game.IsGameOver)
            {
                _logger.LogWarning("Attempted to make a move after the game is over.");
                return BadRequest("The game is already over.");
            }

            // Capture the current player's color before making the move
            bool currentPlayerIsWhite = game.IsWhiteTurn;

            // Track game state changes
            bool isCapture = game.Board.Squares[move.ToRow][move.ToCol] != null;
            bool isPromotion = false;
            bool isCastle = false;
            bool isEnPassantCapture = false;

            var piece = game.Board.Squares[move.FromRow][move.FromCol];

            // Detect En Passant capture
            if (piece is Pawn)
            {
                if (move.ToCol != move.FromCol && game.Board.Squares[move.ToRow][move.ToCol] == null)
                {
                    isEnPassantCapture = true;
                }
            }

            // Detect Castling
            if (piece is King && Math.Abs(move.ToCol - move.FromCol) == 2)
            {
                isCastle = true;
            }

            // Detect Promotion
            if (piece is Pawn && ((piece.IsWhite && move.ToRow == 0) || (!piece.IsWhite && move.ToRow == 7)))
            {
                isPromotion = true;
            }

            var success = game.MakeMove((move.FromRow, move.FromCol), (move.ToRow, move.ToCol), _logger);
            if (!success)
            {
                _logger.LogWarning("Invalid move attempted.");
                return BadRequest("Invalid move.");
            }

            // Checks if the opponent is now in check
            bool opponentIsWhite = !currentPlayerIsWhite;
            bool opponentIsInCheck = game.Board.IsKingInCheck(opponentIsWhite, _logger);

            HttpContext.Session.SetObjectAsJson("Game", game);

            // Prepare response with game state
            var response = new MoveResponse
            {
                Success = true,
                IsGameOver = game.IsGameOver,
                GameResult = game.GameResult,
                PlayerIsCheckmate = game.IsGameOver && game.GameResult.Contains("wins"),
                PlayerIsCheck = opponentIsInCheck,
                PlayerIsCapture = isCapture,
                PlayerIsPromotion = isPromotion,
                PlayerIsCastle = isCastle,
                PlayerIsEnPassantCapture = isEnPassantCapture,
                HalfMoveClock = game.HalfMoveClock,
                FullMoveNumber = game.FullMoveNumber,
                AIMove = null,
                AIIsCheckmate = false,
                AIIsCheck = false,
                AIIsCapture = false,
                AIIsPromotion = false,
                AIIsCastle = false,
                AIIsEnPassantCapture = false
            };

            // After player's move, check if it's AI's turn
            var gameMode = HttpContext.Session.GetString("GameMode");
            if (gameMode == "PvAI" && !game.IsWhiteTurn && !game.IsGameOver)
            {
                var aiName = HttpContext.Session.GetString("SelectedAI");
                if (!string.IsNullOrEmpty(aiName))
                {
                    var aiPlayer = AIFactory.GetAIByName(aiName);
                    if (aiPlayer != null)
                    {
                        var (From, To) = aiPlayer.GetNextMove(game);

                        var aiPiece = game.Board.Squares[From.Row][From.Col];

                        // Detect AI En Passant and Castling
                        bool aiIsEnPassantCapture = false;
                        bool aiIsCastle = false;

                        // Detect En Passant capture
                        if (aiPiece is Pawn)
                        {
                            if (To.Col != From.Col && game.Board.Squares[To.Row][To.Col] == null)
                            {
                                aiIsEnPassantCapture = true;
                            }
                        }

                        // Detect Castling
                        if (aiPiece is King && Math.Abs(To.Col - From.Col) == 2)
                        {
                            aiIsCastle = true;
                        }

                        // Check if AI move is a capture before making the move
                        bool aiIsCapture = game.Board.Squares[To.Row][To.Col] != null;

                        // Detect Promotion
                        bool aiIsPromotion = false;
                        if (aiPiece is Pawn && ((aiPiece.IsWhite && To.Row == 0) || (!aiPiece.IsWhite && To.Row == 7)))
                        {
                            aiIsPromotion = true;
                        }

                        var aiSuccess = game.MakeMove(
                            (From.Row, From.Col),
                            (To.Row, To.Col),
                            _logger
                        );

                        if (!aiSuccess)
                        {
                            _logger.LogWarning("AI attempted an invalid move.");
                            return BadRequest("AI attempted an invalid move.");
                        }

                        // Update session with the new game state after AI moved
                        HttpContext.Session.SetObjectAsJson("Game", game);

                        // Determine AI move effects
                        bool aiOpponentIsWhite = currentPlayerIsWhite;
                        bool aiOpponentIsInCheck = game.Board.IsKingInCheck(aiOpponentIsWhite, _logger);
                        bool aiIsCheckmate = game.IsGameOver && game.GameResult.Contains("wins");

                        response.AIMove = new AIMoveResponse
                        {
                            From = new PositionModel { Row = From.Row, Col = From.Col },
                            To = new PositionModel { Row = To.Row, Col = To.Col }
                        };
                        response.AIIsCheckmate = aiIsCheckmate;
                        response.AIIsCheck = aiOpponentIsInCheck;
                        response.AIIsCapture = aiIsCapture;
                        response.AIIsPromotion = aiIsPromotion;
                        response.AIIsCastle = aiIsCastle;
                        response.AIIsEnPassantCapture = aiIsEnPassantCapture;

                        // Update IsGameOver and GameResult in case the AIs move ended the game
                        response.IsGameOver = game.IsGameOver;
                        response.GameResult = game.GameResult;
                    }
                }
            }
            response.IsWhiteTurn = game.IsWhiteTurn;

            response.IsWhiteKingInCheck = game.Board.IsKingInCheck(true);
            response.IsBlackKingInCheck = game.Board.IsKingInCheck(false);

            return Json(response);
        }

        [HttpPost]
        public IActionResult GetValidMoves([FromBody] PositionModel position)
        {
            // Retrieve the game from session
            var game = HttpContext.Session.GetObjectFromJson<Game>("Game");
            if (game == null)
            {
                _logger.LogWarning("Game not found in session.");
                // Return an empty list instead of BadRequest
                return Json(new List<PositionModel>());
            }

            var piece = game.Board.Squares[position.Row][position.Col];
            if (piece == null || piece.IsWhite != game.IsWhiteTurn)
            {
                _logger.LogWarning("Invalid piece selected or not player's turn.");
                // Return an empty list instead of BadRequest
                return Json(new List<PositionModel>());
            }

            var validMoves = piece.GetValidMoves(game.Board);

            // Filter out moves that would put own king in check
            var safeMoves = new List<PositionModel>();

            foreach (var (Row, Col) in validMoves)
            {
                // Simulate the move
                var boardClone = game.Board.Clone();
                var pieceClone = boardClone.Squares[piece.Position.Row][piece.Position.Col];
                var capturedPiece = boardClone.Squares[Row][Col];

                // Simulate the move on the cloned board
                boardClone.Squares[pieceClone.Position.Row][pieceClone.Position.Col] = null;
                boardClone.Squares[Row][Col] = pieceClone;
                pieceClone.Position = (Row, Col);

                // Handle En Passant capture in simulation
                if (pieceClone is Pawn pawnClone)
                {
                    // En Passant capture
                    if (capturedPiece == null && Math.Abs(Col - piece.Position.Col) == 1)
                    {
                        int capturedPawnRow = piece.IsWhite ? Row + 1 : Row - 1;
                        var enPassantPawn = boardClone.Squares[capturedPawnRow][Col];
                        if (enPassantPawn is Pawn enPassantPawnClone && enPassantPawnClone.EnPassantEligible)
                        {
                            boardClone.Squares[capturedPawnRow][Col] = null;
                        }
                    }
                }

                // Check if own king is in check
                bool isInCheck = boardClone.IsKingInCheck(piece.IsWhite);

                if (!isInCheck)
                {
                    safeMoves.Add(new PositionModel { Row = Row, Col = Col });
                }
            }

            // Return the valid moves as JSON with camelCase property names
            return Json(safeMoves);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SelectAI([FromBody] AISelectionRequest request)
        {
            var aiPlayer = AIFactory.GetAIByName(request.AIName);
            if (aiPlayer != null)
            {
                HttpContext.Session.SetString("SelectedAI", aiPlayer.Name);
                // Sets the game mode to PvAI since AI is selected
                HttpContext.Session.SetString("GameMode", "PvAI");
                // Reset the game when selecting AI
                var newGame = new Game();
                HttpContext.Session.SetObjectAsJson("Game", newGame);
                return Ok();
            }
            return BadRequest("AI not found.");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetGameMode([FromBody] GameModeRequest request)
        {
            var validModes = new[] { "Tutorial", "LocalPvP", "PvAI", "OnlinePvP" };
            if (validModes.Contains(request.GameMode))
            {
                HttpContext.Session.SetString("GameMode", request.GameMode);

                // Reset the game when changing game mode
                var newGame = new Game();
                HttpContext.Session.SetObjectAsJson("Game", newGame);

                // If switching to PvP then remove the selected AI
                if (request.GameMode == "LocalPvP")
                {
                    HttpContext.Session.Remove("SelectedAI");
                }
                if (request.GameMode == "OnlinePvP")
                {
                    HttpContext.Session.Remove("SelectedAI");
                }

                return Ok();
            }
            return BadRequest("Invalid game mode.");
        }

        public IActionResult Victory()
        {
            return View();
        }

        public IActionResult Defeat()
        {
            return View();
        }

        public IActionResult Draw()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }

        #region Supporting Classes

        public class MoveRequest
        {
            public int FromRow { get; set; }
            public int FromCol { get; set; }
            public int ToRow { get; set; }
            public int ToCol { get; set; }
        }

        public class PositionModel
        {
            public int Row { get; set; }
            public int Col { get; set; }
        }

        public class AISelectionRequest
        {
            public required string AIName { get; set; }
        }

        public class GameModeRequest
        {
            public required string GameMode { get; set; }
        }

        public class GameResultResponse
        {
            public bool IsGameOver { get; set; }
            public string GameResult { get; set; }
            public bool IsWhiteTurn { get; set; }
            public bool IsWhiteKingInCheck { get; set; }
            public bool IsBlackKingInCheck { get; set; }
        }

        public class PlayViewModel
        {
            public required Game Game { get; set; }
            public required string GameMode { get; set; }
            public required string SelectedAI { get; set; }
            public bool IsWhiteTurn { get; set; }
            public bool IsWhiteKingInCheck { get; set; }
            public bool IsBlackKingInCheck { get; set; }
            public string? GameResult { get; set; }
        }

        public class MoveResponse
        {
            public bool Success { get; set; }
            public bool IsGameOver { get; set; }
            public required string GameResult { get; set; }
            public bool IsWhiteTurn { get; set; }
            public bool PlayerIsCheckmate { get; set; }
            public bool PlayerIsCheck { get; set; }
            public bool PlayerIsCapture { get; set; }
            public bool PlayerIsPromotion { get; set; }
            public bool PlayerIsCastle { get; set; }
            public bool PlayerIsEnPassantCapture { get; set; }
            public required AIMoveResponse AIMove { get; set; }
            public bool AIIsCheckmate { get; set; }
            public bool AIIsCheck { get; set; }
            public bool AIIsCapture { get; set; }
            public bool AIIsPromotion { get; set; }
            public bool AIIsCastle { get; set; }
            public bool AIIsEnPassantCapture { get; set; }
            public bool IsWhiteKingInCheck { get; set; }
            public bool IsBlackKingInCheck { get; set; }
            public int HalfMoveClock { get; set; }
            public int FullMoveNumber { get; set; }
        }

        public class AIMoveResponse
        {
            public required PositionModel From { get; set; }
            public required PositionModel To { get; set; }
        }

        #endregion
    }
}