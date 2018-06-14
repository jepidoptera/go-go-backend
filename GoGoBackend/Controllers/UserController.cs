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
using StringManipulation;

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
		// gets all data about all users
		// probably disable this method in production
		[HttpGet]
        public async Task Get()
        {
			await SqlPipe.Sql("select * from [dbo].[Table] FOR JSON PATH").Stream(Response.Body, "['No Results Found']"); // correct new version
		}

		// GET: api/User/<username>
		// all infor about a specific user
		[HttpGet("{id}")]
		public async Task Get(string id)
		{
			var cmd = new SqlCommand("select * from [dbo].[Table] where Name = @id FOR JSON PATH, WITHOUT_ARRAY_WRAPPER");
			cmd.Parameters.AddWithValue("id", id);
			// await SqlPipe.Stream(cmd, Response.Body, "{}");
			await SqlPipe.Sql(cmd).Stream(Response.Body, "{}");
		}

		// POST: api/User
		// initializing a new user
		[HttpPost]
		public string Post()
		{
			string password = Request.Form["password"];
			string username = Request.Form["username"];
			string email = Request.Form["email"];

			// make sure all parameters are valid
			if (!Emails.Server.VerifyEmailAddress(email))
			{
				return "invalid email address";
			}

			// create a semi-random validation string
			Random r = new Random();
			string validationString = MD5.Create().ComputeHash((username + password + r.NextDouble().ToString()).Select(c => (byte)c).ToArray()).ToHexString();

			byte[] passwordHash;

			using (MD5 md5Hash = MD5.Create())
			{
				// salt it with the username
				passwordHash = md5Hash.ComputeHash((username + password).Select(c => (byte)c).ToArray());
			}

			// insert new username entry into the database
			using (SqlConnection connection = new SqlConnection(Startup.ConnString))
			{
				connection.Open();
				string sql = "INSERT INTO [dbo].[table] (Username,PasswordHash,Email,Valdiation_String) VALUES(@username,@passwordHash,@email,@validationString)";
				SqlCommand cmd = new SqlCommand(sql, connection);
				cmd.Parameters.AddWithValue("@username", username);
				cmd.Parameters.AddWithValue("@passwordHash", passwordHash);
				cmd.Parameters.AddWithValue("@email", email);
				cmd.Parameters.AddWithValue("@validationString", validationString);
				cmd.ExecuteNonQuery();
			}

			// validation email
			Emails.Server.SendValidationMail(email, String.Format("{0}/api/User/{1}/{2}", Startup.serverName, username, validationString));

			return "success";
		}

		// POST: api/User/username
		// this is a request for a password validation
		[HttpPost("{username}")]
		public string ValidatePassword(string username)
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
				// Get the password hash of any user with this name
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
			// does this user exist?
			if (!foundUser)
			{
				return "user not found";
			}
			// is the password valid??
			// supposedly this comparison method is quite slow, but I don't think I care
			else if (passwordHash.SequenceEqual(passcheck))
			{
				return "success";
			}
			else
			{
				return "wrong password";
			}
		}

		[HttpPost("{username}, {validID}")]
		// POST: api/User/<username>/<validation_code>
		// user clicked link from validation email
		public string ValidateUserLink(string username, string validID)
		{
			var cmd = new SqlCommand(
				@"Select Validation_String from Todo
				where Username = @username;");
			cmd.Parameters.AddWithValue("@username", username);

			string validation_string = "";
			bool foundUser = false;
			using (SqlConnection connection = new SqlConnection(Startup.ConnString))
			{

				connection.Open();
				// Get the password hash of any user with this name
				SqlDataReader reader = cmd.ExecuteReader();
				try
				{
					while (reader.Read())
					{
						validation_string = (string)reader["Validation_String"];
						foundUser = true;
					}
				}
				finally
				{
					// Always call Close when done reading.
					reader.Close();
				}
			}

			if (!foundUser)
			{
				// failed
				return "invalid user";
			}
			else if (validation_string != validID)
			{
				// failed for another reason
				return "invalid code";
			}

			// username and validation code match... update database to show this user is registered

			cmd = new SqlCommand(
				@"update [dbo].[table]
				set Validated = 1,
				Validation_Code = ""
				where Username = @username");
			cmd.Parameters.AddWithValue("@username", username);
			// execute
			SqlCommand.Sql(cmd).Exec();

			return "successfully registered";
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

