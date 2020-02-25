using FastBitmapLib;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using WindowUtils;
using PixelFormat = OpenTK.Graphics.OpenGL.PixelFormat;

namespace GameOfLife
{
	class Window : GameWindow
	{
		[DllImport("user32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool GetCursorPos(out POINT lpPoint);

		[StructLayout(LayoutKind.Sequential)]
		public struct POINT
		{
			public int X;
			public int Y;

			public POINT(int x, int y)
			{
				X = x;
				Y = y;
			}

			public static implicit operator Point(POINT p)
			{
				return new Point(p.X, p.Y);
			}

			public static implicit operator POINT(Point p)
			{
				return new POINT(p.X, p.Y);
			}
		}

		public static Window Instance;

		Matrix4 _projMat;
		DateTime _lastFps = DateTime.Now;
		DateTime _lastSpawn = DateTime.MinValue;

		int _fadeTicks = 120;
		int _frames = 0;

		int _quadVAO;
		int _textureId;

		int _cellsX = 160 * 2;
		int _cellsY = 90 * 2;

		Point _cursorLast = new Point();

		Color[,] _image;

		FBO _frontBuffer;
		FBO _backBuffer;

		GameOfLifeShader _shaderGoL;
		ReadShader _shaderRead;
		WriteShader _shaderWrite;

		static void Main()
		{
			//MessageBox.Show("");

			using (var w = new Window())
			{
				w.Run();
			}
		}

		public Window()
		{
			Instance = this;

			VSync = VSyncMode.On;

			TargetRenderFrequency = 60;

			WindowUtil.SetAsWallpaper(WindowInfo.Handle);
			WindowState = WindowState.Fullscreen;

			_frontBuffer = new FBO(_cellsX, _cellsY);
			_backBuffer = new FBO(_cellsX, _cellsY);

			_shaderGoL = new GameOfLifeShader();
			_shaderWrite = new WriteShader();
			_shaderRead = new ReadShader();

			_quadVAO = ModelManager.LoadModel2ToVao(new[] { 0f, 0, 0, 1, 1, 1, 1, 0 }, new[] { 0f, 1, 0, 0, 1, 0, 1, 1 });
		}

		protected override void OnLoad(EventArgs e)
		{
			GL.Enable(EnableCap.Texture2D);
			GL.Enable(EnableCap.Blend);
			GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

			try
			{
				using (var bmp = (Bitmap)Image.FromFile("image.png"))
				{
					using (var newBmp = new Bitmap(bmp, _cellsX, _cellsY))
					{
						LoadTexture(newBmp);
					}
				}
			}
			catch { }
		}

		protected override void OnRenderFrame(FrameEventArgs e)
		{
			GL.Clear(ClearBufferMask.ColorBufferBit);

			//HANDLE FPS COUNT
			_frames++;
			var now = DateTime.Now;
			if ((now - _lastFps).TotalSeconds >= 1)
			{
				Console.WriteLine(_frames);

				_lastFps = now;
				_frames = 0;
			}

			if (_backBuffer == null)
				return;

			_backBuffer.Bind();

			_shaderWrite.Bind();
			HandleLine();

			if ((DateTime.Now - _lastSpawn).TotalSeconds >= 0.25)
			{
				_lastSpawn = DateTime.Now;

				var r = new Random();

				for (int i = 0; i < 35; i++)
				{
					int sx = r.Next(Width);
					int sy = r.Next(Height);

					for (int y = -10; y < 11; y++)
					{
						for (int x = -10; x < 11; x++)
						{
							FillNode(sx + x, sy + y);
						}
					}
				}
			}

			_shaderWrite.Unbind();

			_frontBuffer.Bind();
			GL.Clear(ClearBufferMask.ColorBufferBit);
			_backBuffer.BindTexture();

			_shaderGoL.Bind();
			_shaderGoL.SetMatrix4("transformationMatrix", Matrix4.CreateScale(_cellsX, _cellsY, 0));
			_shaderGoL.SetVector2("bufferSize", new Vector2(_cellsX, _cellsY));
			_shaderGoL.SetVector2("deltaTime", (float)e.Time * Vector2.UnitX);

			DrawQuad();

			_shaderGoL.Unbind();

			_frontBuffer.CopyTo(_backBuffer, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
			_frontBuffer.BindDefault();
			GL.ActiveTexture(TextureUnit.Texture0);
			_frontBuffer.BindTexture();
			GL.ActiveTexture(TextureUnit.Texture1);
			GL.BindTexture(TextureTarget.Texture2D, _textureId);
			_shaderRead.SetSampler2D("image", TextureUnit.Texture1, _textureId);

			_shaderRead.Bind();
			_shaderRead.SetMatrix4("transformationMatrix", Matrix4.CreateScale(_cellsX, _cellsY, 0));

			DrawQuad();

			_shaderRead.SetSampler2D("image", TextureUnit.Texture0, 0);
			_shaderRead.Unbind();

			GL.ActiveTexture(TextureUnit.Texture0);

			/*
			

			//new generation and render
			_frontBuffer.Bind();
			_shaderGoL.Bind();
			_shaderGoL.SetMatrix4("transformationMatrix", Matrix4.CreateScale(_cellsX, _cellsY, 0));
			_shaderGoL.SetVector2("bufferSize", new Vector2(Width, Height));
			DrawQuad();
			_shaderGoL.Unbind();
			_frontBuffer.BindDefault();

			_frontBuffer.CopyTo(_backBuffer, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
			
			_frontBuffer.CopyToScreen(ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
			*/
			SwapBuffers();
		}

		private void LoadTexture(Bitmap bmp)
		{
			_textureId = GL.GenTexture();

			GL.BindTexture(TextureTarget.Texture2D, _textureId);

			GraphicsUnit gu = GraphicsUnit.Pixel;
			BitmapData bd = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);

			GL.TexImage2D(
				TextureTarget.Texture2D,
				0,
				PixelInternalFormat.Rgba,
				bmp.Width,
				bmp.Height,
				0,
				PixelFormat.Rgba,
				PixelType.UnsignedByte, bd.Scan0);

			GL.TexParameter(
				TextureTarget.Texture2D,
				TextureParameterName.TextureMagFilter,
				(int)TextureMagFilter.Nearest);
			GL.TexParameter(TextureTarget.Texture2D,
				TextureParameterName.TextureMinFilter,
				(int)TextureMinFilter.Nearest);

			bmp.UnlockBits(bd);
		}

		private void HandleLine()
		{
			//RENDER MOUSE
			GetCursorPos(out POINT p);

			var sizeX = Width / (float)_cellsX;
			var sizeY = Height / (float)_cellsY;

			var x = (int)(p.X / sizeX);
			var y = (int)(p.Y / sizeY);

			if (_cursorLast.X != x || _cursorLast.Y != y)
			{
				PopulateLine(_cursorLast.X, _cursorLast.Y, x, y);

				_cursorLast.X = x;
				_cursorLast.Y = y;
			}
		}

		protected override void OnResize(EventArgs e)
		{
			GL.Viewport(ClientRectangle);

			//_frontBuffer?.SetSize(Width, Height);
			//_backBuffer?.SetSize(Width, Height);

			_projMat = Matrix4.CreateOrthographicOffCenter(0, _cellsX, _cellsY, 0, 0, 1);
			Shader.SetProjectionMatrix(_projMat);
		}

		private void FillNode(float x, float y)
		{
			_shaderWrite.SetMatrix4("transformationMatrix", Matrix4.CreateTranslation(x, y, 0));

			DrawQuad();
		}

		private void DrawQuad()
		{
			GL.BindVertexArray(_quadVAO);
			GL.EnableVertexAttribArray(0);
			GL.EnableVertexAttribArray(1);
			GL.DrawArrays(PrimitiveType.Quads, 0, 4);
			GL.BindVertexArray(0);
			GL.DisableVertexAttribArray(0);
			GL.DisableVertexAttribArray(1);
		}

		private void DrawNode(Node n)
		{
			if (n.State == 0 && n.DeadAge >= _fadeTicks)
				return;

			var a = n.State == 1 ? 1 : _fadeTicks == 0 ? 0 : Math.Max(0, (_fadeTicks - n.DeadAge) / (float)_fadeTicks);
			a *= a;

			var c = _image?[n.X, n.Y] ?? Hue(n.Angle * 10);

			GL.Color4(c.R, c.G, c.B, (byte)(a * 255));

			GL.Vertex2(n.X, n.Y);
			GL.Vertex2(n.X, n.Y + 1);
			GL.Vertex2(n.X + 1, n.Y + 1);
			GL.Vertex2(n.X + 1, n.Y);
		}

		private void PopulateLine(int sx, int sy, int ex, int ey)
		{
			var dx = ex - sx;
			var dy = ey - sy;

			int steps = Math.Abs(Math.Abs(dx) > Math.Abs(dy) ? dx : dy);

			var xinc = dx / (float)steps;
			var yinc = dy / (float)steps;

			float x = sx;
			float y = sy;

			for (int i = 0; i < Math.Abs(steps); i++)
			{
				x += xinc;
				y += yinc;

				FillNode((int)Math.Round(x), (int)Math.Round(y));
			}
		}

		private Color Hue(float angle)
		{
			var rad = MathHelper.DegreesToRadians(angle);

			var r = (int)(Math.Sin(rad) * 127.5 + 127.5);
			var g = (int)(Math.Sin(rad + MathHelper.PiOver3 * 2) * 127.5 + 127.5);
			var b = (int)(Math.Sin(rad + MathHelper.PiOver3 * 4) * 127.5 + 127.5);

			return Color.FromArgb(r, g, b);
		}
	}
}
