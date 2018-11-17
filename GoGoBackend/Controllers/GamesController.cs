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
                sql = "select Id from [dbo].[ActiveGames] WHERE player2 ='' and player1 <> @playerID";
            else if (filter == "ongoing")
                sql = "select Id from [dbo].[ActiveGames] WHERE gameover = 0 and history is not null and " +
					"(player2 = @playerID or player1 = @playerID and player2 is not null)";
            else if (filter == "challenge")
                sql = "select Id from [dbo].[ActiveGames] WHERE (player2 = @playerID and history is null)";
            else
                sql = "select Id from [dbo].[ActiveGames]";

            // query the result
            using (SqlConnection connection = new SqlConnection(Startup.ConnString))
            using (SqlCommand cmd = new SqlCommand(sql, connection))
            {
				if (sql.Contains("@playerID")) cmd.Parameters.AddWithValue("@playerID", playerID);

                connection.Open();
                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    // retrieve Game object
                    Game tGame = ActivateGame(reader.GetString(0));
                    tGame.online = UserController.ActivatePlayer(tGame.player1).IsOnline();
                    resultGames.Add(tGame);
                }
                // return json string listing relevant games.
                // unfortunately this adds a stupid number of escape characters which I can't figure out how to get rid of
                // has to be cleaned up on the other end
                return JsonConvert.SerializeObject(resultGames);
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
		public string PostNewGame()
		{
			string gameID;
			string player1 = Request.Form["player1"];
			string player2 = Request.Form["player2"];
			string token = Request.Form["authtoken"];
			int boardSize = Convert.ToInt32(Request.Form["boardsize"]);
			int mode = Convert.ToInt32(Request.Form["mode"]);

			SqlCommand cmd;

			// check if player1 is logged in
			if (!UserController.ValidateAuthToken(player1, token))
			{
				return "auth token invalid";
			}

			// create a game id by hashing two usernames + current date/time
			using (MD5 md5Hash = MD5.Create())
			{
				gameID = md5Hash.ComputeHash((player1 + player2 +
					System.DateTime.Now.ToString(dateTimeString)).Select(c => (byte)c).ToArray()).ToHexString();
			}

			// insert new game entry into the database
			using (SqlConnection connection = new SqlConnection(Startup.ConnString))
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

			return gameID;
		}

		// POST: /api/Games/{ID}/Move
		[HttpPost("move/{gameID}")]
		public async Task<string> MakeMove(string gameID)
		{
			// unpack args
			string currentPlayer = Request.Form["player"];
			string otherPlayer = "";
			string token = Request.Form["authtoken"];
			int x = Convert.ToInt32(Request.Form["x"]);
			int y = Convert.ToInt32(Request.Form["y"]);
			int opCode = Convert.ToInt32(Request.Form["opcode"]);
			Go.Game game;

			// todo: figure out whether or not it's this player's turn
			// if it's not, we should not respond the same way
			// also todo: figure out if this is an actual active player and not just some rando from the internet
			if (!UserController.ValidateAuthToken(currentPlayer, token))
			{
				return "auth token invalid";
			}

            game = ActivateGame(gameID);
            if (game == null)
            {
                // doesn't exist
                return "this game isn't real.";
            }

            // make sure this player is the one who's turn it is
            if (game.currentPlayer != currentPlayer && opCode < (int)Game.Opcodes.ping) 
			{
				return "quit trying to cheat.";
			}

			// add the move to the active game object
			game.MakeMove(x, y, opCode);

			if (opCode < (int)Game.Opcodes.ping)
			{
				// add to move history in the database
				SqlCommand cmd;
				using (SqlConnection connection = new SqlConnection(Startup.ConnString))
				{
					connection.Open();
					string sql = "UPDATE [dbo].[ActiveGames] SET history = @history WHERE Id = @gameID";
					cmd = new SqlCommand(sql, connection);
					cmd.Parameters.AddWithValue("@gameID", gameID);
					cmd.Parameters.AddWithValue("@history", game.history.ToArray());
					// cmd.Parameters.AddWithValue("@datetime", System.DateTime.Now); //.ToString(dateTimeString));
					cmd.ExecuteNonQuery();
				}

				// if players are signed up for email notifications, send those out
				otherPlayer = (game.player1 == currentPlayer)
					? game.player2
					: game.player1;

				string message;
				if (opCode == 0)
				{
					message = string.Format("{0} passed their move.  It is now {1}'s turn.", currentPlayer, otherPlayer);
				}
				else if (opCode == (int)Game.Opcodes.gameover)
				{
					message = string.Format("The game is now over.  Final score: \n {0}: {1} \n {2}: {3}",
						game.player1, game.blackScore, game.player2, game.whiteScore);
				}
				else if (opCode == (int)Game.Opcodes.illegal)
				{
					message = string.Format("{0} attempted an illegal move at {1}, {2} and was rejected. It is now {2}'s turn", currentPlayer, x, y, otherPlayer);
				}
				else
				{
					message = string.Format("{0} played at {1}, {2}. It is now {2}'s turn", currentPlayer, x, y, otherPlayer);
				}
				if (UserController.UserInfo<bool>(currentPlayer, "emailNotifications"))
				{
					UserController.SendEmailNotification(currentPlayer, message);
				}
				if (UserController.UserInfo<bool>(otherPlayer, "emailNotifications"))
				{
					UserController.SendEmailNotification(otherPlayer, message);
				}
			}

			// is this end of game?
			if (game.gameover)
			{
                // if so, end it and reward tokens to players
                // reward with erc20 tokens
                // award 100 per game, split according to score
                // figure out how many go to each player
                int splitPercentage = Math.Min(50 + (int)(50 * Math.Abs(game.blackScore - game.whiteScore) * 2f / game.NodeCount), 99);
                int player1Reward = (game.blackScore > game.whiteScore) ? splitPercentage : 100 - splitPercentage;
                int player2Reward = 100 - player1Reward;

                string player1Address = UserController.UserInfo<string>(game.player1, "ethAddress");
                string player2Address = UserController.UserInfo<string>(game.player2, "ethAddress");

                // give players their reward
                if (player1Address != null) await TokenController.Send(player1Address, player1Reward);
                if (player2Address != null) await TokenController.Send(player2Address, player2Reward);

                // return code 'game over'
                return string.Format("0,0,{0}", Game.Opcodes.gameover);
			}
			else
			{
				// otherwise run this task again, thus releasing the previous instance to return its value
				string move = await Task.Run(() => game.AwaitMove());
				return move;
			}
		}

		private Game ActivateGame(string gameID)
		{
			// if this game already exists in memory, return immediately with that reference
			if (activeGames.ContainsKey(gameID)) return activeGames[gameID];

			// otherwise, construct game object from the database
			List<byte> history = new List<byte>();
			string player1 = "", player2 = "";
			int gameMode = 0, boardSize = 0;

			// get game info from database
			string sql = "Select player1, player2, boardSize, mode, history from [dbo].[ActiveGames] where Id = @gameID";
			using (SqlConnection dbConnection = new SqlConnection(Startup.ConnString))
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
					var nullhist = reader["history"];
					history = (nullhist == DBNull.Value) ? new List<byte>() : new List<byte>((byte[])nullhist);
				}
				dbConnection.Close();
			}

			// model it as an object with manual reset events and gamestate
			return new Game(player1, player2, boardSize, gameMode, gameID, history);
			// success
		}

		// GET: api/games/{ID}
		[HttpGet("{gameID}")]
		public async Task Get(string gameID)
		{
			// return info about this specific game
			var cmd = new SqlCommand("SELECT * FROM [dbo].[ActiveGames] WHERE Id = @gameID FOR JSON PATH, WITHOUT_ARRAY_WRAPPER");
			cmd.Parameters.AddWithValue("gameID", gameID);
			await SqlPipe.Sql(cmd).Stream(Response.Body, "{}");
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
		public string GameState(string gameID)
		{
            // activate this game if need be
            Game game = ActivateGame(gameID);
            if (game == null)
            {
				return "none";
			}
            
			// get state
			int[] nodes = game.GameState();
			string returnVal = string.Join(',', nodes);
			return returnVal;
		}

		// POST: api/games/join/{gameID}
		[HttpPost("join/{gameID}")]
		public string JoinGame(string gameID)
		{
			string playerID = Request.Form["playerID"];
			string token = Request.Form["authtoken"];

			// auth token required for this action
			if (!UserController.ValidateAuthToken(playerID, token))
			{
				return "auth token invalid";
			}

			SqlCommand cmd;
			using (SqlConnection connection = new SqlConnection(Startup.ConnString))
			{
				connection.Open();
                string sql = "UPDATE [dbo].[ActiveGames] SET player2 = @playerID, player2LastMove = GetUTCDate() WHERE Id = @gameID";
				cmd = new SqlCommand(sql, connection);
				cmd.Parameters.AddWithValue("@playerID", playerID);
				cmd.Parameters.AddWithValue("@gameID", gameID);
				// cmd.Parameters.AddWithValue("@datetime", System.DateTime.Now); // .ToString(dateTimeString));
				cmd.ExecuteNonQuery();
			}

            // update in-memory game object
            Game game = ActivateGame(gameID);
            game.player2 = playerID;
			return "joined";
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
			//using (SqlConnection connection = new SqlConnection(Startup.ConnString))
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

		// checked ready button
		[HttpPost("ready/{gameID}")]
		public string SignalReady(string gameID)
		{
			//string playerID = Request.Form["playerID"];
			//string token = Request.Form["authtoken"];

			//// auth token required for this action
			//if (!UserController.ValidateAuthToken(playerID, token))
			//{
			//	return "auth token invalid";
			//}
			//SetReadiness(gameID, playerID, 1, true);
			//SetReadiness(gameID, playerID, 2, true);
			return "api endpoint deprecated";
		}

		// un-checked ready button
		[HttpPost("unready/{gameID}")]
		public string SignalUnReady(string gameID)
		{
			//string playerID = Request.Form["playerID"];
			//string token = Request.Form["authtoken"];

			//// auth token required for this action
			//if (!UserController.ValidateAuthToken(playerID, token))
			//{
			//	return "auth token invalid";
			//}
			//SetReadiness(gameID, playerID, 1, false);
			//SetReadiness(gameID, playerID, 2, false);
			return "api endpoint deprecated";
		}


		//public void SetReadiness(string gameID, string playerID, int playerNum, bool ready)
		//{
		//	SqlCommand cmd;
		//	using (SqlConnection connection = new SqlConnection(Startup.ConnString))
		//	{
		//		connection.Open();
		//		// leave the game - if this is player2 and no moves have been played yet
		//		string sql = "";
		//		if (playerNum == 1) sql = "UPDATE [dbo].[ActiveGames] SET player1Ready = @ready WHERE Id = @gameID and player1 = @playerID and history is NULL";
		//		if (playerNum == 2) sql = "UPDATE [dbo].[ActiveGames] SET player2Ready = @ready WHERE Id = @gameID and player2 = @playerID and history is NULL";
		//		cmd = new SqlCommand(sql, connection);
		//		cmd.Parameters.AddWithValue("@playerID", playerID);
		//		cmd.Parameters.AddWithValue("@gameID", gameID);
		//		cmd.Parameters.AddWithValue("@ready", ready);
		//		// cmd.Parameters.AddWithValue("@datetime", System.DateTime.Now); // .ToString(dateTimeString));
		//		cmd.ExecuteNonQuery();
		//	}
		//}

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
				return "";
			}
			else return "denied";
		}

		[HttpPost("delete/{gameID}")]
		public async Task<string> DeleteGame(string gameID)
		{
			// get name of player1, the host, the only user authorized to delete the game
			string token = Request.Form["authtoken"];

            Game game = ActivateGame(gameID);

			if (token != "nanobot")
			{
                // only player1 can delete a non-finished game, and only if there is no player2
                if (game.gameover) return "";
                if (game.history.Count < 6)
                {
                    // game hasn't started yet, ok to delete
                    // check auth token
                    if (!UserController.ValidateAuthToken(game.player1, token) &&
                       !UserController.ValidateAuthToken(game.player2, token)) return "auth token invalid";

                }
                else return "game already in progress";
			}

			// delete a specific game
			var cmd = new SqlCommand("delete [dbo].[ActiveGames] where Id = @GameID");
			cmd.Parameters.AddWithValue("@GameID", gameID);
			await SqlCommand.Sql(cmd).Exec();
			return "";
		}

		[HttpPost("delete/inactive/{playerID}")]
		public async Task<string> ClearInactiveFromPlayer(string playerID)
		{
			// validate permission
			string token = Request.Form["authtoken"];
			if (!UserController.ValidateAuthToken(playerID, token)) return "auth token invalid";

			// delete all inactive (uninitialized) games
			var cmd = new SqlCommand("delete [dbo].[ActiveGames] where player1 = @PlayerID and gameover is true");
			cmd.Parameters.AddWithValue("@PlayerID", playerID);
			await SqlCommand.Sql(cmd).Exec();
			return "";
		}
	}
}
