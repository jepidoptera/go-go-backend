using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using StringManipulation;

namespace GoGoBackend.Games
{
	public class ActiveGame
	{
		private string username1, username2;
		private ManualResetEvent mre = new ManualResetEvent(false);
		private byte x, y;
		public List<byte[]> moveHistory;
		// private bool active = false;

		public static Dictionary<string, ActiveGame> activeGames = new Dictionary<string, ActiveGame>();

		public ActiveGame(string user1, string user2, string key)
		{
			username1 = user1;
			username2 = user2;
			moveHistory = new List<byte[]>();
			activeGames[key] = this;
		}

		public string MakeMove(byte x, byte y)
		{
			// flag as active
			// active = true;
			// trigger the previous instance of MakeMove to return the value of this (current) move
			this.x = x;
			this.y = y;
			mre.Set();
			// now reset and wait to be called again, then return with the values from the next move
			mre.Reset();
			mre.WaitOne();
			// record and return current move
			byte[] move = new byte[2] { x, y };
			moveHistory.Add(move);
			return move.ToHexString();
		}
	}
}
