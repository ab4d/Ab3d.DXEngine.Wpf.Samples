rem RELESE / DEBUG build:
rem /Op  - disable preshaders (not supported on DX11 - see https://monogame.codeplex.com/discussions/371343)

rem To allow debugging the shaders add the following options:
rem /Zi /Od /Gfp  - enable debugging information

fxc MeshNormalShader.hlsl /T vs_4_0 /Op /E mainVertexShader /Fo MeshNormalShader.vs /Fx MeshNormalShader.vs.txt

fxc MeshNormalShader.hlsl /T ps_4_0 /Op /E mainPixelShader /Fo MeshNormalShader.ps /Fx MeshNormalShader.ps.txt

pause