
@echo off
rem DEBUG option used
rem /Zi /Od /Gfp  - enable debugging information
rem /Od  - disable optimizations
rem /Gfp - prefer flow control constructs 
rem /Op  - disable preshaders

rem Before using DirectX offline compuler (fxc), you need to set the path to fxc.exe to the path environment variable.
rem fxc is usally installe into "C:\Program Files (x86)\Windows Kits\10\bin\x64\fxc.exe"; if not check some other foler or install Windows SDK

fxc ShadedPointCloudShader.hlsl /T vs_4_0 /Op /Zi /Od /Gfp /E VS_ShadedPointCloud /Fo ShadedPointCloud.vs /Fx ShadedPointCloud.vs.txt

rem fxc ShadedPointCloudShader.hlsl /T vs_4_0 /Op /Zi /Od /Gfp /E VS_ShadedPointCloud_Instanced /Fo ShadedPointCloud_Instanced.vs /Fx ShadedPointCloud_Instanced.vs.txt

rem Now compile another vertex shader, this time with support for providing color for each point
rem We do that with defining the PER_POINT_COLOR constant
fxc ShadedPointCloudShader.hlsl /T vs_4_0 /Op /Zi /Od /Gfp /D PER_POINT_COLOR /E VS_ShadedPointCloud /Fo ShadedPointCloudPerPointColor.vs /Fx ShadedPointCloudPerPointColor.vs.txt

fxc ShadedPointCloudShader.hlsl /T gs_4_0 /Op /Zi /Od /Gfp /E GS_ShadedPointCloud /Fo ShadedPointCloud.gs /Fx ShadedPointCloud.gs.txt

fxc ShadedPointCloudShader.hlsl /T ps_4_0 /Op /Zi /Od /Gfp /E PS_ShadedPointCloud /Fo ShadedPointCloud.ps /Fx ShadedPointCloud.ps.txt

pause