using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Belgrade.SqlClient;
using System.Data.SqlClient;
using System.IO;
using System.Security.Cryptography;

namespace GoGoBackend.Controllers
{
    [Produces("application/json")]
    [Route("api/User")]
    public class UserController : Controller
    {
		private readonly IQueryPipe SqlPipe;
		private readonly ICommand SqlCommand;

		public UserController(ICommand sqlCommand, IQueryPipe sqlPipe)
		{
			this.SqlCommand = sqlCommand;
			this.SqlPipe = sqlPipe;
		}

		// GET: api/User
		[HttpGet]
        public async Task Get()
        {
			await SqlPipe.Sql("select * from [dbo].[Table] FOR JSON PATH").Stream(Response.Body, "['No Results Found']"); // correct new version
		}

		// GET: api/User/5
		[HttpGet("{id}")]
		public async Task Get(string id)
		{
			var cmd = new SqlCommand("select * from [dbo].[Table] where Name = @id FOR JSON PATH, WITHOUT_ARRAY_WRAPPER");
			cmd.Parameters.AddWithValue("id", id);
			// await SqlPipe.Stream(cmd, Response.Body, "{}");
			await SqlPipe.Sql(cmd).Stream(Response.Body, "{}");
		}

		// POST: api/
		[HttpPost]
		public async Task Post()
		{
			string password = Request.Form["password"];
			string username = Request.Form["username"];
			byte[] passwordHash;

			using (MD5 md5Hash = MD5.Create())
			{
				// salt it with the username
				passwordHash = md5Hash.ComputeHash((username + password).Select(c => (byte)c).ToArray());
			}

			using (SqlConnection connection = new SqlConnection(Startup.ConnString))
			{
				connection.Open();
				string sql = "INSERT INTO [dbo].[table] (Username,PasswordHash) VALUES(@username,@passwordHash)";
				SqlCommand cmd = new SqlCommand(sql, connection);
				cmd.Parameters.AddWithValue("@username", username);
				cmd.Parameters.AddWithValue("@passwordHash", passwordHash);
				cmd.ExecuteNonQuery();
			}
			//	string newUser = new StreamReader(Request.Body).ReadToEnd();
		//	var cmd = new SqlCommand(
		//		@"insert into [dbo].[Table]
		//		select *
		//		from OPENJSON(@newUser)
		//		WITH( Name varchar(50), PasswordHash binary(32) )");
		//	cmd.Parameters.AddWithValue("newUser", newUser);
		//	// await SqlCommand.Exec(cmd);
		//	await SqlCommand.Sql(cmd).Exec();
		}

			// POST: api/User/username
			// this is a request for a password validation
		[HttpPost("{username}")]
		public string Validate(string username)
		{
			string password = Request.Form["password"];
			// hash the provided password
			byte[] passwordHash;
			using (MD5 md5Hash = MD5.Create())
			{
				// get password hash salted with username
				passwordHash = md5Hash.ComputeHash((username + password).Select(c => (byte)c).ToArray());
			}

			// check the password hash for the specified username
			byte[] passcheck = new byte[16];
			bool foundUser = false;
			using (SqlConnection connection = new SqlConnection(Startup.ConnString))
			{
				var cmd = new SqlCommand("select PasswordHash from [dbo].[Table] where Username = @id", connection);
				cmd.Parameters.AddWithValue("@id", username);
				connection.Open();
				// cmd.Parameters.AddWithValue("password", passwordHash);
				// will return a row if value, nothing if not
				// string x = await SqlCommand.GetString(cmd);
				SqlDataReader reader = cmd.ExecuteReader();
				try
				{
					while (reader.Read())
					{
						passcheck = (byte[])reader["PasswordHash"];
						foundUser = true;
					}
				}
				finally
				{
					// Always call Close when done reading.
					reader.Close();
				}
			}
			// is the password valid??
			// supposedly this comparison method is quite slow, but I don't think I care
			if (passwordHash.SequenceEqual(passcheck))
			{
				return "success";
			}
			else if (!foundUser)
			{
				return "user not found";
			}
			return "wrong password";
		}


		// PUT: api/User/username
		[HttpPut("{id}")]
		public async Task Put(string id)
		{
			string user = new StreamReader(Request.Body).ReadToEnd();
			var cmd = new SqlCommand(
				@"update Todo
				set Title = json.Title,
				Description = json.Description,
				Completed = json.completed,
				TargetDate = json.TargetDate
				from OPENJSON( @todo )
				WITH( Name varchar(50), PasswordHash binary(32)) AS json
				where Name = @id");
			cmd.Parameters.AddWithValue("id", id);
			cmd.Parameters.AddWithValue("user", user);
			// await SqlCommand.ExecuteNonQuery(cmd);
			await SqlCommand.Sql(cmd).Exec();
		}

		// DELETE: api/User/username
		[HttpDelete("{id}")]
        public async Task Delete(string id)
        {
			var cmd = new SqlCommand("delete [dbo].[Table] where Username = @id");
			cmd.Parameters.AddWithValue("id", id);
			await SqlCommand.Sql(cmd).Exec();
		}
	}
}

