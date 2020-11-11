using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
using Ab3d.DirectX;
using Ab3d.DirectX.Materials;
using Ab3d.DirectX.Models;
using Ab3d.Visuals;
using SharpDX;
using Color = System.Windows.Media.Color;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    /// <summary>
    /// Interaction logic for ShadowRenderingSample.xaml
    /// </summary>
    public partial class ShadowRenderingSample : Page
    {
        private const double SceneSize = 1000;

        private TranslateTransform3D _lightTransform;
        private Model3DGroup _lightsModel3DGroup;
        private float _lightHorizontalAngle;
        private float _lightVerticalAngle;
        private float _lightDistance;
        private float _lightRange;
        private DirectionalLight _directionalLight;
        private SpotLight _shadowSpotLight;
        private bool _isShadowEnabled;
        private VarianceShadowRenderingProvider _varianceShadowRenderingProvider;

        private bool _isLightAnimated;
        private DateTime _lastLightAnimationTime;

        private int _shadowMapSize;
        private int _shadowDepthBluringSize;
        private float _shadowThreshold;
        private float _shadowDepthBias;
        private BoxVisual3D _greenBox3D;
        private Model3D _teapotModel;
        private WpfGeometryModel3DNode _teapotSceneNode;

        private bool _isShadowArtifactsMessageBoxShown;

        public ShadowRenderingSample()
        {
            InitializeComponent();

            // Default values can be get before Loaded event
            _shadowMapSize = 512;
            _shadowDepthBluringSize = 4;
            _shadowThreshold = 0.2f;


            CreateCustomScene();
            CreateLights();
            CreateLightSphere();


            _isShadowEnabled = true;

            UpdateCastingShadowLight();


            MainDXViewportView.DXSceneInitialized += delegate(object sender, EventArgs args)
            {
                if (MainDXViewportView.DXScene == null)
                    return; // Probably WPF 3D rendering

                if (MainDXViewportView.DXScene.ShaderQuality == ShaderQuality.Low)
                    LowQualityInfoTextBlock.Visibility = Visibility.Visible; // Show info that shadow rendering is not supported with low quality rendering


                // The DXScene.InitializeShadowRendering method must be called with the VarianceShadowRenderingProvider.
                // The VarianceShadowRenderingProvider controls the rendering of shadows.
                //
                // Variance shadow rendering technique can produce nice shadows with soft edges and without the artifacts that are common for some other shadow rendering (Peter panning or Self shadowing).
                // More info can be read in the GPU Gems3 "Chapter 8. Summed-Area Variance Shadow Maps" (https://developer.nvidia.com/gpugems/GPUGems3/gpugems3_ch08.html)
                _varianceShadowRenderingProvider = new VarianceShadowRenderingProvider();


                // ShadowMapSize represents the size of a shadow depth map texture. For example value 512 means that a 512 x 512 texture will be used.
                // The shadow depth map texture is used to store depth information - distance of a pixel from the light.
                // Bigger texture will produce more detailed shadows but will be slower to render.
                // Also, to bigger texture will require bigger blur amount to achieve nice soft edges.
                // NOTE: Changing the ShadowMapSize after the VarianceShadowRenderingProvider has been initialized has no effect - see UpdateShadowSettings method in this file to see how to change the value
                _varianceShadowRenderingProvider.ShadowMapSize = _shadowMapSize;

                // ShadowDepthBluringSize specifies the blur amount that is applied on the shadow depth map and can produce shadows with nice soft edges.
                // Default value is 4.
                _varianceShadowRenderingProvider.ShadowDepthBluringSize = _shadowDepthBluringSize;

                // ShadowThreshold is a float value that helps prevent light bleeding (having areas that should be in shadow fully illuminated) for variance shadow mapping.
                // The value is used to map all shadow values from 0 ... ShadowThreshold to 0 and then linearly rescale the values from ShadowThreshold to 1 into 0 to 1.
                // For more info see "Shadow bleeding" in "Chapter 8. Summed-Area Variance Shadow Maps" (https://developer.nvidia.com/gpugems/GPUGems3/gpugems3_ch08.html)
                _varianceShadowRenderingProvider.ShadowThreshold = _shadowThreshold;
                
                _varianceShadowRenderingProvider.ShadowDepthBias = _shadowDepthBias;


                // Initialize shadow rendering
                MainDXViewportView.DXScene.InitializeShadowRendering(_varianceShadowRenderingProvider);



                // When the scene is initialized, we can get the SceneNode that is created from WPF teapot model.
                // This will be used later (in OnObjectsFilterValueChanged) to enable or disable casting and receiving shadow.
                _teapotSceneNode = MainDXViewportView.GetSceneNodeForWpfObject(_teapotModel) as WpfGeometryModel3DNode;


                // To use FilterObjectsFunction to filter which objects are casting and receiving shadow, uncomment the lines below (see comment in OnObjectsFilterValueChanged method for more info):
                //_varianceShadowRenderingProvider.RenderShadowDepthRenderingStep.FilterObjectsFunction = FilterNonTeapotObjectsFunction;
                //_varianceShadowRenderingProvider.RenderNonShadowObjectsRenderingStep.FilterObjectsFunction = FilterTeapotObjectsFunction;
                //MainDXViewportView.DXScene.DefaultRenderObjectsRenderingStep.FilterObjectsFunction = FilterNonTeapotObjectsFunction;

                // IMPORTANT:
                // Af least one light need to set to cast shadow. This can be done with the following code:
                //_directionalLight.SetDXAttribute(DXAttributeType.IsCastingShadow, true);
            };


            StartLightAnimation();

            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                StopLightAnimation();

                if (_varianceShadowRenderingProvider != null)
                    _varianceShadowRenderingProvider.Dispose();

                MainDXViewportView.Dispose();
            };



            this.PreviewKeyDown += OnPreviewKeyDown;

            // This will allow receiving keyboard events
            this.Focusable = true;
            this.Focus();
        }

        private void CreateCustomScene()
        {
            var blueMaterial = new DiffuseMaterial(Brushes.Blue);


            var sphereVisual3D = new SphereVisual3D();
            sphereVisual3D.CenterPosition = new Point3D(200, 20, -80);
            sphereVisual3D.Radius = 20;
            sphereVisual3D.Material = blueMaterial;

            MainDXViewportView.Viewport3D.Children.Add(sphereVisual3D);


            var grayCylinder = new CylinderVisual3D();
            grayCylinder.BottomCenterPosition = new Point3D(200, 0, 100);
            grayCylinder.Radius = 20;
            grayCylinder.Height = 100;
            grayCylinder.Material = new DiffuseMaterial(Brushes.LightGray);

            MainDXViewportView.Viewport3D.Children.Add(grayCylinder);


            for (int x = -300; x < 500; x += 100)
            {
                var yellowBox = new BoxVisual3D();
                yellowBox.CenterPosition = new Point3D(x, 30, 0);
                yellowBox.Size = new Size3D(20, 60, 20);
                yellowBox.Material = new DiffuseMaterial(Brushes.Yellow);

                MainDXViewportView.Viewport3D.Children.Add(yellowBox);
            }


            var readerObj = new Ab3d.ReaderObj();
            _teapotModel = readerObj.ReadModel3D(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\Models\Teapot.obj"));

            var transform3DGroup = new Transform3DGroup();
            transform3DGroup.Children.Add(new ScaleTransform3D(3, 3, 3));
            transform3DGroup.Children.Add(new TranslateTransform3D(-100, -20, 200));
            _teapotModel.Transform = transform3DGroup;

            var teapotVisual3D = new ModelVisual3D();
            teapotVisual3D.Content = _teapotModel;

            MainDXViewportView.Viewport3D.Children.Add(teapotVisual3D);


            var imageBrush = new ImageBrush();
            imageBrush.ImageSource = new BitmapImage(new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources/GrassTexture.jpg")));
            var grassMaterial = new DiffuseMaterial(imageBrush);

            _greenBox3D = new BoxVisual3D();
            _greenBox3D.CenterPosition = new Point3D(0, -2, 0);
            _greenBox3D.Size = new Size3D(SceneSize, 4, SceneSize);
            _greenBox3D.Material = grassMaterial;

            MainDXViewportView.Viewport3D.Children.Add(_greenBox3D);
        }

        // Create a yellow sphere that will represent the light
        private void CreateLightSphere()
        {
            _lightTransform = new TranslateTransform3D(300, 80, 0);

            var lightSphere = new Ab3d.Visuals.SphereVisual3D();
            lightSphere.CenterPosition = new Point3D(0, 0, 0);
            lightSphere.Radius = 8;
            lightSphere.Transform = _lightTransform;

            var materialGroup = new MaterialGroup();
            materialGroup.Children.Add(new DiffuseMaterial(Brushes.Black));
            materialGroup.Children.Add(new EmissiveMaterial(Brushes.Yellow));

            lightSphere.Material = materialGroup;

            MainDXViewportView.Viewport3D.Children.Add(lightSphere);

            UpdateLightSpherePosition();
        }

        private void UpdateLightSpherePosition()
        {
            if (_shadowSpotLight == null || _lightTransform == null)
                return;

            _lightTransform.OffsetX = _shadowSpotLight.Position.X;
            _lightTransform.OffsetY = _shadowSpotLight.Position.Y;
            _lightTransform.OffsetZ = _shadowSpotLight.Position.Z;
        }

        private void CreateLights()
        {
            _lightsModel3DGroup = new Model3DGroup();

            var ambientLight = new System.Windows.Media.Media3D.AmbientLight(Color.FromRgb(25, 25, 25));

            _lightsModel3DGroup.Children.Add(ambientLight);


            _lightHorizontalAngle = 0;
            _lightVerticalAngle = 30;

            _lightDistance = 500;
            _lightRange = 2000;

            _directionalLight = new System.Windows.Media.Media3D.DirectionalLight();

            _shadowSpotLight = new System.Windows.Media.Media3D.SpotLight();
            _shadowSpotLight.InnerConeAngle = 40;
            _shadowSpotLight.OuterConeAngle = 50;


            var modelVisual3D = new ModelVisual3D();
            modelVisual3D.Content = _lightsModel3DGroup;

            MainDXViewportView.Viewport3D.Children.Add(modelVisual3D);

            UpdateLights();
            UpdateCastingShadowLight();
        }

        private void UpdateLights()
        {
            var position = CalculateLightPosition();

            _shadowSpotLight.Position = position;
            _shadowSpotLight.Range = _lightRange;

            // Create direction from position - target position = (0,0,0)
            var lightDirection = new Vector3D(-position.X, -position.Y, -position.Z);
            lightDirection.Normalize();

            _shadowSpotLight.Direction = lightDirection;
            _directionalLight.Direction = lightDirection;

            UpdateLightSpherePosition();
        }

        private void UpdateCastingShadowLight()
        {
            int index = LightTypeComboBox.SelectedIndex;

            if (index == -1 || _shadowSpotLight == null)
                return;
            
            if (index == 0) // Spot light
            {
                // Enable shadow casting for spot light
                _shadowSpotLight.SetDXAttribute(DXAttributeType.IsCastingShadow, _isShadowEnabled);
                _directionalLight.SetDXAttribute(DXAttributeType.IsCastingShadow, false);

                // Use only _shadowSpotLight
                if (_lightsModel3DGroup.Children.Contains(_directionalLight))
                    _lightsModel3DGroup.Children.Remove(_directionalLight);

                if (!_lightsModel3DGroup.Children.Contains(_shadowSpotLight))
                    _lightsModel3DGroup.Children.Add(_shadowSpotLight);
            }
            else // Directional light
            {
                // Enable shadow casting for directional light
                _directionalLight.SetDXAttribute(DXAttributeType.IsCastingShadow, _isShadowEnabled);
                _shadowSpotLight.SetDXAttribute(DXAttributeType.IsCastingShadow, false);

                // Use only _directionalLight
                if (_lightsModel3DGroup.Children.Contains(_shadowSpotLight))
                    _lightsModel3DGroup.Children.Remove(_shadowSpotLight);

                if (!_lightsModel3DGroup.Children.Contains(_directionalLight))
                    _lightsModel3DGroup.Children.Add(_directionalLight);
            }
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

        private void UpdateSelectedValues()
        {
            _isShadowEnabled = EnableShadowsCheckBox.IsChecked ?? false;

            string sizeText = GetSelectedText(SizeComboBox);
            string[] sizeTextParts = sizeText.Split('x');
            _shadowMapSize = Int32.Parse(sizeTextParts[0]);

            
            string blurText = GetSelectedText(BlurAmountComboBox);
            _shadowDepthBluringSize = Int32.Parse(blurText);

            
            string shadowThresholdText = GetSelectedText(ShadowThresholdComboBox);
            _shadowThreshold = Single.Parse(shadowThresholdText.Substring(0, 3), NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);


            string shadowDepthBiasText = GetSelectedText(ShadowDepthBiasComboBox);
            
            if (shadowDepthBiasText.Contains(' '))
                shadowDepthBiasText = shadowDepthBiasText.Substring(0, shadowDepthBiasText.IndexOf(' ') - 1); // Removed " (default)"

            _shadowDepthBias = Single.Parse(shadowDepthBiasText, NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);

            UpdateShadowSettings();
        }

        private string GetSelectedText(ComboBox comboBox)
        {
            var comboBoxItem = (ComboBoxItem)comboBox.SelectedItem;
            return (string)comboBoxItem.Content;
        }

        private void UpdateShadowSettings()
        {
            if (MainDXViewportView.DXScene == null) // Probably using WPF 3D rendering
                return;

            // When the ShadowMapSize or ShadowDepthBluringSize is changed,
            // we need to dispose the current VarianceShadowRenderingProvider and create a new one
            if (_varianceShadowRenderingProvider != null &&
                (_varianceShadowRenderingProvider.ShadowMapSize != _shadowMapSize ||
                 _varianceShadowRenderingProvider.ShadowDepthBluringSize != _shadowDepthBluringSize))
            {
                MainDXViewportView.DXScene.InitializeShadowRendering(null);

                _varianceShadowRenderingProvider.Dispose();
                _varianceShadowRenderingProvider = null;
            }

            if (_varianceShadowRenderingProvider == null)
            {
                _varianceShadowRenderingProvider = new VarianceShadowRenderingProvider()
                {
                    ShadowMapSize = _shadowMapSize,
                    ShadowDepthBluringSize = _shadowDepthBluringSize,
                    ShadowThreshold = _shadowThreshold,
                    ShadowDepthBias = _shadowDepthBias
                };

                MainDXViewportView.DXScene.InitializeShadowRendering(_varianceShadowRenderingProvider);
            }
            else
            {
                // ShadowThreshold can be changed without reinitializing the shadow rendering
                _varianceShadowRenderingProvider.ShadowThreshold = _shadowThreshold;
                _varianceShadowRenderingProvider.ShadowDepthBias = _shadowDepthBias;
            }

            UpdateCastingShadowLight();


            MainDXViewportView.Refresh(); // Render the scene again
        }


        private void OnPreviewKeyDown(object sender, KeyEventArgs keyEventArgs)
        {
            bool isChanged = false;
            float stepSize = 5;

            switch (keyEventArgs.Key)
            {
                case Key.Up:
                    _lightVerticalAngle += stepSize;
                    isChanged = true;
                    break;

                case Key.Down:
                    _lightVerticalAngle -= stepSize;
                    isChanged = true;
                    break;


                case Key.Left:
                    _lightHorizontalAngle -= stepSize;
                    isChanged = true;
                    break;

                case Key.Right:
                    _lightHorizontalAngle += stepSize;
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

        private void OnShadowSettingsSelectedValueChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            UpdateSelectedValues();
        }

        private void OnObjectsFilterValueChanged(object sender, RoutedEventArgs e)
        {
            if (_teapotSceneNode == null)
                return;

            // _teapotSceneNode is defined as WpfGeometryModel3DNode.
            // That class implements both IShadowCastingNode, IShadowReceivingNode interfaces 
            // and therefore supports both IsCastingShadow and IsReceivingShadow properties.
            // We can use those two properties to control if object cast and receive shadow.
            //
            // Note that not all objects implement IShadowCastingNode, IShadowReceivingNode interfaces.
            // The following SceneNodes objects implement those two interfaces:
            // - WpfGeometryModel3DNode and WpfOptimizedModel3DGroupNode (the later is used for frozen Model3DGroups) implements both IShadowCastingNode and IShadowReceivingNode (note that when only IsReceivingShadow is true but IsCastingShadow is false this can produce some artifacts)
            // - InstancedMeshGeometry3DNode and InstancedModel3DGroupNode implement only IShadowCastingNode - by default IShadowCastingNode is set to true, but you can disable that.
            // - ScreenSpaceLineNode implements only IShadowCastingNode - by default it is set to false so hardware accelerated 3D lines by default do not cast a shadow.


            _teapotSceneNode.IsCastingShadow   = TeapotIsCastingShadowCheckBox.IsChecked ?? false;
            _teapotSceneNode.IsReceivingShadow = TeapotIsReceivingShadowCheckBox.IsChecked ?? false;

            // Notify the SceneNode that we have changed some settings
            _teapotSceneNode.NotifySceneNodeChange(SceneNode.SceneNodeDirtyFlags.OtherChange);


            // NOTE:
            //
            // Instead of using IsCastingShadow and IsReceivingShadow, it is also possible to filter which objects get shadow and which not
            // by using FilterObjectsFunction in rendering steps that render shadow depth and shadow objects.
            // 
            // When shadow rendering is enabled, some additional rendering steps are added to the rendering pipeline.
            // The new steps can be accessed with _varianceShadowRenderingProvider object.
            //
            // By default the following FilterObjectsFunction are used (the source for those functions is listed below):
            //_varianceShadowRenderingProvider.RenderShadowDepthRenderingStep.FilterObjectsFunction      = FilterShadowCastingObjectsFunction;
            //_varianceShadowRenderingProvider.RenderNonShadowObjectsRenderingStep.FilterObjectsFunction = FilterNonShadowReceivingObjectsFunction;
            //MainDXViewportView.DXScene.DefaultRenderObjectsRenderingStep.FilterObjectsFunction         = FilterShadowReceivingObjectsFunction;

            // What this code does?
            // The RenderShadowDepthRenderingStep step renders shadow map - there only objects that cast shadow are rendered.
            // The RenderNonShadowObjectsRenderingStep renders all objects that do not receive shadow - there FilterNonShadowReceivingObjectsFunction filter is used.
            // The DXScene.DefaultRenderObjectsRenderingStep is the standard rendering function that renders objects that receive shadow - there FilterShadowReceivingObjectsFunction is used.

            // To see an example of using FilterObjectsFunction, uncomment the last 3 lines of code in the DXSceneInitialized event handler (in constructor).
            // This prevent teapot from casting and receiving shadow.
        }

        private bool FilterNonTeapotObjectsFunction(RenderablePrimitiveBase renderablePrimitiveBase)
        {
            var renderablePrimitive = renderablePrimitiveBase as RenderablePrimitive;
            if (renderablePrimitive != null)
            {
                return renderablePrimitive.OriginalObject != _teapotSceneNode;
            }

            return true;
        }

        private bool FilterTeapotObjectsFunction(RenderablePrimitiveBase renderablePrimitiveBase)
        {
            var renderablePrimitive = renderablePrimitiveBase as RenderablePrimitive;
            if (renderablePrimitive != null)
            {
                return renderablePrimitive.OriginalObject == _teapotSceneNode;
            }

            return false;
        }

        private static bool FilterNonShadowReceivingObjectsFunction(RenderablePrimitiveBase renderablePrimitiveBase)
        {
            return !renderablePrimitiveBase.IsReceivingShadow;
        }

        private static bool FilterShadowCastingObjectsFunction(RenderablePrimitiveBase renderablePrimitiveBase)
        {
            return renderablePrimitiveBase.IsCastingShadow;
        }

        private static bool FilterShadowReceivingObjectsFunction(RenderablePrimitiveBase renderablePrimitiveBase)
        {
            return renderablePrimitiveBase.IsReceivingShadow;
        }


        private void OuterConeSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!this.IsLoaded)
                return;

            _shadowSpotLight.OuterConeAngle = OuterConeSlider.Value;
            _shadowSpotLight.InnerConeAngle = OuterConeSlider.Value - 10;
        }

        private void LightTypeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            UpdateCastingShadowLight();

            if (LightTypeComboBox.SelectedIndex != 0)
            {
                SpotLightConeTitleTextBlock.Visibility = Visibility.Collapsed;
                OuterConeSlider.Visibility = Visibility.Collapsed;
            }
            else
            {
                SpotLightConeTitleTextBlock.Visibility = Visibility.Visible;
                OuterConeSlider.Visibility = Visibility.Visible;
            }
        }

        private void ChangeBoxSizeButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_greenBox3D.Size.X > 1000)
                _greenBox3D.Size = new Size3D(SceneSize, _greenBox3D.Size.Y, SceneSize); // Set size back to initial size
            else
                _greenBox3D.Size = new Size3D(SceneSize * 2, _greenBox3D.Size.Y, SceneSize * 2); // Increase size of the scene with increasing size of green box 
        }

        private void StartAnimateLightButton_OnClick(object sender, RoutedEventArgs e)
        {
            StartLightAnimation();
        }

        private void StopAnimateLightButton_OnClick(object sender, RoutedEventArgs e)
        {
            StopLightAnimation();
        }


        private void StartLightAnimation()
        {
            if (_isLightAnimated)
                return;

            _lastLightAnimationTime = DateTime.Now;
            CompositionTarget.Rendering += CompositionTargetOnRendering;

            _isLightAnimated = true;

            StartAnimateLightButton.Visibility = Visibility.Collapsed;
            StopAnimateLightButton.Visibility = Visibility.Visible;
        }
        
        private void StopLightAnimation()
        {
            if (!_isLightAnimated)
                return;

            CompositionTarget.Rendering -= CompositionTargetOnRendering;
            _isLightAnimated = false;

            StopAnimateLightButton.Visibility = Visibility.Collapsed;
            StartAnimateLightButton.Visibility = Visibility.Visible;
        }

        private void CompositionTargetOnRendering(object sender, EventArgs eventArgs)
        {
            var now = DateTime.Now;
            double elapsedSeconds = (now - _lastLightAnimationTime).TotalSeconds;

            _lastLightAnimationTime = now;

            _lightHorizontalAngle -= (float)elapsedSeconds * 30;
            UpdateLights();
        }

        private void ShowArtifactsButton_OnClick(object sender, RoutedEventArgs e)
        {
            StopLightAnimation();

            LightTypeComboBox.SelectedIndex = 1; // Use Directional light
            SizeComboBox.SelectedIndex      = 3; // Set map size to 1024 x 1024

            _lightHorizontalAngle = 85;
            _lightVerticalAngle   = 30;
            _lightDistance        = 500;

            UpdateLights();


            Camera1.TargetPosition = new Point3D(400, 5, 0);
            Camera1.Offset         = new Vector3D(0, 0, 0);

            Camera1.Heading  = -70;
            Camera1.Attitude = -20;
            Camera1.Distance = 150;


            if (!_isShadowArtifactsMessageBoxShown)
            {
                MessageBox.Show("Change the value of the \"Shadow depth bias\" ComboBox to remove the artifacts.\r\n\r\nNote that the correct value depends on the size of 3D scene and do not work for every scene.", "Shadow depth bias", MessageBoxButton.OK, MessageBoxImage.Information);
                _isShadowArtifactsMessageBoxShown = true;
            }
        }
    }
}
