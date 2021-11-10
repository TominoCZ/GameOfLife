#version 330

in vec2 position;
in vec2 textureCoords;

uniform mat4 transformationMatrix;
uniform mat4 projectionMatrix;
uniform vec2 bufferSize;

void main(void){
	gl_Position = projectionMatrix * transformationMatrix * vec4(position, 0.0, 1.0);
}