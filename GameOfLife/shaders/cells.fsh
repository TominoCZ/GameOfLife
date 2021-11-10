#version 330

in vec2 size;

uniform float fadeSpeed;
uniform float deltaTime;
uniform vec2 bufferSize;
uniform sampler2D lastGen;

vec4 GetNode(float x, float y)
{
	int sx = int(bufferSize.x);
	int sy = int(bufferSize.y);
	
	vec2 uv = vec2(mod(x + sx, sx)/sx, mod(y + sy, sy)/sy);
	
	vec4 node = texture(lastGen, uv);

	return node;
}

int CountAliveNeighbors(vec4 state)
{
	int alive = 0;
	
	if (state.r > 0.5)
		alive--;
	
	for (int y = -1; y < 2; y++)
	{
		for (int x = -1; x < 2; x++)
		{
			vec4 node = GetNode(x + gl_FragCoord.x, y + gl_FragCoord.y);
			
			if (node.r > 0.5)
			{
				alive++;
			}
		}
	}
	
	return alive;
}

vec4 GetNextState(vec4 state)
{
	int aliveCount = CountAliveNeighbors(state);
	
	vec4 newState = vec4(state);
	
	if (state.r < 0.5 && aliveCount == 3)
	{
		newState.r = 1.0;
		newState.g = 1.0;
	}
	else if (state.r > 0.5 && (aliveCount < 2 || aliveCount > 3))
	{
		newState.r = 0.0;
	}
	
	if (newState.r < 0.5)
	{
		newState.g = max(0.0, newState.g - deltaTime * fadeSpeed);
	}
	
	return newState;
}

void main(void){
	vec4 state = GetNode(gl_FragCoord.x, gl_FragCoord.y);
	
	gl_FragColor = GetNextState(state);
}