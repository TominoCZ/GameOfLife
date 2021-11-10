#version 330

in vec2 pass_uv;

uniform sampler2D gen;
uniform sampler2D image;

void main(void){
	vec2 uv = vec2(pass_uv.x, 1.0 - pass_uv.y);

	vec4 color = texture(image, pass_uv);
	vec4 gen = texture(gen, uv);
	
	gl_FragColor = vec4(color.rgb * max(0.1, pow(gen.g, 2)), 1.0);
	
	//float f = (gen.a + genLast.a) / 2;
	//gl_FragColor = gen + 0.0001 * vec4((gen.rgb + genLast.rgb) / 2 * f, 1.0);
}