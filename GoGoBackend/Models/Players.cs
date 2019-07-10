using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;
using StringManipulation;

namespace GoGoBackend.Go
{
	public class Player
	{
		public static Dictionary<string, Player> players = new Dictionary<string, Player>();
        // player profile
        public string username;
        public int gamesPlayed;
        public int gamesWon;
        public int stones;
        public float rank;
        public bool validated;
        public string validation_String;
        public byte[] passwordHash;
        public string email;
        public bool emailNotifications;
        public string ethAddress;
        public byte[] authCodeHash;
        private List<string> messages = new List<string>();
        public string Message
        {
            get { string returnval = ""; if (messages.Count > 0) { returnval = messages[0]; messages.RemoveAt(0); } return returnval; }
            set { messages.Add(value); }
        }

        DateTime lastPing;
		//public Player()
        //(string username, string emailAddress = "", string ethAddress = "", 
        //int gamesPlayed = 0, int gamesWon = 0, byte[] authtokenHash = null,
        //bool validated = false, string validationString = "", string email = "", bool emailNotifications = false)
		//{
            //this.ethAddress = ethAddress;
            //this.email = emailAddress;
            //this.gamesPlayed = gamesPlayed;
            //this.gamesWon = gamesWon;
            //this.username = username;
            //this.authtokenHash = authtokenHash;
            //this.validated = validated;
            //this.validationString = validationString;
            // add to static players list
            //nplayers.Add(username, this);
		//}

        public static void Add(Player player)
        {
            // add this play object to static list
            players.Add(player.username.ToLower(), player);
        }

        public void Ping()
		{
			lastPing = DateTime.UtcNow;
		}

		// has it been pinged in the last eleven seconds?
		public bool IsOnline()
		{
			return DateTime.UtcNow.Subtract(lastPing) < TimeSpan.FromSeconds(11);
		}

        // check if authtoken is valid
        public bool ValidateAuthToken(string token)
        {
            using (MD5 md5Hash = MD5.Create())
            {
                return md5Hash.ComputeHash(token.ToHexBytes()).EqualsTo(authCodeHash);
            }
        }
    }
}
