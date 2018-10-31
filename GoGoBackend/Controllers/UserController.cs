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
using GoGoBackend.Go;
using System.Threading;
using GoToken;

namespace GoGoBackend.Controllers
{
    [Produces("application/json")]
    [Route("api/user")]
    public class UserController : Controller
    {
		private readonly IQueryPipe SqlPipe;
		private readonly ICommand SqlCommand;
		private Random rand;

		public UserController(ICommand sqlCommand, IQueryPipe sqlPipe)
		{
			this.SqlCommand = sqlCommand;
			this.SqlPipe = sqlPipe;
			rand = new Random();
		}
		
		// GET: api/user
		// gets all data about all users
		// probably disable this method in production
        [HttpGet("all")]
        public async Task Get()
        {
			await SqlPipe.Sql("select * from [dbo].[Users] FOR JSON PATH").Stream(Response.Body, "['No Results Found']"); 
		}

		// GET: api/user/<username>
		// all information about a specific user
		[HttpGet("info/{id}")]
		public async Task Get(string id)
		{
			var cmd = new SqlCommand("select * from [dbo].[Users] where username = @id FOR JSON PATH, WITHOUT_ARRAY_WRAPPER");
			cmd.Parameters.AddWithValue("id", id);
			// await SqlPipe.Stream(cmd, Response.Body, "{}");
			await SqlPipe.Sql(cmd).Stream(Response.Body, "{}");
		}

		// POST: api/user/new
		// initializing a new user
        [HttpPost("new")]
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
			else if (password == "password")
			{
				return "come on, you can do better.";
			}
			else if (password.Length < 8)
			{
				return "please enter a password of at least 8 characters.";
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
			await Emails.Server.SendValidationMail(email, String.Format("{0}/api/user/validate/{1}/{2}", Startup.apiServer, username, validationString));
			// await Emails.Server.SendValidationMail(email, String.Format("https://{0}/api/user/{1}/{2}", "http://localhost:56533", username, validationString));

			return "registered.  please check your email for a confirmation link, then press \"back\" to log in.";
		}

        public bool ValidatePassword(string username, string password, out string message)
        {
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
                var cmd = new SqlCommand("select PasswordHash, Validated from [dbo].[Users] where username = @id", connection);
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
                message = "user not found. register new?  ------>";
                return false;
            }
            // have they confirmed via email?
            else if (!validated)
            {
                message = "user registered but not confirmed.  please use the confirmation link that was emailed to you.";
                return false;
            }
            // is the password valid??
            // supposedly this comparison method is quite slow, but I don't think I care
            else if (!passwordHash.SequenceEqual(passcheck))
            {
                message = "wrong password";
                return false;
            }
            else 
            {
                message = "success";
                return true;
            }
        }

        // POST: api/user/username
        // this is a login attempt
        [HttpPost("login/{username}")]
        public string Login(string username)
        {
            string password = Request.Form["password"];
			// passed all the checks, you can log in now
            if (!ValidatePassword(username, password, out string message))
            {
                return message;
            }
            else
			{
				// return an auth token
				byte[] authToken;
				byte[] authCodeHash;
				using (MD5 md5Hash = MD5.Create())
				{
					// get a random auth token
					authToken = md5Hash.ComputeHash((password + rand.NextDouble().ToString()).Select(c => (byte)c).ToArray());
					authCodeHash = md5Hash.ComputeHash(authToken);
				}
				// update database with token hash
				SqlCommand cmd = new SqlCommand(
					@"update [dbo].[Users] set authCodeHash = @authCodeHash where username = @username");
				cmd.Parameters.AddWithValue("@username", username);
				cmd.Parameters.AddWithValue("@authCodeHash", authCodeHash);
				// execute
				SqlCommand.Sql(cmd).Exec();

				// give preimage to user
				return "success: " + authToken.ToHexString();
			}
		}

		public static bool ValidateAuthToken(string username, string token)
		{
			byte[] authCode = new byte[0];
			using (SqlConnection connection = new SqlConnection(Startup.ConnString))
			{
				var cmd = new SqlCommand("select authCodeHash from [dbo].[Users] where Username = @username", connection);
				cmd.Parameters.AddWithValue("@username", username);
				connection.Open();
				// Get the auth token hash of any user with this name
				SqlDataReader reader = cmd.ExecuteReader();
				try
				{
					while (reader.Read())
					{
						authCode = (byte[])reader["authCodeHash"];
					}
				}
				finally
				{
					// Always call Close when done reading.
					reader.Close();
				}
			}
			using (MD5 md5Hash = MD5.Create())
			{
				return md5Hash.ComputeHash(token.ToHexBytes()).EqualsTo(authCode);
			}
		}

        // retrieve arbitrary fields relating to a particular user
        public static string[] UserInfo (string username, params string [] fields)
        {
            string[] returnVal = new string[fields.Length];
            string sql = string.Format("select {0} from [dbo].[Users] where username = @username", string.Join(", ", fields));
            using (SqlConnection connection = new SqlConnection(Startup.ConnString))
            using (SqlCommand cmd = new SqlCommand(sql))
            {
                SqlDataReader reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    // get data from this user
                    for (int i = 0; i < fields.Length; i++)
                    {
                        returnVal[i] = (string)reader[fields[i]];
                    }
                }
                else
                {
                    // no user found
                    return null;
                }
            }
            return returnVal;
        }

        // get a single field from a single user
        public static string UserInfo(string username, string field)
        {
            return UserInfo(username, new string[1] { field })[0];
        }

        [HttpGet("tokenbalance/{username}")]
        public async Task<string> TokenBalance(string username)
        {
            string userAddress = UserInfo(username, "ethAddress");
            float result = await TokenController.GetBalance(userAddress);
            return result.ToString();
        }


        [HttpGet("validate/{username}/{validID}")]
		// GET: api/user/<username>/<validation_code>
		// user clicked link from validation email
		public async Task<string> ValidateUserLink(string username, string validID)
		{
			string validation_string = UserInfo(username, "validation_string");

			if (validation_string == null)
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

			SqlCommand cmd = new SqlCommand(
				@"update [dbo].[Users] set Validated = 'true', Validation_String = '' where Username = @username");
			cmd.Parameters.AddWithValue("@username", username);
			// execute
			await SqlCommand.Sql(cmd).Exec();

			return "Successfully registered.  You may now log in.";
		}

        [HttpPost("options/{username}")]
        public async Task<string> SetOptions(string username)
        {
            string password = Request.Form["password"];
            string newpassword = Request.Form["newPassword"];
            string ethAddress = Request.Form["ethAddress"];
            bool notifications = Request.Form["notifications"] == true;

            if (ValidatePassword(username, password, out string message))
            {
                // don't let them choose a shitty password
                // I mean, it can suck, but there are limits
                if (newpassword == "password")
                {
                    return "come on, you can do better.";
                }
                else if (newpassword.Length < 8)
                {
                    return "please enter a password of at least 8 characters.";
                }

                // update all options
                SetNotifications(username, notifications);

                SqlCommand cmd = new SqlCommand(
                    @"update [dbo].[Users] set password = @newPassword, ethAddress = @ethAddress where username = @username");
                cmd.Parameters.AddWithValue("@username", username);
                cmd.Parameters.AddWithValue("@newpassword", (newpassword != "") ? newpassword : password);
                cmd.Parameters.AddWithValue("@ethAddress", ethAddress);
                // execute
                await SqlCommand.Sql(cmd).Exec();

                return "success";
            }
            // password validation failed
            return message;
        }

        [HttpPost("setnotifications/{username}")]
		public void ActivateNotifications(string username)
		{
			SetNotifications(username, true);
		}

        [HttpPost("setnotifications/{username}")]
		public void DeactivateNotifications(string username)
		{
			SetNotifications(username, false);
		}

		public void SetNotifications(string username, bool on)
		{
			string token = Request.Form["authtoken"];
			if (!ValidateAuthToken(username, token))
			{
				// no go
				return;
			}
			SqlCommand cmd = (on) 
				? new SqlCommand(@"update [dbo].[Users] set EmailNotifications = 'true' where Username = @username")
				: new SqlCommand(@"update [dbo].[Users] set EmailNotifications = 'false' where Username = @username");
			cmd.Parameters.AddWithValue("@username", username);
			// execute
			SqlCommand.Sql(cmd).Exec();
		}

		public static bool NotificationsOn(string username)
		{
			bool returnVal = false;
			using (SqlConnection connection = new SqlConnection(Startup.ConnString))
			{
				var cmd = new SqlCommand("select EmailNotifications from [dbo].[Users] where Username = @username", connection);
				cmd.Parameters.AddWithValue("@username", username);
				connection.Open();
				// Get the auth token hash of any user with this name
				SqlDataReader reader = cmd.ExecuteReader();
				try
				{
					while (reader.Read())
					{
						returnVal = (bool)reader["EmailNotifications"];
					}
				}
				finally
				{
					// Always call Close when done reading.
					reader.Close();
				}
			}
			return returnVal;
		}

		public static void SendEmailNotification(string username, string message)
		{
			// retrieve user's email address
			string email = "";
			using (SqlConnection connection = new SqlConnection(Startup.ConnString))
			{
				connection.Open();
				SqlCommand cmd = new SqlCommand("Select email from [dbo].[Users] where username=@username", connection);
				cmd.Parameters.AddWithValue("@username", username);
				email = (string)cmd.ExecuteScalar();
			}
			Emails.Server.SendNotificationEmail(email, message);
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

