using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Ab3d.Cameras;
using Ab3d.Common.Cameras;
using Ab3d.Controls;
using Ab3d.DirectX;
using Ab3d.DirectX.Controls;
using Ab3d.DXEngine.Wpf.Samples.Controls;
using Ab3d.Visuals;

#if SHARPDX
using SharpDX;
#endif

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{

    // GraphicsProfiles array defined GraphicsProfiles that are used to initialized the DXEngine.
    // First NormalQualityHardwareRendering is used.
    // If the hardware DirectX device cannot be created, then software rendering is used (using DirectX Warp).
    // If this is also not supported, then WPF 3D rendering is used.
    //
    // It is possible to change the GraphicsProfiles array to use better quality.
    // The following is the default array:
    // MainDXViewportView.GraphicsProfiles = new GraphicsProfile[] { GraphicsProfile.NormalQualityHardwareRendering,
    //                                                               GraphicsProfile.NormalQualitySoftwareRendering,
    //                                                               GraphicsProfile.Wpf3D };

    /// <summary>
    /// Interaction logic for GraphicsProfilesSample.xaml
    /// </summary>
    public partial class GraphicsProfilesSample : Page
    {
        private TestScene[] _testScenes;

        public GraphicsProfilesSample()
        {
            InitializeComponent();

            CreateDemoScenes();

            this.Unloaded += delegate (object sender, RoutedEventArgs args)
            {
                if (_testScenes != null)
                {
                    foreach (var testScene in _testScenes)
                        testScene.Dispose();
                }
            };
        }

        private void CreateDemoScenes()
        {
            var demonstratedGraphicsProfiles = new GraphicsProfile[] { GraphicsProfile.HighQualityHardwareRendering,
                                                                       GraphicsProfile.NormalQualityHardwareRendering,
                                                                       GraphicsProfile.HighSpeedNormalQualityHardwareRendering,
                                                                       GraphicsProfile.Wpf3D };

            var descriptions = new string[]
            {
@"High quality hardware rendering is recommended for best visual quality with super-smooth 3D lines:
+ per-pixel rendering
+ 4xSSAA: 4 times super-sampling
+ 4xMSAA: 4 times multi-sampling
- slower and requires more memory",

@"Normal quality hardware rendering is the default GraphicsProfile. It is recommended for very good visual quality and for 3D scene that do not require super-smooth lines:
+ per-pixel rendering
+ 4xMSAA: 4 times multi-sampling",

@"HighSpeedNormalQualityHardwareRendering is using low quality shaders and can be used for very complex scenes or on slower computers for faster rendering. Use LowQualityHardwareRendering for even faster rendering but without anti-aliasing.
+ faster rendering because of per-vertex rendering
+ 4xMSAA: 4 times multi-sampling
- per-vertex rendering (worse render quality)",

@"Using WPF to render 3D graphics. This is by default the fallback GraphicsProfile that is used when DirectX 11 rendering is not supported on the computer:
- per-vertex rendering (worse render quality)
- line rendering is not hardware accelerated
- much slower rendering with DirectX 9"
            };
            

            _testScenes = new TestScene[demonstratedGraphicsProfiles.Length];

            for (int i = 0; i < demonstratedGraphicsProfiles.Length; i++)
            {
                _testScenes[i] = new TestScene(demonstratedGraphicsProfiles[i], descriptions[i]);

                var sceneFrameworkElement = _testScenes[i].CreateScene();
                Grid.SetRow(sceneFrameworkElement, (int)(i / 2) + 1);
                Grid.SetColumn(sceneFrameworkElement, (i % 2));

                RootGrid.Children.Add(sceneFrameworkElement);

                _testScenes[i].CameraChanged += OnCameraChanged;

                _testScenes[i].StartAnimation();
            }
        }

        private void OnCameraChanged(object sender, CameraChangedRoutedEventArgs e)
        {
            var testScene = sender as TestScene;

            if (testScene == null)
                return;

            var changedCamera = testScene.MainCamera;

            for (var i = 0; i < _testScenes.Length; i++)
            {
                if (_testScenes[i] == testScene)
                    continue;

                _testScenes[i].SyncCamera(changedCamera);
            }
        }

        class TestScene
        {
            private readonly string _title;
            private readonly GraphicsProfile _graphicsProfile;
            private readonly string _subtitle;

            private bool _isSyncingCamera;
            private DXViewportView _dxViewportView;
            private MouseCameraController _mouseCameraController;

            private PointLight _pointLight;
            private SpotLight _spotLight;

            private bool _isAnimating;
            private DateTime _animationStartTime;
            private TimeSpan _lastRenderTime;

            public ModelVisual3D TestObjectsVisual3D { get; private set; }
            public ModelVisual3D LightModelsVisual3D { get; private set; }
            public Model3DGroup Lights { get; private set; }


            public Viewport3D MainViewport3D { get; private set; }

            public TargetPositionCamera MainCamera { get; private set; }

            public event BaseCamera.CameraChangedRoutedEventHandler CameraChanged;

            public TestScene(GraphicsProfile graphicsProfile, string subtitle = null)
            {
                _graphicsProfile = graphicsProfile;

                _title = graphicsProfile.Name;

                _subtitle = subtitle;
            }

            public Grid CreateScene()
            {
                var rootGrid = new Grid();

                var textBlock = new TextBlock()
                {
                    Text = _title,
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    Margin = new Thickness(10, 5, 10, 10),
                    VerticalAlignment = VerticalAlignment.Top,
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                var rootBorder = new Border()
                {
                    Background = Brushes.Transparent,
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(2, 2, 2, 2),
                    Margin = new Thickness(2, 2, 2, 2),
                    SnapsToDevicePixels = true,
                    UseLayoutRounding = true
                };

                MainViewport3D = new Viewport3D();

                MainCamera = new TargetPositionCamera()
                {
                    TargetViewport3D = MainViewport3D,
                    TargetPosition = new Point3D(-130, 150, 0),
                    Heading = 30,
                    Attitude = -20,
                    Distance = 800,
                    ShowCameraLight = ShowCameraLightType.Always
                };

                MainCamera.CameraChanged += OnMainCameraChanged;


                _mouseCameraController = new MouseCameraController()
                {
                    TargetCamera = MainCamera,
                    EventsSourceElement = rootBorder,
                    RotateCameraConditions = MouseCameraController.MouseAndKeyboardConditions.LeftMouseButtonPressed,
                    MoveCameraConditions = MouseCameraController.MouseAndKeyboardConditions.Disabled // disable mouse move
                };

                _dxViewportView = new DXViewportView(MainViewport3D)
                {
                    BackgroundColor = Colors.Black,
                    GraphicsProfiles = new GraphicsProfile[] { _graphicsProfile } // Use only specified graphics profile. By default this array is set to: NormalQualityHardwareRendering, NormalQualitySoftwareRendering, Wpf3D (if first profile cannot be used, then the next one is created)
                };

                _dxViewportView.DXSceneInitialized += delegate (object sender, EventArgs args)
                {
                    CreatesTestScene();
                };

                rootBorder.Child = _dxViewportView;

                MainCamera.Refresh();

                rootGrid.Children.Add(rootBorder);
                rootGrid.Children.Add(textBlock);


                if (_subtitle != null)
                {
                    var subtitleTextBlock = new TextBlockEx()
                    {
                        Text = _subtitle,
                        FontSize = 12,
                        Foreground = Brushes.White,
                        Margin = new Thickness(10, 30, 10, 10),
                        VerticalAlignment = VerticalAlignment.Top,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        TextWrapping = TextWrapping.Wrap
                    };

                    rootGrid.Children.Add(subtitleTextBlock);
                }

                return rootGrid;
            }

            public void SyncCamera(TargetPositionCamera sourceCamera)
            {
                _isSyncingCamera = true; // Do not trigger CameraChanged event

                MainCamera.BeginInit();

                MainCamera.Heading = sourceCamera.Heading;
                MainCamera.Attitude = sourceCamera.Attitude;
                MainCamera.Distance = sourceCamera.Distance;
                MainCamera.Offset = sourceCamera.Offset;

                MainCamera.EndInit();

                _isSyncingCamera = false;
            }
   
            public void StartAnimation()
            {
                if (_isAnimating)
                    return;

                _animationStartTime = DateTime.Now;
                CompositionTarget.Rendering += CompositionTargetOnRendering;

                _isAnimating = true;
            }

            public void StopAnimation()
            {
                if (!_isAnimating)
                    return;

                CompositionTarget.Rendering -= CompositionTargetOnRendering;
                _isAnimating = false;
            }

            private void CompositionTargetOnRendering(object sender, EventArgs e)
            {
                var args = (System.Windows.Media.RenderingEventArgs)e;

                // It's possible for Rendering to call back twice in the same frame 
                // so only render when we haven't already rendered in this frame.
                if (_lastRenderTime == args.RenderingTime)
                    return;

                _lastRenderTime = args.RenderingTime;

                AnimateLights();
            }

            private void AnimateLights()
            {
                var elapsedSeconds = (DateTime.Now - _animationStartTime).TotalSeconds;

                // Point light is travelling up and down
                double pointLightCycleTime = 5; // 5 seconds
                double yBottom = 10;
                double yTop = 100;

                double yPos = (1 - Math.Cos((elapsedSeconds * 2 * Math.PI) / pointLightCycleTime)) / 2; // yPos in range from 0 to 1; start at the bottom
                yPos = yBottom + yPos * (yTop - yBottom);

                var actualPosition = _pointLight.Position;

                _pointLight.Position = new Point3D(actualPosition.X, yPos, actualPosition.Z);


                // Spot light is circling around the scene
                double radius = 300;
                double spotLightCycleTime = 12; // 12 seconds
                double spotLightStartOffset = -0.7 * Math.PI; // Start on the right

                double xPos = Math.Sin((-elapsedSeconds * 2 * Math.PI) / spotLightCycleTime + spotLightStartOffset); // range from -1 to 1
                double zPos = Math.Cos((-elapsedSeconds * 2 * Math.PI) / spotLightCycleTime + spotLightStartOffset);

                Vector3D direction = new Vector3D(xPos * radius, -_spotLight.Position.Y, zPos * radius);
                direction.Normalize();

                _spotLight.Direction = direction;
            }

            public void CreatesTestScene()
            {
                TestObjectsVisual3D = CreateTestObjects();

                Lights = CreateLights();

                var lightVisual = new ModelVisual3D();
                lightVisual.Content = Lights;

                LightModelsVisual3D = CreateLightModels(Lights.Children);


                MainViewport3D.Children.Clear();
                MainViewport3D.Children.Add(TestObjectsVisual3D);
                MainViewport3D.Children.Add(LightModelsVisual3D);
                MainViewport3D.Children.Add(lightVisual);
            }

            private ModelVisual3D CreateTestObjects()
            {
                var modelVisual3D = new ModelVisual3D();

                // NOTE:
                // The following box will be crated from 10 x 1 x 10 cells
                // This is needed to see at least some lights rendered on it in WPF rendering that is using per-vertex rendering
                // For DXEngine this is not needed because it is using per-pixel rendering (here we can create standard 1 x 1 x 1 box)
                var greenBaseBox = new BoxVisual3D()
                {
                    CenterPosition = new Point3D(0, -5, 0),
                    Size = new Size3D(2000, 10, 2000),
                    XCellsCount = 20,
                    YCellsCount = 1,
                    ZCellsCount = 20,
                    Material = new DiffuseMaterial(Brushes.White)
                };

                modelVisual3D.Children.Add(greenBaseBox);


                var wireGridVisual3D = new WireGridVisual3D()
                {
                    CenterPosition = new Point3D(0, 0.1, 0),
                    Size = new Size(2000, 2000),
                    WidthDirection = new Vector3D(1, 0, 0),
                    HeightDirection = new Vector3D(0, 0, -1),
                    WidthCellsCount = 100,
                    HeightCellsCount = 100,
                    MajorLinesFrequency = 5,
                    IsClosed = true,
                    MajorLineThickness = 1.5,
                    MajorLineColor = System.Windows.Media.Color.FromRgb(0, 0, 0),

                    LineThickness = 0.8,
                    LineColor = System.Windows.Media.Color.FromRgb(30, 30, 30),
                };

                modelVisual3D.Children.Add(wireGridVisual3D);


                double centerX = 0;
                double centerZ = 0;

                var orangeBox = new BoxVisual3D()
                {
                    CenterPosition = new Point3D(centerX, 100, centerZ),
                    Size = new Size3D(50, 200, 50),
                    Material = new DiffuseMaterial(Brushes.Orange)
                };

                modelVisual3D.Children.Add(orangeBox);


                var blueSpecularMaterial = new MaterialGroup();
                blueSpecularMaterial.Children.Add(new DiffuseMaterial(Brushes.LightSkyBlue));
                blueSpecularMaterial.Children.Add(new SpecularMaterial(Brushes.White, 20));

                for (int a = 0; a < 360; a += 40)
                {
                    double rad = MathUtil.DegreesToRadians(a);
                    double x = Math.Sin(rad) * 100 + centerX;
                    double z = Math.Cos(rad) * 100 + centerZ;

                    var sphereVisual3D = new SphereVisual3D()
                    {
                        CenterPosition = new Point3D(x, 25, z),
                        Radius = 25,
                        Segments = 15, // Intentionally lowered from default 30 to make the rendering difference bigger (between per vertex (WPF) and per pixel (DXEngine) rendering)
                        Material = blueSpecularMaterial
                    };

                    modelVisual3D.Children.Add(sphereVisual3D);
                }

                return modelVisual3D;
            }

            private Model3DGroup CreateLights()
            {
                var lightsModelGroup = new Model3DGroup();

                var pointLightPosition = new Point3D(150, 10, 200);

                // Attenuation describes how the distance from the light affects the intensity
                // When ConstantAttenuation == 1 and LinearAttenuation == 0 and QuadraticAttenuation == 0 this means that distance does not affect the light intensity
                // The attenuation factor is calculated as:
                // attFactor = ConstantAttenuation + LinearAttenuation * d + QuadraticAttenuation * d * d;  // d is distance from the light
                // The final light color is get by divided the light color by attFactor.
                _pointLight = new PointLight(Colors.White, pointLightPosition);
                _pointLight.Range = 1000;
                _pointLight.ConstantAttenuation = 0;
                _pointLight.LinearAttenuation = 0.01;
                _pointLight.QuadraticAttenuation = 0;
                lightsModelGroup.Children.Add(_pointLight);


                var spotLightPosition = new Point3D(-60, 70, 200);
                var spotLightVector = new Vector3D(100, -80, -200);
                spotLightVector.Normalize();


                _spotLight = new SpotLight(Colors.White, spotLightPosition, spotLightVector, outerConeAngle: 40, innerConeAngle: 30);
                _spotLight.Range = 1000;
                _spotLight.ConstantAttenuation = 0;
                _spotLight.LinearAttenuation = 0.01;
                _spotLight.QuadraticAttenuation = 0;
                lightsModelGroup.Children.Add(_spotLight);

                //_ambientLight = new AmbientLight(Color.FromRgb(5, 5, 5));
                //lightsModelGroup.Children.Add(_ambientLight);

                return lightsModelGroup;
            }

            private ModelVisual3D CreateLightModels(IEnumerable<Model3D> lights)
            {
                var lightModelsVisual = new ModelVisual3D();

                var yellowEmissiveMaterial = new MaterialGroup();
                yellowEmissiveMaterial.Children.Add(new DiffuseMaterial(Brushes.Black));
                yellowEmissiveMaterial.Children.Add(new EmissiveMaterial(Brushes.Yellow));

                foreach (var light in lights.OfType<Light>())
                {
                    Visual3D lightVisual = null;

                    var pointLight = light as PointLight;

                    if (pointLight != null)
                    {
                        var pointLightSphere = new SphereVisual3D()
                        {
                            CenterPosition = pointLight.Position,
                            Radius = 3,
                            Material = yellowEmissiveMaterial
                        };

                        pointLight.Changed += delegate (object sender, EventArgs args)
                        {
                            pointLightSphere.CenterPosition = pointLight.Position;
                        };

                        lightVisual = pointLightSphere;
                    }
                    else
                    {
                        var spotLight = light as SpotLight;

                        if (spotLight != null)
                        {
                            var spotLightVector = spotLight.Direction;
                            spotLightVector.Normalize();

                            var spotLightArrow = new ArrowVisual3D()
                            {
                                StartPosition = spotLight.Position - spotLightVector * 20,
                                EndPosition = spotLight.Position,
                                Radius = 2,
                                Material = yellowEmissiveMaterial
                            };

                            spotLight.Changed += delegate (object sender, EventArgs args)
                            {
                                var spotLightDirection = spotLight.Direction;
                                spotLightDirection.Normalize();

                                spotLightArrow.StartPosition = spotLight.Position;
                                spotLightArrow.EndPosition = spotLight.Position + spotLightDirection * 20;
                            };

                            lightVisual = spotLightArrow;
                        }
                    }
                    // TODO: DirectionalLight is not supported (it could be rendered as PlaneVisual3D with additional ArrowVisual3Ds)

                    if (lightVisual != null)
                        lightModelsVisual.Children.Add(lightVisual);
                }

                return lightModelsVisual;
            }




            private void OnMainCameraChanged(object sender, CameraChangedRoutedEventArgs e)
            {
                if (_isSyncingCamera)
                    return;

                OnCameraChanged(e);
            }

            protected void OnCameraChanged(CameraChangedRoutedEventArgs e)
            {
                if (CameraChanged != null)
                    CameraChanged(this, e);
            }

            public void Dispose()
            {
                if (_dxViewportView != null)
                {
                    _dxViewportView.Dispose();
                    _dxViewportView = null;
                }
            }
        }
    }
}
