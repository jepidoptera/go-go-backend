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
using GoGoBackend.Games;
using System.Threading;

namespace GoGoBackend.Controllers
{
    [Produces("application/json")]
    [Route("api/user")]
    public class UserController : Controller
    {
		private readonly IQueryPipe SqlPipe;
		private readonly ICommand SqlCommand;

		public UserController(ICommand sqlCommand, IQueryPipe sqlPipe)
		{
			this.SqlCommand = sqlCommand;
			this.SqlPipe = sqlPipe;
		}
		
		// GET: api/user
		// gets all data about all users
		// probably disable this method in production
		[HttpGet]
        public async Task Get()
        {
			await SqlPipe.Sql("select * from [dbo].[Users] FOR JSON PATH").Stream(Response.Body, "['No Results Found']"); 
		}

		// GET: api/user/<username>
		// all information about a specific user
		[HttpGet("{id}")]
		public async Task Get(string id)
		{
			var cmd = new SqlCommand("select * from [dbo].[Users] where username = @id FOR JSON PATH, WITHOUT_ARRAY_WRAPPER");
			cmd.Parameters.AddWithValue("id", id);
			// await SqlPipe.Stream(cmd, Response.Body, "{}");
			await SqlPipe.Sql(cmd).Stream(Response.Body, "{}");
		}

		// POST: api/user
		// initializing a new user
		[HttpPost]
		public async Task<string> PostAsync()
		{
			string password = Request.Form["password"];
			string username = Request.Form["username"];
			string email = Request.Form["email"];

			SqlCommand cmd;

			// make sure all parameters are valid
			if (!Emails.Server.VerifyEmailAddress(email))
			{
				return "invalid email address";
			}
			else if (username == "")
			{
				return "invalid username";
			}
			else if (password == "")
			{
				return "invalid password";
			}
			else if (password.Length < 7)
			{
				return "please enter a password of at least 7 characters.";
			}
			else
			{
				// check if username is already taken
				using (SqlConnection connection = new SqlConnection(Startup.ConnString))
				{
					connection.Open();
					cmd = new SqlCommand("Select Validated from [dbo].[Users] where username=@username", connection);
					cmd.Parameters.AddWithValue("@username", username);
					var result = cmd.ExecuteScalar();
					bool eraseRecord = false;
					if (result != null)
					{
						// username is registered. is it confirmed?
						if ((bool)result)
						{
							// registered and confirmed, can't use it
							return "username not available";
						}
						// registered but not confirmed: overwrite
						else eraseRecord = true;
					}

					// similarly, check if email is used already
					cmd = new SqlCommand("Select Validated from [dbo].[Users] where email=@email", connection);
					cmd.Parameters.AddWithValue("@email", email);
					result = cmd.ExecuteScalar();
					if (result != null)
					{
						// email is already registered
						if ((bool)result)
						{
							// can't use it
							return "an account is already registered under that email.";
						}
						// registered, not confirmed. overwrite
						else eraseRecord = true;
					}

					if (eraseRecord)
					{
						// overwriting a previous entry which was never confirmed
						await DeleteUser(username);
					}
				}
			}


			byte[] passwordHash;
			string validationString;

			using (MD5 md5Hash = MD5.Create())
			{
				// get the hash of (username + password)
				//  --  this is known as a salt and improves resistance to dictionary attacks
				passwordHash = md5Hash.ComputeHash((username.ToLower() + password).Select(c => (byte)c).ToArray());
				// create a semi-random validation string
				// TODO: make it actually (securely) random
				Random r = new Random();
				validationString = md5Hash.ComputeHash(( username + password + System.DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss") +
					r.NextDouble().ToString()).Select(c => (byte)c).ToArray()).ToHexString();
			}

			// insert new username entry into the database
			using (SqlConnection connection = new SqlConnection(Startup.ConnString))
			{
				connection.Open();
				string sql = "INSERT INTO [dbo].[Users] (Username,PasswordHash,Email,Validation_String) VALUES(@username,@passwordHash,@email,@validationString)";
				cmd = new SqlCommand(sql, connection);
				cmd.Parameters.AddWithValue("@username", username);
				cmd.Parameters.AddWithValue("@passwordHash", passwordHash);
				cmd.Parameters.AddWithValue("@email", email);
				cmd.Parameters.AddWithValue("@validationString", validationString);
				cmd.ExecuteNonQuery();
			}

			// validation email
			await Emails.Server.SendValidationMail(email, String.Format("{0}/api/user/{1}/{2}", Startup.apiServer, username, validationString));
			// await Emails.Server.SendValidationMail(email, String.Format("https://{0}/api/user/{1}/{2}", "http://localhost:56533", username, validationString));

			return "registered.  please check your email for a confirmation link, then press \"back\" to log in.";
		}

		// POST: api/user/username
		// this is a login attempt
		[HttpPost("{username}")]
		public string ValidatePassword(string username)
		{
			string password = Request.Form["password"];
			// hash the provided password
			byte[] passwordHash;
			using (MD5 md5Hash = MD5.Create())
			{
				// get password hash salted with username
				passwordHash = md5Hash.ComputeHash((username.ToLower() + password).Select(c => (byte)c).ToArray());
			}

			// check the password hash with the specified username
			byte[] passcheck = new byte[16];
			bool validated = false;
			bool foundUser = false;
			using (SqlConnection connection = new SqlConnection(Startup.ConnString))
			{
				var cmd = new SqlCommand("select PasswordHash, Validated from [dbo].[Users] where Username = @id", connection);
				cmd.Parameters.AddWithValue("@id", username);
				connection.Open();
				// Get the password hash of any user with this name
				SqlDataReader reader = cmd.ExecuteReader();
				try
				{
					while (reader.Read())
					{
						passcheck = (byte[])reader["PasswordHash"];
						validated = (bool)reader["Validated"];
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
				return "user not found. register new?  ------>";
			}
			// have they confirmed via email?
			else if (!validated)
			{
				return "user registered but not confirmed.  please use the confirmation link that was emailed to you.";
			}
			// is the password valid??
			// supposedly this comparison method is quite slow, but I don't think I care
			else if (!passwordHash.SequenceEqual(passcheck))
			{
				return "wrong password";
			}
			// passed all the checks, you can log in now
			else
			{
				return "success";
			}
		}

		[HttpGet("{username}/{validID}")]
		// GET: api/user/<username>/<validation_code>
		// user clicked link from validation email
		public async Task<string> ValidateUserLink(string username, string validID)
		{
			string validation_string = "";
			bool foundUser = false;
			SqlCommand cmd;
			using (SqlConnection connection = new SqlConnection(Startup.ConnString))
			{

				cmd = new SqlCommand(
					@"Select Validation_String from [dbo].[Users]
				where Username = @username;", connection);
				cmd.Parameters.AddWithValue("@username", username);
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
				@"update [dbo].[Users] set Validated = 'true', Validation_String = '' where Username = @username");
			cmd.Parameters.AddWithValue("@username", username);
			// execute
			await SqlCommand.Sql(cmd).Exec();

			return "successfully registered";
		}

		// DELETE: api/user/username
		[HttpDelete("{id}")]
        public async Task Delete(string id)
        {
			await DeleteUser(id);
		}

		public async Task DeleteUser(string username)
		{
			var cmd = new SqlCommand("delete [dbo].[Users] where Username = @username");
			cmd.Parameters.AddWithValue("@username", username);
			await SqlCommand.Sql(cmd).Exec();
		}
	}
}

