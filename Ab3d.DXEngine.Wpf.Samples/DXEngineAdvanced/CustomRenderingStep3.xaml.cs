using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Ab3d.DirectX;
using Ab3d.DirectX.Cameras;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Buffer = SharpDX.Direct3D11.Buffer;
using Matrix = SharpDX.Matrix;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineAdvanced
{
    // This sample is the third in the series on how to render custom SharpDX objects inside DXViewportView object.
    //
    // This sample shows how to render some WPF 3D objects and add custom SharpDX objects to the same scene.
    // This is possible because the same buffers (BackBuffer, DepthStencilBuffer) are used by DXViewportView and SharpDX.
    // 
    // To add DXViewportView object to the scene we just need to add them to the Viewport3D - here this is done in XAML.
    //
    // But because of different coordinate system handiness (DXEngine uses right handed coordinate system, SharpDX used left handed coordinate system)
    // We need to change the IsRightHandedCoordinateSystem property to false for SharpDX and then set it back to true for the DXEngine part of the rendering.
    //
    // Note that we do not need to set the RasterizerState back to Clockwise culling because this is done automatically at the beginning of rendering in the PrepareRenderTargetsRenderingStep.
    // 
    // But before successful rendering of we need to do one final step:
    // In DXEngine we need to disable calculating near and far plane distanced based on the objects in 3D scene.
    // Without that the objects rendered with SharpDX could be clipped because their size is not accounted in the near and far plane calculations.
    // By default OptimizeNearAndFarCameraPlanes is set to true because this greatly improves the resolution of the depth buffer and
    // therefore reduces the possibility of Z-fighting artifacts.
    // This is done by the following line:
    //
    // dxScene.OptimizeNearAndFarCameraPlanes = false;
    //
    // In this case the NearPlaneDistance and FarPlaneDistance that are set in the camera are used.
    // To improve depth resolution, it is wise to set those two properties correctly.

    /// <summary>
    /// Interaction logic for CustomRenderingStep3.xaml
    /// </summary>
    public partial class CustomRenderingStep3 : Page
    {
        private VertexShader _vertexShader;
        private PixelShader _pixelShader;
        private InputLayout _layout;
        private Buffer _constantBuffer;
        private Buffer _vertices;

        public CustomRenderingStep3()
        {
            InitializeComponent();

            MainDXViewportView.DXSceneInitialized += delegate(object sender, EventArgs args)
            {
                if (MainDXViewportView.DXScene == null) // When DXEngine falls back to WPF 3D rendering, the DXScene is null; we could also check for MainDXViewportView.UsedGraphicsProfile.DriverType != GraphicsProfile.DriverTypes.Wpf3D
                    return;

                InitializeSharpDXRendering(MainDXViewportView.DXScene);

                var customActionRenderingStep = new CustomActionRenderingStep("Custom DXScene rendering step");
                customActionRenderingStep.CustomAction = SharpDXRenderingAction;

                MainDXViewportView.DXScene.RenderingSteps.AddBefore(MainDXViewportView.DXScene.DefaultRenderObjectsRenderingStep, customActionRenderingStep);
            };

            CompositionTarget.Rendering += OnCompositionTargetOnRendering;

            this.Unloaded += delegate { Dispose(); };
        }

        private void OnCompositionTargetOnRendering(object sender, EventArgs args)
        {
            // Because we did not change anything in the MainDXViewportView, the scene will not be automatically rendered again
            // Therefore we need to manually render it with calling Refresh method
            MainDXViewportView.Refresh();
        }

        private void InitializeSharpDXRendering(DXScene dxScene)
        {
            var device = dxScene.Device;

            // DirectX device, back buffer, render targets and other resources are initialized by DXEngine
            // So here we only initialize things that are added by this sample

            // Code from SharpDX MiniCube sample:
            //// Compile Vertex and Pixel shaders
            //var vertexShaderByteCode = ShaderBytecode.CompileFromFile("MiniCube.hlsl", "VS", "vs_4_0");
            //var vertexShader = new VertexShader(device, vertexShaderByteCode);

            //var pixelShaderByteCode = ShaderBytecode.CompileFromFile("MiniCube.hlsl", "PS", "ps_4_0");
            //var pixelShader = new PixelShader(device, pixelShaderByteCode);


            var vertexShaderByteCode = System.IO.File.ReadAllBytes(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\Shaders\MiniCube.vs"));
            _vertexShader = new VertexShader(device, vertexShaderByteCode);

            var pixelShaderByteCode = System.IO.File.ReadAllBytes(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\Shaders\MiniCube.ps"));
            _pixelShader = new PixelShader(device, pixelShaderByteCode);

            // Code from SharpDX MiniCube sample:
            //var signature = ShaderSignature.GetInputSignature(vertexShaderByteCode);

            //// Layout from VertexShader input signature
            //var layout = new InputLayout(device, signature, new[]
            //        {
            //            new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0, 0),
            //            new InputElement("COLOR", 0, Format.R32G32B32A32_Float, 16, 0)
            //        });
            _layout = new InputLayout(device, vertexShaderByteCode, new[]
            {
                new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0, 0),
                new InputElement("COLOR", 0, Format.R32G32B32A32_Float, 16, 0)
            });


            // Instantiate Vertex buffer from vertex data
            var vertexBuffer = CustomRenderingStep1.GetSharpDXBoxVertexBuffer();
            _vertices = Buffer.Create(device, BindFlags.VertexBuffer, vertexBuffer);


            // Create Constant Buffer
            _constantBuffer = new Buffer(device, SharpDX.Utilities.SizeOf<Matrix>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);


            // We need to disable calculating near and far plane distanced based on the objects in 3D scene.
            // Without that the objects rendered with SharpDX could be clipped because their size is not accounted in the near and far plane calculations.
            // By default OptimizeNearAndFarCameraPlanes is set to true because this greatly improves the resolution of the depth buffer and
            // therefore reduces the possibility of Z-fighting artifacts.
            dxScene.OptimizeNearAndFarCameraPlanes = false;
        }

        private void SharpDXRenderingAction(RenderingContext renderingContext)
        {
            // Just in case the data were already disposed
            if (_constantBuffer == null)
                return;


            var context = renderingContext.DXDevice.ImmediateContext;

            // Prepare All the stages
            context.InputAssembler.InputLayout = _layout;
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(_vertices, SharpDX.Utilities.SizeOf<Vector4>() * 2, 0));
            context.VertexShader.SetConstantBuffer(0, _constantBuffer);
            context.VertexShader.Set(_vertexShader);
            context.PixelShader.Set(_pixelShader);

            // NOTE:
            // Because DXEngine by default uses different culling mode than SharpDX,
            // we first need to set the culling mode to the one that is used in the ShardDX sample - CounterClockwise culling
            // The SharpDX way to do that is (we are using the already defined CullCounterClockwise rasterizer state):
            //context.Rasterizer.State = renderingContext.DXDevice.CommonStates.CullCounterClockwise;

            // But it is better to do that in the DXEngine's way with ImmediateContextStatesManager
            // That prevents too many state changes with checking the current state and also ensures that any DXEngine's objects that would be rendered after
            // SharpDX objects would be rendered correctly:
            renderingContext.DXDevice.ImmediateContextStatesManager.RasterizerState = renderingContext.DXDevice.CommonStates.CullCounterClockwise;


            // Clear views - this is done by DXEngine in PrepareRenderTargetsRenderingStep
            //context.ClearDepthStencilView(depthView, DepthStencilClearFlags.Depth, 1.0f, 0);
            //context.ClearRenderTargetView(renderView, Color.Black);


            // Original code - from the previous sample
            //// Prepare matrices
            //var view = Matrix.LookAtLH(new Vector3(0, 0, -5), new Vector3(0, 0, 0), Vector3.UnitY);

            //// Setup new projection matrix with correct aspect ratio
            //var proj = Matrix.PerspectiveFovLH((float)Math.PI / 4.0f, (float)renderingContext.Width / (float)renderingContext.Height, 0.1f, 100.0f);

            //var time = _clock.ElapsedMilliseconds / 1000.0f;

            //var viewProj = Matrix.Multiply(view, proj);

            //// Update WorldViewProj Matrix
            //var worldViewProj = Matrix.RotationX(time) * Matrix.RotationY(time * 2) * Matrix.RotationZ(time * .7f) * viewProj;
            //worldViewProj.Transpose();
            //context.UpdateSubresource(ref worldViewProj, _contantBuffer);


            var camera = renderingContext.UsedCamera;

            var leftRightHandedCoordinateSystem = camera as ILeftRightHandedCoordinateSystem;
            if (leftRightHandedCoordinateSystem != null)
                leftRightHandedCoordinateSystem.IsRightHandedCoordinateSystem = false;

            // We also need to force the update of the camera - this will recalculate the matrices
            camera.Update(forceMatrixUpdate: true);

            // Now we can get the viewProjection matrix from the DXViewportView's camera
            var viewProj = camera.GetViewProjection();

            // We will not use world matrix as in the previous example and will just set the viewProj as the worldViewProjection matrix

            // If the matrixes in the shaders are written in default format, then we need to transpose them.
            // To remove the need for transposal we can define the matrixes in HLSL as row_major (this is used in DXEngine's shaders, but here the original shader from SharpDX is preserved).
            viewProj.Transpose();

            // Write the new world view matrix to the constant buffer
            context.UpdateSubresource(ref viewProj, _constantBuffer);


            // Draw the cube
            context.Draw(36, 0);


            // Set IsRightHandedCoordinateSystem back to standard DXEngine's settings
            if (leftRightHandedCoordinateSystem != null)
                leftRightHandedCoordinateSystem.IsRightHandedCoordinateSystem = true;

            camera.Update(forceMatrixUpdate: true);



            // Present is called by DXEngine in CompleteRenderingStep
            //swapChain.Present(0, PresentFlags.None);
        }

        private void Dispose()
        {
            // Dispose all resources that were created here
            SharpDX.Utilities.Dispose(ref _constantBuffer);
            SharpDX.Utilities.Dispose(ref _vertices);
            SharpDX.Utilities.Dispose(ref _layout);
            SharpDX.Utilities.Dispose(ref _vertexShader);
            SharpDX.Utilities.Dispose(ref _pixelShader);

            MainDXViewportView.Dispose();
        }
    }
}
