using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameOfLife
{
	internal class GameOfLifeShader : Shader
    {
        public GameOfLifeShader() : base("cells", "bufferSize", "deltaTime", "fadeSpeed")
		{

        }
    }
}
