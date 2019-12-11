using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
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
using Ab3d.Assimp;
using Ab3d.Common;
using Ab3d.Common.Cameras;
using Ab3d.DirectX;
using Ab3d.DirectX.Effects;
using Ab3d.DirectX.Materials;
using Ab3d.Utilities;
using Ab3d.Visuals;
using SharpDX;
using SharpDX.Direct3D11;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    /// <summary>
    /// Interaction logic for PlanarShadows.xaml
    /// </summary>
    public partial class PlanarShadows : Page, ICompositionRenderingSubscriber
    {
        private PointLight _shadowPointLight;
        private DirectionalLight _shadowDirectionalLight;
        private AmbientLight _ambientLight;

        private Light _currentShadowLight;
        
        private double _lightVerticalAngle;
        private double _lightHorizontalAngle;
        private double _lightDistance;

        private DateTime _lastAnimationTime;

        private const double HalfAnimationHeight = 40;
        private const double AnimationMinHeight = 15;

        private PlanarShadowRenderingProvider _planarShadowRenderingProvider;

        private DisposeList _disposables;
        private PlaneVisual3D _planeVisual3D;

        public PlanarShadows()
        {
            InitializeComponent();

            _disposables = new DisposeList();

            MainDXViewportView.DXSceneInitialized += delegate(object sender, EventArgs args)
            {
                if (MainDXViewportView.DXScene != null)
                {
                    // Load texture file into ShaderResourceView (in our case we load dds file; but we could also load png file)
                    string textureFileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources/10x10-texture.png");
                    var loadedShaderResourceView = Ab3d.DirectX.TextureLoader.LoadShaderResourceView(MainDXViewportView.DXScene.DXDevice.Device, textureFileName);

                    _disposables.Add(loadedShaderResourceView);


                    _planarShadowRenderingProvider = new PlanarShadowRenderingProvider()
                    {
                        ShadowPlaneCenterPosition  = new Vector3(0, 0, 0),
                        ShadowPlaneSize            = new Vector2(400, 400),
                        ShadowPlaneNormalVector    = new Vector3(0, 1, 0),
                        ShadowPlaneHeightDirection = new Vector3(0, 0, -1),

                        ShadowPlaneMaterial = new StandardMaterial()
                        {
                            DiffuseColor = Color3.White, // When DiffuseTextures are set, then DiffuseColor is used as a color filter (White means no filter)
                            DiffuseTextures = new ShaderResourceView[] { loadedShaderResourceView }
                        },

                        ShadowPlaneBackMaterial = new StandardMaterial()
                        {
                            DiffuseColor = Colors.DimGray.ToColor3()
                        },

                        ShadowColor = Color3.Black,
                        ShadowTransparency = 0.65f,

                        // Because shadow is rendered as standard 3D object, we need to offset it from the shadow plane
                        // to prevent z-fighting problems that occur when two 3D objects are rendered to the same 3D position.
                        // This value need to be very small so that it is not seen that the shadow is above the plane.
                        // Default value is 0.01f.
                        ShadowOffsetFromPlane = 0.01f,

                        // When using PlanarShadowRenderingProvider we do not need PlanarShadowMeshCreator from Ab3d.PowerToys
                        // to prepare a special MeshGeometry3D for us. Also PlanarShadowMeshCreator does not need to manually (on the CPU)
                        // cut the shadow to the plane bounds but this can be done with using hardware accelerated algorithm (using stencil buffer).
                        // But if we still want to use PlanarShadowMeshCreator we can set the following two properties to false 
                        // (for example if we wanted to use PlanarShadowRenderingProvider just to provide proper transparent shadows).
                        ApplyShadowMatrix      = true,
                        CutShadowToPlaneBounds = true,

                        // To use a custom light that does not illuminate the 3D scene set the CustomShadowLight.
                        // Otherwise the first light that has DXAttributeType.IsCastingShadow attribute set to true is used.
                        // If no light has IsCastingShadow attribute set, then the first directional or point light is used.
                        //CustomShadowLight = new Ab3d.DirectX.Lights.DirectionalLight(new Vector3(0, -1, 1))
                        //CustomShadowLight = new Ab3d.DirectX.Lights.PointLight(new Vector3(0, 500, 0), 300)
                    };

                    _disposables.Add(_planarShadowRenderingProvider);


                    MainDXViewportView.DXScene.InitializeShadowRendering(_planarShadowRenderingProvider);
                }


                _lightHorizontalAngle = -60;
                _lightVerticalAngle = 60;
                _lightDistance = 500;

                _ambientLight = new AmbientLight(System.Windows.Media.Color.FromRgb(40, 40, 40));

                _shadowPointLight = new PointLight();
                _shadowDirectionalLight = new DirectionalLight();

                Camera1.ShowCameraLight = ShowCameraLightType.Never; // prevent adding camera's light

                SetShadowLight(isDirectionalLight: true);

                UpdateLights();

                CreateSampleObjects();
            };



            this.PreviewKeyDown += OnPreviewKeyDown;

            // This will allow receiving keyboard events
            this.Focusable = true;
            this.Focus();

            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                StopAnimation();

                _disposables.Dispose();
                MainDXViewportView.Dispose();
            };
        }

        private void CreateSampleObjects()
        {
            var spherePositions = new List<Point3D>();

            SampleObjectsVisual3D.Children.Clear();

            var rnd = new Random();

            float planeCellSize = _planarShadowRenderingProvider != null ? _planarShadowRenderingProvider.ShadowPlaneSize.X / 10 : 40;

            while (spherePositions.Count < 10)
            {
                int cellXIndex = rnd.Next(10);
                int cellYIndex = rnd.Next(10);

                var spherePosition = new Point3D((cellXIndex - 5) * planeCellSize + planeCellSize / 2, 
                                                 0,
                                                 (cellYIndex - 5) * planeCellSize + planeCellSize / 2);

                // Check if this position was already taken
                if (spherePositions.Any(p => Ab3d.Utilities.MathUtils.IsSame((float) p.X, spherePosition.X) && Ab3d.Utilities.MathUtils.IsSame(p.Z, spherePosition.Z)))
                    continue;

                spherePositions.Add(spherePosition);


                // t defines an animation time between 0 and 1.
                double t = rnd.NextDouble();

                var sphereVisual3D = new SphereVisual3D()
                {
                    CenterPosition = spherePosition,
                    Radius         = planeCellSize * 0.25,
                    Material       = new DiffuseMaterial(new SolidColorBrush(System.Windows.Media.Color.FromRgb((byte)rnd.Next(255), (byte)rnd.Next(255), (byte)rnd.Next(255)))),
                    Tag            = t
                };

                sphereVisual3D.Transform = new TranslateTransform3D(0, GetAnimatedHeight(t), 0);

                SampleObjectsVisual3D.Children.Add(sphereVisual3D);
            }

            StartAnimation();
        }

        private void StartAnimation()
        {
            // Use CompositionRenderingHelper to subscribe to CompositionTarget.Rendering event
            // This is much safer because in case we forget to unsubscribe from Rendering, the CompositionRenderingHelper will unsubscribe us automatically
            // This allows to collect this class will Grabage collector and prevents infinite calling of Rendering handler.
            // After subscribing the ICompositionRenderingSubscriber.OnRendering method will be called on each CompositionTarget.Rendering event
            CompositionRenderingHelper.Instance.Subscribe(this);
        }

        private void StopAnimation()
        {
            CompositionRenderingHelper.Instance.Unsubscribe(this);
        }

        void ICompositionRenderingSubscriber.OnRendering(EventArgs e)
        {
            var now = DateTime.Now;

            if (_lastAnimationTime != DateTime.MinValue)
            {
                double elapsedSeconds = (now - _lastAnimationTime).TotalSeconds;
                AnimateAllObjects(elapsedSeconds * 0.5); // take 2 seconds for one animation
            }

            _lastAnimationTime = now;
        }

        private double GetAnimatedHeight(double t)
        {
            return Math.Sin(t * Math.PI * 2) // make new sin cycle on each whole value of t
                   * HalfAnimationHeight     // adjust result to be between -HalfAnimationHeight to +HalfAnimationHeight
                   + HalfAnimationHeight     // make the result positive: between 0 and 2 * HalfAnimationHeight
                   + AnimationMinHeight;     // add min height
        }

        private void AnimateAllObjects(double dt)
        {
            foreach (var visual3D in SampleObjectsVisual3D.Children.OfType<SphereVisual3D>())
            {
                var translateTransform3D = visual3D.Transform as TranslateTransform3D;

                if (!(visual3D.Tag is double) || translateTransform3D == null)
                    continue;

                var t = (double) visual3D.Tag;

                t += dt;

                visual3D.Tag = t;
                translateTransform3D.OffsetY = GetAnimatedHeight(t);
            }
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs keyEventArgs)
        {
            bool isChanged = false;
            double stepSize = 5;

            switch (keyEventArgs.Key)
            {
                case Key.Up:
                    if (_lightVerticalAngle - stepSize > 15)
                    {
                        _lightVerticalAngle += stepSize;
                        isChanged = true;
                    }
                    break;

                case Key.Down:
                    if (_lightVerticalAngle + stepSize < 165)
                    {
                        _lightVerticalAngle -= stepSize;
                        isChanged = true;
                    }
                    break;

                case Key.Left:
                    _lightHorizontalAngle += stepSize;
                    isChanged = true;
                    break;

                case Key.Right:
                    _lightHorizontalAngle -= stepSize;
                    isChanged = true;
                    break;


                case Key.PageUp:
                    _lightDistance += stepSize;
                    isChanged = true;
                    break;

                case Key.PageDown:
                    _lightDistance -= stepSize;
                    isChanged = true;
                    break;
            }

            if (isChanged)
            {
                UpdateLights();
                keyEventArgs.Handled = true;
            }
            else
            {
                keyEventArgs.Handled = false;
            }
        }

        private void UpdateLights()
        {
            var position = CalculateLightPosition();

            // Create direction from position - target position = (0,0,0)
            var lightDirection = new Vector3D(-position.X, -position.Y, -position.Z);
            lightDirection.Normalize();

            _shadowPointLight.Position = position;
            _shadowDirectionalLight.Direction = lightDirection;
        }

        private Point3D CalculateLightPosition()
        {
            double xRad = _lightHorizontalAngle * Math.PI / 180.0;
            double yRad = _lightVerticalAngle * Math.PI / 180.0;

            double x = (Math.Sin(xRad) * Math.Cos(yRad)) * _lightDistance;
            double y = Math.Sin(yRad) * _lightDistance;
            double z = (Math.Cos(xRad) * Math.Cos(yRad)) * _lightDistance;

            return new Point3D(x, y, z);
        }

        private void SetShadowLight(bool isDirectionalLight)
        {
            if (isDirectionalLight)
            {
                if (_currentShadowLight == _shadowDirectionalLight)
                    return;

                _currentShadowLight = _shadowDirectionalLight;
            }
            else
            {
                if (_currentShadowLight == _shadowPointLight)
                    return;

                _currentShadowLight = _shadowPointLight;
            }


            LightsModel3DGroup.Children.Clear();

            if (_ambientLight != null)
                LightsModel3DGroup.Children.Add(_ambientLight);

            if (_currentShadowLight != null)
                LightsModel3DGroup.Children.Add(_currentShadowLight);
        }

        private void OnShowShadowCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_planarShadowRenderingProvider == null)
                return;

            if (ShowShadowCheckBox.IsChecked ?? false)
            {
                if (_planeVisual3D != null)
                {
                    MainViewport.Children.Remove(_planeVisual3D);
                    _planeVisual3D = null;
                }

                if (MainDXViewportView.DXScene.ShadowRenderingProvider == null)
                    MainDXViewportView.DXScene.InitializeShadowRendering(_planarShadowRenderingProvider);
            }
            else
            {
                if (MainDXViewportView.DXScene.ShadowRenderingProvider != null)
                    MainDXViewportView.DXScene.InitializeShadowRendering(null);

                // Because we are rendering the plane with ShadowRenderingProvider,
                // we need to add a new plane to the scene after disabling the ShadowRenderingProvider.
                //
                // NOTE:
                // If we would always have a PlaneVisual3D on the scene, then it would also generate a shadow.
                // To prevent casting a shadow, we could change the rendering queue of the plane.
                // This way it would not be rendered as standard object, but would be rendered specially.
                // For example:
                //_planeVisual3D.SetDXAttribute(DXAttributeType.CustomRenderingQueue, _planarShadowRenderingProvider.ShadowPlaneRenderingQueue);

                _planeVisual3D = new PlaneVisual3D()
                {
                    Size     = new Size(400, 400),
                    Material = new DiffuseMaterial(new ImageBrush(new BitmapImage(new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources/10x10-texture.png")))))
                };

                MainViewport.Children.Add(_planeVisual3D);
            }
        }
    }
}
