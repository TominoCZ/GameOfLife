using OpenTK.Graphics.OpenGL;
using System;

namespace GameOfLife
{
	public class FBO
	{
		public int TextureId;

		private int _frameBuffer;
		private int _depthBuffer;

		private int _width, _height;

		private int _colorBuffer = -1;

		public FBO(int w, int h)
		{
			SetSize(w, h);

			if (!Init())
			{
				Console.WriteLine("Failed to create FBO");
			}
		}

		private bool Init()
		{
			_frameBuffer = GL.GenFramebuffer();
			GL.BindFramebuffer(FramebufferTarget.Framebuffer, _frameBuffer);

			CreateTexture();

			//if (_samples <= 1)
			//CreateDepthBuffer();

			// Set "renderedTexture" as our colour attachement #0
			GL.FramebufferTexture(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureId, 0);


			GL.DrawBuffer(DrawBufferMode.ColorAttachment0);

			// Always check that our framebuffer is ok
			var b = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) == FramebufferErrorCode.FramebufferComplete;

			BindDefault();

			return b;
		}

		public void SetSize(int w, int h)
		{
			if (w == _width && h == _height)
				return;

			Destroy();

			_width = w;
			_height = h;

			Init();
		}

		private void CreateTexture()
		{
			CreateRenderBuffer();

			TextureId = GL.GenTexture();

			GL.BindTexture(TextureTarget.Texture2D, TextureId);

			GL.TexImage2D(
				TextureTarget.Texture2D,
				0,
				PixelInternalFormat.Rgba,
				_width,
				_height,
				0,
				PixelFormat.Rgba,
				PixelType.UnsignedByte,
				(IntPtr)null);

			GL.TexParameter(
				TextureTarget.Texture2D,
				TextureParameterName.TextureMagFilter,
				(int)TextureMagFilter.Nearest);
			GL.TexParameter(TextureTarget.Texture2D,
				TextureParameterName.TextureMinFilter,
				(int)TextureMinFilter.Nearest);
		}
		/*
		private void CreateDepthBuffer()
		{
			// The depth buffer
			_depthBuffer = GL.GenRenderbuffer();

			GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _depthBuffer);
			GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent, _width, _height);
			GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, _depthBuffer);
		}*/

		private void CreateRenderBuffer()
		{
			_colorBuffer = GL.GenRenderbuffer();
			GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _colorBuffer);
			GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Rgba8, _width, _height);
			GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, _colorBuffer);
		}

		public void CopyToScreen()
		{
			CopyToScreen(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
		}

		public void CopyToScreen(ClearBufferMask what)
		{
			CopyToScreen(what, BlitFramebufferFilter.Nearest);
		}

		public void CopyToScreen(ClearBufferMask what, BlitFramebufferFilter how)
		{
			//create();
			GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _frameBuffer);
			GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
			GL.BlitFramebuffer(0, 0, _width, _height, 0, 0, Window.Instance.Width, Window.Instance.Height, what, how);
			GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
		}

		public void CopyTo(FBO fbo, ClearBufferMask what, BlitFramebufferFilter how)
		{
			//bind
			GL.BindFramebuffer(FramebufferTarget.Framebuffer, _frameBuffer);
			GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _frameBuffer);
			GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, fbo._frameBuffer);

			//copy
			GL.BlitFramebuffer(0, 0, _width, _height, 0, 0, fbo._width, fbo._height, what, how);

			//unbind
			GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);
			GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
			GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
		}

		public void BindTexture()
		{
			GL.BindTexture(TextureTarget.Texture2D, TextureId);
		}

		public void Bind()
		{
			GL.BindTexture(TextureTarget.Texture2D, 0);
			GL.BindFramebuffer(FramebufferTarget.Framebuffer, _frameBuffer);
			GL.Viewport(0, 0, _width, _height);
		}

		public static void BindDefault()
		{
			GL.BindTexture(TextureTarget.Texture2D, 0);

			GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
			GL.Viewport(Window.Instance.ClientSize);
		}

		public void Destroy()
		{
			if (_colorBuffer != -1)
			{
				GL.DeleteRenderbuffer(_colorBuffer);
				_colorBuffer = -1;
			}

			GL.DeleteFramebuffer(_frameBuffer);
			GL.DeleteTexture(TextureId);
		}
	}
}