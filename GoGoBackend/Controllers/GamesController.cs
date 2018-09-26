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
using GoGoBackend.Games;

namespace GoGoBackend.Controllers
{
	[Produces("application/json")]
	[Route("api/games")]
	public class GamesController : Controller
	{

		private readonly IQueryPipe SqlPipe;
		private readonly ICommand SqlCommand;

		private static Dictionary<string, ActiveGame> activeGames;

		public const string dateTimeString = "dd-MM-yyyy HH:mm:ss";

		public GamesController(ICommand sqlCommand, IQueryPipe sqlPipe)
		{
			this.SqlCommand = sqlCommand;
			this.SqlPipe = sqlPipe;
			activeGames = Games.ActiveGame.activeGames;
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

		// GET: api/games/open
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
			string player1 = Request.Form["Player1"];
			string player2 = Request.Form["Player2"];
			int boardSize = Convert.ToInt32(Request.Form["BoardSize"]);
			int mode = Convert.ToInt32(Request.Form["Mode"]);

			SqlCommand cmd;

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
			new ActiveGame(player1, player2, gameID);

			return gameID;
		}

		// POST: /api/Games/{ID}/Move
		[HttpPost("move/{gameID}")]
		public async Task<string> MakeMove(string gameID)
		{
			// unpack args
			string player1 = Request.Form["player"];
			string token = Request.Form["token"];
			int x = Convert.ToInt32(Request.Form["x"]);
			int y = Convert.ToInt32(Request.Form["y"]);
			int opCode = Convert.ToInt32(Request.Form["opcode"]);

			// todo: figure out whether or not it's this player's turn
			// if it's not, we should not respond the same way
			// also todo: figure out if this is an actual active player and not just some rando from the internet


			// is this game already active?
			if (!activeGames.ContainsKey(gameID))
			{
				// no? better activate it
				List<byte> history = new List<byte>();
				string player2 = "";

				// get game info from database
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
						history = (nullhist == DBNull.Value) ? new List<byte>(): new List<byte>((byte[])nullhist);
					}
					dbConnection.Close();
				}
				// does this game even exist?? sanity check
				if (player2 == "") return "this game isn't real";
				// model it as an object with manual reset events
				new ActiveGame(player1, player2, gameID, history);
			}

			// add the move to the active game object
			activeGames[gameID].MakeMove(x, y, opCode);

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
					cmd.Parameters.AddWithValue("@history", activeGames[gameID].moveHistory.ToArray());
					// cmd.Parameters.AddWithValue("@datetime", System.DateTime.Now); //.ToString(dateTimeString));
					cmd.ExecuteNonQuery();
				}
			}

			// run this task again, thus releasing the previous instance to return its value
			string move = await Task.Run(() => activeGames[gameID].AwaitMove());

			return move;
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
		public bool JoinGame(string gameID)
		{
			SqlCommand cmd;
			string playerID = Request.Form["playerID"];
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
			return true;
		}

		// POST: api/games/join/{gameID}
		[HttpPost("leave/{gameID}")]
		public string LeaveGame(string gameID)
		{
			SqlCommand cmd;
			string playerID = Request.Form["playerID"];
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
			return "left";
		}

		// checked ready button
		[HttpPost("ready/{gameID}")]
		public string SignalReady(string gameID)
		{
			SqlCommand cmd;
			string playerID = Request.Form["playerID"];
			SetReadiness(gameID, playerID, false, true);
			SetReadiness(gameID, playerID, true, true);
			return "ready";
		}

		// un-checked ready button
		[HttpPost("unready/{gameID}")]
		public string SignalUnReady(string gameID)
		{
			SqlCommand cmd;
			string playerID = Request.Form["playerID"];
			SetReadiness(gameID, playerID, false, false);
			SetReadiness(gameID, playerID, true, false);
			return "unready";
		}


		public void SetReadiness(string gameID, string playerID, bool player2, bool ready)
		{
			SqlCommand cmd;
			using (SqlConnection connection = new SqlConnection(Startup.ConnString))
			{
				connection.Open();
				// leave the game - if this is player2 and no moves have been played yet
				string sql = "UPDATE [dbo].[ActiveGames] SET player1Ready = @ready WHERE Id = @gameID and player1 = @playerID and history is NULL";
				if (player2) sql = "UPDATE [dbo].[ActiveGames] SET player2Ready = @ready WHERE Id = @gameID and player2 = @playerID and history is NULL";
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
				return "cleared";
			}
			else return "denied";
		}

		[HttpDelete("{gameID}")]
		public async Task<string> DeleteGame(string gameID)
		{
			// delete a specific game
			var cmd = new SqlCommand("delete [dbo].[ActiveGames] where Id = @GameID");
			cmd.Parameters.AddWithValue("@GameID", gameID);
			await SqlCommand.Sql(cmd).Exec();
			return "gone";
		}

		[HttpDelete("inactive/{playerID}")]
		public async Task<string> ClearInactiveFromPlayer(string playerID)
		{
			var cmd = new SqlCommand("delete [dbo].[ActiveGames] where player1 = @PlayerID and history is NULL");
			cmd.Parameters.AddWithValue("@PlayerID", playerID);
			await SqlCommand.Sql(cmd).Exec();
			return "gone";
		}
	}
}
