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
};

cbuffer cbPerObject : register(b2)
{
	float3 gColorMask : COLOR;

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
	float4 posH    : SV_POSITION;
	float3 normalW : TEXCOORD1;
};


// Vertex shader is executed for each vertex
vsOUT mainVertexShader(vsIN vIn)
{
	vsOUT vOut;

	// Calculate WorldViewProjection matrix
	float4x4 wvp = mul(gWorld, gViewProjection);

	// Transform local space position into homogeneous clip space.
	vOut.posH = mul(float4(vIn.posL, 1.0f), wvp);

	// To preserve the angle of normal, we must multiply it with WorldInverseTranspose (instead of just World)
	vOut.normalW = mul(float4(vIn.normalL, 1.0f), gWorldInverseTranspose).xyz;

	return vOut;
}

// Pixel shader is executed for each pixel
float4 mainPixelShader(vsOUT pIn) : SV_Target
{
	// Normalize normal because interpolation from vertex shader does not preserve the length
	float3 normalW = normalize(pIn.normalW);

	float3 color = gColorMask * normalW + gAmbientColor;

	// Saturate the color (prevent values bigger then 1) and set alpha to 1
	float4 finalColor = float4(saturate(color), 1.0);

	return finalColor;
}


// This shader can be compiled with fxc command line tool with the following line (with included debug info and removed optimizations - /Zi /Od switches):
//
// vertex shader:
// fxc "6 - Directional light with fog shader.hlsl" /T vs_4_0 /Op /Zi /Od /E mainVertexShader /Fo VertexShader.vs /Fx VertexShader.vs.txt
//
// pixel shader:
// fxc "6 - Directional light with fog shader.hlsl" /T ps_4_0 /Op /Zi /Od /E mainPixelShader /Fo PixelShader.ps /Fx PixelShader.ps.txt
// 
// To remove debugging information and add optimizations remove the /Zi /Od switches