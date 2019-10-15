// Shaders in this sample use 3 constant buffers (standard in DXEngine)
// One for camera related data
// One for lights data
// One for objects data
// All three constant buffers are initialized in the Effects/SimpleDirectionalLightEffect.cs class
// All changes in the constant buffers in the hlsl file must be done with changing the constant buffer declarations in SimpleDirectionalLightEffect class.


cbuffer cbPerFrameCamera : register(b0)
{
	row_major float4x4 gViewProjection : VIEWPROJECTION;
	float3 gEyePosW : POSITION;
}

cbuffer cbPerFrameLights : register(b1)
{
	float3 gAmbientColor : COLOR;

	float3 gLightDirection;
	float3 gLightColor;

	float gFogStartW;
	float gFogEndW;
	float3 gFogColor;
};

cbuffer cbPerObject : register(b2)
{
	float4 gDiffuseColor : COLOR;

	row_major float4x4 gWorld : WORLD;
	row_major float4x4 gWorldInverseTranspose : WORLDINVERSETRANSPOSE;
};




struct vsIN
{
	float3 posL    : POSITION;
	float3 normalL : NORMAL;
};

struct vsOUT
{
	float4 posH            : SV_POSITION;
	float4 posW            : TEXCOORD0;
	float3 normalW         : TEXCOORD1;
};


// Vertex shader is executed for each vertex
vsOUT mainVertexShader(vsIN vIn)
{
	vsOUT vOut;

	// Calculate WorldViewProjection matrix
	float4x4 wvp = mul(gWorld, gViewProjection);

	// Transform local space position into homogeneous clip space.
	vOut.posH = mul(float4(vIn.posL, 1.0f), wvp);

	vOut.posW = mul(float4(vIn.posL, 1.0f), gWorld);

	// To preserve the angle of normal, we must multiply it with WorldInverseTranspose (instead of just World)
	vOut.normalW = mul(float4(vIn.normalL, 1.0f), gWorldInverseTranspose).xyz;

	return vOut;
}

// Pixel shader is executed for each pixel
float4 mainPixelShader(vsOUT pIn) : SV_Target
{
	// Normalize normal because interpolation from vertex shader does not preserve the length
	float3 normalW = normalize(pIn.normalW);


	// Add Lambertian directional lighting :
	// lighting amount on a given surface is the dot product of the light vector and the face's normal:
	// light vector is pointing in the oposize direction as light direction

	float diffuseFactor = dot(-gLightDirection, normalW);

	// if diffuseFactor < 0 than this position is facing away from the light - not illuminated
	// saturate function: Clamps the specified value within the range of 0 to 1 - so negative (not illuminated) values are clamped to 0
	float3 diffuseColor = saturate(diffuseFactor);

	// Add diffuseColor to gAmbientColor
	float3 color = gAmbientColor + diffuseColor;

	// Multiply color by materials' color
	color = color * gDiffuseColor.rgb;


	// Adding fog

	// Get distance from the current point to camera (in world coordinates)
	float distanceW = length(gEyePosW - pIn.posW);

	// Fog settings (in world coordinates)
	//float fogStartW = 150.0; // fog starts when objects are 150 units from camera
	//float fogEndW = 220.0;   // full color fog start at 220 units
	//float3 fogColor = float3(1, 1, 1); // fog color in RGB

	// interpolate from 0 to 1: 0 starting at fogStart and 1 at fogEnd 
	// saturate clamps the specified value within the range of 0 to 1.
	float fogFactor = saturate((distanceW - gFogStartW) / (gFogEndW - gFogStartW));

	// lerp lineary interpolates the color
	color = lerp(color, gFogColor, fogFactor);


	// For final color add alpha chanel from material's diffuse color
	// Saturate (with adding gAmbientColor we could get values bigger than 1)
	float4 finalColor = float4(saturate(color), gDiffuseColor.a);

	return finalColor;
}


// technique section is not used in ShaderFactory because here the shader is not compiled into Effect but directly into shader with specified entry point name
technique Technique1 {
	pass p0 {
		VertexShader = compile vs_4_0 mainVertexShader();
		PixelShader = compile ps_4_0 mainPixelShader();
	}
}



// This shader can be compiled with fxc command line tool with the following line (with included debug info and removed optimizations - /Zi /Od switches):
//
// vertex shader:
// fxc "FogShader.hlsl" /T vs_4_0 /Op /Zi /Od /E mainVertexShader /Fo VertexShader.vs /Fx VertexShader.vs.txt
//
// pixel shader:
// fxc "FogShader.hlsl" /T ps_4_0 /Op /Zi /Od /E mainPixelShader /Fo PixelShader.ps /Fx PixelShader.ps.txt
// 
// To remove debugging information and add optimizations remove the /Zi /Od switches