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
using Ab3d.Visuals;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Buffer = SharpDX.Direct3D11.Buffer;
using Matrix = SharpDX.Matrix;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineAdvanced
{
    // This sample is the forth in the series on how to render custom SharpDX objects inside DXViewportView object.
    //
    // In this sample we are using a CustomRenderableNode SceneNode objects to execute our custom SharpDX rendering code.
    // This way we do not need to provide custom RenderingStep as it is done in the previous sample.
    //
    // This also provides easier integration into the rendering process.
    // Also, because the CustomRenderableNode provides Bounds of the custom rendered object,
    // the near and far plane calculation can be turned on (and not disabled as in the previous sample).

    /// <summary>
    /// Interaction logic for CustomRenderingStep4.xaml
    /// </summary>
    public partial class CustomRenderingStep4 : Page
    {
        private VertexShader _vertexShader;
        private PixelShader _pixelShader;
        private InputLayout _layout;
        private Buffer _constantBuffer;
        private Buffer _vertices;
        private VertexBufferBinding _vertexBufferBinding;

        private Matrix _viewProjectionMatrix;
        private int _viewProjectionMatrixFrameNumber;
        

        public CustomRenderingStep4()
        {
            InitializeComponent();


            // Instead of creating new RenderingStep as in the previous sample,
            // we will use CustomRenderableNode instead.
            // This type of SceneNode object allows specifying custom rendering action 
            // that is called to render the object.

            var bounds = CustomRenderingStep1.GetSharpDXBoxBounds(); // CustomRenderableNode also requires bounds so that the camera near and far calculations can account the custom data.
            var customRenderableNode = new CustomRenderableNode(CustomRenderAction, bounds);

            // To add CustomRenderableNode to the 3D scene, we need to embed it into a SceneNodeVisual3D
            var sceneNodeVisual3D = new SceneNodeVisual3D(customRenderableNode);
            MainViewport.Children.Add(sceneNodeVisual3D);



            MainDXViewportView.DXSceneInitialized += delegate (object sender, EventArgs args)
            {
                if (MainDXViewportView.DXScene == null) // When DXEngine falls back to WPF 3D rendering, the DXScene is null; we could also check for MainDXViewportView.UsedGraphicsProfile.DriverType != GraphicsProfile.DriverTypes.Wpf3D
                    return;

                InitializeSharpDXRendering(MainDXViewportView.DXScene);
            };

            this.Unloaded += delegate { Dispose(); };
        }

        // This method is called on each rendering step.
        private void CustomRenderAction(RenderingContext renderingContext, CustomRenderableNode customRenderableNode, object originalObject)
        {
            // Just in case the data were already disposed
            if (_constantBuffer == null)
                return;

            // If we have not yet read the _viewProjectionMatrix in this frame, do this now
            if (renderingContext.FrameNumber != _viewProjectionMatrixFrameNumber)
                UpdateViewProjectionMatrix(renderingContext);


            var context = renderingContext.DXDevice.ImmediateContext;
            var statesManager = renderingContext.ContextStatesManager;


            // Write the new world view projection matrix to the constant buffer
            context.UpdateSubresource(ref _viewProjectionMatrix, _constantBuffer);

            // Prepare all the stages
            // It is possible to do that directly on the DirectX ImmediateContext:

            //context.InputAssembler.InputLayout = _layout;
            //context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            //context.InputAssembler.SetVertexBuffers(0, _vertexBufferBinding);
            //context.VertexShader.SetConstantBuffer(0, _contantBuffer);
            //context.VertexShader.Set(_vertexShader);
            //context.PixelShader.Set(_pixelShader);

            // But when custom rendering is mixed with DXEngine rendering,
            // It is highly recommended to use DXEngine's ContextStatesManager.
            // This provides caching of existing states and prevent unneeded state changes.
            // What is more, if we change the state directly, then the ContextStatesManager will
            // not be aware of the change and will still "think" that the previous state is currently set.
            // This way the DXEngine might be rendered with state set by SharpDX code.
            //
            // If you will use ImmediateContext directly, then you need to call Reset method on ContextStatesManager:
            //statesManager.Reset(ContextStatesManager.ResetType.All);

            
            statesManager.InputLayout = _layout;
            statesManager.PrimitiveTopology = PrimitiveTopology.TriangleList;
            statesManager.RasterizerState = renderingContext.DXDevice.CommonStates.CullCounterClockwise;

            statesManager.SetVertexBuffer(0, _vertexBufferBinding.Buffer, _vertexBufferBinding.Stride, _vertexBufferBinding.Offset);
            statesManager.SetVertexShaderConstantBuffer(_constantBuffer, 0);

            statesManager.SetVertexShader(_vertexShader);
            statesManager.SetPixelShader(_pixelShader);


            // Draw the cube
            context.Draw(36, 0);
        }

        private void UpdateViewProjectionMatrix(RenderingContext renderingContext)
        {
            var camera = renderingContext.UsedCamera;

            var leftRightHandedCoordinateSystem = camera as ILeftRightHandedCoordinateSystem;
            if (leftRightHandedCoordinateSystem != null)
                leftRightHandedCoordinateSystem.IsRightHandedCoordinateSystem = false;

            // We also need to force the update of the camera - this will recalculate the matrices
            camera.Update(forceMatrixUpdate: true);


            // Now we can get the viewProjection matrix from the DXViewportView's camera
            _viewProjectionMatrix = camera.GetViewProjection();

            // If the matrices in the shaders are written in default format, then we need to transpose them.
            // To remove the need for transpose we can define the matrices in HLSL as row_major (this is used in DXEngine's shaders, but here the original shader from SharpDX is preserved).
            _viewProjectionMatrix.Transpose();


            // Set IsRightHandedCoordinateSystem back to standard DXEngine's settings
            if (leftRightHandedCoordinateSystem != null)
                leftRightHandedCoordinateSystem.IsRightHandedCoordinateSystem = true;

            camera.Update(forceMatrixUpdate: true);

            // Save frame number so we will not update _viewProjectionMatrix in this frame any more
            _viewProjectionMatrixFrameNumber = renderingContext.FrameNumber;
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

            _layout = new InputLayout(device, vertexShaderByteCode, new[]
            {
                new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0, 0),
                new InputElement("COLOR", 0, Format.R32G32B32A32_Float, 16, 0)
            });


            // Instantiate Vertex buffer from vertex data
            var vertexBuffer = CustomRenderingStep1.GetSharpDXBoxVertexBuffer();
            _vertices = Buffer.Create(device, BindFlags.VertexBuffer, vertexBuffer);

            _vertexBufferBinding = new VertexBufferBinding(_vertices, SharpDX.Utilities.SizeOf<Vector4>() * 2, 0);

            // Create Constant Buffer
            _constantBuffer = new Buffer(device, SharpDX.Utilities.SizeOf<Matrix>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
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
