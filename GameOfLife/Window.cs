using FastBitmapLib;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
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

		Point _cursorLast;

		bool _movedCursor;

		int _blurFrames = 200;
		int _frames = 0;

		int _quadVAO;
		int _textureId;

		int _cellsX;
		int _cellsY;

		Random _r = new Random();

		FBO _frontBuffer;
		FBO _backBuffer;

		GameOfLifeShader _shaderGoL;
		ReadShader _shaderRead;
		WriteShader _shaderWrite;

		static void Main()
		{
			if (File.Exists("p.id"))
			{
				try
				{
					if (int.TryParse(File.ReadAllText("p.id", Encoding.ASCII), out int id))
					{
						Process.GetProcessById(id).Kill();
					}
				}
				catch { }

				try
				{
					File.Delete("p.id");
				}
				catch { }
			}

			try
			{
				FileInfo fi = new FileInfo("p.id");

				using (var fs = fi.Create())
				{
					fi.Attributes = FileAttributes.Hidden;

					var data = Encoding.ASCII.GetBytes(Process.GetCurrentProcess().Id.ToString());

					fs.Write(data, 0, data.Length);
					fs.Flush();
				}
			}
			catch { }

			using (var w = new Window())
			{
				w.Run();
			}
		}

		public Window()
		{
			Instance = this;

			WindowUtil.SetAsWallpaper(WindowInfo.Handle);
			WindowState = WindowState.Fullscreen;

			SettingsHandler.TryLoad(this);

			VSync = SettingsHandler.Settings.VSync ? VSyncMode.On : VSyncMode.Off;
			TargetRenderFrequency = SettingsHandler.Settings.FPS;

			_blurFrames = SettingsHandler.Settings.BlurFrames;

			_cellsX = SettingsHandler.Settings.CellsX;
			_cellsY = SettingsHandler.Settings.CellsY;

			_frontBuffer = new FBO(_cellsX, _cellsY);
			_backBuffer = new FBO(_cellsX, _cellsY);

			_shaderGoL = new GameOfLifeShader();
			_shaderWrite = new WriteShader();
			_shaderRead = new ReadShader();

			_shaderGoL.Bind();
			_shaderGoL.SetVector2("bufferSize", (float)_cellsX, _cellsY);
			_shaderGoL.Unbind();

			_quadVAO = ModelManager.LoadModel2ToVao(new[] { 0f, 0, 0, 1, 1, 1, 1, 0 }, new[] { 0f, 0, 0, 1, 1, 1, 1, 0 });//new[] { 0f, 1, 0, 0, 1, 0, 1, 1 });
		}

		protected override void OnLoad(EventArgs e)
		{
			GL.Enable(EnableCap.Texture2D);
			GL.Disable(EnableCap.Blend);
			//GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

			try
			{
				using (var bmp = (Bitmap)Image.FromFile(SettingsHandler.Settings.Image))
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
				Console.WriteLine(_frames + " FPS");

				_lastFps = now;
				_frames = 0;
			}

			if (_backBuffer == null)
				return;

			//draw stuff beforehand
			/*_backBuffer.Bind();
			_shaderWrite.Bind();
			SpawnCells();
			CursorDraw();
			_shaderWrite.Unbind();
			FBO.BindDefault();*/

			//bind front buffer, use back buffer as last gen
			_frontBuffer.Bind();
			_backBuffer.BindTexture();
			GL.Clear(ClearBufferMask.ColorBufferBit);

			DrawGoL((float)e.Time);

			//spawn cells and draw with cursor (so that it first shows up)
			_shaderWrite.Bind();
			SpawnCells();
			CursorDraw();
			_shaderWrite.Unbind();

			//maybe it doesn't do it right here?
			//_frontBuffer.CopyTo(_backBuffer, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
			FBO.BindDefault();

			DrawResult();

			var b = _backBuffer;
			_backBuffer = _frontBuffer;
			_frontBuffer = b;

			SwapBuffers();
		}

		protected override void OnMouseMove(MouseMoveEventArgs e)
		{
			WindowUtil.SetAsWallpaper(WindowInfo.Handle);
			WindowState = WindowState.Fullscreen;
		}

		protected override void OnResize(EventArgs e)
		{
			GL.Viewport(ClientRectangle);

			//_frontBuffer?.SetSize(Width, Height);
			//_backBuffer?.SetSize(Width, Height);

			_projMat = Matrix4.CreateOrthographicOffCenter(0, _cellsX, _cellsY, 0, 0, 1);
			Shader.SetProjectionMatrix(_projMat);
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			GL.DeleteTexture(_textureId);

			_backBuffer.Destroy();
			_frontBuffer.Destroy();

			Shader.DestroyAll();
			ModelManager.Cleanup();
		}

		private void LoadTexture(Bitmap bmp)
		{
			_textureId = GL.GenTexture();

			GL.BindTexture(TextureTarget.Texture2D, _textureId);
			BitmapData bd = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);

			GL.TexImage2D(
				TextureTarget.Texture2D,
				0,
				PixelInternalFormat.Rgba,
				bmp.Width,
				bmp.Height,
				0,
				PixelFormat.Bgra,
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

		private void DrawResult()
		{
			_shaderRead.Bind();
			_shaderRead.SetMatrix4("transformationMatrix", Matrix4.CreateScale(_cellsX, _cellsY, 0));

			_shaderRead.SetSampler2D("gen", 0);
			GL.ActiveTexture(TextureUnit.Texture0);
			_frontBuffer.BindTexture();

			_shaderRead.SetSampler2D("image", 1);
			GL.ActiveTexture(TextureUnit.Texture1);
			GL.BindTexture(TextureTarget.Texture2D, _textureId);

			DrawQuad();

			_shaderRead.Unbind();

			//GL.BindTexture(TextureTarget.Texture2D, 0);
			GL.ActiveTexture(TextureUnit.Texture0);
			//GL.BindTexture(TextureTarget.Texture2D, 0);
		}

		private void DrawGoL(float step)
		{
			_shaderGoL.Bind();
			_shaderGoL.SetMatrix4("transformationMatrix", Matrix4.CreateScale(_cellsX, _cellsY, 0));
			_shaderGoL.SetFloat("deltaTime", step);
			_shaderGoL.SetFloat("fadeSpeed", (float)(60.0 / _blurFrames));

			DrawQuad();

			_shaderGoL.Unbind();
		}

		private void SpawnCells()
		{
			var sx = _r.Next(_cellsX);
			var sy = _r.Next(_cellsY);

			for (int oy = -1; oy < 2; oy++)
			{
				for (int ox = -1; ox < 2; ox++)
				{
					if (_r.Next(2) == 0)
						continue;

					var x = sx + ox;
					var y = sy + oy;

					x = (x + _cellsX) % _cellsX;
					y = (y + _cellsY) % _cellsY;

					FillNode(x, y);
				}
			}
		}

		private void CursorDraw()
		{
			//RENDER MOUSE
			GetCursorPos(out POINT p);

			var sizeX = Width / (float)_cellsX;
			var sizeY = Height / (float)_cellsY;

			var x = (int)(p.X / sizeX);
			var y = (int)(p.Y / sizeY);

			if (_cursorLast.X != x || _cursorLast.Y != y)
			{
				if (_movedCursor)
				{
					PopulateLine(_cursorLast.X, _cursorLast.Y, x, y);
				}

				_cursorLast.X = x;
				_cursorLast.Y = y;

				_movedCursor = true;
			}
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
	}
}
