using FastBitmapLib;
using System;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GameOfLife
{
	public partial class Form1 : Form
	{
		Game _game = new Game();

		Bitmap _buffer;
		FastBitmap _frame;

		public Form1()
		{
			InitializeComponent();

			_buffer = new Bitmap(160, 90, PixelFormat.Format32bppPArgb);
			_frame = new FastBitmap(_buffer);

			MinimumSize = new Size(_frame.Width * 10, _frame.Height * 10);
			MaximumSize = MinimumSize;
		}

		private void Form1_Shown(object sender, EventArgs e)
		{
			new Thread(() =>
			{
				while (!Disposing && !IsDisposed && IsHandleCreated && Visible)
				{
					try
					{
						BeginInvoke((MethodInvoker)(() =>
						{
							Invalidate();
						}));
					}
					catch { }

					Thread.Sleep(16);
				}
			})
			{ IsBackground = true }.Start();
		}

		private void Form1_Paint(object sender, PaintEventArgs e)
		{
			if (!_game.Initialized)
				return;

			var gen = _game.CurrentGen();

			_frame.Lock();

			for (int y = 0; y < gen.GetLength(1); y++)
			{
				for (int x = 0; x < gen.GetLength(0); x++)
				{
					var node = gen[x, y];

					var c = Hue(node.Age * 10);

					_frame.SetPixel(x, y, node.State == 1 ? c : Color.Black);
					//e.Graphics.FillRectangle(state ? Brushes.White : Brushes.Black, x * 4, y * 4, 4, 4);
				}
			}

			_frame.Unlock();

			e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
			e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
			e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
			e.Graphics.DrawImage(_buffer, ClientRectangle);

			_game.NextGen();
		}

		private void Form1_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Space)
			{
				_game.Init(_frame.Width, _frame.Height);
			}
		}

		private Color Hue(float a)
		{
			var rad = Math.PI / 180 * a;
			var third = Math.PI / 3;

			var r = (int)(Math.Sin(rad) * 127.5 + 127.5);
			var g = (int)(Math.Sin(rad + third * 2) * 127.5 + 127.5);
			var b = (int)(Math.Sin(rad + third * 4) * 127.5 + 127.5);

			return Color.FromArgb(r, g, b);
		}
	}
}
