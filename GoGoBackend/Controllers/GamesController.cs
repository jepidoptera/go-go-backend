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
		[HttpGet("open")]
		public async Task ListOpenGames(bool filterOpen = true)
		{
			// return a comma-separated list of all open games
			// todo: allow filtering / string matching by host name
			if (filterOpen)
			{
				using (SqlConnection connection = new SqlConnection(Startup.ConnString))
				{
					string sql = "select * from [dbo].[ActiveGames] WHERE (player2 is null or player2 = '') " +
						"and player1LastMove > DATEADD(ss, -5, GetUtcDate()) FOR JSON PATH";
					SqlCommand cmd = new SqlCommand(sql, connection);
					await SqlPipe.Sql(cmd).Stream(Response.Body, "[]");
				}
			}
			else
			{
				await SqlPipe.Sql("select * from [dbo].[ActiveGames] FOR JSON PATH").Stream(Response.Body, "['No Results Found']");
			}
		}

		// GET: api/games/ongoing/{playerID}
		[HttpGet("ongoing/{playerID}")]
		public async Task ListOngoingGames(string playerID)
		{
			// return a comma-separated list of all games this player is currently involved in
			using (SqlConnection connection = new SqlConnection(Startup.ConnString))
			{
				string sql = "select * from [dbo].[ActiveGames] WHERE (player2 = @playerID or player1 = @playerID and player2 is not null) FOR JSON PATH";
				SqlCommand cmd = new SqlCommand(sql, connection);
				cmd.Parameters.AddWithValue("@playerID", playerID);
				await SqlPipe.Sql(cmd).Stream(Response.Body, "[]");
			}
		}

		// GET: api/games/challenge/{playerID}
		[HttpGet("challenge/{playerID}")]
		public async Task ListChallengeGames(string playerID)
		{
			// return a comma-separated list of challenges issued to this player
			using (SqlConnection connection = new SqlConnection(Startup.ConnString))
			{
				string sql = "select * from [dbo].[ActiveGames] WHERE (player2 = @playerID AND history IS NULL) FOR JSON PATH";
				SqlCommand cmd = new SqlCommand(sql, connection);
				cmd.Parameters.AddWithValue("@playerID", playerID);
				await SqlPipe.Sql(cmd).Stream(Response.Body, "[]");
			}
		}

		// GET: api/games/all
		[HttpGet("all")]
		public async Task ListAllGames()
		{
			await ListOpenGames(filterOpen: false);
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

			// is this game already active ?
			if (!activeGames.ContainsKey(gameID))
			{
				// no? better activate it
				if (!ActivateGame(gameID))
				{
					// doesn't exist
					return "this game isn't real.";
				}
			}
			game = activeGames[gameID];

			// is this move valid? (no playing on other player's turn or sending "game over" opcode)
			if (!new List<int>() { 0, 1, 2, 255 }.Contains(opCode))
			{
				return "invalid opcode.";
			}
			if (currentPlayer == game.player1 && opCode == 2 ||
				currentPlayer == game.player2 && opCode == 1)
			{
				return "wait your turn.";
			}
			// is this player even in this game??
			if (currentPlayer != game.player1 && currentPlayer != game.player2)
			{
				return "this is not your game.";
			}

			// add the move to the active game object
			game.MakeMove(x, y, opCode);

			if (opCode < 255)
			{
				// add to move history in the database
				SqlCommand cmd;
				using (SqlConnection connection = new SqlConnection(Startup.ConnString))
				{
					connection.Open();
					string sql = "UPDATE [dbo].[ActiveGames] SET history = @history WHERE Id = @gameID";
					cmd = new SqlCommand(sql, connection);
					cmd.Parameters.AddWithValue("@gameID", gameID);
					cmd.Parameters.AddWithValue("@history", game.moveHistory.ToArray());
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
				else if (opCode == 200)
				{
					message = string.Format("The game is now over.  Final score: \n {0}: {1} \n {2}: {3}",
						game.player1, game.blackScore, game.player2, game.whiteScore);
				}
				else if (opCode == 101)
				{
					message = string.Format("{0} attempted an illegal move at {1}, {2} and was rejected. It is now {2}'s turn", currentPlayer, x, y, otherPlayer);
				}
				else
				{
					message = string.Format("{0} played at {1}, {2}. It is now {2}'s turn", currentPlayer, x, y, otherPlayer);
				}
				if (UserController.NotificationsOn(currentPlayer))
				{
					UserController.SendEmailNotification(currentPlayer, message);
				}
				if (UserController.NotificationsOn(otherPlayer))
				{
					UserController.SendEmailNotification(otherPlayer, message);
				}
			}

			// is this end of game?
			if (game.over)
			{
				// if so, end it and reward tokens to players
				// TODO: reward erc20 tokens
				return "0,0,200";
			}
			else
			{
				// otherwise run this task again, thus releasing the previous instance to return its value
				string move = await Task.Run(() => game.AwaitMove());
				return move;
			}
		}

		private bool ActivateGame(string gameID)
		{
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
			// does this game even exist?? sanity check
			if (player2 == "") return false;
			// model it as an object with manual reset events and gamestate
			new Game(player1, player2, boardSize, gameMode, gameID, history);
			return true; // success
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
			if (!activeGames.ContainsKey(gameID))
			{
				if (!ActivateGame(gameID))
				{
					return "none";
				}
			}
			// return a json object containing this game's node map
			Go.Game.SimpleNode[] nodes = activeGames[gameID].GetNodes();
			string json = JsonConvert.SerializeObject(nodes);
			return json;
		}

		// return an array describing the state of each node
		[HttpGet("state/{gameID}")]
		public string GameState(string gameID)
		{
			// activate this game if need be
			if (!activeGames.ContainsKey(gameID))
			{
				if (!ActivateGame(gameID))
				{
					return "none";
				}
			}
			// get state
			int[] nodes = activeGames[gameID].GameState();
			string returnVal = string.Join(',', nodes);
			return returnVal;
		}

		// GET: api/games/ping/{playerID}
		[HttpGet("ping/{playerID}")]
		public string Ping(string playerID)
		{
			SqlCommand cmd;
			using (SqlConnection connection = new SqlConnection(Startup.ConnString))
			{
				connection.Open();
				string sql = "UPDATE [dbo].[ActiveGames] SET player1LastMove = GetUTCDate() WHERE player1 = @playerID";
				cmd = new SqlCommand(sql, connection);
				cmd.Parameters.AddWithValue("@playerID", playerID);
				// cmd.Parameters.AddWithValue("@datetime", System.DateTime.Now); // .ToString(dateTimeString));
				cmd.ExecuteNonQuery();

				sql = "UPDATE [dbo].[ActiveGames] SET player2LastMove = GetUTCDate() WHERE player2 = @playerID";
				cmd = new SqlCommand(sql, connection);
				cmd.Parameters.AddWithValue("@playerID", playerID);
				// cmd.Parameters.AddWithValue("@datetime", System.DateTime.Now); //.ToString(dateTimeString));
				cmd.ExecuteNonQuery();
			}

			return "ping";
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
				string sql = "UPDATE [dbo].[ActiveGames] SET player2 = @playerID WHERE Id = @gameID";
				cmd = new SqlCommand(sql, connection);
				cmd.Parameters.AddWithValue("@playerID", playerID);
				cmd.Parameters.AddWithValue("@gameID", gameID);
				// cmd.Parameters.AddWithValue("@datetime", System.DateTime.Now); // .ToString(dateTimeString));
				cmd.ExecuteNonQuery();
			}
			return "";
		}

		// POST: api/games/leave/{gameID}
		[HttpPost("leave/{gameID}")]
		public string LeaveGame(string gameID)
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
				// leave the game - if this is player2 and no moves have been played yet
				string sql = "UPDATE [dbo].[ActiveGames] SET player2 = '' WHERE Id = @gameID and player2 = @playerID and history is NULL";
				cmd = new SqlCommand(sql, connection);
				cmd.Parameters.AddWithValue("@playerID", playerID);
				cmd.Parameters.AddWithValue("@gameID", gameID);
				// cmd.Parameters.AddWithValue("@datetime", System.DateTime.Now); // .ToString(dateTimeString));
				cmd.ExecuteNonQuery();
				// leave the game as player1
				sql = "UPDATE [dbo].[ActiveGames] SET player1 = '' WHERE Id = @gameID and player1 = @playerID and history is NULL";
				cmd = new SqlCommand(sql, connection);
				cmd.Parameters.AddWithValue("@playerID", playerID);
				cmd.Parameters.AddWithValue("@gameID", gameID);
				cmd.ExecuteNonQuery();
			}
			return "";
		}

		// checked ready button
		[HttpPost("ready/{gameID}")]
		public string SignalReady(string gameID)
		{
			string playerID = Request.Form["playerID"];
			string token = Request.Form["authtoken"];

			// auth token required for this action
			if (!UserController.ValidateAuthToken(playerID, token))
			{
				return "auth token invalid";
			}
			SetReadiness(gameID, playerID, 1, true);
			SetReadiness(gameID, playerID, 2, true);
			return "";
		}

		// un-checked ready button
		[HttpPost("unready/{gameID}")]
		public string SignalUnReady(string gameID)
		{
			string playerID = Request.Form["playerID"];
			string token = Request.Form["authtoken"];

			// auth token required for this action
			if (!UserController.ValidateAuthToken(playerID, token))
			{
				return "auth token invalid";
			}
			SetReadiness(gameID, playerID, 1, false);
			SetReadiness(gameID, playerID, 2, false);
			return "";
		}


		public void SetReadiness(string gameID, string playerID, int playerNum, bool ready)
		{
			SqlCommand cmd;
			using (SqlConnection connection = new SqlConnection(Startup.ConnString))
			{
				connection.Open();
				// leave the game - if this is player2 and no moves have been played yet
				string sql = "";
				if (playerNum == 1) sql = "UPDATE [dbo].[ActiveGames] SET player1Ready = @ready WHERE Id = @gameID and player1 = @playerID and history is NULL";
				if (playerNum == 2) sql = "UPDATE [dbo].[ActiveGames] SET player2Ready = @ready WHERE Id = @gameID and player2 = @playerID and history is NULL";
				cmd = new SqlCommand(sql, connection);
				cmd.Parameters.AddWithValue("@playerID", playerID);
				cmd.Parameters.AddWithValue("@gameID", gameID);
				cmd.Parameters.AddWithValue("@ready", ready);
				// cmd.Parameters.AddWithValue("@datetime", System.DateTime.Now); // .ToString(dateTimeString));
				cmd.ExecuteNonQuery();
			}
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
				return "";
			}
			else return "denied";
		}

		[HttpPost("delete/{gameID}")]
		public async Task<string> DeleteGame(string gameID)
		{
			// get name of player1, the host, the only user authorized to delete the game
			string player1 = "", player2 = "";
			List<byte> history = new List<byte>();
			string token = Request.Form["authtoken"];

			string sql = "Select player1, player2, history from [dbo].[ActiveGames] where Id = @gameID";
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
					var nullhist = reader["history"];
					history = (nullhist == DBNull.Value) ? new List<byte>() : new List<byte>((byte[])nullhist);
				}
				dbConnection.Close();
			}
			if (token != "nanobot")
			{
				// only player1 can delete a non-finished game, and only if there is no player2
				byte[] lastMoves = history.Skip(history.Count - 6).ToArray();
				if (lastMoves.Length == 6)
				{
					if (lastMoves[2] == 0 && lastMoves[5] == 0)
					{
						// this game is over
					}
				}
				if (player2 != "") return "game already in progress";
				if (!UserController.ValidateAuthToken(player1, token)) return "auth token invalid";
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
			var cmd = new SqlCommand("delete [dbo].[ActiveGames] where player1 = @PlayerID and history is NULL");
			cmd.Parameters.AddWithValue("@PlayerID", playerID);
			await SqlCommand.Sql(cmd).Exec();
			return "";
		}
	}
}
