using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ab3d.DirectX;

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
    // This sample shows how to integrate existing SharpDX code into Ab3d.DXEngine
    // The SharpDX code is taken from its sample:
    // https://github.com/sharpdx/SharpDX-Samples/blob/master/Desktop/Direct3D11/MiniCube/Program.cs
    //
    // To add custom SharpDX rendering code to the Ab3d.DXEngine, you need to insert that code into existing rendering pipeline.
    // The rendering pipeline is defined in DXScene as a series of RenderingSteps (dxScene.RenderingSteps property).
    // By default the rendering steps are:
    // 1) InitializeRenderingStep - InitializeRendering is the first rendering step. It sets up the RenderingContext with current RenderTargets, resets statistics, etc.
    // 2) PrepareRenderTargetsRenderingStep - PrepareRenderTargets sets rendering targets and clears them and sets Viewport.
    // 3) RenderObjectsRenderingStep - Default RenderObjects renders the objects with their default effect and material.
    // 4) ResolveBackBufferRenderingStep - Resolve multi-sampled back buffer (MSAABackBuffer) into back buffer.
    // 5) PreparePostProcessingRenderingStep - prepares the buffers for post-processing. When no post-processing effects are used, this and the next steps are not present in the RenderingSteps collection.
    // 6) RenderPostProcessingRenderingStep - renders the post processing effects.
    // 7) CompleteRenderingStep - CompleteRendering is the last rendering step. It Presents SwapChain (if used) or prepares the output buffer that can be send to WPF or CPU memory.
    //
    // You can add your own rendering step between already defined rendering steps.
    // This can be done with the following steps:
    // 1) Define your own rendering step
    //    This can be done with derived a class from Ab3d.DirectX.RenderingStepBase and implement OnRun method.
    //    Another simpler option is to create a new instance of Ab3d.DirectX.CustomActionRenderingStep class and set its CustomAction delegate to your method that will execute your code (this is also used in this sample).
    // 
    // 2) Insert your rendering step into the existing rendering steps
    //    This is done with calling AddAfter or AddBefore methods.
    //    Both this methods take an existing rendering step as first parameter. To get existing rendering steps, you can use the properties defined in DXScene -
    //    the names of properties start with Default and then the name of rendering step class - for example:
    //
    //    dxScene.RenderingSteps.AddAfter(dxScene.DefaultPrepareRenderTargetsRenderingStep, newRenderingStep);
    //
    // 3) Execute your code based on the RenderingContext
    //    OnRun or CustomAction are called with RenderingContext that provide various properties about current rendering process - for example
    //    Current DirectX device, device context, render target, back buffer, viewport, frame number, etc.
    //    Many other scene related properties can be get from the DXScene - for example the current camera and lights.

    // This is the first sample in the customization series
    // The next sample will show how to use the camera provided by the DXViewportView.

    /// <summary>
    /// Interaction logic for CustomRenderingStep1.xaml
    /// </summary>
    public partial class CustomRenderingStep1 : Page
    {
        private VertexShader _vertexShader;
        private PixelShader _pixelShader;
        private InputLayout _layout;
        private Buffer _constantBuffer;
        private Buffer _vertices;
        private Stopwatch _clock;

        public CustomRenderingStep1()
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

            this.Unloaded += delegate
            {
                CompositionTarget.Rendering -= OnCompositionTargetOnRendering;
                Dispose();
            };
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
            var vertexBuffer = GetSharpDXBoxVertexBuffer();
            _vertices = Buffer.Create(device, BindFlags.VertexBuffer, vertexBuffer);

            // Create Constant Buffer
#if SHARPDX
            _constantBuffer = new Buffer(device, SharpDX.Utilities.SizeOf<Matrix>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
#else
            _constantBuffer = new Buffer(device, System.Runtime.CompilerServices.Unsafe.SizeOf<Matrix>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
#endif

            // Use clock
            _clock = new Stopwatch();
            _clock.Start();
        }

        private void SharpDXRenderingAction(RenderingContext renderingContext)
        {
            // Just in case the data were already disposed
            if (_constantBuffer == null)
                return;


            var context = renderingContext.DXDevice.ImmediateContext;

            // NOTE:
            // Because DXEngine by default uses different culling mode than SharpDX,
            // we first need to set the culling mode to the one that is used in the ShardDX sample - CounterClockwise culling
            // The SharpDX way to do that is (we are using the already defined CullCounterClockwise rasterizer state):
            //context.Rasterizer.State = renderingContext.DXDevice.CommonStates.CullCounterClockwise;

            // But it is better to do that in the DXEngine's way with ImmediateContextStatesManager
            // That prevents to many state changes with checking the current state and also ensures that any DXEngine's objects that would be rendered after
            // SharpDX objects would be rendered correctly:
            renderingContext.DXDevice.ImmediateContextStatesManager.RasterizerState = renderingContext.DXDevice.CommonStates.CullCounterClockwise;


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


            // Clear views - this is done by DXEngine in PrepareRenderTargetsRenderingStep
            //context.ClearDepthStencilView(depthView, DepthStencilClearFlags.Depth, 1.0f, 0);
            //context.ClearRenderTargetView(renderView, Color.Black);


            // Prepare matrices
            var view = Matrix.LookAtLH(new Vector3(0, 0, -5), new Vector3(0, 0, 0), Vector3.UnitY);

            // Setup new projection matrix with correct aspect ratio
            var proj = Matrix.PerspectiveFovLH((float)Math.PI / 4.0f, (float)renderingContext.DXScene.Width / (float)renderingContext.DXScene.Height, 0.1f, 100.0f);

            var viewProj = Matrix.Multiply(view, proj);


            var time = _clock.ElapsedMilliseconds / 1000.0f;

            // Update WorldViewProj Matrix
            var worldViewProj = Matrix.RotationX(time) * Matrix.RotationY(time * 2) * Matrix.RotationZ(time * .7f) * viewProj;

            // If the matrixes in the shaders are written in default format, then we need to transpose them.
            // To remove the need for transposal we can define the matrixes in HLSL as row_major (this is used in DXEngine's shaders, but here the original shader from SharpDX is preserved).
            worldViewProj.Transpose();

            // Write the new world view projection matrix to the constant buffer
            context.UpdateSubresource(ref worldViewProj, _constantBuffer);

            // Draw the cube
            context.Draw(36, 0);

            // Present is called by DXEngine in CompleteRenderingStep
            //swapChain.Present(0, PresentFlags.None);
        }


        public static Vector4[] GetSharpDXBoxVertexBuffer()
        {
            return new[]
            {
                // POSITION as Vector4                  // COLOR as Vector4
                new Vector4(-1.0f, -1.0f, -1.0f, 1.0f), new Vector4(1.0f, 0.0f, 0.0f, 1.0f), // Front
                new Vector4(-1.0f,  1.0f, -1.0f, 1.0f), new Vector4(1.0f, 0.0f, 0.0f, 1.0f),
                new Vector4( 1.0f,  1.0f, -1.0f, 1.0f), new Vector4(1.0f, 0.0f, 0.0f, 1.0f),
                new Vector4(-1.0f, -1.0f, -1.0f, 1.0f), new Vector4(1.0f, 0.0f, 0.0f, 1.0f),
                new Vector4( 1.0f,  1.0f, -1.0f, 1.0f), new Vector4(1.0f, 0.0f, 0.0f, 1.0f),
                new Vector4( 1.0f, -1.0f, -1.0f, 1.0f), new Vector4(1.0f, 0.0f, 0.0f, 1.0f),

                new Vector4(-1.0f, -1.0f,  1.0f, 1.0f), new Vector4(0.0f, 1.0f, 0.0f, 1.0f), // BACK
                new Vector4( 1.0f,  1.0f,  1.0f, 1.0f), new Vector4(0.0f, 1.0f, 0.0f, 1.0f),
                new Vector4(-1.0f,  1.0f,  1.0f, 1.0f), new Vector4(0.0f, 1.0f, 0.0f, 1.0f),
                new Vector4(-1.0f, -1.0f,  1.0f, 1.0f), new Vector4(0.0f, 1.0f, 0.0f, 1.0f),
                new Vector4( 1.0f, -1.0f,  1.0f, 1.0f), new Vector4(0.0f, 1.0f, 0.0f, 1.0f),
                new Vector4( 1.0f,  1.0f,  1.0f, 1.0f), new Vector4(0.0f, 1.0f, 0.0f, 1.0f),

                new Vector4(-1.0f, 1.0f, -1.0f,  1.0f), new Vector4(0.0f, 0.0f, 1.0f, 1.0f), // Top
                new Vector4(-1.0f, 1.0f,  1.0f,  1.0f), new Vector4(0.0f, 0.0f, 1.0f, 1.0f),
                new Vector4( 1.0f, 1.0f,  1.0f,  1.0f), new Vector4(0.0f, 0.0f, 1.0f, 1.0f),
                new Vector4(-1.0f, 1.0f, -1.0f,  1.0f), new Vector4(0.0f, 0.0f, 1.0f, 1.0f),
                new Vector4( 1.0f, 1.0f,  1.0f,  1.0f), new Vector4(0.0f, 0.0f, 1.0f, 1.0f),
                new Vector4( 1.0f, 1.0f, -1.0f,  1.0f), new Vector4(0.0f, 0.0f, 1.0f, 1.0f),

                new Vector4(-1.0f,-1.0f, -1.0f,  1.0f), new Vector4(1.0f, 1.0f, 0.0f, 1.0f), // Bottom
                new Vector4( 1.0f,-1.0f,  1.0f,  1.0f), new Vector4(1.0f, 1.0f, 0.0f, 1.0f),
                new Vector4(-1.0f,-1.0f,  1.0f,  1.0f), new Vector4(1.0f, 1.0f, 0.0f, 1.0f),
                new Vector4(-1.0f,-1.0f, -1.0f,  1.0f), new Vector4(1.0f, 1.0f, 0.0f, 1.0f),
                new Vector4( 1.0f,-1.0f, -1.0f,  1.0f), new Vector4(1.0f, 1.0f, 0.0f, 1.0f),
                new Vector4( 1.0f,-1.0f,  1.0f,  1.0f), new Vector4(1.0f, 1.0f, 0.0f, 1.0f),

                new Vector4(-1.0f, -1.0f, -1.0f, 1.0f), new Vector4(1.0f, 0.0f, 1.0f, 1.0f), // Left
                new Vector4(-1.0f, -1.0f,  1.0f, 1.0f), new Vector4(1.0f, 0.0f, 1.0f, 1.0f),
                new Vector4(-1.0f,  1.0f,  1.0f, 1.0f), new Vector4(1.0f, 0.0f, 1.0f, 1.0f),
                new Vector4(-1.0f, -1.0f, -1.0f, 1.0f), new Vector4(1.0f, 0.0f, 1.0f, 1.0f),
                new Vector4(-1.0f,  1.0f,  1.0f, 1.0f), new Vector4(1.0f, 0.0f, 1.0f, 1.0f),
                new Vector4(-1.0f,  1.0f, -1.0f, 1.0f), new Vector4(1.0f, 0.0f, 1.0f, 1.0f),

                new Vector4( 1.0f, -1.0f, -1.0f, 1.0f), new Vector4(0.0f, 1.0f, 1.0f, 1.0f), // Right
                new Vector4( 1.0f,  1.0f,  1.0f, 1.0f), new Vector4(0.0f, 1.0f, 1.0f, 1.0f),
                new Vector4( 1.0f, -1.0f,  1.0f, 1.0f), new Vector4(0.0f, 1.0f, 1.0f, 1.0f),
                new Vector4( 1.0f, -1.0f, -1.0f, 1.0f), new Vector4(0.0f, 1.0f, 1.0f, 1.0f),
                new Vector4( 1.0f,  1.0f, -1.0f, 1.0f), new Vector4(0.0f, 1.0f, 1.0f, 1.0f),
                new Vector4( 1.0f,  1.0f,  1.0f, 1.0f), new Vector4(0.0f, 1.0f, 1.0f, 1.0f),
            };
        }

        public static Bounds GetSharpDXBoxBounds()
        {
            var vertexBuffer = GetSharpDXBoxVertexBuffer();

            var bounds = new Bounds();
            for (var i = 0; i < vertexBuffer.Length; i += 2)
                bounds.Add(new Vector3(vertexBuffer[i].X, vertexBuffer[i].Y, vertexBuffer[i].Z));

            return bounds;
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

            if (_clock != null)
                _clock.Stop();

            MainDXViewportView.Dispose();
        }

        private void AnimationButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_clock.IsRunning)
            {
                AnimationButton.Content = "Start animation";
                _clock.Stop();
            }
            else
            {
                AnimationButton.Content = "Stop animation";
                _clock.Start();
            }
        }
    }
}