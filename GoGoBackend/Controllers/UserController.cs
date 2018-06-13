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
			string newUser = new StreamReader(Request.Body).ReadToEnd();
			var cmd = new SqlCommand(
				@"insert into [dbo].[Table]
				select *
				from OPENJSON(@newUser)
				WITH( Name varchar(50), PasswordHash binary(32) )");
			cmd.Parameters.AddWithValue("newUser", newUser);
			// await SqlCommand.Exec(cmd);
			await SqlCommand.Sql(cmd).Exec();
		}

		// POST: api/User/username
		// this is a request for a password validation
		[HttpPost("{id}")]
		public async Task Validate(string id)
		{
			string password = Request.Form["Password"];
			// hash the provided password
			string passwordHash;
			using (MD5 md5Hash = MD5.Create())
			{
				passwordHash = md5Hash.ComputeHash(password.Select(c => (byte)c).ToArray()).ToString();
			}

			// get the record for the specified username
			var cmd = new SqlCommand("select * from [dbo].[Table] where Name = @id AND PasswordHash = @password");
			cmd.Parameters.AddWithValue("id", id);
			cmd.Parameters.AddWithValue("password", passwordHash);
			// will return a row if value, nothing if not
			await SqlCommand.Sql(cmd).Exec();

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
        public void Delete(string id)
        {
			var cmd = new SqlCommand("delete [dbo].[Table] where Name = @id");
			cmd.Parameters.AddWithValue("id", id);
        }
	}
}

