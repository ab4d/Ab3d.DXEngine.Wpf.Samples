rem DEBUG option used
rem /Zi /Od /Gfp  - enable debugging information
rem /Od  - disable optimizations
rem /Gfp - prefer flow control constructs 
rem /Op  - disable preshaders

rem Before using DirectX offline compuler (fxc), you need to set the path to fxc.exe to the path environment variable.
rem fxc is usally installe into "C:\Program Files (x86)\Windows Kits\10\bin\x64\fxc.exe"; if not check some other foler or install Windows SDK

fxc FogShader.hlsl /T vs_4_0 /Op /Zi /Od /Gfp /E mainVertexShader /Fo FogShader.vs /Fx FogShader.vs.txt

fxc FogShader.hlsl /T ps_4_0 /Op /Zi /Od /Gfp /E mainPixelShader /Fo FogShader.ps /Fx FogShader.ps.txt

pause