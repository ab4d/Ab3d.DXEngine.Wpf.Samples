using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
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
using Ab3d.Common.Cameras;
using Ab3d.DirectX;
using Ab3d.DirectX.Controls;
using Ab3d.DXEngine.Wpf.Samples.Common;
using Ab3d.Visuals;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    /// <summary>
    /// Interaction logic for PerPixelRenderingSample.xaml
    /// </summary>
    public partial class PerPixelRenderingSample : Page
    {
        public PerPixelRenderingSample()
        {
            InitializeComponent();

            // If possible use UltraQualityHardwareRendering settings (software rendering or WPF 3D will be used in case a hardware rendering cannot be used)
            // This will use supersampling and improve rendering quality.
            MainViewportView.GraphicsProfiles = new GraphicsProfile[] { GraphicsProfile.UltraQualityHardwareRendering,
                                                                        GraphicsProfile.HighQualitySoftwareRendering,
                                                                        GraphicsProfile.Wpf3D, };

            var dxTestScene = new TestScene(DXViewport3D);
            dxTestScene.CreatesTestScene();
            dxTestScene.StartAnimation();

            var wpfTestScene = new TestScene(WpfViewport3D);
            wpfTestScene.CreatesTestScene();
            wpfTestScene.StartAnimation();

            CopyWpfCamera(); // Copy settings from WpfCamera to DXCamera

            this.Unloaded += (sender, args) => MainViewportView.Dispose();
        }

        private void DXCamera_OnCameraChanged(object sender, CameraChangedRoutedEventArgs e)
        {
            CopyDXCamera();
        }

        private void WpfCamera_OnCameraChanged(object sender, CameraChangedRoutedEventArgs e)
        {
            CopyWpfCamera();
        }

        private void CopyDXCamera()
        {
            WpfCamera.Heading = DXCamera.Heading;
            WpfCamera.Attitude = DXCamera.Attitude;
            WpfCamera.Distance = DXCamera.Distance;
            WpfCamera.Offset = DXCamera.Offset;
        }

        private void CopyWpfCamera()
        {
            DXCamera.Heading = WpfCamera.Heading;
            DXCamera.Attitude = WpfCamera.Attitude;
            DXCamera.Distance = WpfCamera.Distance;
            DXCamera.Offset = WpfCamera.Offset;
        }


        class TestScene
        {
            private PointLight _pointLight;
            private SpotLight _spotLight;

            private bool _isAnimating;
            private DateTime _animationStartTime;
            private TimeSpan _lastRenderTime;

            public readonly Viewport3D TargetViewport3D;

            public ModelVisual3D TestObjectsVisual3D { get; private set; }
            public ModelVisual3D LightModelsVisual3D { get; private set; }
            public Model3DGroup Lights { get; private set; }

            public TestScene(Viewport3D targetViewport3D)
            {
                if (targetViewport3D == null) throw new ArgumentNullException("targetViewport3D");

                TargetViewport3D = targetViewport3D;

                targetViewport3D.Unloaded += (sender, args) => StopAnimation();
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


                TargetViewport3D.Children.Clear();
                TargetViewport3D.Children.Add(TestObjectsVisual3D);
                TargetViewport3D.Children.Add(LightModelsVisual3D);
                TargetViewport3D.Children.Add(lightVisual);
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
                    CenterPosition      = new Point3D(0, 0.1, 0),
                    Size                = new Size(2000, 2000),
                    WidthDirection      = new Vector3D(1, 0, 0),
                    HeightDirection     = new Vector3D(0, 0, -1),
                    WidthCellsCount     = 100,
                    HeightCellsCount    = 100,
                    MajorLinesFrequency = 5,
                    IsClosed = true,
                    MajorLineThickness = 1.5,
                    MajorLineColor     = Colors.DimGray,

                    LineThickness = 0.8,
                    LineColor     = Colors.Gray,

                    RenderingTechnique = WireGridVisual3D.WireGridRenderingTechniques.FixedMesh3DLines, // Use fixed MeshGeometry3D to render wire grid instead of dynamic 3D lines
                    IsEmissiveMaterial = false                                                          // This also allows us to use standard material instead of emissive material - this means that the lines will be visible only where they are illuminated by the light.
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
                    double rad = SharpDX.MathUtil.DegreesToRadians(a);
                    double x = Math.Sin(rad) * 100 + centerX;
                    double z = Math.Cos(rad) * 100 + centerZ;

                    var sphereVisual3D = new SphereVisual3D()
                    {
                        CenterPosition = new Point3D(x, 25, z),
                        Radius = 25,
                        Segments = 10, // Intentially lowered from default 30 to make the rendering difference bigger (between per vertex (WPF) and per pixel (DXEngine) rendering)
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
                _spotLight.ConstantAttenuation  = 0;
                _spotLight.LinearAttenuation    = 0.01;
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
        }
    }
}
