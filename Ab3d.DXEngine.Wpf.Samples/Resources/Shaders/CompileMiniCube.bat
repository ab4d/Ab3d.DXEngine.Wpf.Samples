rem DEBUG option used
rem /Zi /Od /Gfp  - enable debugging information
rem /Od  - disable optimizations
rem /Gfp - prefer flow control constructs 
rem /Op  - disable preshaders

rem Before using DirectX offline compuler (fxc), you need to set the path to fxc.exe to the path environment variable.
rem fxc is usally installe into "C:\Program Files (x86)\Windows Kits\10\bin\x64\fxc.exe"; if not check some other foler or install Windows SDK

fxc MiniCube.hlsl /T vs_4_0 /Op /Zi /Od /Gfp /E VS /Fo MiniCube.vs /Fx MiniCube.vs.txt

fxc MiniCube.hlsl /T ps_4_0 /Op /Zi /Od /Gfp /E PS /Fo MiniCube.ps /Fx MiniCube.ps.txt

pause