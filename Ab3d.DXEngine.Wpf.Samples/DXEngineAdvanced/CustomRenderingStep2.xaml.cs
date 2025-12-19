using System;
using System.Windows.Controls;
using Ab3d.DirectX;
using Ab3d.DirectX.Cameras;

#if SHARPDX
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Buffer = SharpDX.Direct3D11.Buffer;
using Matrix = SharpDX.Matrix;
#endif

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineAdvanced
{
    // This is the second sample that shows how to use custom SharpDX code to render 3D objects in DXViewportView.
    //
    // This sample shows how to use camera provided by DXViewportView to show SharpDX objects.
    // The camera on the DXViewportView is controlled by MouseCameraController from Ab3d.PowerToys library.
    //
    // For the 3D scene (and for the GPU) the camera is needed because it provides view and projection matrices.
    // In the previous sample both view and projection matrices were fixed and then a simple time based rotation was applied to the model to rotate it.
    //
    // In this sample we will use the view and projection matrices from the DXViewportView camera.
    // The camera can be get from DXScene object: renderingContext.DXScene.Camera
    // When we have the camera we can call the GetViewProjection method to get the combined viewProjection matrix.
    //
    // But there are a few tricks that need to be done to make the code work correctly.
    //
    // 1) The cameras in DXEngine by default use right handed coordinate system (used in WPF 3D, OpenGL), but the SharpDX code used left handed coordinate system (standard in DirectX).
    //    This means that the view and projection matrices are calculated differently. 
    //    Luckily DXEngine provides a solution to this with allowing to specify using left handed coordinate system. This is done with ILeftRightHandedCoordinateSystem interface: 
    // 
    //    var leftRightHandedCoordinateSystem = camera as ILeftRightHandedCoordinateSystem;
    //    if (leftRightHandedCoordinateSystem != null && leftRightHandedCoordinateSystem.IsRightHandedCoordinateSystem)
    //    {
    //        leftRightHandedCoordinateSystem.IsRightHandedCoordinateSystem = false;

    //        // We also need to force the update of the camera - this will recalculate the matrices
    //        camera.Update(forceMatrixUpdate: true);
    //    }
    //
    //
    // 2) Another difference between DXEngine's rendering and standard SharpDX rendering is in the culling mode.
    //    By default DXEngine is using clockwise culling (rejects triangles that are oriented in clockwise direction), but SharpDX is using counter clockwise culling.
    //    It is easy to change the culling mode with using the CommonStates and ImmediateContextStatesManager:
    //    
    //    renderingContext.DXDevice.ImmediateContextStatesManager.RasterizerState = renderingContext.DXDevice.CommonStates.CullCounterClockwise;
    // 
    // After those two adjustments we can use the camera provided by DXViewportView.
    //
    // The next sample shows how to render some WPF 3D objects and add custom SharpDX objects to the same scene.



    /// <summary>
    /// Interaction logic for CustomRenderingStep2.xaml
    /// </summary>
    public partial class CustomRenderingStep2 : Page
    {
        private VertexShader _vertexShader;
        private PixelShader _pixelShader;
        private InputLayout _layout;
        private Buffer _constantBuffer;
        private Buffer _vertices;

        public CustomRenderingStep2()
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
#if SHARPDX
            _constantBuffer = new Buffer(device, SharpDX.Utilities.SizeOf<Matrix>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
#else
            _constantBuffer = new Buffer(device, System.Runtime.CompilerServices.Unsafe.SizeOf<Matrix>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
#endif
        }

        private void SharpDXRenderingAction(RenderingContext renderingContext)
        {
            // Just in case the data were already disposed
            if (_constantBuffer == null)
                return;


            var context = renderingContext.DXDevice.ImmediateContext;

            // Prepare All the stages
#if SHARPDX
            var vertexBufferBinding = new VertexBufferBinding(_vertices, SharpDX.Utilities.SizeOf<Vector4>() * 2, 0);
#else
            var vertexBufferBinding = new VertexBufferBinding(_vertices, System.Runtime.CompilerServices.Unsafe.SizeOf<Vector4>() * 2, 0);
#endif

            context.InputAssembler.InputLayout = _layout;
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            context.InputAssembler.SetVertexBuffers(0, vertexBufferBinding);
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

            // Change the default right handed coordinate system into left handed coordinate (see noted in the beginning of this sample)
            var leftRightHandedCoordinateSystem = camera as ILeftRightHandedCoordinateSystem;
            if (leftRightHandedCoordinateSystem != null && leftRightHandedCoordinateSystem.IsRightHandedCoordinateSystem)
            {
                leftRightHandedCoordinateSystem.IsRightHandedCoordinateSystem = false;

                // We also need to force the update of the camera - this will recalculate the matrices
                camera.Update(forceMatrixUpdate: true);
            }

            // Now we can get the viewProjection matrix from the DXViewportView's camera
            var viewProj = camera.GetViewProjection();

            // We will not use world matrix as in the previous example and will just set the viewProj as the worldViewProjection matrix

            // If the matrixes in the shaders are written in default format, then we need to transpose them.
            // To remove the need for transposal we can define the matrixes in HLSL as row_major (this is used in DXEngine's shaders, but here the original shader from SharpDX is preserved).
            viewProj.Transpose();

            // Write the new world view projection matrix to the constant buffer
            context.UpdateSubresource(ref viewProj, _constantBuffer);

            // Draw the cube
            context.Draw(36, 0);

            // Present is called by DXEngine in CompleteRenderingStep
            //swapChain.Present(0, PresentFlags.None);
        }

        private void Dispose()
        {
            // Dispose all resources that were created here
            if (_constantBuffer != null)
            {
                _constantBuffer.Dispose();
                _constantBuffer = null;
            }
            
            if (_vertices != null)
            {
                _vertices.Dispose();
                _vertices = null;
            }
            
            if (_layout != null)
            {
                _layout.Dispose();
                _layout = null;
            }
            
            if (_vertexShader != null)
            {
                _vertexShader.Dispose();
                _vertexShader = null;
            }
            
            if (_pixelShader != null)
            {
                _pixelShader.Dispose();
                _pixelShader = null;
            }

            MainDXViewportView.Dispose();
        }
    }
}