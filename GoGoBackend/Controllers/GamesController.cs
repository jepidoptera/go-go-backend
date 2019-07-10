using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Belgrade.SqlClient;
using System.Data.SqlClient;
using System.Security.Cryptography;
using StringManipulation;
using System.Threading;
using System.Collections;
using GoGoBackend.Go;
using Newtonsoft.Json;
using GoToken;

namespace GoGoBackend.Controllers
{
	[Produces("application/json")]
	[Route("api/games")]
	public class GamesController : Controller
	{
		private readonly IQueryPipe SqlPipe;
		private readonly ICommand SqlCommand;

		private static Dictionary<string, Game> activeGames;

		public const string dateTimeString = "dd-MM-yyyy HH:mm:ss";

		public GamesController(ICommand sqlCommand, IQueryPipe sqlPipe)
		{
			this.SqlCommand = sqlCommand;
			this.SqlPipe = sqlPipe;
			activeGames = Go.Game.activeGames;
		}

		// GET: api/games/open
		[HttpGet("open/{playerID}")]
        public string ListGames(string filter = "open", string playerID = "")
		{
            // return a comma-separated list of games
            // after applying whatever filters
            List<Game> resultGames = new List<Game>();
            string sql;
            if (filter == "open")
                sql = "select Id from [dbo].[ActiveGames] WHERE player2 = '' and player1 <> @playerID";
            else if (filter == "ongoing")
                sql = "select Id from [dbo].[ActiveGames] WHERE player2LastMove is not null and " +
					"(player2 = @playerID or player1 = @playerID and player2 is not null)";
            else if (filter == "challenge")
                sql = "select Id from [dbo].[ActiveGames] WHERE (player2 = @playerID and history is null)";
            else
                sql = "select Id from [dbo].[ActiveGames]";

            // query the result
            using (SqlConnection connection = new SqlConnection(SecretsController.ConnString))
            using (SqlCommand cmd = new SqlCommand(sql, connection))
            {
				if (sql.Contains("@playerID")) cmd.Parameters.AddWithValue("@playerID", playerID);
                // if (filter == "ongoing") cmd.Parameters.AddWithValue("@playerID2", playerID);

                connection.Open();
                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    // retrieve Game object
                    Game tGame = ActivateGame(reader.GetString(0));
                    // is this player online?
                    // check the other player, the one who is not submitting this query
                    tGame.online = (playerID.Equals(tGame.white, StringComparison.OrdinalIgnoreCase))
                        ? UserController.ActivatePlayer(tGame.black).IsOnline()
                        : UserController.ActivatePlayer(tGame.white).IsOnline(); 
                    resultGames.Add(tGame);
                }
                // return json string listing relevant games.
                // unfortunately this adds a stupid number of escape characters which I can't figure out how to get rid of
                // so it has to be cleaned up on the other end
                string response = (resultGames.Count == 0) ? "[]" : JsonConvert.SerializeObject(resultGames);
                return response;
			}
		}

		// GET: api/games/open
		[HttpGet("ongoing/{playerID}")]
		public string ListOngoingGames(string playerID)
		{
            return ListGames(filter: "ongoing", playerID: playerID);
        }

        // GET: api/games/challenges
        [HttpGet("challenges/{playerID}")]
        public string ListChallengeGames(string playerID)
        {
            return ListGames(filter: "challenge", playerID: playerID);
        }

        // GET: api/games/all
        [HttpGet("all")]
		public string ListAllGames()
		{
            return ListGames(filter: "all");
		}

		// GET: api/games/new
		[HttpPost("new")]
		public JsonResult PostNewGame()
		{
			string gameID;
			// switch players - challenger should go second, right?
            string player1 = Request.Form["player1"];
			string player2 = Request.Form["player2"];
			string token = Request.Form["authtoken"];
			int boardSize = Convert.ToInt32(Request.Form["boardsize"]);
			int mode = Convert.ToInt32(Request.Form["mode"]);

			// validate data
			if (player1 == null) return Json(new { error = "missing: player1. " });
			if (boardSize == 0) return Json(new { error = "missing: board size. " });
			if (player2 == null) player2 = "";

			SqlCommand cmd;

			// check if player1 is logged in
			if (!UserController.ValidateAuthToken(player1, token))
			{
				return Json(new { message = "auth token invalid. please log in again." });
			}

			// check if player2 exists
			if (player2 != "")
			{
				Player Player2 = UserController.ActivatePlayer(player2);
				if (Player2 != null)
				{
					// notify them of the challenge
					Player2.Message = "challenge: " + player1;
					// may as well get the capitalization right
					player2 = Player2.username;
				}
				else
					// player2 does not exist. request fails
					return Json(new { message = player2 + " not found in database." });
			}

			// create a game id by hashing two usernames + current date/time
			using (MD5 md5Hash = MD5.Create())
			{
				gameID = md5Hash.ComputeHash((player1 + player2 +
					System.DateTime.Now.ToString(dateTimeString)).Select(c => (byte)c).ToArray()).ToHexString();
			}

			// insert new game entry into the database
			using (SqlConnection connection = new SqlConnection(SecretsController.ConnString))
			{
				connection.Open();
				string sql = "INSERT INTO [dbo].[ActiveGames] (Id,Player1,Player2,BoardSize,Mode,Player1LastMove) VALUES(@Id,@Player1,@Player2,@BoardSize,@Mode,GetUtcDate())";
				cmd = new SqlCommand(sql, connection);
				cmd.Parameters.AddWithValue("Id", gameID);
				cmd.Parameters.AddWithValue("@Player1", player1);
				cmd.Parameters.AddWithValue("@Player2", player2);
				cmd.Parameters.AddWithValue("@BoardSize", boardSize);
				cmd.Parameters.AddWithValue("@Mode", mode);
				cmd.ExecuteNonQuery();
			}

			// create a new active game
			new Game(player1, player2, boardSize, mode, gameID);

			return Json(new { gameID });
		}

        [HttpPost("chat/{gameID}")]
        public JsonResult SendChat(string gameID)
        {
            // unpack args
            Player currentPlayer = UserController.ActivatePlayer(Request.Form["username"]);
            string token = Request.Form["authtoken"];
            string text = Request.Form["text"];
            Go.Game game;

            // validate input data
            // does this user exist?
            if (currentPlayer == null)
            {
                return Json(new { error = "player " + Request.Form["username"] + " not found." });
            }
            // is their auth token valid?
            if (!UserController.ValidateAuthToken(currentPlayer.username, token))
            {
                return Json(new { error = "auth token invalid" });
            }

            // does this game exist?
            game = ActivateGame(gameID);
            if (game == null)
            {
                // doesn't exist
                return Json(new { error = "this game does not exist." });
            }

            // is this player in this game?
            if (game.black != currentPlayer.username && game.white != currentPlayer.username)
            {
                // no sending chats into someone else's game
                return Json(new { error = "you are not a part of this game." });
            }

            // ok, everything appears good... send the text
            // we'll just store it in the in-memory game object, not database
            if (text != "") game.chatHistory.Add(currentPlayer.username + ": " + text);
            return Json(new { status = "success", chat = text });
        }

        // POST: /api/Games/{ID}/Move
        [HttpPost("move/{gameID}")]
		public async Task<JsonResult> MakeMove(string gameID)
		{
			// unpack args
			Player currentPlayer = UserController.ActivatePlayer(Request.Form["username"]);
			Player otherPlayer = null;
			string token = Request.Form["authtoken"];
			int x = Convert.ToInt32(Request.Form["x"]);
			int y = Convert.ToInt32(Request.Form["y"]);
			int opCode = Convert.ToInt32(Request.Form["opcode"]);
			Go.Game game;

			// validate input data
			// does this user exist?
			if (currentPlayer == null)
			{
				return Json(new { error = "player " + Request.Form["username"] + " not found." });
			}
			// is their auth token valid?
			else if (!UserController.ValidateAuthToken(currentPlayer.username, token))
			{
				return Json(new { error = "auth token invalid" } );
			}

			// does this game exist?
            game = ActivateGame(gameID);
            if (game == null)
            {
				// doesn't exist
				return Json(new { error = "this game does not exist." });
            }

            if (game.gameover)
            {
				// return code 'game over'
				return Json(new { move = string.Format("0,0,{0}", Game.Opcodes.gameover) });
            }

            // make sure this player is the one whose turn it is
            if (game.currentPlayer != currentPlayer.username && opCode < (int)Game.Opcodes.ping) 
			{
				return Json(new { error = "it's not your turn." });
			}

			// add the move to the active game object

			if (game.MakeMove(x, y, opCode))
			{
				// add to move history in the database
				SqlCommand cmd;
				using (SqlConnection connection = new SqlConnection(SecretsController.ConnString))
				{
					connection.Open();
                    string changes = "SET history = @history";
                    if (game.gameover) changes += ", gameover = 'true'";
                    string sql = string.Format("UPDATE [dbo].[ActiveGames] {0} WHERE Id = @gameID", changes);
					cmd = new SqlCommand(sql, connection);
					cmd.Parameters.AddWithValue("@gameID", gameID);
					cmd.Parameters.AddWithValue("@history", game.history.ToArray());
					// cmd.Parameters.AddWithValue("@datetime", System.DateTime.Now); //.ToString(dateTimeString));
					cmd.ExecuteNonQuery();
				}

				// if players are signed up for email notifications, send those out
				otherPlayer = (game.white == currentPlayer.username)
					? UserController.ActivatePlayer(game.black)
					: UserController.ActivatePlayer(game.white);

				string message;
				if (opCode == (int)Game.Opcodes.pass)
				{
					message = string.Format("{0} passed their move.  It is now {1}'s turn.", currentPlayer, otherPlayer);
				}
				else if (opCode == (int)Game.Opcodes.gameover)
				{
					message = string.Format("The game is now over.  Final score: \n {0}: {1} \n {2}: {3}",
						game.white, game.blackScore, game.black, game.whiteScore);
				}
				else if (opCode == (int)Game.Opcodes.illegal)
				{
					message = string.Format("{0} attempted an illegal move at {1}, {2} and was rejected. It is now {3}'s turn", currentPlayer.username, x, y, otherPlayer.username);
				}
				else
				{
					message = string.Format("{0} played at {1}, {2}. It is now {3}'s turn", currentPlayer.username, x, y, otherPlayer.username);
				}
				if (currentPlayer.emailNotifications)
				{
					UserController.SendEmailNotification(currentPlayer.email, message);
				}
				if (otherPlayer.emailNotifications)
				{
					UserController.SendEmailNotification(otherPlayer.email, message);
				}
			}

			// is this end of game?
			if (game.gameover)
			{
                // if so, end it and reward tokens to players
                // reward with erc20 tokens
                // one token per move that was played, split according to score
                // figure out how many go to each player
                Player[] players =
                {
                    UserController.ActivatePlayer(game.white),
                    UserController.ActivatePlayer(game.black)
                };
                // distribute one coin for each move that was played
                int totalReward = game.history.Count / 3;
                int maxReward = Math.Min(totalReward / 2 + (int)(totalReward / 2 * Math.Abs(game.blackScore - game.whiteScore) * 2f / game.NodeCount), totalReward - 1);
                int player1Reward = (game.blackScore > game.whiteScore) ? maxReward : totalReward - maxReward;
                int player2Reward = totalReward - player1Reward;
                int winner = (game.blackScore > game.whiteScore) ? 0 : 1;

                // give players their reward
                if (players[0].ethAddress != null) await TokenController.Send(players[0].ethAddress, player1Reward);
                if (players[1].ethAddress != null) await TokenController.Send(players[1].ethAddress, player2Reward);

                // update win stats
                for (int i = 0; i < 2; i++)
                {
                    UserController.UpdatePlayer(players[i].username, gamesPlayed: players[i].gamesPlayed + 1, 
                    gamesWon: (i == winner) ? players[i].gamesWon + 1 : players[i].gamesWon);
                }

                // update database (flag for removal)
                UpdateGame(gameID, gameover: true);
                activeGames.Remove(gameID);

                // return code 'game over' to both players
                game.ReturnMove(player1Reward, player2Reward, (int)Game.Opcodes.gameover);
				return Json(new {
					move = string.Format("{0},{1},{2}", player1Reward, player2Reward, (int)Game.Opcodes.gameover),
					// also return the reward each player has received
					player1Reward, player2Reward
				});
			}
			else
			{
                // ping, requesting callback with opponent's move
                // if opponent has already moved, return directly
                if (game.currentPlayer == currentPlayer.username)
                {
                    return game.history.Count >= 3
                        ? Json( new { move = string.Format("{0},{1},{2}",
							 game.history[game.history.Count - 3],
							 game.history[game.history.Count - 2],
							 game.history[game.history.Count - 1]) } )
						// apparently no one has moved yet??  or even joined the game??
						: Json(new { move = "" });
				}
				// otherwise run awaitmove, which will return once opponent plays a move
				string move = await Task.Run(() => game.AwaitMove());
				return Json(new { move });
			}
		}

		private Game ActivateGame(string gameID)
		{
			// if this game already exists in memory, return immediately with that reference
			if (activeGames.ContainsKey(gameID)) return activeGames[gameID];

			// otherwise, construct game object from the database
			List<byte> history = new List<byte>();
			string player1 = "", player2 = "";
			// string player1LastMove, player2LastMove;
			int gameMode = 0, boardSize = 0;

			// get game info from database
			string sql = "Select player1, player2, boardSize, mode, history from [dbo].[ActiveGames] where Id = @gameID";
			using (SqlConnection dbConnection = new SqlConnection(SecretsController.ConnString))
			using (SqlCommand dbCommand = new SqlCommand(sql, dbConnection))
			{
				dbCommand.Parameters.AddWithValue("@gameID", gameID);
				dbConnection.Open();

				SqlDataReader reader = dbCommand.ExecuteReader();

				while (reader.Read())
				{
					player1 = (string)reader["player1"];
					player2 = (string)reader["player2"];
					gameMode = (int)reader["mode"];
					boardSize = (int)reader["boardSize"];
					// these ones have to be handled a bit differently because they are optional fields and may be DBNull
					// not regular null -- DBNull.
					//var lastMove =reader. reader["player1LastMove"];
					//player1LastMove = (lastMove == DBNull.Value) ? "" : (string)lastMove;
					//lastMove = reader["player2LastMove"];
					//player2LastMove = (lastMove == DBNull.Value) ? "" : (string)lastMove;
					var nullhist = reader["history"];
					history = (nullhist == DBNull.Value) ? new List<byte>() : new List<byte>((byte[])nullhist);
				}
				dbConnection.Close();
			}

            // this would indicate the game was not found in the database
            if (player1 == "") return null;

			// model it as an object with manual reset events and gamestate
			return new Game(player1, player2, boardSize, gameMode, gameID, history);
			// success
		}

        private void UpdateGame(string gameID, object gameover)
        {
            // updating both database and game dictionary object
            Game game = ActivateGame(gameID);
            string sql = "update [dbo].[ActiveGames] set ";
            List<string> args = new List<string>();
            if (gameover != null)
            {
                args.Add("gameover = @gameover");
                game.gameover = (bool)gameover;
            }
            sql += string.Join(", ", args.ToArray()) + " where Id = @gameid";

            using (SqlConnection connection = new SqlConnection(SecretsController.ConnString))
            using (SqlCommand cmd = new SqlCommand(sql, connection))
            {
                connection.Open();

                // add in parameters
                cmd.Parameters.AddWithValue("@gameID", gameID);
                if (gameover != null) cmd.Parameters.AddWithValue("@gameover", ((bool)gameover).ToString());
                // run it
                cmd.ExecuteNonQuery();
                connection.Close();
            }
        }

        // GET: api/games/{ID}
        [HttpGet("{gameID}")]
		public JsonResult Get(string gameID)
		{
            // return info about this specific game
            Game game = ActivateGame(gameID);
            if (game == null) return Json(new { error = "game not found." });
            return Json(game);
		}

		// return the node graph for this game as a json object
		[HttpGet("nodes/{gameID}")]
		public string Nodes(string gameID)
		{
            Game game = ActivateGame(gameID);
            if (game == null)
			{
				return "none";
			}
			// return a json object containing this game's node map
			Game.SimpleNode[] nodes = game.GetNodes();
			string json = JsonConvert.SerializeObject(nodes);
			return json;
		}

		// return an array describing the state of each node
		[HttpGet("state/{gameID}")]
		public JsonResult GameState(string gameID)
		{
            // activate this game if need be
            Game game = ActivateGame(gameID);
            if (game == null)
            {
				return Json(new { error = "game not found" });
			}
            
			// get state
			int[] nodes = game.GameState();
			return Json(nodes);
		}

		// POST: api/games/join/{gameID}
		[HttpPost("join/{gameID}")]
		public JsonResult JoinGame(string gameID)
		{
			string username = Request.Form["username"];
			string token = Request.Form["authtoken"];

			// any player can do this if the game is open, but
			// an auth token is required for this action
			if (!UserController.ValidateAuthToken(username, token))
			{
				return Json(new { error = "auth token invalid." });
			}

			// do some validation
			Game game = ActivateGame(gameID);
			// what are all the stupid things someone might try to do?
			if (game == null)
			{
				// joining a non-existent game
				return Json(new { error = "game does not exist." });
			}
			else if (game.white == username)
			{
				// this player was already in this game
				return Json(new { message = "rejoined game " + game.Id });
			}
			else if (game.black != "" && game.black != username)
			{
				// joining a game that already belongs to someone else
				return Json(new { error = String.Format("game is already in progress. {0} vs {1}", game.white, game.black )});
			}

			// joining an open game for the first time as player2
			else if (game.history.Count == 0)
			{
				// add a token "joined game" move
				game.history.AddRange(new byte[] { 0, 0, (byte)Game.Opcodes.joingame });

				// add this player as player 2
				game.black = username;

                // black goes first
                game.currentPlayer = game.black;

				// update the database
				SqlCommand cmd;
				using (SqlConnection connection = new SqlConnection(SecretsController.ConnString))
				{
					connection.Open();
					string sql = "UPDATE [dbo].[ActiveGames] SET player2 = @player2, history = @history, player2LastMove = GetUTCDate() WHERE Id = @gameID";
					cmd = new SqlCommand(sql, connection);
					cmd.Parameters.AddWithValue("@player2", game.black);
					cmd.Parameters.AddWithValue("@gameID", gameID);
					cmd.Parameters.AddWithValue("@history", game.history.ToArray());
					// cmd.Parameters.AddWithValue("@datetime", System.DateTime.Now); // .ToString(dateTimeString));
					cmd.ExecuteNonQuery();
				}

				// notify player1 that their challenge was accepted
				UserController.ActivatePlayer(game.white).Message = game.black + " joined " + game.Id;
				return Json(new { message = "joined" });

			}
            else if (game.black == username)
            {
                // this player was already in this game, and it already started
                return Json(new { message = "rejoined game " + game.Id });
            }
            else
            {
				// this should literally never happen.
				return Json(new { error = "sanity check failed." });
			}
		}

		// POST: api/games/leave/{gameID}
		[HttpPost("leave/{gameID}")]
		public string LeaveGame(string gameID)
		{
			//string playerID = Request.Form["playerID"];
			//string token = Request.Form["authtoken"];

			//// auth token required for this action
			//if (!UserController.ValidateAuthToken(playerID, token))
			//{
			//	return "auth token invalid";
			//}

			//SqlCommand cmd;
			//using (SqlConnection connection = new SqlConnection(SecretsController.ConnString))
			//{
			//	connection.Open();
			//	// leave the game - if this is player2 and no moves have been played yet
			//	string sql = "UPDATE [dbo].[ActiveGames] SET player2 = '' WHERE Id = @gameID and player2 = @playerID and history is NULL";
			//	cmd = new SqlCommand(sql, connection);
			//	cmd.Parameters.AddWithValue("@playerID", playerID);
			//	cmd.Parameters.AddWithValue("@gameID", gameID);
			//	// cmd.Parameters.AddWithValue("@datetime", System.DateTime.Now); // .ToString(dateTimeString));
			//	cmd.ExecuteNonQuery();
			//	// leave the game as player1
			//	sql = "UPDATE [dbo].[ActiveGames] SET player1 = '' WHERE Id = @gameID and player1 = @playerID and history is NULL";
			//	cmd = new SqlCommand(sql, connection);
			//	cmd.Parameters.AddWithValue("@playerID", playerID);
			//	cmd.Parameters.AddWithValue("@gameID", gameID);
			//	cmd.ExecuteNonQuery();
			//}
			return "api endpoint deprecated";
		}

		// POST: api/Games
		[HttpPost]
		public void Post([FromBody]string value)
		{
		}

		// PUT: api/Games/{ID}
		[HttpPut("{id}")]
		public void Put(int id, [FromBody]string value)
		{
		}

		// DELETE: api/games/clear
		// definitely remove this one in production
		[HttpDelete("clear")]
		public string ClearGames()
		{
			// yeah, hardcoded plaintext passwords. woot
			// maximum security
			if (Request.Form["Password"] == "nanobot")
			{
				var cmd = new SqlCommand("delete [dbo].[ActiveGames]");
				SqlCommand.Sql(cmd).Exec();
				return "does that make you happy?";
			}
			else return "denied";
		}

		[HttpDelete("delete/{gameID}")]
		public JsonResult DeleteGame(string gameID)
		{
			string token = Request.Form["authtoken"];

            Game game = ActivateGame(gameID);

			if (token != "nanobot")
			{
                if (game.gameover) return Json( new { error = "game is already over." });
                if (game.history.Count < 6)
                {
                    // game hasn't started yet, ok to delete
                    // check auth token
                    if (!UserController.ValidateAuthToken(game.white, token) &&
                       !UserController.ValidateAuthToken(game.black, token)) return Json(new { error = "auth token invalid" });

                }
                else return Json(new { error = "game already in progress" });
			}

			// delete a specific game
			var cmd = new SqlCommand("delete [dbo].[ActiveGames] where Id = @GameID");
			cmd.Parameters.AddWithValue("@GameID", gameID);
			SqlCommand.Sql(cmd).Exec();

            // notify other player that their challenge was denied
            Player player1 = UserController.ActivatePlayer(game.white);
            if (player1.emailNotifications)
                Emails.Server.SendNotificationEmail(player1.email, string.Format("{0} denied your challenge request.", game.black));

            // remove from games dictionary
            activeGames.Remove(gameID);
            return Json ( new { message = "deleted" });
		}

		[HttpPost("delete/inactive/{playerID}")]
		public async Task<string> ClearInactiveFromPlayer(string playerID)
		{
			// validate permission
			string token = Request.Form["authtoken"];
			if (!UserController.ValidateAuthToken(playerID, token)) return "auth token invalid";

			// delete all inactive (uninitialized) games
			var cmd = new SqlCommand("delete [dbo].[ActiveGames] where player1 = @PlayerID and gameover = 'true'");
			cmd.Parameters.AddWithValue("@PlayerID", playerID);
			await SqlCommand.Sql(cmd).Exec();
			return "";
		}
	}
}
