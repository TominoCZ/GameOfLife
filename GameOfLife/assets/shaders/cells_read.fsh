#version 330

in vec2 pass_uv;

uniform sampler2D gen;
uniform sampler2D image;

void main(void){
	vec4 c = texture(gen, pass_uv);
		
	gl_FragColor = c;
}