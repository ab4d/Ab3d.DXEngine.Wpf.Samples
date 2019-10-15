// When TWO_SIDED is defined, then triangles are illuminated with the same material on the front and back side.
// This also required that the RasterizerState is set to CullNode (see ApplyMaterial in ShadedPointCloudEffect).
// 
// When TWO_SIDED is not defined (is commented), then only front side of triangles is illuminated (back side is black or in ambient light).
// This is usually used for standard 3D models where RasterizerState is set to CullClockwise)
//
// NOTE that TWO_SIDED can be also set when compiling with fxc with /D TWO_SIDED parameter.
// This way it is possible to compile multiple versions of shader in a bat file.
#define TWO_SIDED; 


// When PER_POINT_COLOR is defined, then each point have its own color.
// This constant is defined in fxc call in CompileShadedPointCloudShader.bat
//#define PER_POINT_COLOR;


// When SPECULAR_LIGHTING is defined, the vertex shader has code to calculate specular light color.
// Commenting this constant sligtly improves the performance of the vertex shader.
// Note that there are a few fields in the cbVertexBuffer that are used only for specula color (gWorld, gEyePosW, gSpecularColorPower).
// They are not removed when SPECULAR_LIGHTING is commented because this would complicate the effect code in cs where we would require two versions of constant buffer and this does not have any performance effect.
// But if you do not require the specular light calculations, then this part of the cbVertexBuffer and in the ShadedPointCloudEffect can be commented.
#define SPECULAR_LIGHTING

// When defined the point color is defined by the value of normal vector
//#define RENDER_NORMAL_VECTOR_AS_COLOR;

//-----------------------------------------------------------------------------
// Constant buffers
//-----------------------------------------------------------------------------

// Usually we create 2 constant buffers: perFrame and perObject. This way we need to update the perFrame only once per frame.
// But usually PointClouds are not rendered many times so we can merge all data into one constant buffer.

cbuffer cbVertexBuffer : register(b2)
{
	// camera properties:
	row_major float4x4 gWorld : WORLD;
	row_major float4x4 gWorldViewProjection   : WORLDVIEWPROJECTION;
	row_major float4x4 gWorldInverseTranspose : WORLDINVERSETRANSPOSE;

	float3 gEyePosW;

	// global pixel properties:
	float4 gDiffuseColor       : COLOR; // This color is multiplied with the color of each pixel defined in VS_INPUT; It also define the global alpha color value for all pixels
	float4 gSpecularColorPower : COLOR;

	// light properties:
	float3 gAmbientColor : COLOR;
	float3 gLightDirection;
	float3 gLightColor : COLOR;
};

cbuffer cbGeometyBuffer
{
	float2 halfPixelSizePerViewport; // = halfPixelSize / Viewport  (note: lineThickness is float and not float2)
};

//-----------------------------------------------------------------------------
// Shader Input / Output Structures
//-----------------------------------------------------------------------------
struct VS_INPUT
{
	float3 posL    : POSITION;
	float3 normalL : NORMAL;

#ifdef PER_POINT_COLOR
	float3 color   : COLOR0;
#endif
};

struct GEO_IN
{
	float4 posH       : SV_POSITION; // Center position of the quad
	float4 color      : COLOR0;
};

struct GEO_OUT
{
	float4 posH  : SV_POSITION;
	float4 color : COLOR0;
};

struct PS_OUTPUT
{
	float4 color : SV_Target0;
};



//-----------------------------------------------------------------------------
// Vertex shader
//-----------------------------------------------------------------------------
GEO_IN VS_ShadedPointCloud(in VS_INPUT input)
{
	GEO_IN output;

	float4 posH = mul(float4(input.posL, 1.0f), gWorldViewProjection);
	output.posH = posH;

	float3 normalW = mul(float4(input.normalL, 1.0f), gWorldInverseTranspose).xyz;
	normalW = normalize(normalW);

	// Add Lambertian directional lighting :
	// lighting amount on a given surface is the dot product of the light vector and the face's normal:
	// light vector is pointing in the oposize direction as light direction

	float diffuseFactor = dot(-gLightDirection, normalW);

	// if diffuseFactor < 0 than this triangle is facing away from the light

#ifdef TWO_SIDED
	// If we are rendering two-sided material, then just abs the diffuseFactor so the negative (not illuminated) values will also be rendered
	float3 diffuseColor = abs(diffuseFactor);
#else
	// saturate function: Clamps the specified value within the range of 0 to 1 - so negative (not illuminated) values are clamped to 0
	float3 diffuseColor = saturate(diffuseFactor);
#endif

	
	// Add gAmbientColor to diffuseColor and multiply by the global diffuse color
	float3 finalColor = saturate(gAmbientColor + diffuseColor * gLightColor) * gDiffuseColor.rgb;

#ifdef PER_POINT_COLOR
	// Multiply with the individual point color
	finalColor *= input.color;
#endif


#ifdef SPECULAR_LIGHTING
	float specularPower = gSpecularColorPower.w;

	if (specularPower > 0)
	{
		float3 posW = mul(float4(input.posL, 1.0f), gWorld).xyz;
		float3 toEye = normalize(gEyePosW - posW);

		// Blinn-Phong specualar lighting (using half vector instead of reflection)
		// See also: https://en.wikipedia.org/wiki/Blinn%E2%80%93Phong_shading_model
		float3 halfDir = normalize(toEye - gLightDirection);

		//Intensity of the specular light
		float specFactor = pow(saturate(dot(normalW, halfDir)), specularPower);

		// Add specular color to the final light
		finalColor += saturate(specFactor * gSpecularColorPower.xyz);
	}
#endif	

#ifdef RENDER_NORMAL_VECTOR_AS_COLOR
	finalColor = abs(normalW); // Render normal vector as color (X direction is shown as Red color, y as Green and z as Blue color)
	
	// The following code can be used to show positive normalW.x values as red pixles and negative normalW.x values as blue pixels (saturate method clamps values smaller than 0 to 0 and bigger then 1 to 1).
	//finalColor = float3(saturate(normalW.x), 0, saturate(-normalW.x));
#endif

	// Finally add the alpha channel and outpu the color for the pixel shader
	output.color = float4(saturate(finalColor), gDiffuseColor.a);

	return output;
}

//-----------------------------------------------------------------------------
// Geometry shader
//-----------------------------------------------------------------------------

// This geometry shader created quads with size defined in screen space
[maxvertexcount(4)]
void GS_ShadedPointCloud(point GEO_IN points[1], inout TriangleStream<GEO_OUT> output)
{
	float4 color = points[0].color;

	GEO_OUT v[4];

	// Pass the color further to the pixel shader
	v[0].color = color;
	v[1].color = color;
	v[2].color = color;
	v[3].color = color;


	float4 posH = points[0].posH;
	float2 pScreen = posH.xy / posH.w; // position in screen coordinates

	v[0].posH = float4((pScreen + float2(-halfPixelSizePerViewport.x, halfPixelSizePerViewport.y)) * posH.w, posH.z, posH.w);
	v[1].posH = float4((pScreen + float2( halfPixelSizePerViewport.x, halfPixelSizePerViewport.y)) * posH.w, posH.z, posH.w);

	v[2].posH = float4((pScreen + float2( halfPixelSizePerViewport.x, -halfPixelSizePerViewport.y)) * posH.w, posH.z, posH.w);
	v[3].posH = float4((pScreen + float2(-halfPixelSizePerViewport.x, -halfPixelSizePerViewport.y)) * posH.w, posH.z, posH.w);

	// Create TriangleStrip (see this link for triangles orientation: https://msdn.microsoft.com/en-us/library/windows/desktop/bb206274(v=vs.85).aspx)
	output.Append(v[0]);
	output.Append(v[3]);
	output.Append(v[1]);
	output.Append(v[2]);

	output.RestartStrip();
}



//-----------------------------------------------------------------------------
// Pixel shader
//-----------------------------------------------------------------------------
PS_OUTPUT PS_ShadedPointCloud(GEO_OUT input)
{
	PS_OUTPUT output;
	output.color = input.color;

	return output;
}




/*

Shaders that generate quads in world space are not used any more - we only generate quad in screen space

cbuffer cbVertexBuffer : register(b2)
{
// camera properties:
row_major float4x4 gWorld : WORLD;
row_major float4x4 gWorldViewProjection   : WORLDVIEWPROJECTION;
row_major float4x4 gWorldInverseTranspose : WORLDINVERSETRANSPOSE;

float3 gEyePosW;


// global pixel properties:
float4 gDiffuseColor       : COLOR; // This color is multiplied with the color of each pixel defined in VS_INPUT; It also define the global alpha color value for all pixels
float4 gSpecularColorPower : COLOR;

float gHalfPointSize; // half size of the quad that defines the pixel (in world space; not in screen space as with PixelVisual3D or 3D lines)


// light properties:
float3 gAmbientColor : COLOR;
float3 gLightDirection;
float3 gLightColor : COLOR;
};

struct GEO_IN
{
float4 posH       : SV_POSITION; // Center position of the quad
float4 quadUpH    : NORMAL0;      // Up direction of the quad
float4 quadRightH : NORMAL1;      // Right direction of the quad
float4 color      : COLOR0;
};

GEO_IN VS_ShadedPointCloud(in VS_INPUT input)
{
GEO_IN output;

float4 posH = mul(float4(input.posL, 1.0f), gWorldViewProjection);
output.posH = posH;

float3 normalW = mul(float4(input.normalL, 1.0f), gWorldInverseTranspose).xyz;
normalW = normalize(normalW);

// To create a quad from a position and normal vector
// we need to get the horizontal and up vector.
// They define the directions into which the position is expanded.
//
// We do that by calcualting horizontalVector as a vector perpendicular to the UpAxis (0,1,0) and the normal vector.
float3 horizontalVector = cross(float3(0, 1, 0), normalW);

float horizontalVectorLengthSquared = dot(horizontalVector, horizontalVector);
if (horizontalVectorLengthSquared < 0.00001)
{
// In case normalW points up and do not provide information about the horizontalVector direction,
// we pick a vector that points in the Z axis direction
horizontalVector = float3(0, 0, 1);
}

float3 upVector = cross(horizontalVector, normalW);
upVector = normalize(upVector);

horizontalVector *= gHalfPointSize;
upVector *= gHalfPointSize;

output.quadUpH = mul(float4(upVector, 0.0f), gWorldViewProjection); // prevent using translation in gWorldViewProjection with specifying 0 for w (in float4)
output.quadRightH = mul(float4(horizontalVector, 0.0f), gWorldViewProjection);


// Add Lambertian directional lighting :
// lighting amount on a given surface is the dot product of the light vector and the face's normal:
// light vector is pointing in the oposize direction as light direction

float diffuseFactor = dot(-gLightDirection, normalW);

// if diffuseFactor < 0 than this triangle is facing away from the light

#ifdef TWO_SIDED
// If we are rendering two-sided material, then just abs the diffuseFactor so the negative (not illuminated) values will also be rendered
float3 diffuseColor = abs(diffuseFactor);
#else
// saturate function: Clamps the specified value within the range of 0 to 1 - so negative (not illuminated) values are clamped to 0
float3 diffuseColor = saturate(diffuseFactor);
#endif


// Add gAmbientColor to diffuseColor and multiply by the global diffuse color
float3 finalColor = saturate(gAmbientColor + diffuseColor * gLightColor) * gDiffuseColor.rgb;

#ifdef PER_POINT_COLOR
// Multiply with the individual point color
finalColor *= input.color;
#endif


#ifdef SPECULAR_LIGHTING
float3 posW = mul(float4(input.posL, 1.0f), gWorld).xyz;
float3 toEye = normalize(gEyePosW - posW);

// Blinn-Phong specualar lighting (using half vector instead of reflection)
// See also: https://en.wikipedia.org/wiki/Blinn%E2%80%93Phong_shading_model
float3 halfDir = normalize(toEye - gLightDirection);

//Intensity of the specular light
float specFactor = pow(saturate(dot(normalW, halfDir)), gSpecularColorPower.w);

// Add specular color to the final light
finalColor += saturate(specFactor * gSpecularColorPower.xyz);
#endif

#ifdef RENDER_NORMAL_VECTOR_AS_COLOR
finalColor = abs(normalW); // Render normal vector as color (X direction is shown as Red color, y as Green and z as Blue color)

// The following code can be used to show positive normalW.x values as red pixles and negative normalW.x values as blue pixels (saturate method clamps values smaller than 0 to 0 and bigger then 1 to 1).
//finalColor = float3(saturate(normalW.x), 0, saturate(-normalW.x));
#endif

// Finally add the alpha channel and outpu the color for the pixel shader
output.color = float4(saturate(finalColor), gDiffuseColor.a);

return output;
}


// This geometry shader requies quadUpH and quadRightH to be passed to it so that it can create a quad defined in the world space (and not screen space)
[maxvertexcount(4)]
void GS_ShadedPointCloud(point GEO_IN points[1], inout TriangleStream<GEO_OUT> output)
{
float4 color = points[0].color;

GEO_OUT v[4];

// Pass the color further to the pixel shader
v[0].color = color;
v[1].color = color;
v[2].color = color;
v[3].color = color;


float4 posH       = points[0].posH;
float4 quadUpH    = points[0].quadUpH;
float4 quadRightH = points[0].quadRightH;

v[0].posH = posH - quadRightH + quadUpH;
v[1].posH = posH + quadRightH + quadUpH;

v[2].posH = posH - quadRightH - quadUpH;
v[3].posH = posH + quadRightH - quadUpH;


// Create TriangleStrip (see this link for triangles orientation: https://msdn.microsoft.com/en-us/library/windows/desktop/bb206274(v=vs.85).aspx)
output.Append(v[0]);
output.Append(v[1]);
output.Append(v[2]);
output.Append(v[3]);

output.RestartStrip();
}

*/