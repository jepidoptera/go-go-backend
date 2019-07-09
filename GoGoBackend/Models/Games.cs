using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using StringManipulation;
using Shapes;

namespace GoGoBackend.Go
{
	public class Game
	{
		private class Point
		{
			public int x;
			public int y;
            public Point(int x, int y)
			{
				this.x = x;
				this.y = y;
			}
		}

		public class SimpleNode
		{
			public int index;
			public int[] neighbors;
		}

		public enum Opcodes
		{
			black = 0,
			white = 1,
			pass = 2,
			lake = 3,
			illegal = 9,
			gameover = 10,
			joingame = 11,
			ping = 15
		}

		public string Id = "";
		public string white, black, currentPlayer;
		public string player1LastMove;
		public string player2LastMove;

		public List<byte> history;
		public int gameMode;
		public int boardSize;
		public bool gameover = false;
		private Node[] gameState;
        public int blackStonesCaptured = 0;
        public int whiteStonesCaptured = 0;
		public int whiteScore = 0;
		public int blackScore = 0;
		public bool online = false;
		public string description;

		private const int stone_black = 0;
		private const int stone_white = 1;

		private int passTurns;
		private int turn;

		private ManualResetEvent mre = new ManualResetEvent(false);
		private byte x, y;
		private Opcodes opcode;

		// private bool active = false;

		public static Dictionary<string, Game> activeGames = new Dictionary<string, Game>();

        public int NodeCount { get { return gameState.Length; } }

        // constructor overloads - with or without history
		public Game(string user1, string user2, int boardSize, int gameMode, string key)
		{
			Initialize(user1, user2, boardSize, gameMode, key, new List<byte>());
		}

		public Game(string user1, string user2, int boardSize, int gameMode, string key, List<byte> history, string player1LastMove = "", string player2LastMove = "")
		{
			Initialize(user1, user2, boardSize, gameMode, key, history, player1LastMove, player2LastMove);
		}

		// take arguments from either of the two constructor options
		private void Initialize(string player1, string player2, int boardSize, int gameMode, string gameID, List<byte> history, string player1LastMove = "", string player2LastMove = "")
		{
			this.Id = gameID;
			this.history = history;
			this.white = player1;
			this.black = player2;
			this.player1LastMove = player1LastMove;
			this.player2LastMove = player2LastMove;
            currentPlayer = player2;
			this.boardSize = boardSize;
			this.gameMode = gameMode;
			this.gameState = BuildNodeGraph(gameMode, boardSize);
			this.turn = (int)Opcodes.black;
			if (history.Count > 0) PlayThrough(history, boardSize, gameMode);
			activeGames[gameID] = this;
			if (gameMode == 0) description = string.Format("standard square {0}x{0}", boardSize);
			if (gameMode == 1) description = string.Format("icosasphere: {0}", boardSize);
			if (gameMode == 2) description = string.Format("hexasphere: {0}", boardSize);
		}

		public string AwaitMove()
		{
			mre.Reset();
			mre.WaitOne();
			// once waitone has been released by the next call, return the value of the latest move
			return string.Format("{0},{1},{2}", this.x, this.y, (int)this.opcode);
		}

		public bool MakeMove(int x, int y, int opCode)
		{
			// record current move
			if (opCode < (int)Opcodes.ping)
			{
				this.x = (byte)x;
				this.y = (byte)y;
				this.opcode = (Opcodes)opCode;
				// try to play that move
				if (TryPlayStone(LocationOf(x, y), opCode))
				{
					// success
					history.Add(this.x);
					history.Add(this.y);
					history.Add((byte)this.opcode);
					// if game ends, return opcode.gameover
					if (gameover)
					{
						this.opcode = Opcodes.gameover;
					}
				}
				else
				{
					// illegal move, lose one turn
					this.opcode = Opcodes.illegal;
                    mre.Set();
                    return true;
				}
				// trigger the previous instance of MakeMove to return the value of this (current) move
				if (!gameover) mre.Set();
				return true;
			}
			else
			{
				// opcode.ping indicates this isn't a real move but just priming the resetEvent to receive the next one
				return false;
			}
		}

        // return value without adding an actual move - happens at gameover
        public void ReturnMove(int x, int y, int opcode)
        {
            this.x = (byte)x;
            this.y = (byte)y;
            this.opcode = (Opcodes)opcode;
            mre.Set();
        }

        // attempt to play a stone
        // if the move isn't legal for any reason, return false
        bool TryPlayStone(int location, int color)
		{
			if (color == (int)Opcodes.pass)
			{
				PassTurn();
				return true;
			}
			// gotta play in turn
			if (color != turn) return false;

			// only play on a square that isn't occupied
			if (gameState[location].stone < 0) return false;

			// fill that grid point with a placeholder stone
			gameState[location].stone = turn;

			// check for captured stones
			List<int> captures = Captures(location);
			if (captures.Count > 0)
			{
				CaptureStones(captures);
			}

			// next turn...
			NextTurn();

			// if the stone that was just played would be captured, 
			// (by neighbor[0], for instance) it isn't a legal move
			if (Captures(gameState[location].neighbors[0].index).Count > 0)
			{
				// take it back
				gameState[location].stone = 0;
				NextTurn();
				return false;
			}

            // todo: research and implement ko rule

            // if turn was not passed and was played successfully, reset pass turn count
            passTurns = 0;

            // played successfully
            return true;
		}

		private void CaptureStones(List<int> captures)
		{
			foreach (int stone in captures)
			{
				// -1 point for the player whose stone was captured
				if (gameState[stone].stone == stone_black) blackScore -= 1;
				if (gameState[stone].stone == stone_white) whiteScore -= 1;
				gameState[stone].stone = 0;
			}
		}

		private void PassTurn()
		{
			passTurns += 1;
			if (passTurns > 1)
			{
				// two consecutive passes = game over!
				GameOver();
			}
			// next player's turn
			NextTurn();
		}

		// it's the next player's turn.
		// change whatever needs to change to reflect that
		void NextTurn()
		{
			// switch turns
			turn = (turn == stone_white) ? stone_black : stone_white;
            currentPlayer = (turn == stone_black) ? black : white;
		}

		// create a list of points which contain stones which would be captured if current player moves at [location]
		List<int> Captures(int location)
		{
			List<int> captiveGroup = new List<int>();
			bool breathingRoom;
			for (int i = 0; i < gameState[location].neighbors.Count; i++)
			{
				int n = gameState[location].neighbors[i].index;
				if (gameState[n].stone != 0 && gameState[n].stone != gameState[location].stone &&
					!captiveGroup.Contains(n))
				{
					GroupAlike(start: n, group: out List<int> enemyGroup, neighbors: out List<int> enemyNeighbors);
					// do they have breathing room?
					breathingRoom = false;
					foreach (int e in enemyNeighbors)
					{
						if (gameState[e].stone == 0)
						{
							breathingRoom = true;
							break;
						}
					}
					if (!breathingRoom)
					{
						// nope
						captiveGroup.AddRange(enemyGroup);
					}
				}
			}

			return captiveGroup;
		}

		private void GroupAlike(int start, out List<int> group, out List<int> neighbors)
		{
			int groupColor = gameState[start].stone;
			group = new List<int>();
			neighbors = new List<int>();
			// start search from the given point
			List<int> searching = new List<int>() { start };
			List<int> newSearches = new List<int>();

			int thisStone;

			while (searching.Count > 0)
			{
				foreach (int i in searching)
				{
					for (int j = 0; j < gameState[i].neighbors.Count; j++)
					{
						// look at neighboring grid points to see if they match the given type (black, white, or empty)
						thisStone = gameState[i].neighbors[j].stone;
						int index = gameState[i].neighbors[j].index;
						if (thisStone == groupColor)
						{
							// same color, add it to group and search from there next
							if (!group.Contains(index) && !searching.Contains(index) && !newSearches.Contains(index))
							{
								newSearches.Add(index);
							}
						}
						else
						{
							// different color, add it to neighbors if it isn't there already
							if (!neighbors.Contains(index))
							{
								neighbors.Add(index);
							}
						}
					}
				}
				// search the next group
				group.AddRange(searching);
				searching.Clear();
				searching.AddRange(newSearches);
				newSearches.Clear();
			}
		}

		int[] Score(out List<int> dead_stones)
		{
			// find the score between two players on the given board
			dead_stones = new List<int>();
			bool[] scored = new bool[gameState.Length];
			List<int>[] territory = new List<int>[3] { new List<int>(), new List<int>(), new List<int>() };
			List<int> nullTerritory = territory[0];
			List<int> blackTerritory = territory[1];
			List<int> whiteTerritory = territory[2];
			for (int i = 0; i < gameState.Length; i++)
			{
				if (gameState[i].owner == 0)
				{
					// this may be an uncounted region of territory
					if (gameState[i].stone == 0)
					{
						// empty, uncounted space
						int blackBorders = 0, whiteBorders = 0;
						GroupAlike(i, out nullTerritory, out List<int> surroundings);
						// count white vs. black stones surrounding the territory
						foreach (int p in surroundings)
						{
							if (gameState[p].stone == stone_black) blackBorders += 1;
							else whiteBorders += 1;
						}
						// this is kinda crude, but should suffice in most circumstances
						int winner = (blackBorders > whiteBorders) ? stone_black : stone_white;
						territory[winner].AddRange(nullTerritory);
						// mark the whole region as scored
						foreach (int p in nullTerritory)
						{
							gameState[p].owner = winner;
						}
					}
				}
			}

			// capture stones which find themselves surrounded by enemy territory
			for (int i = 0; i < gameState.Length; i++)
			{
				if (gameState[i].owner == 0)
				{
					// a group of stones which hasn't been processed yet
					bool safe = false;
					int groupColor = gameState[i].stone;
					int opponent = (groupColor == stone_black) ? stone_white : stone_black;
					GroupAlike(i, out List<int> group, out List<int> surroundings);
					// does this group border on any friendly territory?
					foreach (int p in surroundings)
					{
						if (gameState[p].owner == groupColor)
						{
							safe = true;
							break;
						}
					}

					if (safe)
					{
						// mark the whole group as scored
						foreach (int p in group)
						{
							gameState[p].owner = groupColor;
						}
					}
					else
					{
						// score for opponent
						territory[opponent].AddRange(group);
						foreach (int p in group)
						{
							gameState[p].owner = opponent;
						}
						// capture stones
						dead_stones.AddRange(group);
					}
				}
			}

			return new int[2] { blackTerritory.Count + blackScore, whiteTerritory.Count + whiteScore };
		}

		void PlayThrough(List<byte> history, int boardSize = 19, int boardType = 0)
		{
			for (int i = 0; i < history.Count; i += 3)
			{
				// three bytes at a time; one corresponds to x and the other to y 
				// (or both to location, as the case may be)
				int x = history[i];
				int y = history[i + 1];
				int opCode = history[i + 2];

				if (opCode == (int)Opcodes.joingame) { /* the opcode that does nothing. */ }
				else if (opCode == 0)
				{
					// pass
					PassTurn();
				}
				else if (!TryPlayStone(LocationOf((byte)x, (byte)y), opCode))
				{
					// problem
				}
			}
		}

		private int LocationOf(int x, int y)
		{
			if (gameMode == 0)
			{
				return x * boardSize + y;
			}
			else
			{
				return x * 256 + y;
			}
		}

		// the game has ended
		void GameOver()
		{
			List<int> captiveStones;
			int[] scores = Score(out captiveStones);
			// capture those which were surrounded in enemy territory
			CaptureStones(captiveStones);
			gameover = true;
			// save final scores
			blackScore = scores[0];
			whiteScore = scores[1];
		}

		public static Node[] BuildNodeGraph(int type, int size)
		{
			Node[] gameState = new Node[0];
			if (type == 0)
			{
				// nodes array
				Node[,] grid = new Node[size, size];
				gameState = new Node[size * size];
				// init each node
				for (int x = 0; x < size; x++)
				{
					for (int y = 0; y < size; y++)
					{
						grid[x, y] = new Node(-1);
					}
				}
				// initialize each node in the grid,
				// assign its neighbors and commit to 1-d array
				for (int x = 0; x < size; x++)
				{
					for (int y = 0; y < size; y++)
					{
						List<Point> neighbors = SquareNeighbors(x, y, size);
						for (int i = 0; i < neighbors.Count; i++)
						{
							grid[x, y].neighbors.Add(grid[neighbors[i].x, neighbors[i].y]);
						}
						/// get array position
						int index = x * size + y;
						grid[x, y].index = index;
						// translate to final array
						gameState[index] = grid[x, y];
					}
				}
			}
			else if (type == 1)
			{
				// icosasphere
				gameState = TriangleGrid.BuildWorld(size);
			}
			else if (type == 2)
			{
				// hexasphere
				int meshSize = size * 3 + 1;

				gameState = TriangleGrid.BuildWorld(meshSize, out TriangleGrid[] faces);

				// this map is built of hexagons, not triangles
				// so we'll remove some of the nodes to leave the correct shape
				int startPosition = 0;
				int index;
				foreach (TriangleGrid face in faces)
				{
					index = 0;
					startPosition = 0;
					for (int row = 0; row < meshSize; row++)
					{
						for (int i = 0; i <= row; i++)
						{
							if ((i + startPosition) % 3 == 0)
							{
								// delete every third node to form hexagons
								face.nodes[index].Remove();
							}
							index += 1;
						}
						startPosition = (startPosition + 1) % 3;
					}
				}

				// build a list of all the nodes which were not removed
				List<Node> tempNodes = new List<Node>();
				index = 0;
				foreach (Node node in gameState)
				{
					if (node.neighbors.Count > 0)
					{
						// we're keeping this one, add to temp list
						node.index = index;
						tempNodes.Add(node);
						index += 1;
					}
				}
				// make temp list permanent
				gameState = tempNodes.ToArray();
			}
			return gameState;
		}

		static List<Point> SquareNeighbors(int x, int y, int size)
		{
			List<Point> neighbors = new List<Point>();
			if (x > 0) neighbors.Add(new Point(x - 1, y));
			if (y > 0) neighbors.Add(new Point(x, y - 1));
			if (x < size - 1) neighbors.Add(new Point(x + 1, y));
			if (y < size - 1) neighbors.Add(new Point(x, y + 1));
			return neighbors;
		}

		// return a simplified node graph describing the board
		public SimpleNode[] GetNodes()
		{
			SimpleNode[] returnVal = new SimpleNode[gameState.Length];
			for (int i = 0; i < returnVal.Length; i++)
			{
				returnVal[i] = new SimpleNode
				{
					index = i,
					neighbors = new int[gameState[i].neighbors.Count]
				};
				for (int j = 0; j < returnVal[i].neighbors.Length; j++)
				{
					returnVal[i].neighbors[j] = gameState[i].neighbors[j].index;
				}
			}
			return returnVal;
		}

		public int[] GameState()
		{

			int[] returnval = new int[gameState.Length];
			for (int i = 0; i < gameState.Length; i++)
			{
				returnval[i] = gameState[i].stone;
			}
			return returnval;
		}
	}
}
