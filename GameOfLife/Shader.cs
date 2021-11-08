using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.IO;

namespace GameOfLife
{
	internal class Shader
    {
        private static readonly List<Shader> Shaders = new List<Shader>();

        private int _vsh;
        private int _fsh;

        private int _program;
        private bool _registered;
        public readonly string ShaderName;

        private readonly Dictionary<string, int> _uniforms = new Dictionary<string, int>();

        public Shader(string shaderName, params string[] uniforms)
        {
            ShaderName = shaderName;

            Init();

            RegisterUniformsSilent("projectionMatrix");
            RegisterUniformsSilent("transformationMatrix");
            RegisterUniforms(uniforms);

            Shaders.Add(this);
        }

        private void Init()
        {
            LoadShader(ShaderName);

            //creates and ID for this program
            _program = GL.CreateProgram();

            //attaches shaders to this program
            GL.AttachShader(_program, _vsh);
            GL.AttachShader(_program, _fsh);

            BindAttributes();

            GL.LinkProgram(_program);
            GL.ValidateProgram(_program);
        }

        private void BindAttributes()
        {
            BindAttribute(0, "position");
            BindAttribute(1, "textureCoords");
            //BindAttribute(2, "normal");
        }

        private int GetUniformLocation(string uniform)
        {
            if (_uniforms.TryGetValue(uniform, out var loc))
                return loc;

            return -1;
        }

        private void RegisterUniforms(params string[] uniforms)
        {
            if (_registered)
                throw new Exception("Can't register uniforms twice, they need to be registered only once.");

            _registered = true;

            Bind();
            foreach (var uniform in uniforms)
            {
                if (_uniforms.ContainsKey(uniform))
                {
                    Console.WriteLine($"Attemted to register uniform '{uniform}' in shader '{ShaderName}' twice");
                    continue;
                }

                var loc = GL.GetUniformLocation(_program, uniform);

                if (loc == -1)
                {
                    Console.WriteLine($"Could not find uniform '{uniform}' in shader '{ShaderName}'");
                    continue;
                }

                _uniforms.Add(uniform, loc);
            }
            Unbind();
        }

        private void RegisterUniformsSilent(params string[] uniforms)
        {
            Bind();
            foreach (var uniform in uniforms)
            {
                if (_uniforms.ContainsKey(uniform))
                    continue;

                var loc = GL.GetUniformLocation(_program, uniform);

                if (loc == -1)
					throw new NotImplementedException(uniform);

				_uniforms.Add(uniform, loc);
            }
            Unbind();
        }

        public void SetFloat(string uniform, float f)
        {
            var loc = GetUniformLocation(uniform);

			if (loc == -1)
				throw new NotImplementedException(uniform);

			GL.Uniform1(loc, f);
		}

		public void SetVector2(string uniform, float x, float y)
        {
            var loc = GetUniformLocation(uniform);

			if (loc == -1)
				throw new NotImplementedException(uniform);

            GL.Uniform2(loc, x, y);
        }

		public void SetVector2(string uniform, int x, int y)
		{
			var loc = GetUniformLocation(uniform);

			if (loc == -1)
				throw new NotImplementedException(uniform);

			GL.Uniform2(loc, x, y);
		}

		public void SetVector3(string uniform, Vector3 vec)
        {
            var loc = GetUniformLocation(uniform);

			if (loc == -1)
				throw new NotImplementedException(uniform);

			GL.Uniform3(loc, vec);
        }

        public void SetVector4(string uniform, Vector4 vec)
        {
            var loc = GetUniformLocation(uniform);

			if (loc == -1)
				throw new NotImplementedException(uniform);

			GL.Uniform4(loc, vec);
        }

        public void SetMatrix4(string uniform, Matrix4 mat)
        {
            var loc = GetUniformLocation(uniform);

			if (loc == -1)
				throw new NotImplementedException(uniform);

			GL.UniformMatrix4(loc, false, ref mat);
        }

		public void SetSampler2D(string uniform, int unit)
		{
			var loc = GetUniformLocation(uniform);

			if (loc == -1)
				throw new NotImplementedException(uniform);

			GL.Uniform1(loc, unit);
		}


		public static void SetProjectionMatrix(Matrix4 mat)
        {
            for (var index = 0; index < Shaders.Count; index++)
            {
                var shader = Shaders[index];
                shader.Bind();
                shader.SetMatrix4("projectionMatrix", mat);
                shader.Unbind();
            }
        }

		public static void SetViewMatrix(Matrix4 mat)
        {
            for (var index = 0; index < Shaders.Count; index++)
            {
                var shader = Shaders[index];
                shader.Bind();
                shader.SetMatrix4("viewMatrix", mat);
                shader.Unbind();
            }
        }

		private void BindAttribute(int attrib, string variable)
        {
            GL.BindAttribLocation(_program, attrib, variable);
        }

        private void LoadShader(string shaderName)
        {
            //vertex and fragment shader code
            string vshCode = File.ReadAllText($"shaders/{shaderName}.vsh");
            string fshCode = File.ReadAllText($"shaders/{shaderName}.fsh");

            //create IDs for shaders
            _vsh = GL.CreateShader(ShaderType.VertexShader);
            _fsh = GL.CreateShader(ShaderType.FragmentShader);

            //load shader codes into memory
            GL.ShaderSource(_vsh, vshCode);
            GL.ShaderSource(_fsh, fshCode);

            //compile shaders
            GL.CompileShader(_vsh);
            GL.CompileShader(_fsh);

			CheckError(_vsh);
			CheckError(_fsh);
		}

		private void CheckError(int id)
		{
			GL.GetShader(id, ShaderParameter.CompileStatus, out int r);

			var err = GL.GetShaderInfoLog(id);

			if (r == 0 && !string.IsNullOrEmpty(err))
			{
				throw new Exception(err);
			}
		}

        public void Bind()
        {
            GL.UseProgram(_program);
        }

        public void Unbind()
        {
            GL.UseProgram(0);
        }

        public void Reload()
        {
            Destroy();

            Init();
        }

        public void Destroy()
        {
            Unbind();

            GL.DetachShader(_program, _vsh);
            GL.DetachShader(_program, _fsh);

            GL.DeleteShader(_vsh);
            GL.DeleteShader(_fsh);

            GL.DeleteProgram(_program);
        }

        public static void ReloadAll()
        {
            for (int index = 0; index < Shaders.Count; index++)
            {
                var shader = Shaders[index];

                shader.Reload(); //WARNING: keep this name of the function the same
            }
        }

        public static void DestroyAll()
        {
            for (int index = 0; index < Shaders.Count; index++)
            {
                var shader = Shaders[index];

                shader.Destroy(); //keep this name of the function the same
            }
        }

        public Shader Reloaded()
        {
            Reload();
            return this;
        }
    }
}