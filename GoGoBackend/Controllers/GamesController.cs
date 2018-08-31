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
			activeGames = new Dictionary<string, ActiveGame>();
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
						"and player1LastPing > DATEADD(ss, -5, GETUTCDATE()) FOR JSON PATH";
					SqlCommand cmd = new SqlCommand(sql, connection);
					await SqlPipe.Sql(cmd).Stream(Response.Body, "[]");
				}
			}
			else
			{
				await SqlPipe.Sql("select * from [dbo].[ActiveGames] FOR JSON PATH").Stream(Response.Body, "['No Results Found']");
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
				string sql = "INSERT INTO [dbo].[ActiveGames] (Id,Player1,Player2,BoardSize,LastMove) VALUES(@Id,@Player1,@Player2,@BoardSize,@LastMove)";
				cmd = new SqlCommand(sql, connection);
				cmd.Parameters.AddWithValue("Id", gameID);
				cmd.Parameters.AddWithValue("@Player1", player1);
				cmd.Parameters.AddWithValue("@Player2", player2);
				cmd.Parameters.AddWithValue("@BoardSize", boardSize);
				cmd.Parameters.AddWithValue("@LastMove", System.DateTime.Now); //.ToString(dateTimeString));
				cmd.ExecuteNonQuery();
			}

			// get request origin
			var values = Request.Headers.GetCommaSeparatedValues("Origin");

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
			int x = Convert.ToInt32(Request.Form["x"]);
			int y = Convert.ToInt32(Request.Form["y"]);
			// todo: figure out whether or not it's this player's turn
			// if it's not, we sould not respond the same way

			// is this game already active?
			if (activeGames[gameID] == null)
			{
				// no? better activate it
			}

			// add to move history in the database
			SqlCommand cmd;
			using (SqlConnection connection = new SqlConnection(Startup.ConnString))
			{
				connection.Open();
				string sql = "UPDATE [dbo].[ActiveGames] SET history = @history WHERE Id = @gameID";
				cmd = new SqlCommand(sql, connection);
				cmd.Parameters.AddWithValue("@gameID", gameID);
				cmd.Parameters.AddWithValue("@gameID", activeGames[gameID].moveHistory);
				// cmd.Parameters.AddWithValue("@datetime", System.DateTime.Now); //.ToString(dateTimeString));
				cmd.ExecuteNonQuery();
			}

			// run this task again, thus releasing the current instance to return its value
			return await Task.Run(() => activeGames[gameID].MakeMove((byte)x, (byte)y));

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
				string sql = "UPDATE [dbo].[ActiveGames] SET player1LastPing = GetUTCDate() WHERE player1 = @playerID";
				cmd = new SqlCommand(sql, connection);
				cmd.Parameters.AddWithValue("@playerID", playerID);
				// cmd.Parameters.AddWithValue("@datetime", System.DateTime.Now); // .ToString(dateTimeString));
				cmd.ExecuteNonQuery();

				sql = "UPDATE [dbo].[ActiveGames] SET player2LastPing = GetUTCDate() WHERE player2 = @playerID";
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
		public void ClearGames()
		{
			var cmd = new SqlCommand("delete [dbo].[ActiveGames]");
			SqlCommand.Sql(cmd).Exec();
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

		// DELETE api/games/inactive
		[HttpDelete("inactive")]
		public void DeleteInactiveGames()
		{
			string sql = "DELETE [dbo].[ActiveGames] WHERE (player2 is null or player2 = '') and player1LastPing < DATEADD(ss, -5, GETUTCDATE())";
		}

		[HttpDelete("inactive/{playerID}")]
		public async Task<string> ClearInactiveFromPlayer(string playerID)
		{
			var cmd = new SqlCommand("delete [dbo].[ActiveGames] where player1 = @PlayerID");
			cmd.Parameters.AddWithValue("@PlayerID", playerID);
			await SqlCommand.Sql(cmd).Exec();
			return "gone";
		}
	}
}
