using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        public string passwordHash;
        public string email;
        public bool emailNotifications;
        public string ethAddress;

		DateTime lastPing;
		public Player(string username, string emailAddress = "", string ethAddress = "", int gamesPlayed = 0, int gamesWon = 0)
		{
            this.ethAddress = ethAddress;
            this.email = emailAddress;
            this.gamesPlayed = gamesPlayed;
            this.gamesWon = gamesWon;
            this.username = username;
            // add to static players list
            players.Add(username, this);
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
	}
}
