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
using System.Data.SqlTypes;
using Newtonsoft.Json;

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
        [HttpGet("all")]
        public async Task Get()
        {
			await SqlPipe.Sql("select * from [dbo].[Users] FOR JSON PATH").Stream(Response.Body, "['No Results Found']"); 
		}

        // GET: api/user
        // gets all data about all users
        [HttpGet("names/all")]
        public string GetNames()
        {
            List<string> results = new List<string>();
            string sql = "select username from[dbo].[Users]";
            using (SqlConnection connection = new SqlConnection(Startup.ConnString))
            using (SqlCommand cmd = new SqlCommand(sql, connection))
            {
                connection.Open();
                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    results.Add((string)reader["username"]);
                }
            }
            return string.Join(",", results);
        }

        // GET: api/user/<username>
        // all information about a specific user
        [HttpGet("info/{username}")]
        public async Task Get(string username)
		{
			var cmd = new SqlCommand("select * from [dbo].[Users] where username = @id FOR JSON PATH, WITHOUT_ARRAY_WRAPPER");
			cmd.Parameters.AddWithValue("id", username);
			// await SqlPipe.Stream(cmd, Response.Body, "{}");
			await SqlPipe.Sql(cmd).Stream(Response.Body, "{}");
		}

		// POST: api/user/new
		// initializing a new user
        [HttpPost("new")]
		public async Task<string> NewUser()
		{
			string password = Request.Form["password"];
			string username = Request.Form["username"];
			string email = Request.Form["email"];
			string ethAddress = Request.Form["ethAddress"];

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
            else if (username.Contains(',') || username.Contains(' '))
            {
                return "username may not contain spaces or commas";
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
                // or don't, this is probably good enough
				Random r = new Random();
				validationString = md5Hash.ComputeHash(( username + password + System.DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss") +
					r.NextDouble().ToString()).Select(c => (byte)c).ToArray()).ToHexString();
			}

			// insert new username entry into the database
			using (SqlConnection connection = new SqlConnection(Startup.ConnString))
			{
				connection.Open();
				string sql = "INSERT INTO [dbo].[Users] (Username,PasswordHash,Email,ethAddress,Validation_String)" +
					" VALUES(@username,@passwordHash,@email,@ethAddress,@validationString)";
				cmd = new SqlCommand(sql, connection);
				cmd.Parameters.AddWithValue("@username", username);
				cmd.Parameters.AddWithValue("@passwordHash", passwordHash);
				cmd.Parameters.AddWithValue("@email", email);
				cmd.Parameters.AddWithValue("@ethaddress", ethAddress);
				cmd.Parameters.AddWithValue("@validationString", validationString);
				cmd.ExecuteNonQuery();
			}

			// validation email
			await Emails.Server.SendValidationMail(email, String.Format("{0}/api/user/validate/{1}/{2}", Startup.apiServer, username, validationString));

			return "registered.  please check your email for a confirmation link, then press \"back\" to log in.";
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
                using (MD5 md5Hash = MD5.Create())
                {
                    // get a random auth token
                    authToken = md5Hash.ComputeHash((password + rand.NextDouble().ToString()).Select(c => (byte)c).ToArray()); 
                    //authCodeHash = md5Hash.ComputeHash(authToken);
                }
                // update database with token hash
                UpdatePlayer(username, authToken: authToken);
                //SqlCommand cmd = new SqlCommand(
                //  @"update [dbo].[Users] set authCodeHash = @authCodeHash where username = @username");
                //cmd.Parameters.AddWithValue("@username", username);
                //cmd.Parameters.AddWithValue("@authCodeHash", authCodeHash);
                //// execute
                //SqlCommand.Sql(cmd).Exec();

                // give preimage to user
                return "success: " + authToken.ToHexString();
            }
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

            // get player object
            Player player = ActivatePlayer(username);

            // does this user exist?
            if (player == null)
            {
                message = "user not found. register new?  ------>";
                return false;
            }
            // have they confirmed via email?
            else if (!player.validated)
            {
                message = "user registered but not confirmed.  please use the confirmation link that was emailed to you.";
                return false;
            }
            // is the password valid??
            // supposedly this comparison method is quite slow, but I don't think I care
            else if (!player.passwordHash.SequenceEqual(passwordHash))
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

		public static bool ValidateAuthToken(string username, string token)
		{
            // check against stored player object
            Player player = ActivatePlayer(username);
            if (player == null)
                return false;

            // does the hash match?
            using (MD5 md5Hash = MD5.Create())
			{
				return md5Hash.ComputeHash(token.ToHexBytes()).EqualsTo(player.authCodeHash);
			}
		}

        [HttpGet("tokenbalance/{username}")]
        public async Task<int> TokenBalance(string username)
        {
            string userAddress = ActivatePlayer(username).ethAddress; // UserInfo<string>(username, "ethAddress");
            if (userAddress != null)
            {
                float result = await TokenController.GetBalance(userAddress);
                return (int)result;
            }
            else return 0;
        }


        [HttpGet("validate/{username}/{validID}")]
		// GET: api/user/<username>/<validation_code>
		// user clicked link from validation email
		public string ValidateUserLink(string username, string validID)
		{
            string validation_string = ActivatePlayer(username).validationString; // UserInfo<String>(username, "validation_string");

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

            UpdatePlayer(username, validated: "'true'", validationString: "");

			return "Successfully registered.  You may now log in.";
		}

        [HttpPost("options/{username}")]
        public string SetOptions(string username)
        {
            string password = Request.Form["password"];
            string newpassword = Request.Form["newPassword"];
            string ethAddress = Request.Form["ethAddress"];
            string notifications = Request.Form["notifications"];

            if (ValidatePassword(username, password, out string message))
            {
                // don't let them choose a shitty password
                // I mean, it can suck, but there are limits
                if (newpassword == "")
                {
                    // that's fine, you don't have to set a new password. just use old one in this case
                    newpassword = null;
                }
                else if (newpassword == "password")
                {
                    return "come on, you can do better.";
                }
                else if (newpassword.Length < 8)
                {
                    return "please enter a password of at least 8 characters.";
                }

                // update all options
                UpdatePlayer(username, newpassword, emailNotifications: "'" + notifications + "'", ethAddress: ethAddress);

                return "success";
            }
            // password validation failed
            return message;
        }

        // retrieve arbitrary fields relating to a particular user
        //public static T[] UserInfo<T>(string username, params string[] fields)
        //{
        //    T[] returnVal = new T[fields.Length];
        //    string sql = string.Format("select {0} from [dbo].[Users] where username = @username", string.Join(", ", fields));
        //    using (SqlConnection connection = new SqlConnection(Startup.ConnString))
        //    using (SqlCommand cmd = new SqlCommand(sql, connection))
        //    {
        //        connection.Open();
        //        cmd.Parameters.AddWithValue("@username", username);
        //        SqlDataReader reader = cmd.ExecuteReader();
        //        if (reader.Read())
        //        {
        //            // get data from this user
        //            for (int i = 0; i < fields.Length; i++)
        //            {
        //                if (!(reader[fields[i]] is DBNull))
        //                    returnVal[i] = (T)reader[fields[i]];
        //            }
        //        }
        //        else
        //        {
        //            // no user found
        //            returnVal = null;
        //        }
        //        connection.Close();
        //    }
        //    return returnVal;
        //}

        //// get a single field from a single user
        //public static T UserInfo<T>(string username, string field)
        //{
        //    return UserInfo<T>(username, new string[1] { field })[0];
        //}

        // a general method to update player info in database and memory at the same time
        public static void UpdatePlayer(string username, string password = null, string emailAddress = null, 
        string emailNotifications = null, string ethAddress = null, 
        int gamesPlayed = -1, int gamesWon = -1, byte[] authToken = null, 
        string validated = null, string validationString = null)
        {
            Player player = ActivatePlayer(username);

            string sql = "update [dbo].[Users] set";
            byte[] passwordHash = null;
            byte[] authtokenHash = null;
            List<string> args = new List<string>();

            if (password != null)
            {
                args.Add(" passwordHash=@passwordHash");
                // salt and hash password
                passwordHash = MD5.Create().ComputeHash((username.ToLower() + password).Select(c => (byte)c).ToArray());
                player.passwordHash = passwordHash;
            }
            if (emailAddress != null)
            {
                args.Add(" emailAddress=@emailAddress");
                player.email = emailAddress;
            }
            if (emailNotifications != null)
            {
                if (!emailNotifications.Equals("'true'",StringComparison.OrdinalIgnoreCase) && 
                    !emailNotifications.Equals("'false'", StringComparison.OrdinalIgnoreCase))
                    throw new Exception("invalid boolean value: emailNotifications");
                args.Add(" emailNotifications=@emailNotifications");
                player.emailNotifications = emailNotifications == "'true'";
            }
            if (ethAddress != null)
            {
                args.Add(" ethAddress=@ethAddress");
                player.ethAddress = ethAddress;
            }
            if (gamesPlayed != -1)
            {
                args.Add(" gamesPlayed=@gamesPlayed");
                player.gamesPlayed = gamesPlayed;
            }
            if (gamesWon != -1)
            {
                args.Add(" gamesWon=@gamesWon");
                player.gamesWon = gamesWon;
            }
            if (authToken != null)
            {
                args.Add(" authCodeHash=@authtokenHash");
                authtokenHash = MD5.Create().ComputeHash(authToken);
                player.authCodeHash = authtokenHash;
            }
            if (validated != null)
            {
                if (!validated.Equals("'true'", StringComparison.OrdinalIgnoreCase) && 
                    !validated.Equals("'false'", StringComparison.OrdinalIgnoreCase))
                    throw new Exception("invalid boolean value: validated");
                args.Add(" validated=@validated");
                player.validated = validated == "'true'";
            }
            if (validationString != null)
            {
                args.Add(" validation_String=@validationString");
                player.validationString = validationString;
            }

            // put it all together
            sql += string.Join(',', args.ToArray()) + " where username=@username";
            // add parameters
            using (SqlConnection connection = new SqlConnection(Startup.ConnString))
            using (SqlCommand cmd = new SqlCommand(sql, connection))
            {
                connection.Open();
                cmd.Parameters.AddWithValue("@username", username);
                if (emailAddress != null) cmd.Parameters.AddWithValue("@emailAddress", emailAddress);
                if (emailNotifications != null) cmd.Parameters.AddWithValue("@emailNotifications", emailNotifications.Equals("'true'", StringComparison.OrdinalIgnoreCase));
                if (ethAddress != null) cmd.Parameters.AddWithValue("@ethAddress", ethAddress);
                if (gamesPlayed != -1) cmd.Parameters.AddWithValue("@gamesPlayed", gamesPlayed);
                if (gamesWon != -1) cmd.Parameters.AddWithValue("@gamesWon", gamesWon);
                if (authtokenHash != null) cmd.Parameters.AddWithValue("@authtokenHash", authtokenHash);
                if (passwordHash != null) cmd.Parameters.AddWithValue("@passwordHash", passwordHash);
                if (validated != null) cmd.Parameters.AddWithValue("@validated", validated.Equals("'true'", StringComparison.OrdinalIgnoreCase));
                if (validationString != null) cmd.Parameters.AddWithValue("@validationString", validationString);

                cmd.ExecuteNonQuery();
                connection.Close();
            }
        }

        public static void SendEmailNotification(string email, string message)
		{
			Emails.Server.SendNotificationEmail(email, message);
		}

        // GET: api/user/ping/{playerID}
        [HttpGet("ping/{playerID}")]
        public string Ping(string playerID)
        {
            Player player = ActivatePlayer(playerID);
            if (player == null) return null;
            player.Ping();
            return player.Message;
        }

        public static Player ActivatePlayer(string playerID)
		{
            // let's not be case-sensitive
            playerID = playerID.ToLower();

            // if player already exists in memory, return immediately
            if (Player.players.ContainsKey(playerID)) return Player.players[playerID];

            // otherwise, create the player object from database info
            Player returnPlayer = null;

            // query database
            string sql = "Select * from [dbo].[Users] where username = @playerID for json auto, without_array_wrapper";
            using (SqlConnection dbConnection = new SqlConnection(Startup.ConnString))
            using (SqlCommand dbCommand = new SqlCommand(sql, dbConnection))
            {
                dbCommand.Parameters.AddWithValue("@playerID", playerID);
                dbConnection.Open();

                SqlDataReader reader = dbCommand.ExecuteReader();

                while (reader.Read())
                {
					// get all fields
                    string readerString = (string)reader[0];
                    // get rid of those superfluous escape characters - since i can't figure out how to prevent them being inserted
                    readerString = readerString.Replace(@"\", "");
                    returnPlayer = JsonConvert.DeserializeObject<Player>(readerString);
                }
                dbConnection.Close();
            }
            // add to player dictionary
            Player.Add(returnPlayer);

            return returnPlayer; // new Player(username, emailAddress, ethAddress, gamesPlayed, gamesWon);
            // success
        }

        // DELETE: api/user/username
        [HttpDelete("{username}")]
        public async Task Delete(string username)
        {
			await DeleteUser(username);
		}

		public async Task DeleteUser(string username)
		{
			var cmd = new SqlCommand("delete [dbo].[Users] where Username = @username");
			cmd.Parameters.AddWithValue("@username", username);
			await SqlCommand.Sql(cmd).Exec();
		}
	}
}

