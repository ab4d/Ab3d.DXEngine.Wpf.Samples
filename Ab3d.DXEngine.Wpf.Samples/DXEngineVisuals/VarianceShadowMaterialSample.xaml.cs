using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Ab3d.Common;
using Ab3d.Common.Cameras;
using Ab3d.DirectX;
using Ab3d.DirectX.Materials;
using Ab3d.Visuals;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    /// <summary>
    /// Interaction logic for VarianceShadowMaterialSample.xaml
    /// </summary>
    public partial class VarianceShadowMaterialSample : Page
    {
        private PlaneVisual3D _shadowPlaneVisual3D;
        private RectangleVisual3D _shadowEdgeRectangleVisual3D;

        private int _shadowMapSize;
        private int _shadowDepthBluringSize;
        private float _shadowThreshold;
        private float _shadowDepthBias = 0;
        private VarianceShadowRenderingProvider _varianceShadowRenderingProvider;

        private ReaderObj _readerObj;
        private DiffuseMaterial _shadowDiffuseMaterial;
        private VarianceShadowMaterial _shadowDxMaterial;

        private Light _shadowLight;

        private DisposeList _disposables;

        private int _currentModelIndex;

        private readonly string[] _modelNames = new string[] { "dragon_vrip_res3.obj", "bun_zipper_res3.obj", "Teapot.obj" };

        public VarianceShadowMaterialSample()
        {
            InitializeComponent();


            _disposables = new DisposeList();

            MainDXViewportView.DXSceneInitialized += delegate(object sender, EventArgs args)
            {
                if (MainDXViewportView.DXScene != null)
                {
                    // First setup VarianceShadowRenderingProvider - see ShadowRenderingSample for more info
                    _varianceShadowRenderingProvider = new VarianceShadowRenderingProvider();

                    _shadowMapSize = 512;
                    _shadowDepthBluringSize = 8;
                    _shadowThreshold = 0.4f;

                    _varianceShadowRenderingProvider.ShadowMapSize = _shadowMapSize;
                    _varianceShadowRenderingProvider.ShadowDepthBluringSize = _shadowDepthBluringSize;
                    _varianceShadowRenderingProvider.ShadowThreshold = _shadowThreshold;
                    _varianceShadowRenderingProvider.ShadowDepthBias = _shadowDepthBias;

                    // Add a shadow light:
                    var lightDirection = new Vector3D(0.1, -1, -0.5);
                    lightDirection.Normalize();

                    var shadowDirectionalLight = new DirectionalLight(Colors.White, lightDirection);
                    shadowDirectionalLight.SetDXAttribute(DXAttributeType.IsCastingShadow, true);

                    _shadowLight = shadowDirectionalLight;
                    LightsModel3DGroup.Children.Add(shadowDirectionalLight);

                    Camera1.ShowCameraLight = ShowCameraLightType.Never; // prevent adding camera's light

                    // Initialize shadow rendering
                    MainDXViewportView.DXScene.InitializeShadowRendering(_varianceShadowRenderingProvider);


                    //var sphereVisual3D = new SphereVisual3D()
                    //{
                    //    CenterPosition = shadowDirectionalLight.Position,
                    //    Radius = 5,
                    //    Material = new EmissiveMaterial(Brushes.Yellow)
                    //};

                    //MainViewport.Children.Add(sphereVisual3D);


                    //var lineVisual3D = new LineVisual3D()
                    //{
                    //    StartPosition = shadowDirectionalLight.Position,
                    //    EndPosition = (shadowDirectionalLight.Direction * 50).ToPoint3D(),
                    //    LineColor = Colors.Yellow,
                    //    LineThickness = 3,
                    //    EndLineCap = LineCap.ArrowAnchor
                    //};

                    //MainViewport.Children.Add(lineVisual3D);


                    // Now create PlaneVisual3D that use VarianceShadowMaterial
                    // Note that to set DXEngine's VarianceShadowMaterial to WPF's material, 
                    // we need to create a DiffuseMaterial and then call SetUsedDXMaterial

                    double shadowPlaneSize = ShadowPlaneSlider.Value;

                    _shadowDiffuseMaterial = new DiffuseMaterial(Brushes.Green);
                    _shadowDxMaterial = new Ab3d.DirectX.Materials.VarianceShadowMaterial() { ShadowDarkness = 0.5f };

                    _shadowDiffuseMaterial.SetUsedDXMaterial(_shadowDxMaterial);

                    _disposables.Add(_shadowDxMaterial);


                    _shadowPlaneVisual3D = new PlaneVisual3D("ShadowMaterialPlane")
                    {
                        CenterPosition = new Point3D(0, -DistanceSlider.Value, 0),
                        Size = new Size(shadowPlaneSize, shadowPlaneSize),
                        Material = _shadowDiffuseMaterial,
                        BackMaterial = _shadowDiffuseMaterial,
                    };

                    // Disable casting shadow by _shadowPlaneVisual3D
                    // This is not needed in this sample but is a good practice and also shows how to do that
                    _shadowPlaneVisual3D.SetDXAttribute(DXAttributeType.IsCastingShadow, false);

                    MainViewport.Children.Add(_shadowPlaneVisual3D);


                    // Add RectangleVisual3D that will show where is the edge of the PlaneVisual3D

                    _shadowEdgeRectangleVisual3D = new RectangleVisual3D("ShadowEdgeRectangle")
                    {
                        //Position = new Point3D(), // Position is set when calling UpdateShadowEdgePosition 
                        Size = _shadowPlaneVisual3D.Size,
                        WidthDirection = new Vector3D(1, 0, 0),
                        HeightDirection = new Vector3D(0, 0, 1),
                        LineColor = Colors.Black,
                        LineThickness = 2,
                        IsVisible = ShowShadowEdgeCheckBox.IsChecked ?? false
                    };

                    UpdateShadowEdgePosition();

                    // Disable casting and receiving shadow by _shadowEdgeRectangleVisual3D
                    _shadowEdgeRectangleVisual3D.SetDXAttribute(DXAttributeType.IsCastingShadow, false);
                    _shadowEdgeRectangleVisual3D.SetDXAttribute(DXAttributeType.IsReceivingShadow, false);

                    MainViewport.Children.Add(_shadowEdgeRectangleVisual3D);
                }

                LoadModel(_modelNames[0]);
            };

            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                _disposables.Dispose();
                MainDXViewportView.Dispose();
            };
        }

        private void UpdateShadowEdgePosition()
        {
            if (_shadowPlaneVisual3D == null || _shadowEdgeRectangleVisual3D == null)
                return;

            _shadowEdgeRectangleVisual3D.Position = new Point3D(_shadowPlaneVisual3D.CenterPosition.X - 0.5 * _shadowPlaneVisual3D.Size.Width,
                                                                _shadowPlaneVisual3D.CenterPosition.Y,
                                                                _shadowPlaneVisual3D.CenterPosition.Z + 0.5 * _shadowPlaneVisual3D.Size.Height);
        }
         
        private void LoadNextModel()
        {
            _currentModelIndex = (_currentModelIndex + 1) % _modelNames.Length;
            LoadModel(_modelNames[_currentModelIndex]);
        }

        private void LoadModel(string modelFileName)
        {
            string dragonModelFileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources\\Models\\" + modelFileName);

            if (_readerObj == null)
                _readerObj = new Ab3d.ReaderObj();

            var readModel3D = _readerObj.ReadModel3D(dragonModelFileName);

            Ab3d.Utilities.ModelUtils.PositionAndScaleModel3D(readModel3D, new Point3D(0, 0, 0), PositionTypes.Bottom, new Size3D(200, 200, 200));

            SampleObjectsVisual3D.Children.Clear();
            SampleObjectsVisual3D.Children.Add(readModel3D.CreateModelVisual3D());
        }
        
        private void OnShowShadowEdgeCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_shadowEdgeRectangleVisual3D == null)
                return;

            _shadowEdgeRectangleVisual3D.IsVisible = ShowShadowEdgeCheckBox.IsChecked ?? false;
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

        private void NextModelButton_OnClick(object sender, RoutedEventArgs e)
        {
            LoadNextModel();
        }

        private void ShadowDarknessSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_shadowDxMaterial == null)
                return;

            _shadowDxMaterial.ShadowDarkness = (float)ShadowDarknessSlider.Value;

            var sceneNode = MainDXViewportView.GetSceneNodeForWpfObject(_shadowPlaneVisual3D);
            if (sceneNode != null)
                sceneNode.NotifySceneNodeChange(SceneNode.SceneNodeDirtyFlags.MaterialChanged);
        }
        
        private void DistanceSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_shadowPlaneVisual3D == null)
                return;

            _shadowPlaneVisual3D.CenterPosition = new Point3D(0, -DistanceSlider.Value, 0);
            UpdateShadowEdgePosition();
        }
        
        private void ShadowPlaneSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_shadowPlaneVisual3D == null)
                return;

            var newSize = ShadowPlaneSlider.Value;
            _shadowPlaneVisual3D.Size = new Size(newSize, newSize);

            _shadowEdgeRectangleVisual3D.Size = _shadowPlaneVisual3D.Size;
            UpdateShadowEdgePosition();
        }

        private void OnShadowSettingsSelectedValueChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            UpdateSelectedValues();
        }

        private void UpdateSelectedValues()
        {
            string sizeText = GetSelectedText(SizeComboBox);
            string[] sizeTextParts = sizeText.Split('x');
            _shadowMapSize = Int32.Parse(sizeTextParts[0]);

            string blurText = GetSelectedText(BlurAmountComboBox);
            _shadowDepthBluringSize = Int32.Parse(blurText);

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

            MainDXViewportView.Refresh(); // Render the scene again
        }

        private void OnMaterialTypeCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            if (UseVarianceShadowMaterialRadioButton.IsChecked ?? false)
                _shadowPlaneVisual3D.Material = _shadowDiffuseMaterial;
            else if (UseGreenDiffuseMaterialRadioButton.IsChecked ?? false)
                _shadowPlaneVisual3D.Material = new DiffuseMaterial(Brushes.Green);
            else
                _shadowPlaneVisual3D.Material = new DiffuseMaterial(Brushes.White);
        }

        private void OnLightTypeCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            if (UseDirectionalLightRadioButton.IsChecked ?? false)
            {
                var shadowDirectionalLight = new DirectionalLight(Colors.White, direction: new Vector3D(0.1, -1, -0.5));
                shadowDirectionalLight.SetDXAttribute(DXAttributeType.IsCastingShadow, true);

                LightsModel3DGroup.Children.Clear();
                LightsModel3DGroup.Children.Add(shadowDirectionalLight);

                _shadowLight = shadowDirectionalLight;
            }
            else
            {
                var shadowSpotLight = new SpotLight(Colors.White, 
                                                    position: new Point3D(-80, 300, 120), 
                                                    direction: new Vector3D(0.1, -1, -0.5), 
                                                    outerConeAngle: 30, 
                                                    innerConeAngle: 25);

                shadowSpotLight.SetDXAttribute(DXAttributeType.IsCastingShadow, true);

                LightsModel3DGroup.Children.Clear();
                LightsModel3DGroup.Children.Add(shadowSpotLight);

                _shadowLight = shadowSpotLight;
            }
        }
    }
}
