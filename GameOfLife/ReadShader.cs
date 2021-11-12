using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameOfLife
{
	internal class ReadShader : Shader
    {
        public ReadShader() : base("cells_read", "gen", "image", "bottomValue")
        {

        }
    }
}
