using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace GoGoBackend.Games
{
	public class ActiveGame
	{
		private string username1, username2;
		private ManualResetEvent mre = new ManualResetEvent(false);
		private byte x, y;
		private List<byte[]> moveHistory;

		public static readonly Dictionary<string, ActiveGame> activeGames = new Dictionary<string, ActiveGame>();

		public ActiveGame(string user1, string user2, string key)
		{
			username1 = user1;
			username2 = user2;
			moveHistory = new List<byte[]>();
			activeGames[key] = this;
		}

		public byte[] MakeMove(byte x, byte y)
		{
			this.x = x;
			this.y = y;
			// trigger the previous instance of MakeMove to return the value of this (current) move
			mre.Set();
			// now reset and wait to be called again, then return with the values from the next move
			mre.Reset();
			mre.WaitOne();
			// record and return current move
			byte[] move = new byte[2] { x, y };
			moveHistory.Add(move);
			return move;
		}
	}
}
