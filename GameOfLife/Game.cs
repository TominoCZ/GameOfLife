using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameOfLife
{
	internal class Game
	{
		private Node[,] _grid;
		private Node[,] _backBuffer;

		private readonly Node[] _neighbours =
		{
			new Node(-1, -1),
			new Node(0, -1),
			new Node(1, -1),
			new Node(1, 0),
			new Node(1, 1),
			new Node(0, 1),
			new Node(-1, 1),
			new Node(-1, 0)
		};

		public int CellsX;
		public int CellsY;

		public bool Initialized => _grid != null;

		public void Init(int sx, int sy)
		{
			_grid = new Node[sx, sy];
			_backBuffer = new Node[sx, sy];

			CellsX = sx;
			CellsY = sy;

			var r = new Random();

			for (int y = 0; y < sy; y++)
			{
				for (int x = 0; x < sx; x++)
				{
					var n = new Node(x, y);

					if (r.Next(0, 2) == 1)
						n.Populate();

					_grid[x, y] = n;
				}
			}
		}

		private ref Node GetNode(int x, int y)
		{
			x = (x + CellsX) % CellsX;
			y = (y + CellsY) % CellsY;

			return ref _grid[x, y];
		}

		private Node GetNewState(Node n)
		{
			var alive = GetAliveNeighbours(n.X, n.Y);

			if (n.State == 0 && alive.Count == 3)
			{
				n.Angle = alive.Sum(node => node.Angle) / alive.Count;
				n.Populate();
				return n;
			}
			else if (n.State == 1 && (alive.Count < 2 || alive.Count > 3))
			{
				n.SetDead();
				return n;
			}

			if (n.State == 0)
				n.DeadAge++;

			return n;
		}

		private Queue<Node> GetAliveNeighbours(int x, int y)
		{
			var list = new Queue<Node>();

			foreach (Node node in _neighbours)
			{
				var n = GetNode(x + node.X, y + node.Y);

				if (n.State == 1)
					list.Enqueue(n);
			}

			return list;
		}

		private int CountAlive()
		{
			var alive = 0;

			for (int y = 0; y < CellsX; y++)
			{
				for (int x = 0; x < CellsY; x++)
				{
					alive += _grid[x, y].State;
				}
			}

			return alive;
		}

		public Node[,] CurrentGen()
		{
			return _grid;
		}

		public void Populate(int x, int y)
		{
			if (x < 0 || x > CellsX || y < 0 || y > CellsY)
				return;

			ref var n = ref GetNode(x, y);

			//var alive = GetAliveNeighbours(x, y);

			//n.Age = alive.Count == 0 ? 0 : alive.Sum(node => node.Age) / alive.Count;
			n.Populate();
		}

		public void NextGen(Action<Node> iterateLast)
		{
			var gen = _backBuffer;
            
			for (int y = 0; y < CellsY; y++)
			{
				for (int x = 0; x < CellsX; x++)
				{
					var node = GetNewState(_grid[x, y]);

                    gen[x, y] = node;

                    iterateLast(node);
                }
			}
            
			_backBuffer = _grid;
			_grid = gen;
		}
	}
}
