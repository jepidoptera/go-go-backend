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
		public List<byte> moveHistory;
		// private bool active = false;

		public static Dictionary<string, ActiveGame> activeGames = new Dictionary<string, ActiveGame>();

		public ActiveGame(string user1, string user2, string key)
		{
			moveHistory = new List<byte>();
			Initialize(user1, user2, key, moveHistory);
		}

		public ActiveGame(string user1, string user2, string key, List<byte> history)
		{
			Initialize(user1, user2, key, history);
		}

		// take arguments from either of the two constructor options
		private void Initialize(string user1, string user2, string key, List<byte> history)
		{
			this.moveHistory = history;
			username1 = user1;
			username2 = user2;
			activeGames[key] = this;
		}

		public string MakeMove(int x, int y)
		{
			// record current move
			if (x >= 0)
			{
				this.x = (byte)x;
				this.y = (byte)y;
				moveHistory.Add(this.x);
				moveHistory.Add(this.y);
			}
			else
			{
				// x < 0 indicates this isn't a real move but just priming the resetevent to receive the next one
			}
			// trigger the previous instance of MakeMove to return the value of this (current) move
			mre.Set();
			// now reset and wait to be called again, then return with the values from the next move
			mre.Reset();
			mre.WaitOne();
			// once waitone has been released by the next call, return the value of the latest move
			return string.Format("{0},{1}", this.x, this.y);
		}
	}
}
