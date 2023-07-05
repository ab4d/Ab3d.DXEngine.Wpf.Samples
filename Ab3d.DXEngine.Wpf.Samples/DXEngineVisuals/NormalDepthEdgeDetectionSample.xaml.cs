using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Ab3d.Common;
using Ab3d.DirectX;
using Ab3d.DirectX.Effects;
using Ab3d.DirectX.PostProcessing;
using Ab3d.Visuals;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    // This sample shows an advanced edge detection technique that uses differences in normal-depth texture to detect edges
    // (instead of differences in color that are used for edge detection in previous sample).
    //
    // The normal-depth texture is created by the NormalDepthEffect and renders the normal vector (perpendicular to the triangle)
    // and depth (distance) to the camera to a texture. Normal is rendered to RGB values and depth to the alpha channel.
    //
    // The NormalDepthEdgeDetectionPostProcessing checks if changes in normal and depth values are big enough (bigger than the threshold)
    // and in this case draws the edge. This works better then when edges are detected only dy checking the differences in colors.
    //
    // To check how depth defines edges without normals, set normal's threshold to disabled (max value).
    // Also set the depth threshold to disabled to see when normal changes generate an edge.

    /// <summary>
    /// Interaction logic for NormalDepthEdgeDetectionSample.xaml
    /// </summary>
    public partial class NormalDepthEdgeDetectionSample : Page
    {
        private BoxVisual3D _greenBox3D;
        private InstancedMeshGeometryVisual3D _instancedMeshGeometryVisual3D;

        private DisposeList _disposables;

        private Texture2D _savedBackBuffer;
        private Texture2DDescription _savedBackBufferDescription;
        private RenderTargetView _savedRenderTargetView;
        private DepthStencilView _savedDepthStencilView;
        private ViewportF _savedViewport;
        private int _savedSuperSamplingCount;

        private Texture2D _normalDepthBackBuffer;
        private Texture2DDescription _normalDepthBackBufferDescription;
        private RenderTargetView _normalDepthRenderTargetView;
        private ShaderResourceView _normalDepthShaderResourceView;
        private DepthStencilView _normalDepthDepthStencilView;

        private Texture2D _blurredOutlineBackBuffer;
        private RenderTargetView _blurredRenderTargetView;
        private ShaderResourceView _blurredShaderResourceView;



        private ViewportF _normalDepthBufferViewport;

        private NormalDepthEffect _normalDepthEffect;
        private RenderObjectsRenderingStep _renderNormalDepthRenderingStep;
        private RenderTextureRenderingStep _showNormalsTextureRenderingStep;
        private RenderTextureRenderingStep _showDepthTextureRenderingStep;
        private NormalDepthEdgeDetectionPostProcessing _normalDepthEdgeDetectionPostProcessing;

        public NormalDepthEdgeDetectionSample()
        {
            InitializeComponent();

            _disposables = new DisposeList();

            MainDXViewportView.DXSceneDeviceCreated += delegate(object sender, EventArgs args)
            {
                SetupNormalDepthRenderingStep();
                UpdateNormalDepthParameters();

                CreateScene();
            };

            // IMPORTANT:
            // It is very important to call Dispose method on DXSceneView after the control is not used any more (see help file for more info)
            this.Unloaded += (sender, args) =>
            {
                _disposables.Dispose();

                // Because we have created the PostProcess objects here, we also need to dispose them
                DisposeNormalDepthBackBuffers();

                // Now we can also dispose the MainMainDXViewportView
                MainDXViewportView.Dispose();
            };
        }


        private void CreateScene()
        {
            _greenBox3D = new BoxVisual3D();
            _greenBox3D.CenterPosition = new Point3D(0, -0.05, 0);
            _greenBox3D.Size = new Size3D(1000, 0.1, 1000);
            _greenBox3D.Material = new DiffuseMaterial(Brushes.Green);

            _greenBox3D.SetName("Green box");
            MainViewport.Children.Add(_greenBox3D);



            var readerObj = new Ab3d.ReaderObj();
            var dragonModel3D = readerObj.ReadModel3D(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\Models\teapot-hires.obj")) as GeometryModel3D;

            Ab3d.Utilities.ModelUtils.PositionAndScaleModel3D(dragonModel3D, new Point3D(100, 0, 200), PositionTypes.Bottom, new Size3D(300, 300, 300));

            MainViewport.Children.Add(dragonModel3D.CreateModelVisual3D());



            var sphereVisual3D = new SphereVisual3D();
            sphereVisual3D.CenterPosition = new Point3D(100, 40, -200);
            sphereVisual3D.Radius = 40;
            sphereVisual3D.Material = new DiffuseMaterial(Brushes.Blue);

            sphereVisual3D.SetName("Blue sphere");
            MainViewport.Children.Add(sphereVisual3D);



            var grayCylinder = new CylinderVisual3D();
            grayCylinder.BottomCenterPosition = new Point3D(200, 0, -200);
            grayCylinder.Radius = 20;
            grayCylinder.Height = 100;
            grayCylinder.Material = new DiffuseMaterial(Brushes.LightGray);

            grayCylinder.SetName("Gray Cylinder");
            MainViewport.Children.Add(grayCylinder);



            // Use InstancedMeshGeometryVisual3D to show boxes instead of BoxVisual3D

            var boxGeometry3D = new Ab3d.Meshes.BoxMesh3D(centerPosition: new Point3D(0, 0, 0), size: new Size3D(20, 60, 20), xSegments: 1, ySegments: 1, zSegments: 1).Geometry;

            var instancedData = new List<InstanceData>();

            for (int x = -300; x < 500; x += 100)
                instancedData.Add(new InstanceData(SharpDX.Matrix.Translation(x, 30, 0), Colors.Yellow.ToColor4()));

            _instancedMeshGeometryVisual3D = new InstancedMeshGeometryVisual3D(boxGeometry3D);
            _instancedMeshGeometryVisual3D.InstancesData = instancedData.ToArray();

            MainViewport.Children.Add(_instancedMeshGeometryVisual3D);


            // Disable automatic setting of near and far planes
            // and manually set the near and far plane distances so that the Depth texture is constant
            //if (MainDXViewportView.DXScene != null)
            //    MainDXViewportView.DXScene.OptimizeNearAndFarCameraPlanes = false;

            //Camera1.NearPlaneDistance = 250;
            //Camera1.FarPlaneDistance = 3000;
        }


        private void SetupNormalDepthRenderingStep()
        {
            _normalDepthEffect = new NormalDepthEffect();
            _normalDepthEffect.InitializeResources(MainDXViewportView.DXScene.DXDevice);

            _disposables.Add(_normalDepthEffect);

            _renderNormalDepthRenderingStep = new RenderObjectsRenderingStep("RenderNormalDepth", "Renders normal and depth for all visible objects");
            _renderNormalDepthRenderingStep.OverrideEffect = _normalDepthEffect;
            //_renderNormalDepthRenderingStep.OverrideDepthStencilState = MainDXViewportView.DXScene.DXDevice.CommonStates.DepthRead;

            // NormalDepthEffect does not support rendering Lines, so we need to skip rendering them.
            // If lines are rendered, the following is got:
            // D3D11 WARNING: ID3D11DeviceContext::Draw: Input vertex slot 0 has stride 12 which is less than the minimum stride logically expected from the current Input Layout (32 bytes). This is OK, as hardware is perfectly capable of reading overlapping data. However the developer probably did not intend to make use of this behavior.  [ EXECUTION WARNING #355: DEVICE_DRAW_VERTEX_BUFFER_STRIDE_TOO_SMALL]
            // D3D11 WARNING: ID3D11DeviceContext::Draw: Vertex Buffer at the input vertex slot 0 is not big enough for what the Draw * () call expects to traverse.This is OK, as reading off the end of the Buffer is defined to return 0.However the developer probably did not intend to make use of this behavior.  [EXECUTION WARNING #356: DEVICE_DRAW_VERTEX_BUFFER_TOO_SMALL]

            var lineGeometryRenderingQueue = MainDXViewportView.DXScene.LineGeometryRenderingQueue;
            _renderNormalDepthRenderingStep.FilterRenderingQueuesFunction = renderingQueue => renderingQueue != lineGeometryRenderingQueue;

            _disposables.Add(_renderNormalDepthRenderingStep);

            _renderNormalDepthRenderingStep.BeforeRunningStep += (object sender, DirectX.RenderingEventArgs args) =>
            {
                var renderingContext = args.RenderingContext;

                // Set new back buffer where we will render outline objects
                SetupNormalDepthBackBuffers(renderingContext);
            };

            _renderNormalDepthRenderingStep.AfterRunningStep += (object sender, DirectX.RenderingEventArgs args) =>
            {
                var renderingContext = args.RenderingContext;

                // Reset the saved back buffer
                RestoreBackBuffers(renderingContext);
            };


            _showNormalsTextureRenderingStep = new RenderTextureRenderingStep(RenderTextureRenderingStep.TextureChannelsCount.FourChannels, "Show DepthNormal texture")
            {
                Offsets = new Vector4(0.5f, 0.5f, 0.5f, 1), // show only normals
                Factors = new Vector4(0.5f, 0.5f, 0.5f, 1),
                TargetViewport = new ViewportF(0.02f, 0.68f, 0.3f, 0.3f), // render to lower left part of screen (values are relative to view size)
            };

            _showNormalsTextureRenderingStep.BeforeRunningStep += delegate (object sender, DirectX.RenderingEventArgs args)
            {
                var renderingContext = args.RenderingContext;

                renderingContext.SetBackBuffer(renderingContext.CurrentBackBuffer,
                                               renderingContext.CurrentBackBufferDescription,
                                               renderingContext.CurrentRenderTargetView,
                                               _normalDepthDepthStencilView,
                                               renderingContext.CurrentSupersamplingCount,
                                               false);
            };

            _showDepthTextureRenderingStep = new RenderTextureRenderingStep(RenderTextureRenderingStep.TextureChannelsCount.FourChannels, "Show DepthNormal texture")
            {
                Offsets = new Vector4(0, 0, 0, 0), // show only depth
                Factors = new Vector4(0, 0, 0, 1),
                TargetViewport = new ViewportF(0.34f, 0.68f, 0.3f, 0.3f), // render to lower left part of screen (values are relative to view size)
            };

            _showDepthTextureRenderingStep.BeforeRunningStep += delegate (object sender, DirectX.RenderingEventArgs args)
            {
                var renderingContext = args.RenderingContext;

                renderingContext.SetBackBuffer(renderingContext.CurrentBackBuffer,
                                               renderingContext.CurrentBackBufferDescription,
                                               renderingContext.CurrentRenderTargetView,
                                               _normalDepthDepthStencilView,
                                               renderingContext.CurrentSupersamplingCount,
                                               false);
            };

            MainDXViewportView.DXScene.RenderingSteps.AddBefore(MainDXViewportView.DXScene.DefaultRenderPostProcessingRenderingStepsGroup, _renderNormalDepthRenderingStep);
            MainDXViewportView.DXScene.RenderingSteps.AddBefore(MainDXViewportView.DXScene.DefaultCompleteRenderingStep, _showNormalsTextureRenderingStep);
            MainDXViewportView.DXScene.RenderingSteps.AddBefore(MainDXViewportView.DXScene.DefaultCompleteRenderingStep, _showDepthTextureRenderingStep);


            _normalDepthEdgeDetectionPostProcessing = new NormalDepthEdgeDetectionPostProcessing();
            MainDXViewportView.DXScene.PostProcesses.Add(_normalDepthEdgeDetectionPostProcessing);

            // To render the outlines with super-sampling (SSAA), then instead of adding the _addOutlineRenderingStep before DefaultCompleteRenderingStep,
            // add it before DefaultResolveBackBufferRenderingStep (comment the previous line and uncomment the next line).
            // This way the outlines will be rendered at super-sampled resolutions and and down-sampled.
            // You will also need to update the _outlineBackBufferDescription (see comment in the SetupOutlineBackBuffers method).
            // Note that it is not possible to render outlines with multi-sampling (MSAA) because there it is not possible to use texture sampler in pixel shader.
            //MainMainDXViewportView.DXScene.RenderingSteps.AddBefore(MainMainDXViewportView.DXScene.DefaultResolveBackBufferRenderingStep, _addOutlineRenderingStep);
        }

        private void SetupNormalDepthBackBuffers(RenderingContext renderingContext)
        {
            var dxDevice = renderingContext.DXDevice;

            // Save current back buffer
            _savedBackBuffer = renderingContext.CurrentBackBuffer;
            _savedBackBufferDescription = renderingContext.CurrentBackBufferDescription;
            _savedRenderTargetView = renderingContext.CurrentRenderTargetView;
            _savedDepthStencilView = renderingContext.CurrentDepthStencilView;
            _savedViewport = renderingContext.CurrentViewport;
            _savedSuperSamplingCount = renderingContext.CurrentSupersamplingCount;

            // Read size from FinalBackBufferDescription because CurrentBackBufferDescription can be super-sampled
            int normalDepthBackBufferWidth = renderingContext.FinalBackBufferDescription.Width;
            int normalDepthBackBufferHeight = renderingContext.FinalBackBufferDescription.Height;

            // If size is changed, we need to recreate the back buffer
            if (_normalDepthBackBuffer != null &&
                (normalDepthBackBufferWidth != _normalDepthBackBufferDescription.Width ||
                 normalDepthBackBufferHeight != _normalDepthBackBufferDescription.Height))
            {
                DisposeNormalDepthBackBuffers();
            }

            if (_normalDepthBackBuffer == null)
            {
                _normalDepthBackBufferDescription = dxDevice.CreateTexture2DDescription(normalDepthBackBufferWidth,
                                                                                        normalDepthBackBufferHeight,
                                                                                        new SampleDescription(1, 0),
                                                                                        isRenderTarget: true,
                                                                                        isSharedResource: false,
                                                                                        isStagingTexture: false,
                                                                                        isShaderResource: true,
                                                                                        //format: Format.R16G16B16A16_Float); // This use 16 bits for depth but requires double the bandwidth as B8G8R8A8_UNorm
                                                                                        format: Format.B8G8R8A8_UNorm);  // In this case we only have 8 bits for depth (we may improve that by using RA for depth - see DX11 book page 723)

                // Super-sampling note:
                // To render the outlines at super-sampling resolution, then use the size from CurrentBackBufferDescription and not from FinalBackBufferDescription:
                // (you will also need to update the position of _addOutlineRenderingStep at the end of the constructor).
                //_outlineBackBufferDescription.Width  = renderingContext.CurrentBackBufferDescription.Width;
                //_outlineBackBufferDescription.Height = renderingContext.CurrentBackBufferDescription.Height;

                _normalDepthBackBuffer = dxDevice.CreateTexture2D(_normalDepthBackBufferDescription);

                // Create RenderTargetView and DepthStencilView for LEFT eye - the scene will be rendered to that buffer
                _normalDepthRenderTargetView = new RenderTargetView(dxDevice.Device, _normalDepthBackBuffer);


                _normalDepthBufferViewport = new ViewportF(0, 0, _normalDepthBackBufferDescription.Width, _normalDepthBackBufferDescription.Height);


                var resourceViewDescription = new ShaderResourceViewDescription
                {
                    Format = _normalDepthBackBufferDescription.Format,
                    Dimension = ShaderResourceViewDimension.Texture2D,
                    Texture2D = new ShaderResourceViewDescription.Texture2DResource
                    {
                        MipLevels = 1,
                        MostDetailedMip = 0
                    }
                };

                _normalDepthShaderResourceView = new ShaderResourceView(dxDevice.Device, _normalDepthBackBuffer, resourceViewDescription);


                _showNormalsTextureRenderingStep.SourceTexture = _normalDepthShaderResourceView;
                _showDepthTextureRenderingStep.SourceTexture = _normalDepthShaderResourceView;

                _normalDepthEdgeDetectionPostProcessing.NormalDepthShaderResourceView = _normalDepthShaderResourceView;

                _normalDepthDepthStencilView = dxDevice.CreateDepthStencilView(_normalDepthBackBufferDescription.Width,
                                                                               _normalDepthBackBufferDescription.Height,
                                                                               _normalDepthBackBufferDescription.SampleDescription,
                                                                               Format.D32_Float,
                                                                               "NormalDepthDepthStencilBuffer");

                if (dxDevice.IsDebugDevice)
                {
                    _normalDepthBackBuffer.DebugName = "NormalDepthBackBuffer";
                    _normalDepthRenderTargetView.DebugName = "NormalDepthRenderTargetView";
                    _normalDepthShaderResourceView.DebugName = "NormalDepthShaderResourceView";
                }
            }

            renderingContext.SetBackBuffer(_normalDepthBackBuffer,
                                           _normalDepthBackBufferDescription,
                                           _normalDepthRenderTargetView,
                                           _normalDepthDepthStencilView,
                                           currentSupersamplingCount: 1,
                                           bindNewRenderTargetsToDeviceContext: true);

            renderingContext.CurrentViewport = _normalDepthBufferViewport;
            renderingContext.DeviceContext.Rasterizer.SetViewport(_normalDepthBufferViewport);

            renderingContext.DeviceContext.ClearRenderTargetView(_normalDepthRenderTargetView, new RawColor4(0, 0, 0, 0));
            renderingContext.DeviceContext.ClearDepthStencilView(_normalDepthDepthStencilView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);
        }

        private void RestoreBackBuffers(RenderingContext renderingContext)
        {
            renderingContext.SetBackBuffer(_savedBackBuffer,
                                           _savedBackBufferDescription,
                                           _savedRenderTargetView,
                                           _savedDepthStencilView,
                                           _savedSuperSamplingCount,
                                           bindNewRenderTargetsToDeviceContext: true);

            renderingContext.CurrentViewport = _savedViewport;

            _savedBackBuffer = null;
            _savedRenderTargetView = null;
            _savedDepthStencilView = null;
        }

        private void UpdateNormalDepthParameters()
        {
            if (_normalDepthEdgeDetectionPostProcessing == null)
                return;

            _normalDepthEdgeDetectionPostProcessing.NormalThreshold = new float[] { 0.01f, 0.02f, 0.05f, 0.1f, 0.2f, 0.3f, 0.5f, 1, float.MaxValue }[NormalThresholdComboBox.SelectedIndex];
            _normalDepthEdgeDetectionPostProcessing.DepthThreshold = new float[] { 0.001f, 0.002f, 0.005f, 0.0075f, 0.01f, 0.02f, 0.05f, 0.1f, float.MaxValue }[DepthThresholdComboBox.SelectedIndex];
            _normalDepthEdgeDetectionPostProcessing.Distance = new float[] { 0.1f, 0.2f, 0.5f, 0.75f, 1, 2, 3, 4, 5 }[DistanceComboBox.SelectedIndex];
        }

        private void NormalThresholdComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            UpdateNormalDepthParameters();
            MainDXViewportView.Refresh(); // Render the scene again
        }

        private void DepthThresholdComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            UpdateNormalDepthParameters();
            MainDXViewportView.Refresh(); // Render the scene again
        }

        private void DistanceComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            UpdateNormalDepthParameters();
            MainDXViewportView.Refresh(); // Render the scene again
        }

        private void MultiplyWithCurrentColorCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded || _normalDepthEdgeDetectionPostProcessing == null)
                return;

            _normalDepthEdgeDetectionPostProcessing.MultiplyWithCurrentColor = MultiplyWithCurrentColorCheckBox.IsChecked ?? false;
            
            MainDXViewportView.Refresh(); // Render the scene again
        }

        private void ShowNormalCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded || _showNormalsTextureRenderingStep == null)
                return;

            if (ShowNormalCheckBox.IsChecked ?? false)
                MainDXViewportView.DXScene.RenderingSteps.AddBefore(MainDXViewportView.DXScene.DefaultCompleteRenderingStep, _showNormalsTextureRenderingStep);
            else
                MainDXViewportView.DXScene.RenderingSteps.Remove(_showNormalsTextureRenderingStep);

            MainDXViewportView.Refresh(); // Render the scene again

        }
        
        private void ShowDepthCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded || _showDepthTextureRenderingStep == null)
                return;

            if (ShowDepthCheckBox.IsChecked ?? false)
                MainDXViewportView.DXScene.RenderingSteps.AddBefore(MainDXViewportView.DXScene.DefaultCompleteRenderingStep, _showDepthTextureRenderingStep);
            else
                MainDXViewportView.DXScene.RenderingSteps.Remove(_showDepthTextureRenderingStep);

            MainDXViewportView.Refresh(); // Render the scene again
        }


        private void DisposeNormalDepthBackBuffers()
        {
            DisposeHelper.DisposeAndNullify(ref _normalDepthBackBuffer);
            DisposeHelper.DisposeAndNullify(ref _normalDepthRenderTargetView);
            DisposeHelper.DisposeAndNullify(ref _normalDepthDepthStencilView);
            DisposeHelper.DisposeAndNullify(ref _normalDepthShaderResourceView);

            DisposeHelper.DisposeAndNullify(ref _blurredOutlineBackBuffer);
            DisposeHelper.DisposeAndNullify(ref _blurredRenderTargetView);
            DisposeHelper.DisposeAndNullify(ref _blurredShaderResourceView);

            // Reset structs
            _normalDepthBackBufferDescription = new Texture2DDescription();
        }
    }
}
