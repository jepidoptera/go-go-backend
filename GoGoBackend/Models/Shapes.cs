using System.Collections.Generic;
using System;

namespace Shapes
{

	// todo: mapVertex should be defined in world, inheriting from vertexNode, which should be defined here
	// or whatever. just an idea, and it works ok as is
	public class Node
	{
		public List<Node> neighbors;
		public int region;

		public int index; // each mapvertex has a unique index within the whole world
		
		public int owner; // won't be used until end-of-game scoring

		public int stone = -1;

		public Node(int index)
		{
			this.index = index;
			this.neighbors = new List<Node>();
		}

		public static void AddNeighbors(Node a, Node b)
		{
			// these two are neighbors of each other, if they weren't already
			if (!a.neighbors.Contains(b)) a.neighbors.Add(b);
			if (!b.neighbors.Contains(a)) b.neighbors.Add(a);
		}

		public void Remove()
		{
			foreach (Node neighbor in neighbors)
			{
				neighbor.neighbors.Remove(this);
			}
			neighbors.Clear();
		}
	}

	// a triangular segment of the icosahedral sphere
	public class TriangleGrid
	{
		public Node[] nodes;
		private readonly int vertsPerSide;
		public List<int> triangles;
		public int lastVertex;
		public int index;

		private int IndexOf(int x, int y)
		{
			// given x and y coordinates, which index are we referring to?
			if (x > vertsPerSide || y > x)
			{
				return -1;
			}
			// coordinates are good: calculate and return
			return (int)((x + 1) * (x / 2f) + y);
		}

		private List<int> TrianglesAt(int x, int y)
		{
			// given x and y coordinates, which indices in the triangles array attach to this vertex?
			// check for coordinates in-bounds
			if (x > vertsPerSide || y > x)
			{
				return null;
			}

			// coordinates are good, return triangles
			int t1Index = -1, t2Index = -1;
			if (y > 0)
			{
				t1Index = (x - 1) * (x - 1) + (y - 1) * 2;
				if (x > y)
				{
					t2Index = t1Index + 1;
				}
			}

			// add up whatever triangles we found and return them
			List<int> returnVal = new List<int>();
			foreach (int t in new int[2] { t1Index, t2Index })
			{
				if (t > -1)
				{
					returnVal.Add(triangles[t * 3]);
					returnVal.Add(triangles[t * 3 + 1]);
					returnVal.Add(triangles[t * 3 + 2]);
				}
			}
			return returnVal;
		}

		public TriangleGrid[] Split(int subGrids = 1)
		{
			// the grid needs to divide correctly for this to work
			if (vertsPerSide % subGrids != 1)
			{
				return null;
			}

			int i = 0;
			TriangleGrid[] subs = new TriangleGrid[subGrids * subGrids];

			// split into multiple grids
			int vertsPerSub = vertsPerSide / subGrids;
			for (int x = 0; x < subGrids; x++)
			{
				for (int y = 0; y <= x; y++)
				{
					// x2 and y2 are the coordinates within the sub-grid
					int x2 = 0, y2 = 0;
					// x1 and y1 are coordinates within the major grid,
					// while x and y designate the current sub-grid
					int z = 0;
					// z is the index of the current mapvertex within the sub-grid
					// here we construct a sub-grid, which will become a map region
					subs[i] = new TriangleGrid(vertsPerSub + 1);
					for (int x1 = x * vertsPerSub; x1 <= (x + 1) * vertsPerSub; x1++)
					{
						for (int y1 = y * vertsPerSub; y1 <= y * vertsPerSub + x2; y1++)
						{
							// this works for "up-facing" grid segments
							subs[i].nodes[z] = nodes[IndexOf(x1, y1)];
							// add any triangles connected to this vertex
							// any triangles that should be anchored here
							if (y2 > 0)
							{
								// one triangle
								subs[i].triangles.Add(z);
								subs[i].triangles.Add(z - x2 - 1);
								subs[i].triangles.Add(z - 1);

								// in most cases, there are two triangles per vertex
								// but if it's on the right edge, where y = x, there's only one
								if (x2 > y2)
								{
									// another one
									subs[i].triangles.Add(z);
									subs[i].triangles.Add(z - x2);
									subs[i].triangles.Add(z - x2 - 1);
								}
							}
							// increment mapvertex index
							z += 1;
							// increment y coordinate
							y2 += 1;
						}
						x2 += 1;
						y2 = 0;
					}
					// count last index
					subs[i].lastVertex = z - 1;
					// increment sub-grid index
					i += 1;

					if (y < x)
					{
						// here we'll do a "down pointing" region
						// again, x2 and y2 are the coordinates within the sub-grid
						x2 = 0; y2 = 0;
						// x1 and y1 are coordinates within the major grid,
						// while x and y designate the current sub-grid
						z = 0;
						// and z is the index of the current mapvertex within the sub-grid
						// initialize sub-grid
						subs[i] = new TriangleGrid(vertsPerSub + 1);
						for (int x1 = x * vertsPerSub; x1 <= (x + 1) * vertsPerSub; x1++)
						{
							for (int y1 = y * vertsPerSub + x2; y1 <= (y + 1) * vertsPerSub; y1++)
							{
								subs[i].nodes[z] = nodes[IndexOf(x1, y1)];
								// add any triangles connected to this vertex
								// any triangles that should be anchored here
								if (x2 > 0)
								{
									// one triangle
									subs[i].triangles.Add(z);
									subs[i].triangles.Add(z - vertsPerSub + x2 - 1);
									subs[i].triangles.Add(z - vertsPerSub + x2 - 2);

									// do a second triangle sometimes
									if (y2 > 0)
									{
										subs[i].triangles.Add(z);
										subs[i].triangles.Add(z - vertsPerSub + x2 - 2);
										subs[i].triangles.Add(z - 1);
									}
								}
								// increment mapvertex index
								z += 1;
								// increment y coordinate
								y2 += 1;
							}
							x2 += 1;
							y2 = 0;
						}
						// count last index
						subs[i].lastVertex = z - 1;
						// increment sub-grid index
						i += 1;
					}
				}
			}
			return subs;
		}

		public TriangleGrid(int vertsPerSide, int startIndex = 0, Node[] rightEdge = null, Node[] leftEdge = null, Node[] bottomEdge = null)
		{
			// full scale constructor
			this.vertsPerSide = vertsPerSide;
			int i = 0;

			// vertices and triangles arrays
			nodes = new Node[(vertsPerSide + 1) * vertsPerSide / 2];
			triangles = new List<int>();

			// initialize each node in the grid
			for (i = 0; i < nodes.Length; i++)
			{
				nodes[i] = new Node(-1);
			}
			i = 0;

			// next, install the pre-defined edge vertices, if any are given
			if (rightEdge != null)
			{
				// the right edge of this triangle corresponds to the given right edge
				int a = 0;
				for (i = 0; i < vertsPerSide; i += 1)
				{
					nodes[a] = rightEdge[i];
					a += i + 2;
				}
			}

			if (leftEdge != null)
			{
				// left edge
				int a = 0;
				for (i = 0; i < vertsPerSide; i += 1)
				{
					nodes[a] = leftEdge[i];
					a += i + 1;
				}
			}

			if (bottomEdge != null)
			{
				// bottom edge
				Node[] r = new Node[vertsPerSide];
				for (i = 0; i < vertsPerSide; i++)
				{
					nodes[nodes.Length - i - 1] = bottomEdge[i];
				}
			}

			// now go through the vertices in rows from point to base, connecting mapvertices and defining triangles
			int index = startIndex;
			i = 0;
			float xOffSet = 0;

			for (int x = 0; x < vertsPerSide; x++)
			{
				for (int y = 0; y <= x; y++)
				{
					// calculate xoffset
					xOffSet = -y * .5f;
					// if index hasn't been assigned yet, assign it
					if (nodes[i].index == -1)
					{
						nodes[i].index = index;
						index += 1;
					}

					// connect neighbors
					if (x < vertsPerSide - 1)
					{
						Node.AddNeighbors(nodes[i], nodes[i + x + 1]);
						Node.AddNeighbors(nodes[i], nodes[i + x + 2]);
						Node.AddNeighbors(nodes[i + x + 1], nodes[i + x + 2]);
					}

					// create triangles
					// any triangles that should be anchored here
					if (y > 0)
					{
						triangles.Add(nodes[i].index);
						triangles.Add(nodes[i - x - 1].index);
						triangles.Add(nodes[i - 1].index);

						// in most cases, there are two triangles per vertex
						// but if it's on the right edge, where y = x, there's only one
						if (x > y)
						{
							// another one
							triangles.Add(nodes[i].index);
							triangles.Add(nodes[i - x].index);
							triangles.Add(nodes[i - x - 1].index);
						}
					}
					// increment i
					i += 1;
				}
			}
			// keep track of which was the last index used, so we can start again on the next one with no overlap
			this.lastVertex = index;
		}
		public Node[] RightEdge()
		{
			// get all the vertices down the right side of this triangle
			Node[] r = new Node[vertsPerSide];
			int a = 0;
			for (int i = 0; i < vertsPerSide; i += 1)
			{
				r[i] = nodes[a];
				a += i + 2;
			}
			return r;
		}

		public Node[] LeftEdge()
		{
			// get all the vertices down the left side
			Node[] r = new Node[vertsPerSide];
			int a = 0;
			for (int i = 0; i < vertsPerSide; i += 1)
			{
				r[i] = nodes[a];
				a += i + 1;
			}
			return r;
		}

		public Node[] BottomEdge()
		{
			// bottom edge
			Node[] r = new Node[vertsPerSide];
			for (int i = 0; i < vertsPerSide; i++)
			{
				r[i] = nodes[nodes.Length - i - 1];
			}
			return r;
		}

		// method to return a reversed array
		static Node[] Reversed(Node[] a)
		{
			Node[] b = a;
			Array.Reverse(b);
			return b;
		}

		// build the "physical" mesh for display
		// build the "physical" mesh for display
		public static Node[] BuildWorld(int sideLength, out TriangleGrid[] segment)
		{
			Node[] map;
			// make twenty segments, which are each a grid of triangles
			segment = new TriangleGrid[20];

			// this part is somewhat complicated
			// luckily there are 'only' 20 of them
			segment[0] = new TriangleGrid(sideLength);

			segment[1] = new TriangleGrid(sideLength, rightEdge: segment[0].LeftEdge(), startIndex: segment[0].lastVertex);

			segment[2] = new TriangleGrid(sideLength, rightEdge: segment[1].LeftEdge(), startIndex: segment[1].lastVertex);

			segment[3] = new TriangleGrid(sideLength, rightEdge: segment[2].LeftEdge(), startIndex: segment[2].lastVertex);

			segment[4] = new TriangleGrid(sideLength, rightEdge: segment[3].LeftEdge(), leftEdge: segment[0].RightEdge(), startIndex: segment[3].lastVertex);

			// middle segment

			segment[5] = new TriangleGrid(sideLength, bottomEdge: Reversed(segment[4].BottomEdge()), startIndex: segment[4].lastVertex);

			segment[6] = new TriangleGrid(sideLength, bottomEdge: Reversed(segment[3].BottomEdge()), startIndex: segment[5].lastVertex);

			segment[7] = new TriangleGrid(sideLength, bottomEdge: Reversed(segment[2].BottomEdge()), startIndex: segment[6].lastVertex);

			segment[8] = new TriangleGrid(sideLength, bottomEdge: Reversed(segment[1].BottomEdge()), startIndex: segment[7].lastVertex);

			segment[9] = new TriangleGrid(sideLength, bottomEdge: Reversed(segment[0].BottomEdge()), startIndex: segment[8].lastVertex);

			// second middle segment

			segment[10] = new TriangleGrid(sideLength, rightEdge: segment[5].LeftEdge(),
			bottomEdge: Reversed(segment[6].RightEdge()), startIndex: segment[9].lastVertex);

			segment[11] = new TriangleGrid(sideLength, rightEdge: segment[6].LeftEdge(),
			bottomEdge: Reversed(segment[7].RightEdge()), startIndex: segment[10].lastVertex);

			segment[12] = new TriangleGrid(sideLength, rightEdge: segment[7].LeftEdge(),
			bottomEdge: Reversed(segment[8].RightEdge()), startIndex: segment[11].lastVertex);

			segment[13] = new TriangleGrid(sideLength, rightEdge: segment[8].LeftEdge(),
			bottomEdge: Reversed(segment[9].RightEdge()), startIndex: segment[12].lastVertex);

			segment[14] = new TriangleGrid(sideLength, rightEdge: segment[9].LeftEdge(),
			bottomEdge: Reversed(segment[5].RightEdge()), startIndex: segment[13].lastVertex);

			// bottom segment

			segment[15] = new TriangleGrid(sideLength, leftEdge: Reversed(segment[10].LeftEdge()), startIndex: segment[14].lastVertex);

			segment[16] = new TriangleGrid(sideLength, leftEdge: Reversed(segment[11].LeftEdge()),
			bottomEdge: Reversed(segment[15].RightEdge()), startIndex: segment[15].lastVertex);

			segment[17] = new TriangleGrid(sideLength, leftEdge: Reversed(segment[12].LeftEdge()),
			bottomEdge: Reversed(segment[16].RightEdge()), startIndex: segment[16].lastVertex);

			segment[18] = new TriangleGrid(sideLength, leftEdge: Reversed(segment[13].LeftEdge()),
			bottomEdge: Reversed(segment[17].RightEdge()), startIndex: segment[17].lastVertex);

			segment[19] = new TriangleGrid(sideLength, leftEdge: Reversed(segment[14].LeftEdge()),
			rightEdge: Reversed(segment[15].BottomEdge()), bottomEdge: Reversed(segment[18].RightEdge()),
			startIndex: segment[18].lastVertex);

			for (int n = 0; n < 20; n++)
			{
				segment[n].index = n;
			}

			// how many is that?
			int totalVertices = segment[19].lastVertex;
			map = new Node[totalVertices];

			// assemble mesh- and map-vertices together into single arrays
			for (int i = 0; i < 20; i++)
			{
				foreach (Node n in segment[i].nodes)
				{
					// map vertex. there's a one-to-one correspondence
					map[n.index] = n;
				}
			}
			// put the triangles together as well
			List<int> mapTriangles = new List<int>();
			for (int i = 0; i < 20; i++)
			{
				mapTriangles.AddRange(segment[i].triangles);
			}

			return map;
		}
		
		public static Node[] BuildWorld(int size)
		{
			TriangleGrid[] nothing;
			return BuildWorld(size, out nothing);
		}
	}
}
