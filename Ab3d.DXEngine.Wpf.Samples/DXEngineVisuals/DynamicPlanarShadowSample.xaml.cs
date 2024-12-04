using Ab3d.Common.Cameras;
using Ab3d.DirectX;
using Ab3d.DirectX.Materials;
using Ab3d.Utilities;
using Ab3d.Visuals;
using SharpDX.Direct3D11;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
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
using SharpDX.DXGI;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    /// <summary>
    /// Interaction logic for DynamicPlanarShadowSample.xaml
    /// </summary>
    public partial class DynamicPlanarShadowSample : Page, ICompositionRenderingSubscriber
    {
        private PointLight _shadowPointLight;
        private DirectionalLight _shadowDirectionalLight;
        private AmbientLight _ambientLight;

        private Light _currentShadowLight;
        
        private double _lightVerticalAngle;
        private double _lightHorizontalAngle;
        private double _lightDistance;

        private PlaneVisual3D _shadowPlaneVisual3D;

        private int _shadowMapSize = 256;
        private int _blurFilterSize = 15;
        private float _blurAmount = 2;
        private float _maxShadowDistance = 100;
        private float _shadowDarkness = 1;
        private bool _isTransparentShadow = true;

        private DynamicPlanarShadowRenderingProvider _dynamicPlanarShadowRenderingProvider;

        private DateTime _lastAnimationTime;

        private const double HalfAnimationHeight = 30;

        private DisposeList _disposables;
        private AxisAngleRotation3D _torusRotation;
        private float _shadowPlaneSize;
        private DiffuseMaterial _shadowDiffuseMaterial;
        private RectangleVisual3D _shadowEdgeRectangleVisual3D;

        public DynamicPlanarShadowSample()
        {
            InitializeComponent();


            _disposables = new DisposeList();

            MainDXViewportView.DXSceneInitialized += delegate(object sender, EventArgs args)
            {
                if (MainDXViewportView.DXScene != null)
                {
                    _shadowPlaneSize = 200;

                    _shadowPlaneVisual3D = new PlaneVisual3D()
                    {
                        CenterPosition = new Point3D(0, -0.1f, 0),
                        Size = new Size(_shadowPlaneSize, _shadowPlaneSize),
                        //Normal = new Vector3D(0, 1, 0),           // This is already the default value - if a different value is used, then also change the _dynamicPlanarShadowRenderingProvider.PlaneNormal
                        //HeightDirection = new Vector3D(0, 0, -1), // This is already the default value - if a different value is used, then also change the _dynamicPlanarShadowRenderingProvider.PlaneHeightDirectionl
                        // Material is set in InitializeDynamicPlanarShadow method
                    };

                    // Disable casting shadow by _shadowPlaneVisual3D
                    // This is not needed in this sample but is a good practice and also shows how to do that
                    _shadowPlaneVisual3D.SetDXAttribute(DXAttributeType.IsCastingShadow, false);

                    MainViewport.Children.Add(_shadowPlaneVisual3D);


                    // Add RectangleVisual3D that will show where is the edge of the PlaneVisual3D

                    _shadowEdgeRectangleVisual3D = new RectangleVisual3D("ShadowEdgeRectangle")
                    {
                        Position = new Point3D(_shadowPlaneVisual3D.CenterPosition.X - 0.5 * _shadowPlaneVisual3D.Size.Width,
                                               _shadowPlaneVisual3D.CenterPosition.Y,
                                               _shadowPlaneVisual3D.CenterPosition.Z + 0.5 * _shadowPlaneVisual3D.Size.Height),
                        Size = _shadowPlaneVisual3D.Size,
                        WidthDirection = new Vector3D(1, 0, 0),
                        HeightDirection = new Vector3D(0, 0, 1),
                        LineColor = Colors.Black,
                        LineThickness = 2,
                        IsVisible = ShowShadowEdgeCheckBox.IsChecked ?? false
                    };

                    // Disable casting and receiving shadow by _shadowEdgeRectangleVisual3D
                    _shadowEdgeRectangleVisual3D.SetDXAttribute(DXAttributeType.IsCastingShadow, false);
                    _shadowEdgeRectangleVisual3D.SetDXAttribute(DXAttributeType.IsReceivingShadow, false);

                    MainViewport.Children.Add(_shadowEdgeRectangleVisual3D);


                    InitializeDynamicPlanarShadow();
                }

                Camera1.ShowCameraLight = ShowCameraLightType.Auto;

                CreateSampleObjects();
            };

            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                StopAnimation();

                _disposables.Dispose();
                MainDXViewportView.Dispose();
            };
        }

        private void InitializeDynamicPlanarShadow()
        {
            if (MainDXViewportView.DXScene == null)
                return;

            _dynamicPlanarShadowRenderingProvider = new DynamicPlanarShadowRenderingProvider(shadowMapWidth: _shadowMapSize, shadowMapHeight: _shadowMapSize); // 256 is also the default shadow map size

            // Set where the shadow will start - ShadowCenterPosition is a 3D position
            // where the shadow camera will be positioned to render the shadow map - distance of the objects from the camera.
            _dynamicPlanarShadowRenderingProvider.ShadowCenterPosition = _shadowPlaneVisual3D.CenterPosition.ToVector3() + new Vector3(0, 0.1f, 0);  // (0,0,0) is also the default position

            // PlaneNormal and PlaneHeightDirection define the 2 vectors that are used to define the PlaneVisual3D that will show the shadow.
            // The following two values are default (written here for demonstration).
            _dynamicPlanarShadowRenderingProvider.PlaneNormal          = _shadowPlaneVisual3D.Normal.ToVector3();          // default value: new Vector3(0, 1, 0);
            _dynamicPlanarShadowRenderingProvider.PlaneHeightDirection = _shadowPlaneVisual3D.HeightDirection.ToVector3(); // default value: new Vector3(0, 0, -1);
            _dynamicPlanarShadowRenderingProvider.ShadowWorldSize      = new Vector2((float)_shadowPlaneVisual3D.Size.Width, (float)_shadowPlaneVisual3D.Size.Height);

            // If we render shadow for only a part of the scene, then we can set the ShadowWorldSize
            // When it is not set, then scene's bounding box is used.
            //_dynamicPlanarShadowRenderingProvider.ShadowWorldSize = new Vector2(100, 100);
            
            _dynamicPlanarShadowRenderingProvider.BlurAmount               = _blurAmount;
            _dynamicPlanarShadowRenderingProvider.ShadowVisibilityDistance = _maxShadowDistance;
            _dynamicPlanarShadowRenderingProvider.BlurFilterSize           = _blurFilterSize;
            _dynamicPlanarShadowRenderingProvider.ShadowDarkness           = _shadowDarkness; // fully opaque black; when lower than 1 and IsTransparentShadow is true, then alpha is lower
            _dynamicPlanarShadowRenderingProvider.IsTransparentShadow      = _isTransparentShadow;

            // Initialize shadow rendering
            MainDXViewportView.DXScene.InitializeShadowRendering(_dynamicPlanarShadowRenderingProvider);

            _disposables.Add(_dynamicPlanarShadowRenderingProvider);


            _shadowDiffuseMaterial = new DiffuseMaterial(Brushes.Green);

            var shadowDXMaterial = new StandardMaterial()
            {
                // Set ShaderResourceView into array of diffuse textures
                DiffuseTextures   = new ShaderResourceView[] { _dynamicPlanarShadowRenderingProvider.ShadowShaderResourceView },
                TextureBlendState = MainDXViewportView.DXScene.DXDevice.CommonStates.GetRecommendedBlendState(hasTransparency: true, hasPreMultipliedAlpha: true),
                HasTransparency = true,

                // When showing texture, the DiffuseColor represents a color mask - each color from texture is multiplied with DiffuseColor (White preserves the original color)
                DiffuseColor = Color3.White
            };

            _disposables.Add(shadowDXMaterial);

            _shadowDiffuseMaterial.SetUsedDXMaterial(shadowDXMaterial);


            if (_shadowPlaneVisual3D != null)
            {
                _shadowPlaneVisual3D.Material = _shadowDiffuseMaterial;

                if (ShowShadowBackMaterialCheckBox.IsChecked ?? false)
                    _shadowPlaneVisual3D.BackMaterial = _shadowDiffuseMaterial;
            }
        }
        
        private void CreateSampleObjects()
        {
            SampleObjectsVisual3D.Children.Clear();

            var silverMaterial = new DiffuseMaterial(Brushes.Silver);

            for (int i = 0; i < 6; i++)
            {
                double t = (double)i / 5.0;

                var sphereVisual3D = new SphereVisual3D()
                {
                    CenterPosition = new Point3D(-70 + i * 30, HalfAnimationHeight + 5, 0),
                    Radius = 12,
                    Material = silverMaterial,
                    Tag = t, // This is used for animation
                    Transform = new TranslateTransform3D(0, GetAnimatedHeight(t), 0)
                };

                SampleObjectsVisual3D.Children.Add(sphereVisual3D);
            }

            
            for (int i = 0; i < 6; i++)
            {
                double t = (double)i / 5.0;

                var boxVisual3D = new BoxVisual3D()
                {
                    CenterPosition = new Point3D(-70 + i * 30, HalfAnimationHeight + 5, -60),
                    Size = new Size3D(24, 24, 24),
                    Material = silverMaterial,
                    Tag = t, // This is used for animation
                    Transform = new TranslateTransform3D(0, GetAnimatedHeight(t), 0)
                };

                SampleObjectsVisual3D.Children.Add(boxVisual3D);
            }


            _torusRotation = new AxisAngleRotation3D(new Vector3D(0, 0, 1), angle: 0);

            var torusKnotVisual3D = new TorusKnotVisual3D()
            {
                CenterPosition = new Point3D(20, 50, 70),
                P = 3,
                Q = 2,
                Radius1 = 25,
                Radius2 = 10,
                Radius3 = 5,
                USegments = 300,
                VSegments = 30,
                Material = silverMaterial,
            };

            torusKnotVisual3D.Transform = new RotateTransform3D(_torusRotation, center: torusKnotVisual3D.CenterPosition);

            SampleObjectsVisual3D.Children.Add(torusKnotVisual3D);


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
            if (!IsAnimatingCheckBox.IsChecked ?? false)
            {
                _lastAnimationTime = DateTime.MinValue;
                return;
            }

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
            // convert t that goes from 0 ... 1 to go from -PI/2 ... +PI/2
            return Math.Sin((t - 0.5) * Math.PI) * HalfAnimationHeight + HalfAnimationHeight * 0.5;
        }

        private void AnimateAllObjects(double dt)
        {
            foreach (var visual3D in SampleObjectsVisual3D.Children.OfType<BaseModelVisual3D>())
            {
                if (visual3D is TorusKnotVisual3D)
                    continue;

                var translateTransform3D = visual3D.Transform as TranslateTransform3D;

                if (!(visual3D.Tag is double) || translateTransform3D == null)
                    continue;

                var t = (double) visual3D.Tag;

                t += dt;

                visual3D.Tag = t;

                translateTransform3D.OffsetY = GetAnimatedHeight(t);
            }

            _torusRotation.Angle += dt * 90; // 90 degrees per second
        }

        private void RecreateDynamicPlanarShadowRenderingProvider()
        {
            if (_dynamicPlanarShadowRenderingProvider != null)
                _dynamicPlanarShadowRenderingProvider.Dispose();

            InitializeDynamicPlanarShadow();
        }

        private string GetSelectedText(ComboBox comboBox)
        {
            var comboBoxItem = (ComboBoxItem)comboBox.SelectedItem;
            return (string)comboBoxItem.Content;
        }

        private void UpdateSettings()
        {
            if (_dynamicPlanarShadowRenderingProvider == null)
                return;

            string comboBoxText = GetSelectedText(ShadowMapSizeComboBox);
            string[] sizeTextParts = comboBoxText.Split('x');
            _shadowMapSize = Int32.Parse(sizeTextParts[0]);

            comboBoxText = GetSelectedText(BlurFilterSizeComboBox);
            _blurFilterSize = Int32.Parse(comboBoxText);

            _blurAmount = (float)BlurAmountSlider.Value;
            _maxShadowDistance     = (float)MaxDistanceSlider.Value;
            _shadowDarkness        = (float)DarknessSlider.Value;

            _isTransparentShadow = IsTransparentShadowCheckBox.IsChecked ?? false; 
            
            if (_dynamicPlanarShadowRenderingProvider.ShadowMapWidth != _shadowMapSize)
            {
                RecreateDynamicPlanarShadowRenderingProvider();
                return;
            }

            _dynamicPlanarShadowRenderingProvider.BlurAmount               = _blurAmount;
            _dynamicPlanarShadowRenderingProvider.ShadowVisibilityDistance = _maxShadowDistance;
            _dynamicPlanarShadowRenderingProvider.ShadowDarkness           = _shadowDarkness;
            _dynamicPlanarShadowRenderingProvider.BlurFilterSize           = _blurFilterSize;
            _dynamicPlanarShadowRenderingProvider.IsTransparentShadow      = _isTransparentShadow;

            MainDXViewportView.Refresh(); // Render again
        }

        private void ShadowMapSizeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSettings();
        }

        private void BlurFilterSizeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSettings();
        }

        private void BlurAmountSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSettings();
        }
        
        private void DarknessSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSettings();
        }

        private void MaxDistanceSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSettings();
        }

        private void OnIsTransparentShadowCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            UpdateSettings();
        }
        
        private void OnShowPartialShadowCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded || _dynamicPlanarShadowRenderingProvider == null)
                return;

            Point3D shadowCenterPosition;
            Size shadowWorldSize;

            if (ShowPartialShadowCheckBox.IsChecked ?? false)
            {
                // Show shadow only for the upper half of the scene
                shadowCenterPosition = new Point3D(0, 0, -_shadowPlaneSize / 4);
                shadowWorldSize = new Size(_shadowPlaneSize, _shadowPlaneSize / 2);
            }
            else
            {
                // Show the shadow for the whole scene
                shadowCenterPosition = new Point3D(0, 0, 0);
                shadowWorldSize = new Size(_shadowPlaneSize, _shadowPlaneSize);
            }

            // Update the _dynamicPlanarShadowRenderingProvider
            _dynamicPlanarShadowRenderingProvider.ShadowCenterPosition = shadowCenterPosition.ToVector3();
            _dynamicPlanarShadowRenderingProvider.ShadowWorldSize      = new Vector2((float)shadowWorldSize.Width, (float)shadowWorldSize.Height);


            // We also need to update the _shadowPlaneVisual3D
            // If not, then the shadow would be shown on the wrong position and with invalid size.
            _shadowPlaneVisual3D.CenterPosition = shadowCenterPosition - new Vector3D(0, 0.1f, 0); // Move the _shadowPlaneVisual3D just slightly below the shadow's ShadowCenterPosition
            _shadowPlaneVisual3D.Size           = shadowWorldSize;


            // Also update the _shadowEdgeRectangleVisual3D
            _shadowEdgeRectangleVisual3D.Position = new Point3D(shadowCenterPosition.X - 0.5 * shadowWorldSize.Width,
                                                                shadowCenterPosition.Y,
                                                                shadowCenterPosition.Z + 0.5 * shadowWorldSize.Height);

            _shadowEdgeRectangleVisual3D.Size = _shadowPlaneVisual3D.Size;
        }

        private void OnShowShadowBackMaterialCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_shadowPlaneVisual3D == null)
                return;

            if (ShowShadowBackMaterialCheckBox.IsChecked ?? false)
                _shadowPlaneVisual3D.BackMaterial = _shadowDiffuseMaterial;
            else
                _shadowPlaneVisual3D.BackMaterial = null;
        }

        private void OnShowShadowEdgeCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_shadowEdgeRectangleVisual3D == null)
                return;

            _shadowEdgeRectangleVisual3D.IsVisible = ShowShadowEdgeCheckBox.IsChecked ?? false;
        }
        
        private void OnRenderShadowOnEachFrameCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            if (_dynamicPlanarShadowRenderingProvider == null)
                return;

            // To prevent rendering shadow on each frame, just disable the _dynamicPlanarShadowRenderingProvider
            _dynamicPlanarShadowRenderingProvider.IsEnabled = RenderShadowOnEachFrameCheckBox.IsChecked ?? false;
            ManuallyUpdateButton.IsEnabled = !_dynamicPlanarShadowRenderingProvider.IsEnabled;
        }

        private void ManuallyUpdateButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_dynamicPlanarShadowRenderingProvider == null)
                return;

            if (!_dynamicPlanarShadowRenderingProvider.IsEnabled)
            {
                // To update the shadow texture, we can call the EnableForOneFrame.
                // This will enable the DynamicPlanarShadowRenderingProvider only for one frame.
                // This will also mark the shadow as dirty and will render the scene again in the next render pass.
                _dynamicPlanarShadowRenderingProvider.EnableForOneFrame();

                // We could also manually do the following:
                //_dynamicPlanarShadowRenderingProvider.IsEnabled = true;
                //MainDXViewportView.Refresh();
                //_dynamicPlanarShadowRenderingProvider.IsEnabled = false;
            }
        }
    }
}
