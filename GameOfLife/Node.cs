namespace GameOfLife
{
	struct Node
	{
		public static Node operator +(Node n1, Node n2)
		{
			return new Node(n1.X + n2.X, n1.Y + n2.Y);
		}

		public int X;
		public int Y;

        public int Angle;
		public int DeadAge;

		public byte State { get; private set; }

		public Node(int x, int y)
		{
			X = x;
			Y = y;

			State = 0;
			Angle = 0;
			DeadAge = 0;
		}

		public void SetDead()
		{
			State = 0;
		}

		public void Populate()
		{
			Angle++;
			DeadAge = 0;
			State = 1;
		}
	}
}
