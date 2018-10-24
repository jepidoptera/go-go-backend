using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Belgrade.SqlClient;
using System.Data.SqlClient;
using GoGoBackend.GoToken;

namespace GoGoBackend.Controllers
{
	[Produces("application/json")]
	[Route("api/token")]
	public class TokenController : Controller
	{

		private readonly IQueryPipe SqlPipe;
		private readonly ICommand SqlCommand;

		// GET: api/games/open
		[HttpPost("init")]
		public async Task<string> InitToken()
		{
			// check for the token contract address - if exists, the token is deployed
			
			string sql = "select * from [dbo].[Token] WHERE ChainUrl = @url";
			string tokenAddress = "";
			using (SqlConnection dbConnection = new SqlConnection(Startup.ConnString))
			using (SqlCommand dbCommand = new SqlCommand(sql, dbConnection))
			{
				dbConnection.Open();
				dbCommand.Parameters.AddWithValue("@url", Token.url);

				SqlDataReader reader = dbCommand.ExecuteReader();

				while (reader.Read())
				{
					tokenAddress = (string)reader["TokenContractAddress"];
				}
				dbConnection.Close();
			}

			if (tokenAddress != "")
			{
				return string.Format("token already deployed at address: {0}", tokenAddress);
			}

			// generate a new account key
			// string privateKey = await Token.GenerateKey();

			// deploy the token contract
			string contractAddress = await Token.DeployContractAsync(out string privateKey);

			if (contractAddress.Length != 40) return contractAddress;

			// save the address
			SqlCommand cmd = new SqlCommand("insert into [dbo].[Token] (id, ChainUrl, TokenContractAddress) values (0, @url, @contractAddress");
			cmd.Parameters.AddWithValue("@url", Token.url);
			cmd.Parameters.AddWithValue("@contractAddress", contractAddress);
			using (SqlConnection sqlConnection = new SqlConnection(Startup.ConnString))
			{
				cmd.ExecuteNonQuery();
			}

			return contractAddress;
		}
	}
}
