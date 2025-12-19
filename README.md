# Ab3d.DXEngine.Wpf.Samples

![Ab3d.DXEngine engine image](https://www.ab4d.com/images/DXEngine/DXEngine-car_engine-500.png)

[Ab3d.DXEngine](https://www.ab4d.com/DXEngine.aspx) is a DirectX 11 rendering engine for Desktop .Net applications. Ab3d.DXEngine is built for advanced business and scientific 3D visualization.

Ab3d.DXEngine uses **super fast rendering** techniques that can fully utilize graphics cards and provide the **ultimate performance**. It also supports **top quality visuals** with PBR materials and shadows.

The samples in this repository demonstrate many features of the engine and show that with .Net it is easily possible to create applications with complex 3D graphics.

The samples are using [Ab3d.PowerToys](https://www.ab4d.com/PowerToys.aspx) library - the ultimate WPF and WinForms 3D toolkit library that greatly simplifies developing desktop applications with scientific, technical, CAD or other 3D graphics.

Both Ab3d.DXEngine and Ab3d.PowerToys are commercial libraries. You can start a 60-day trial when they are first used.

> NOTE:
> Ab3d.DXEngine v7 and v8 have the same features. The difference is that v7 requires the third-party SharpDX library, but v8 has all the DirectX 11 interop built into the library and does not require any third-party dependencies.


## Repository solutions

The Ab3d.DXEngine.Wpf.Samples repository contains the following Visual Studio solutions:
* Ab3d.DXEngine WPF Samples net48.sln (for .Net Framework 4.8; uses Ab3d.DXEngine v7 and requires SharpDX library)
* Ab3d.DXEngine WPF Samples net8.sln (for .Net 8; uses Ab3d.DXEngine v7 and requires SharpDX library)
* Ab3d.DXEngine WPF Samples net10.sln (for .Net 10; uses Ab3d.DXEngine v7 and requires SharpDX library)
* Ab3d.DXEngine WPF Samples net10.0 no SharpDX.sln (for .Net 10; uses Ab3d.DXEngine v8 and does not require SharpDX library)

## Dependencies

The projects uses the following dependencies:
* Ab3d.DXEngine - Core Ab3d.DXEngine assembly - https://www.nuget.org/packages/Ab3d.DXEngine
* Ab3d.DXEngine.Wpf - WPF support for Ab3d.DXEngine - https://www.nuget.org/packages/Ab3d.DXEngine.Wpf
* Ab3d.DXEngine.glTF - glTF importer for DXEngine - https://www.nuget.org/packages/Ab3d.DXEngine.glTF
* Ab3d.PowerToys - The ultimate WPF 3D helper library - https://www.nuget.org/packages/Ab3d.PowerToys
* Assimp - Assimp 3D files importer (native library) - 32 and 64-bit dlls available in lib folder
* Assimp.Net - Managed wrapper for native assimp importer - available in lib folder
* Ab3d.PowerToys.Assimp - Assimp wrapper for WPF 3D objects - available in lib folder

Ab3d.DXEngine v7 also require the following SharpDX libraries (this is not required for v8):
* SharpDX - core assembly for DirectX managed wrapper - https://www.nuget.org/packages/SharpDX
* SharpDX.DXGI - DirectX - DXGI managed API - https://www.nuget.org/packages/SharpDX
* SharpDX.Direct3D11 - Direct3D11 managed API - https://www.nuget.org/packages/SharpDX
* SharpDX.Mathematics - DirectX - Mathematics managed API - https://www.nuget.org/packages/SharpDX

## Support

* Online users guide: https://www.ab4d.com/DirectX/3D/Introduction.aspx
* Online reference help: https://www.ab4d.com/help/DXEngine/html/R_Project_Ab3d_DXEngine_Help.htm
* Change log: https://www.ab4d.com/DXEngine-history.aspx
* Forum: https://forum.ab4d.com/forumdisplay.php?fid=11
* Related blog posts: http://blog.ab4d.com/category/DXEngine.aspx
* Feedback: https://www.ab4d.com/Feedback.aspx

## See also

* [AB4D Homepage](https://www.ab4d.com/)
* [Ab3d.DXEngine](https://www.ab4d.com/DXEngine.aspx) library
* [Ab3d.PowerToys](https://www.ab4d.com/PowerToys.aspx) library
* [Ab3d.PowerToys Samples on GitHub](https://github.com/ab4d/Ab3d.PowerToys.Wpf.Samples)
* [AB4D products price list](https://www.ab4d.com/Purchase.aspx#DXEngine)
