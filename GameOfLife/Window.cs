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
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WindowUtils;
using PixelFormat = OpenTK.Graphics.OpenGL.PixelFormat;

namespace GameOfLife
{
	internal class Window : GameWindow
	{
		[DllImport("user32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool GetCursorPos(out POINT lpPoint);
		[DllImport("user32.dll")]
		private static extern IntPtr GetForegroundWindow();
		[DllImport("user32.dll")]
		private static extern bool GetWindowRect(IntPtr hWnd, [In, Out] ref Rect rect);

		private static WINDOWPLACEMENT GetPlacement(IntPtr hwnd)
		{
			WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
			placement.length = Marshal.SizeOf(placement);
			GetWindowPlacement(hwnd, ref placement);
			return placement;
		}

		[DllImport("user32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool GetWindowPlacement(
			IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

		[Serializable]
		[StructLayout(LayoutKind.Sequential)]
		internal struct WINDOWPLACEMENT
		{
			public int length;
			public int flags;
			public ShowWindowCommands showCmd;
			public System.Drawing.Point ptMinPosition;
			public System.Drawing.Point ptMaxPosition;
			public System.Drawing.Rectangle rcNormalPosition;
		}

		internal enum ShowWindowCommands : int
		{
			Hide = 0,
			Normal = 1,
			Minimized = 2,
			Maximized = 3,
		}

		[StructLayout(LayoutKind.Sequential)]
		private readonly struct Rect
		{
			public readonly int Left;
			public readonly int Top;
			public readonly int Right;
			public readonly int Bottom;
		}

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

		private Matrix4 _projMat;
		private DateTime _lastFps = DateTime.Now;

		private Point _cursorLast;

		private bool _firstFrame = false;
		private bool _shouldUpdate = true;
		private readonly bool _fullscreen;
		private bool _movedCursor;

		private readonly int _blurFrames = 200;
		private int _frames = 0;

		private readonly int _quadVAO;
		private int _textureId;

		private readonly int _cellsX;
		private readonly int _cellsY;

		private readonly Random _r = new Random();

		private FBO _frontBuffer;
		private FBO _backBuffer;

		private readonly GameOfLifeShader _shaderGoL;
		private readonly ReadShader _shaderRead;
		private readonly WriteShader _shaderWrite;

		private static void Main(string[] args)
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

			using (var w = new Window(args))
			{
				w.Run();
			}
		}

		public Window(string[] args)
		{
			Instance = this;

			_fullscreen = args.Any(arg => arg.ToLower().Contains("fs"));
			_fullscreen = true;
			if (_fullscreen)
			{
				WindowUtil.SetAsWallpaper(WindowInfo.Handle);
				WindowState = WindowState.Fullscreen;
			}

			SettingsHandler.TryLoad(this);

			VSync = SettingsHandler.Settings.VSync ? VSyncMode.On : VSyncMode.Off;
			TargetRenderFrequency = SettingsHandler.Settings.FPS;
			TargetUpdateFrequency = 10;

			_blurFrames = SettingsHandler.Settings.BlurFrames;

			if (!_fullscreen)
			{
				ClientRectangle = GetScreensRectangle();
				//ClientSize = new Size(_cellsX, _cellsY);
			}

			_cellsX = ClientRectangle.Width / SettingsHandler.Settings.CellsSize;
			_cellsY = ClientRectangle.Height / SettingsHandler.Settings.CellsSize;

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
					using (var canvas = new Bitmap(ClientRectangle.Size.Width, ClientSize.Height))
					{
						using (var g = Graphics.FromImage(canvas))
						{
							var bbs = GetScreenRectangles();
							foreach (var b in bbs)
							{
								g.DrawImage(bmp, b);
							}

							using (var newBmp = new Bitmap(canvas, _cellsX, _cellsY))
							{
								LoadTexture(newBmp);
							}
						}
					}
				}
			}
			catch { }
		}

		protected override void OnRenderFrame(FrameEventArgs e)
		{
			if (!_shouldUpdate && _firstFrame)
				return;

			_firstFrame = true;

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

			(_backBuffer, _frontBuffer) = (_frontBuffer, _backBuffer);

			SwapBuffers();
		}

		protected override void OnUpdateFrame(FrameEventArgs e)
		{
			base.OnUpdateFrame(e);

			var max = false;

			var ptr = GetForegroundWindow();
			if (ptr != IntPtr.Zero && ptr != WindowInfo.Handle)
			{
				var rect = new Rect();
				if (GetWindowRect(ptr, ref rect))
				{
					var placement = new WINDOWPLACEMENT();
					if (GetWindowPlacement(ptr, ref placement))
					{
						max = (placement.showCmd & ShowWindowCommands.Maximized) == ShowWindowCommands.Maximized;
					}
				}
			}

			_shouldUpdate = !max;

			TargetRenderFrequency = max ? 1 : SettingsHandler.Settings.FPS;
		}

		protected override void OnMouseMove(MouseMoveEventArgs e)
		{
			if (_fullscreen)
			{
				WindowUtil.SetAsWallpaper(WindowInfo.Handle);
				WindowState = WindowState.Fullscreen;
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

			//var sizeX = Width / (float)_cellsX;
			//var sizeY = Height / (float)_cellsY;

			var x = p.X;
			var y = p.Y;

			if (WindowBorder != WindowBorder.Hidden)
			{
				x -= Location.X + 1;
				y -= Location.Y + 31;
			}
			else
			{
				var sr = GetScreensRectangle();
				var r = Screen.PrimaryScreen.Bounds;

				x += r.Left - sr.Left;
				y += r.Top - sr.Top;
			}

			x /= SettingsHandler.Settings.CellsSize;
			y /= SettingsHandler.Settings.CellsSize;

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

		private Rectangle GetScreensRectangle()
		{
			int minX = int.MaxValue;
			int minY = int.MaxValue;
			int maxX = int.MinValue;
			int maxY = int.MinValue;

			foreach (var screen in Screen.AllScreens)
			{
				minX = Math.Min(minX, screen.Bounds.Left);
				minY = Math.Min(minY, screen.Bounds.Top);
				maxX = Math.Max(maxX, screen.Bounds.Right);
				maxY = Math.Max(maxY, screen.Bounds.Bottom);
			}

			return new Rectangle(minX, minY, maxX - minX, maxY - minX);
		}

		private Rectangle[] GetScreenRectangles()
		{
			int minX = int.MaxValue;
			int minY = int.MaxValue;

			var screens = Screen.AllScreens;
			var bbs = new Rectangle[screens.Length];

			for (int i = 0; i < screens.Length; i++)
			{
				Screen screen = screens[i];
				minX = Math.Min(minX, screen.Bounds.Left);
				minY = Math.Min(minY, screen.Bounds.Top);

				bbs[i] = screen.Bounds;
			}

			for (int i = 0; i < screens.Length; i++)
			{
				bbs[i].Offset(Math.Max(0, -minX), Math.Max(0, -minY));
			}

			return bbs;
		}
	}
}
