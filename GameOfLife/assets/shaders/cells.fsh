#version 330

in vec2 size;

out vec4 out_Color;

//uniform float deltaTime;
uniform vec2 bufferSize;
uniform sampler2D lastGen;

vec4 GetNode(float x, float y)
{
	float sx = bufferSize.x;
	float sy = bufferSize.y;
	
	vec2 uv = vec2(mod(x + sx, sx)/sx, mod(y + sy, sy)/sy);
	
	vec4 node = texture(lastGen, uv);

	return node;
}

int CountAliveNeighbors()
{
	int alive = 0;
	
	for (int y = -1; y < 2; y++)
	{
		for (int x = -1; x < 2; x++)
		{
			if (x != 0 || y != 0) {
				vec4 node = GetNode(x + gl_FragCoord.x, y + gl_FragCoord.y);
				
				if (node.x == 1.0)
				{
					alive++;
				}
			}
		}
	}
	
	return alive;
}

void main(void){
	vec4 state = GetNode(gl_FragCoord.x, gl_FragCoord.y);
	
	bool alive = state.x == 1.0;
	
	int aliveCount = CountAliveNeighbors();
	
	if (!alive && aliveCount == 3)
	{
		state.x = 1.0;
		state.y = 1.0;
		state.z = 1.0;
		
		alive = true;
	}
	else if (alive && (aliveCount < 2 || aliveCount > 3))
	{
		state.x = 0.0;
		state.y = 0.0;
		state.z = 0.0;
		
		alive = false;
	}
	
	out_Color = state;
}