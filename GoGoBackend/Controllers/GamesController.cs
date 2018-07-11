﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Belgrade.SqlClient;
using System.Data.SqlClient;
using System.Security.Cryptography;
using StringManipulation;
using System.Threading.Tasks;
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

		private readonly Dictionary<string, ActiveGame> activeGames;

		public GamesController(ICommand sqlCommand, IQueryPipe sqlPipe)
		{
			this.SqlCommand = sqlCommand;
			this.SqlPipe = sqlPipe;
			activeGames = new Dictionary<string, ActiveGame>();
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
					System.DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss")).Select(c => (byte)c).ToArray()).ToHexString();
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
				cmd.Parameters.AddWithValue("@LastMove", System.DateTime.Now);
				cmd.ExecuteNonQuery();
			}

			// get request origin
			var values = Request.Headers.GetCommaSeparatedValues("Origin");

			// create a new active game
			new ActiveGame(player1, player2, gameID);

			return gameID;
        }

		// POST: /api/Games/{ID}/Move
		[HttpPost("{gameID}/move")]
		public async Task<byte[]> MakeMove(string gameID, [FromBody]string player, [FromBody]int x, [FromBody]int y)
		{
			// todo: figure out whether or not it's this player's turn
			// run this task again, thus releasing the current instance to return its value
			return await Task.Run(() => Games.ActiveGame.activeGames[gameID].MakeMove((byte)x, (byte)y));
		}

        // GET: api/Games/5
        [HttpGet("{id}", Name = "Get")]
        public string Get(int id)
        {
            return "value";
        }
        
        // POST: api/Games
        [HttpPost]
        public void Post([FromBody]string value)
        {
        }
        
        // PUT: api/Games/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody]string value)
        {
        }
        
        // DELETE: api/ApiWithActions/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }

		public async Task DeleteGame(string gameIDString)
		{
			// get gameID from hash of two usernames
			byte[] gameID;
			using (MD5 md5Hash = MD5.Create())
			{
				gameID = md5Hash.ComputeHash(gameIDString.Select(c => (byte)c).ToArray());
			}

			var cmd = new SqlCommand("delete [ActiveGames] where GameID = @GameID");
			cmd.Parameters.AddWithValue("@GameID", gameID);
			await SqlCommand.Sql(cmd).Exec();
		}

	}
}
