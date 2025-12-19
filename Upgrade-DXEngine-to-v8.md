# Guide to upgrade Ab3d.DXEngine to v8.0

## Changes overview

Ab3d.DXEngine v8.0 has the same features as v7.2. The only difference is that v7.2 depends on the third-party SharpDX library.

The SharpDX library is used for some math structs (Vector3, Vector2, Matrix, Color3, Color4, etc.) and for DirectX 11 interop. The v8.0, on the other hand, has no third-party dependencies. For some math structs (Vector3, Vector2, Matrix4x4) it uses System.Numerics namespace that defines super fast SIMD-friendly structs. Other structs, like Color3, Color4 and BoundingBox are defined in the Ab3d.DXEngine. Also, the DirectX interop structs and COM classes are defined in the Ab3D.DXEngine library.


## Changes details

Here is a table showing the types that were previously defined in SharpDX and SharpDX.Mathematics assemblies and are now part of Ab3d.DXEngine v8.0 or types from System.Numerics are used:

type	        | v7.2	    | v8.0
:---------------|:----------|:-------------------------------------
Vector3	        | SharpDX	| System.Numerics
Vector2	        | SharpDX	| System.Numerics
Matrix	        | SharpDX	| System.Numerics (alias to Matrix4x4)
Color3	        | SharpDX	| Ab3d.DirectX.Common
Color4	        | SharpDX	| Ab3d.DirectX.Common
BoundingBox	    | SharpDX	| Ab3d.DirectX.Common
BoundingFrustum	|SharpDX	| Ab3d.DirectX.Common
Ray	            | SharpDX	| Ab3d.DirectX.Common


Because the types in System.Numerics can define different methods than the types in SharpDX, the C# 14 extensions are used to map the old methods to the new methods. For example, Vector3 struct in SharpDX has a Normalize instance method that normalizes the vector. But Vector3 struct in System.Numerics only has a static Vector3.Normalize method. Therefore, an extension is used to define an instance Normalize method so the same code can be used for both v7.2 and v8.0.

Ab3d.DXEngine also requires many structs and COM classes for DirectX interop. For that, the v7.2 and previous versions require references to SharpDX.DXGI and SharpDX.Direct3D11 assemblies.

The Ab3d.DXEngine v8.0 does not define all the classes from the DXGI and Direct3D11 namespaces but **only those that are used** by the Ab3d.DXEngine. Those structs and classes are defined in the Ab3d.DirectX.Common, Ab3d.DXGI, Ab3d.Direct3D and Ab3d.Direct3D11 namespaces.

## Upgrade process

First, you will need to change the TargetFramework to net10.0-windows. This is required because Ab3d.DXEngine v8.0 uses instance extension methods that are supported only in C# 14.

### Use only DXEngine v8.0

Then you can use “Replace in Files” option to replace the common SharpDX using with the new using statements.

The first replacement is:

`using SharpDX;` => `using Ab3d.DirectX.Common;\r\nusing System.Numerics;` (enable regular expression to support replacing one line with two lines.)

This should solve most of the problems.

Then, if you are using DirectX 11 or DXGI objects, you will also need to perform the following replacements:

`using SharpDX.DXGI;` => `using Ab3d.DXGI;`

`using SharpDX.Direct3D;` => `using Ab3d.Direct3D;`

`using SharpDX.Direct3D11;` => `using Ab3d.Direct3D11;`



If you are using SharpDX.Matrix, then you can replace the Matrix with Matrix4x4 or add the following using alias to the using block:

`using Matrix = System.Numerics.Matrix4x4;`

This will use Matrix4x4 from System.Numerics instead of SharpDX.Matrix.


### Use both DXEngine v7.2 and v8.0

This method is used with Ab3d.DXEngine.Wpf.Samples project where the same .cs and .xaml files can be used for DXEngine v7.2 and for v8.0.

In this case when the project is used for v7.3, the csproj file defines the SHARPDX compiler constant. This is done by:

```
 <PropertyGroup>
    <DefineConstants>SHARPDX</DefineConstants>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Debug|AnyCPU'">
    <DefineConstants>$(DefineConstants);DEBUG</DefineConstants>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Release|AnyCPU'">
    <DefineConstants>$(DefineConstants);TRACE</DefineConstants>
  </PropertyGroup>
```

Then referenced packages can be set based on the SHARPDX constant:

```
  <ItemGroup Condition="$(DefineConstants.Contains('SHARPDX'))">
    <PackageReference Include="Ab3d.DXEngine" Version="7.2.9484" />
    <PackageReference Include="Ab3d.DXEngine.Wpf" Version="7.2.9484" />
    <PackageReference Include="SharpDX" Version="4.2.0" />
    <PackageReference Include="SharpDX.Direct3D" Version="4.2.0" />
    <PackageReference Include="SharpDX.Direct3D11" Version="4.2.0" />
    <PackageReference Include="SharpDX.Mathematics" Version="4.2.0" />
  </ItemGroup>

  <ItemGroup Condition="!$(DefineConstants.Contains('SHARPDX'))">
    <PackageReference Include="Ab3d.DXEngine" Version="8.0.9484" />
    <PackageReference Include="Ab3d.DXEngine.Wpf" Version="8.0.9484" />
  </ItemGroup>
```

In cs files, you can conditionally define different usings. For example (from DXEngineAdvanced/CustomRenderingStep4.xaml.cs):

```
#if SHARPDX
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Buffer = SharpDX.Direct3D11.Buffer;
using Matrix = SharpDX.Matrix;
#else
using System.Numerics;
using Ab3d.DirectX.Common;
using Ab3d.Direct3D;
using Ab3d.Direct3D11;
using Ab3d.DXGI;
using Buffer = Ab3d.Direct3D11.Buffer;
using Matrix = System.Numerics.Matrix4x4;
#endif
```

In you are using .Net 6 or newer, then you can also remove all SharpDX and DirectX specific using from .cs files and define them in a global using file (using #if SHARPDX as described before).