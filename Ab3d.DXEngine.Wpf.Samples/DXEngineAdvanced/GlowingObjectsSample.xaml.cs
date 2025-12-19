using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Ab3d.DirectX;
using Ab3d.DirectX.Effects;
using Ab3d.DirectX.PostProcessing;
using Ab3d.Meshes;
using Ab3d.Utilities;
using Ab3d.Visuals;

#if SHARPDX
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
#endif

// This sample shows how to render glowing objects to a different texture, expand and blur it and the use additive blending to add that to the 3D scene.
// NOTE that this is an approximation of the glow and cannot simulate that glowing object actually emits light and illuminates other objects.
// Therefore, there are some artifacts, especially if you rotate the camera around so that the background object is in front of the glowing object.
// To correctly render glowing object, ray tracing is required.
//
// Rendering glowing objects is enabled by using the ObjectsGlowProvider class that is defined in the second part of this file!

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineAdvanced
{
    /// <summary>
    /// Interaction logic for GlowingObjectsSample.xaml
    /// </summary>
    public partial class GlowingObjectsSample : Page
    {
        private const double TextFlatteningTolerance = 0.001;

        private ObjectsGlowProvider _objectsGlowProvider;
        private BoxVisual3D _backgroundBoxVisual3D;

        public GlowingObjectsSample()
        {
            InitializeComponent();

            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
                return;

            _objectsGlowProvider = new ObjectsGlowProvider();

            MainDXViewportView.DXSceneInitialized += (sender, args) =>
            {
                if (MainDXViewportView.DXScene == null) // When using WPF 3D rendering
                    return;

                _objectsGlowProvider.InitializeResources(MainDXViewportView.DXScene);

                CreateSceneObjects();
            };

            MainDXViewportView.SceneRendered += (sender, args) =>
            {
                UpdateGlowBackBufferSize();
            };

            this.Unloaded += (sender, args) =>
            {
                if (_objectsGlowProvider != null)
                {
                    _objectsGlowProvider.Dispose();
                    _objectsGlowProvider = null;
                }
            };
        }

        private void UpdateGlowBackBufferSize()
        {
            if (_objectsGlowProvider == null || !_objectsGlowProvider.IsEnabled)
                GlowTextureSizeTextBlock.Text = "";
            else
                GlowTextureSizeTextBlock.Text = string.Format("{0} x {1}", _objectsGlowProvider.GlowBackBufferWidth, _objectsGlowProvider.GlowBackBufferHeight);
        }


        private void CreateSceneObjects()
        {
            // Add RGB chars to the scene
            // Note that the glowing part of the objects are added to the _objectsGlowProvider.GlowObjectsRenderingQueue (see ShowTriangulated3DMesh)
            AddText3D("R", new System.Windows.Point(-115, -50), Colors.Red);
            AddText3D("G", new System.Windows.Point(-40, -50),  Colors.Green);
            AddText3D("B", new System.Windows.Point(40, -50),   Colors.Blue);


            // To add background objects that do not overwrite the glow,
            // we need to add them to the BackgroundRenderingQueue so that they are rendered before the glow is added to the scene.
            // This is done by using the SetDXAttribute - see below:

            _backgroundBoxVisual3D = new BoxVisual3D()
            {
                CenterPosition = new Point3D(0, 0, -100),
                Size = new Size3D(400, 100, 10),
                Material = new DiffuseMaterial(Brushes.Gray),
            };

            _backgroundBoxVisual3D.SetDXAttribute(DXAttributeType.CustomRenderingQueue, MainDXViewportView.DXScene.BackgroundRenderingQueue);

            MainViewport.Children.Add(_backgroundBoxVisual3D);
        }


        #region Get 3D text
        private void AddText3D(string text, System.Windows.Point position, System.Windows.Media.Color color)
        {
            var rgbTextPolygons = CreateTextPolygons(text, fontSize: 100, position: position);

            var triangulator = new Triangulator(rgbTextPolygons, isYAxisUp: false);

            List<int> triangleIndices;
            PointCollection triangulatedPositions;
            triangulator.Triangulate(out triangulatedPositions, out triangleIndices);

            ShowTriangulated3DMesh(triangulatedPositions,
                                   triangleIndices,
                                   rgbTextPolygons,
                                   color,
                                   extrudeDistance: 20,
                                   isYAxisUp: triangulator.IsYAxisUp,
                                   flipNormals: triangulator.IsPolygonPositionsOrderReversed);
        }

        private void ShowTriangulated3DMesh(PointCollection triangulatedPositions, List<int> triangulatedIndices, PointCollection[] allPolygons, System.Windows.Media.Color color, double extrudeDistance, bool isYAxisUp, bool flipNormals)
        {
            var meshGeometry3D = Ab3d.Meshes.Mesh3DFactory.CreateExtrudedMeshGeometry(triangulatedPositions,
                                                                                      triangulatedIndices,
                                                                                      allPolygons,
                                                                                      isSmooth: false,
                                                                                      isYAxisUp: isYAxisUp,
                                                                                      flipNormals: flipNormals,
                                                                                      modelOffset: new Vector3D(0, 0, 0),
                                                                                      extrudeVector: new Vector3D(0, 0, extrudeDistance),
                                                                                      meshXVector: new Vector3D(1, 0, 0), 
                                                                                      meshYVector: new Vector3D(0, 1, 0), 
                                                                                      textureCoordinatesGenerationType: ExtrudeTextureCoordinatesGenerationType.None);

            var emissiveMaterial = new MaterialGroup();
            emissiveMaterial.Children.Add(new DiffuseMaterial(Brushes.Black));
            emissiveMaterial.Children.Add(new EmissiveMaterial(new SolidColorBrush(color)));

            var glowModel = new GeometryModel3D(meshGeometry3D, emissiveMaterial);

            // IMPORTANT:
            // To render object's glow, we need to add the object to the GlowObjectsRenderingQueue (objects in this rendering queue will get glow effect)
            glowModel.SetDXAttribute(DXAttributeType.CustomRenderingQueue, _objectsGlowProvider.GlowObjectsRenderingQueue);

            MainViewport.Children.Add(glowModel.CreateModelVisual3D());


            var topModel = new GeometryModel3D(meshGeometry3D, new DiffuseMaterial(Brushes.Silver));
            topModel.Transform = new TranslateTransform3D(0, 0, extrudeDistance);
            MainViewport.Children.Add(topModel.CreateModelVisual3D());
        }


        // The following code is from the Objects3D/TriangulatorWithHolesSample.xaml.cs file from the Ab3d.PowerToys.Samples samples project:

        private PointCollection[] CreateTextPolygons(string text, double fontSize, System.Windows.Point position)
        {
            var formattedText = new FormattedText(text,
                                                  CultureInfo.CurrentCulture,
                                                  FlowDirection.LeftToRight,
                                                  new Typeface("Arial Black"),
                                                  pixelsPerDip: 1,
                                                  foreground: Brushes.Black,
                                                  emSize: fontSize);

            var wpfGeometry = formattedText.BuildGeometry(position);

            var allPolygons = GetAllPolygonsFromGeometry(wpfGeometry);

            return allPolygons;
        }
        
        private PointCollection[] GetAllPolygonsFromGeometry(Geometry wpfGeometry)
        {
            //var comboBoxItem = (ComboBoxItem)FlatteningToleranceComboBox.SelectedItem;
            //double flatteningTolerance = double.Parse((string)comboBoxItem.Content, NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);

            var flattenedPathGeometry = wpfGeometry.GetFlattenedPathGeometry(TextFlatteningTolerance, ToleranceType.Relative);

            var allPolygons = new PointCollection[flattenedPathGeometry.Figures.Count];

            for (var i = 0; i < flattenedPathGeometry.Figures.Count; i++)
            {
                var figure = flattenedPathGeometry.Figures[i];
                var oneFigurePolygon = GetPolylinePoints(figure);

                if (oneFigurePolygon.Count >= 3) // each polygon should have at least 3 positions
                    allPolygons[i] = oneFigurePolygon;
            }

            return allPolygons;
        }

        private static PointCollection GetPolylinePoints(PathFigure figure)
        {
            // First count all positions so we can correctly pre-alloacte the PointCollection
            int pointsCount = 1; // Start with StartPoint

            for (var i = 0; i < figure.Segments.Count; i++)
            {
                var polyLineSegment = figure.Segments[i] as PolyLineSegment;
                if (polyLineSegment != null)
                    pointsCount += polyLineSegment.Points.Count; 
            }


            var points = new PointCollection(pointsCount);

            points.Add(figure.StartPoint);

            for (var i = 0; i < figure.Segments.Count; i++)
            {
                var polyLineSegment = figure.Segments[i] as PolyLineSegment;
                if (polyLineSegment != null)
                {
                    var polylinePoints = polyLineSegment.Points;
                    var polylinePointCount = polylinePoints.Count;
                    for (int j = 0; j < polylinePointCount; j++)
                        points.Add(polylinePoints[j]);
                }
            }

            return points;
        }
        #endregion

        private void OnShowGlowCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_objectsGlowProvider == null)
                return;

            bool isEnabled = ShowGlowCheckBox.IsChecked ?? false;
            _objectsGlowProvider.IsEnabled = isEnabled;

            MainDXViewportView.Refresh(); // Render again
        }

        private void GlowBackBufferSizeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_objectsGlowProvider == null)
                return;

            int index = GlowBackBufferSizeComboBox.SelectedIndex;
            _objectsGlowProvider.GlowBackBufferSizeDownscale = (int)Math.Pow(2, index); // 1, 2, 4, 8, 16, 32

            MainDXViewportView.Refresh(); // Render again
        }

        private void BlurSizeSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_objectsGlowProvider == null)
                return;

            _objectsGlowProvider.BlurSize = (int)BlurSizeSlider.Value;

            MainDXViewportView.Refresh(); // Render again
        }

        private void OnWhiteBackgroundObjectCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_backgroundBoxVisual3D == null)
                return;

            _backgroundBoxVisual3D.Material = (WhiteBackgroundObjectCheckBox.IsChecked ?? false) ? new DiffuseMaterial(Brushes.White) 
                                                                                                 : new DiffuseMaterial(Brushes.Gray);
        }
    }

    /// <summary>
    /// ObjectsGlowProvider is a helper class that helps enable rendering glowing objects.
    /// Glowing objects are the objects that are added to the GlowObjectsRenderingQueue that is defined by this class.
    /// Objects that are rendered before glowing objects need to be added to the DXScene.BackgroundRenderingQueue.
    /// </summary>
    public class ObjectsGlowProvider : DXSceneResource
    {
        private DisposeList _disposables;
        private RenderingQueue _glowObjectsRenderingQueue;
        private SolidColorEffect _solidColorEffect;
        private DepthStencilState _depthEqualStencilState;

        private Texture2D _savedBackBuffer;
        private Texture2DDescription _savedBackBufferDescription;
        private RenderTargetView _savedRenderTargetView;
        private DepthStencilView _savedDepthStencilView;
        private ViewportF _savedViewport;
        private int _savedSuperSamplingCount;

        private Texture2D _glowBackBuffer;
        private Texture2DDescription _glowBackBufferDescription;
        private RenderTargetView _glowRenderTargetView;
        private ShaderResourceView _glowShaderResourceView;
        private ViewportF _glowBufferViewport;

        private Texture2D _blurredGlowBackBuffer;
        private RenderTargetView _blurredRenderTargetView;
        private ShaderResourceView _blurredShaderResourceView;
        private RenderPostProcessingRenderingStep _blurPostProcessesRenderingSteps;
        private RenderTextureRenderingStep _addGlowRenderingStep;
        private RenderingStepsGroup _glowRenderingStepsGroup;

        private Color4 _backgroundGlowColor;
        private RenderObjectsRenderingStep _renderBackgroundObjectsRenderingStep;

        private bool _isEnabled;
        

        public bool IsEnabled
        {
            get { return _isEnabled; }
            set
            {
                if (_isEnabled == value)
                    return;

                if (value)
                    EnableGlow();
                else
                    DisableGlow();

                _isEnabled = value;
            }
        }

        public int GlowBackBufferSizeDownscale { get; set; }

        private int _blurSize = 16;
        public int BlurSize
        {
            get => _blurSize;
            set
            {
                if (_blurSize == value)
                    return;

                if (value < 0 || value > 32) // Max blur size is 32 (this is determined by ExpandPostProcess.MaxExpansionWidth)
                    throw new ArgumentOutOfRangeException("value");

                _blurSize = value;

                if (_blurPostProcessesRenderingSteps != null)
                {
                    if (_blurSize == 0)
                    {
                        _blurPostProcessesRenderingSteps.IsEnabled = false;
                    }
                    else
                    {
                        foreach (var postProcessBase in _blurPostProcessesRenderingSteps.PostProcesses)
                        {
                            var expandPostProcess = postProcessBase as ExpandPostProcess;
                            if (expandPostProcess != null)
                            {
                                expandPostProcess.ExpansionWidth = Math.Max(1, _blurSize / 2);
                            }
                            else
                            {
                                var simpleBlurPostProcess = postProcessBase as SimpleBlurPostProcess;
                                if (simpleBlurPostProcess != null)
                                    simpleBlurPostProcess.FilterWidth = Math.Max(1, _blurSize);
                            }
                        }

                        _blurPostProcessesRenderingSteps.IsEnabled = true;
                    }
                }
            }
        }

        public RenderingQueue GlowObjectsRenderingQueue { get { return _glowObjectsRenderingQueue; }}

        public int GlowBackBufferWidth { get; private set; }
        public int GlowBackBufferHeight { get; private set; }

        public ObjectsGlowProvider()
        {
            GlowBackBufferSizeDownscale = 4;

            _isEnabled = true;
            _disposables = new DisposeList();
        }

        protected override void OnInitializeResources(DXScene dxScene)
        {
            if (IsEnabled)
                SetupGlowRenderingStep(dxScene);

            base.OnInitializeResources(dxScene);
        }

        protected override void Dispose(bool disposing)
        {
            _disposables.Dispose();
            DisposeGlowResources();

            if (parentDXScene != null)
                parentDXScene.DefaultRenderObjectsRenderingStep.FilterRenderingQueuesFunction = null;

            base.Dispose(disposing);
        }

        private void EnableGlow()
        {
            if (_glowObjectsRenderingQueue == null)
            {
                if (parentDXScene == null)
                    throw new InvalidOperationException("Cannot call EnableGlow before the InitializeResources method is called");

                SetupGlowRenderingStep(parentDXScene);
                return;
            }

            _renderBackgroundObjectsRenderingStep.IsEnabled = true;
            _glowRenderingStepsGroup.IsEnabled = true;
            parentDXScene.DefaultRenderObjectsRenderingStep.FilterRenderingQueuesFunction = FilterNonBackgroundRenderingQueue;
        }

        private void DisableGlow()
        {
            if (_glowObjectsRenderingQueue == null)
                return;

            _renderBackgroundObjectsRenderingStep.IsEnabled = false;
            _glowRenderingStepsGroup.IsEnabled = false;
            parentDXScene.DefaultRenderObjectsRenderingStep.FilterRenderingQueuesFunction = null;
        }

        private void SetupGlowRenderingStep(DXScene dxScene)
        {
            var dxDevice = dxScene.DXDevice;

            _backgroundGlowColor = new Color4(0, 0, 0, 0);

            _glowObjectsRenderingQueue = new RenderingQueue("GlowObjectsRenderingQueue");
            dxScene.AddRenderingQueueAfter(_glowObjectsRenderingQueue, dxScene.BackgroundRenderingQueue);


            _solidColorEffect = new SolidColorEffect();
            _solidColorEffect.InitializeResources(dxDevice);

            _disposables.Add(_solidColorEffect);



            var depthEqualDescription = new DepthStencilStateDescription()
            {
                IsDepthEnabled = true,
                DepthWriteMask = DepthWriteMask.Zero, // No write to depth buffer (this is not needed)

                // IMPORTANT:
                // We only want to render the parts of the objects, that are actually visible
                // This can be done by setting DepthComparison to Equal - so only if the rendered object has the same distance as the already rendered pixel, then render that object
                DepthComparison = Comparison.Equal, 

                IsStencilEnabled = false,
                StencilReadMask = 0xFF,
                StencilWriteMask = 0xFF,
                FrontFace = {
                    Comparison = Comparison.Always, 
                    DepthFailOperation = StencilOperation.Keep, 
                    FailOperation = StencilOperation.Keep, 
                    PassOperation = StencilOperation.Keep
                }
            };

            _depthEqualStencilState = new DepthStencilState(dxDevice.Device, depthEqualDescription);

            if (dxDevice.IsDebugDevice)
                _depthEqualStencilState.DebugName = "DepthEqualStencilState";

            _disposables.Add(_depthEqualStencilState);



            _renderBackgroundObjectsRenderingStep = new RenderObjectsRenderingStep("Render Background Objects")
            {
                FilterRenderingQueuesFunction = FilterBackgroundRenderingQueue
            };

            dxScene.RenderingSteps.AddBefore(dxScene.DefaultRenderObjectsRenderingStep, _renderBackgroundObjectsRenderingStep);
            _disposables.Add(_renderBackgroundObjectsRenderingStep);

            
            _glowRenderingStepsGroup = new RenderingStepsGroup("Render glow group", dxScene.RenderingSteps);

            _glowRenderingStepsGroup.BeforeRunningStep += (object sender, DirectX.RenderingEventArgs args) =>
            {
                var renderingContext = args.RenderingContext;

                // Set new back buffer where we will render glow objects
                SetupGlowBackBuffers(renderingContext);
            };


            var renderGlowObjectsRenderingStep = new RenderObjectsRenderingStep("Render glow objects")
            {
                OverrideEffect = _solidColorEffect,
                //OverrideDepthStencilState = _depthEqualStencilState,
                OverrideDepthStencilState = dxDevice.CommonStates.DepthNone,
                FilterRenderingQueuesFunction = queue => ReferenceEquals(queue, _glowObjectsRenderingQueue)
            };

            _glowRenderingStepsGroup.Children.Add(renderGlowObjectsRenderingStep);
            _disposables.Add(_glowRenderingStepsGroup);



            // Blur the glow
            var blurPostProcesses = new List<PostProcessBase>();

            // If we would only blur the objects, then because the background is black the blurred objects would be smaller 
            // (for example if there is a red pixel at the edge of the object, then when blurred it becomes half red because the neighbouring black pixels are mixed with red pixel)
            // To solve that, we first need to expand the objects so that after blurring the, for example, red color starts fading from fully red and not from half red
            var horizontalExpandPostProcess = new Ab3d.DirectX.PostProcessing.ExpandPostProcess(isVerticalRenderingPass: false, expansionWidth: _blurSize / 2, backgroundColor: _backgroundGlowColor);
            var verticalExpandPostProcess = new Ab3d.DirectX.PostProcessing.ExpandPostProcess(isVerticalRenderingPass: true, expansionWidth: _blurSize / 2, backgroundColor: _backgroundGlowColor);

            blurPostProcesses.Add(horizontalExpandPostProcess);
            blurPostProcesses.Add(verticalExpandPostProcess);
            _disposables.Add(horizontalExpandPostProcess);
            _disposables.Add(verticalExpandPostProcess);

            // GaussianBlurPostProcess does not support so good control over size of filter (max size is 15)
            //var horizontalBlurPostProcess = new Ab3d.DirectX.PostProcessing.GaussianBlurPostProcess(isVerticalBlur: false, blurStandardDeviation: 2, filterSize: 15);
            //var verticalBlurPostProcess = new Ab3d.DirectX.PostProcessing.GaussianBlurPostProcess(isVerticalBlur: true, blurStandardDeviation: 2, filterSize: 15);

            var horizontalBlurPostProcess = new Ab3d.DirectX.PostProcessing.SimpleBlurPostProcess(isVerticalBlur: false, filterWidth: _blurSize);
            var verticalBlurPostProcess = new Ab3d.DirectX.PostProcessing.SimpleBlurPostProcess(isVerticalBlur: true, filterWidth: _blurSize);

            blurPostProcesses.Add(horizontalBlurPostProcess);
            blurPostProcesses.Add(verticalBlurPostProcess);
            _disposables.Add(horizontalBlurPostProcess);
            _disposables.Add(verticalBlurPostProcess);


            _blurPostProcessesRenderingSteps = new RenderPostProcessingRenderingStep("Blur glow rendering step", blurPostProcesses);
            _disposables.Add(_blurPostProcessesRenderingSteps);

            _glowRenderingStepsGroup.Children.Add(_blurPostProcessesRenderingSteps);


            _addGlowRenderingStep = new RenderTextureRenderingStep(RenderTextureRenderingStep.TextureChannelsCount.FourChannels, "Add glow over 3D scene")
            {
                Offsets = new Vector4(0, 0, 0, 0),                      // preserve original colors
                Factors = new Vector4(1, 1, 1, 1),
                TargetViewport = new ViewportF(0, 0, 1f, 1f),           // render to full screen
                CustomBlendState = dxDevice.CommonStates.AdditiveBlend, // ADDITIVE BLEND
            };

            _addGlowRenderingStep.BeforeRunningStep += delegate(object sender, DirectX.RenderingEventArgs args)
            {
                RestoreBackBuffers(args.RenderingContext);
            };

            _disposables.Add(_addGlowRenderingStep);

            _glowRenderingStepsGroup.Children.Add(_addGlowRenderingStep);


            dxScene.RenderingSteps.AddBefore(dxScene.DefaultRenderObjectsRenderingStep, _glowRenderingStepsGroup);

            dxScene.DefaultRenderObjectsRenderingStep.FilterRenderingQueuesFunction = FilterNonBackgroundRenderingQueue;

            _disposables.Add(_glowRenderingStepsGroup);
        }

        private bool FilterBackgroundRenderingQueue(RenderingQueue queue)
        {
            return ReferenceEquals(queue, parentDXScene.BackgroundRenderingQueue);
        }
        
        private bool FilterNonBackgroundRenderingQueue(RenderingQueue queue)
        {
            return !ReferenceEquals(queue, parentDXScene.BackgroundRenderingQueue);
        }

        private void SetupGlowBackBuffers(RenderingContext renderingContext)
        {
            var dxDevice = renderingContext.DXDevice;

            // Save current back buffer
            _savedBackBuffer            = renderingContext.CurrentBackBuffer;
            _savedBackBufferDescription = renderingContext.CurrentBackBufferDescription;
            _savedRenderTargetView      = renderingContext.CurrentRenderTargetView;
            _savedDepthStencilView      = renderingContext.CurrentDepthStencilView;
            _savedViewport              = renderingContext.CurrentViewport;
            _savedSuperSamplingCount    = renderingContext.CurrentSupersamplingCount;

            // If size is changed, we need to recreate the back buffer

            int requiredWidth  = renderingContext.FinalBackBufferDescription.Width / GlowBackBufferSizeDownscale;
            int requiredHeight = renderingContext.FinalBackBufferDescription.Height / GlowBackBufferSizeDownscale;

            if (_glowBackBuffer != null &&
                (requiredWidth != _glowBackBufferDescription.Width ||
                 requiredHeight != _glowBackBufferDescription.Height))
            {
                DisposeGlowResources();
            }

            if (_glowBackBuffer == null)
            {
                _glowBackBufferDescription = dxDevice.CreateTexture2DDescription(requiredWidth,
                                                                                 requiredHeight,
                                                                                 new SampleDescription(1, 0),
                                                                                 isRenderTarget: true,
                                                                                 isSharedResource: false,
                                                                                 isStagingTexture: false,
                                                                                 isShaderResource: true,
                                                                                 format: DXDevice.StandardBufferFormat);

                // Super-sampling note:
                // To render the outlines at super-sampling resolution, then use the size from CurrentBackBufferDescription and not from FinalBackBufferDescription:
                // (you will also need to update the position of _addOutlineRenderingStep at the end of the constructor).
                //_outlineBackBufferDescription.Width  = renderingContext.CurrentBackBufferDescription.Width;
                //_outlineBackBufferDescription.Height = renderingContext.CurrentBackBufferDescription.Height;

                _glowBackBuffer = dxDevice.CreateTexture2D(_glowBackBufferDescription);

                // Create RenderTargetView and DepthStencilView for LEFT eye - the scene will be rendered to that buffer
                _glowRenderTargetView = new RenderTargetView(dxDevice.Device, _glowBackBuffer);


                _glowBufferViewport = new ViewportF(0, 0, _glowBackBufferDescription.Width, _glowBackBufferDescription.Height);


                var resourceViewDescription = new ShaderResourceViewDescription
                {
                    Format    = _glowBackBufferDescription.Format,
                    Dimension = ShaderResourceViewDimension.Texture2D,
                    Texture2D = new ShaderResourceViewDescription.Texture2DResource
                    {
                        MipLevels       = 1,
                        MostDetailedMip = 0
                    }
                };

                _glowShaderResourceView = new ShaderResourceView(dxDevice.Device, _glowBackBuffer, resourceViewDescription);

                
                // Create the same back buffers for blurred outline
                _blurredGlowBackBuffer = dxDevice.CreateTexture2D(_glowBackBufferDescription);
                _blurredRenderTargetView = new RenderTargetView(dxDevice.Device, _blurredGlowBackBuffer);
                _blurredShaderResourceView = new ShaderResourceView(dxDevice.Device, _blurredGlowBackBuffer, resourceViewDescription);

                // Render horizontal blurring pass to _blurredOutlineBackBuffer
                // and then the vertical pass back to _outlineBackBuffer.
                _blurPostProcessesRenderingSteps.InitializeResourcesForMultiplePostProcesses(
                    /* source: */             _glowBackBuffer, _glowBackBufferDescription, _glowRenderTargetView, _glowShaderResourceView,
                    /* additional buffers: */ _blurredGlowBackBuffer, _glowBackBufferDescription, _blurredRenderTargetView, _blurredShaderResourceView,
                    /* destination: */        _glowBackBuffer, _glowBackBufferDescription, _glowRenderTargetView);

                if (parentDXScene.DXDevice.IsDebugDevice)
                {
                    _glowBackBuffer.DebugName = "GlowBackBuffer";
                    _glowRenderTargetView.DebugName = "GlowRenderTargetView";
                    _glowShaderResourceView.DebugName = "GlowShaderResourceView";

                    _blurredGlowBackBuffer.DebugName = "BlurredGlowBackBuffer";
                    _blurredRenderTargetView.DebugName = "BlurredRenderTargetView";
                    _blurredShaderResourceView.DebugName = "BlurredShaderResourceView";
                }

                GlowBackBufferWidth = requiredWidth;
                GlowBackBufferHeight = requiredHeight;
            }


            _addGlowRenderingStep.SourceTexture = _glowShaderResourceView;

            renderingContext.SetBackBuffer(_glowBackBuffer, 
                                           _glowBackBufferDescription, 
                                           _glowRenderTargetView, 
                                           depthStencilView: null, //renderingContext.CurrentDepthStencilView,  // IMPORTANT: We need to preserve the current depth buffer because we need to read depth from it
                                           currentSupersamplingCount: 1,
                                           bindNewRenderTargetsToDeviceContext: true);

            renderingContext.CurrentViewport = _glowBufferViewport;
            renderingContext.DeviceContext.Rasterizer.SetViewport(_glowBufferViewport);

            renderingContext.DeviceContext.ClearRenderTargetView(_glowRenderTargetView, _backgroundGlowColor); 
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
            renderingContext.DeviceContext.Rasterizer.SetViewport(_savedViewport);

            _savedBackBuffer       = null;
            _savedRenderTargetView = null;
            _savedDepthStencilView = null;
        }

        private void DisposeGlowResources()
        {
            DisposeHelper.DisposeAndNullify(ref _glowBackBuffer);
            DisposeHelper.DisposeAndNullify(ref _glowRenderTargetView);
            DisposeHelper.DisposeAndNullify(ref _glowShaderResourceView);

            DisposeHelper.DisposeAndNullify(ref _blurredGlowBackBuffer);
            DisposeHelper.DisposeAndNullify(ref _blurredRenderTargetView);
            DisposeHelper.DisposeAndNullify(ref _blurredShaderResourceView);

            // Reset structs
            _glowBackBufferDescription = new Texture2DDescription();
        }
    }
}