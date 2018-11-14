using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GoGoBackend.Go
{
	public class Player
	{
		public static Dictionary<string, Player> players = new Dictionary<string, Player>();

		DateTime lastPing;
		public Player()
		{

		}

		public void Ping()
		{
			lastPing = DateTime.UtcNow;
		}

		// determine if the player is online
		public bool IsOnline()
		{
			return DateTime.UtcNow.Subtract(lastPing) < TimeSpan.FromSeconds(5);
		}
	}
}
