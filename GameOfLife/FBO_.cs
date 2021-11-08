using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameOfLife
{
	internal class FBO_
    {
        private int Width;
        private int Height;

        private int FboId;
        private int TextureId;

        public FBO_(int w, int h)
        {
            Width = w;
            Height = h;

            Init();
        }

        private void Init()
        {
            FboId = GL.GenFramebuffer();

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, FboId);

            TextureId = GL.GenTexture();
            
            GL.BindTexture(TextureTarget.Texture2D, TextureId);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, Width, Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            
            var mag = (int)TextureMagFilter.Nearest;
            var min = (int)TextureMinFilter.Nearest;
            GL.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, ref mag);
            GL.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, ref min);

            GL.FramebufferTexture(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureId, 0);

            //var renderBuffer = GL.GenRenderbuffer();
            //GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, renderBuffer);
            //GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent, Width, Height);
            //GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, renderBuffer);

            var b = DrawBuffersEnum.ColorAttachment0;
            GL.DrawBuffers(1, ref b);

            var state = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);

            Unbind();
            UnbindTexture();
        }

        public void Resize(int w, int h)
        {
            Dispose();

            Width = w;
            Height = h;

            Init();
        }

		public void CopyTo(FBO_ other)
		{
			// bind fbo as read / draw fbo
			GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, FboId);
			GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, FboId);

			// bind source texture to color attachment
			GL.BindTexture(TextureTarget.Texture2D, TextureId);
			GL.FramebufferTexture2D(FramebufferTarget.DrawFramebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, TextureId, 0);
			GL.DrawBuffer(DrawBufferMode.ColorAttachment0);

			// specify source, destination drawing (sub)rectangles. 
			GL.BlitFramebuffer(0, 0, Width, Height, 0, 0, other.Width, other.Height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);

			// release state
			GL.BindTexture(TextureTarget.Texture2D, 0);
			GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);
			GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
		}

        public void Bind()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, FboId);
        }

        public void Unbind()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        public void BindTexture()
        {
            GL.BindTexture(TextureTarget.Texture2D, TextureId);
        }

        public void UnbindTexture()
        {
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        public void Dispose()
        {
            GL.DeleteBuffer(FboId);
            GL.DeleteTexture(TextureId);
        }
    }
}
