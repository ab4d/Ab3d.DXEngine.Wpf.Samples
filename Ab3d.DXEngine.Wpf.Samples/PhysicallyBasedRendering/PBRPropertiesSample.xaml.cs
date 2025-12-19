using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Ab3d.Common.Cameras;
using Ab3d.DirectX;
using Ab3d.DirectX.Materials;

#if SHARPDX
using SharpDX;
using SharpDX.Direct3D11;
#endif


namespace Ab3d.DXEngine.Wpf.Samples.PhysicallyBasedRendering
{
    /// <summary>
    /// Interaction logic for PBRPropertiesSample.xaml
    /// </summary>
    public partial class PBRPropertiesSample : Page
    {
        private DisposeList _disposables;

        private PhysicallyBasedMaterial _physicallyBasedMaterial;

        private DXCubeMap _environmentCubeMap;

        private string _texturesFolder;
        private string _modelsFolder;


        public PBRPropertiesSample()
        {
            InitializeComponent();

            _disposables = new DisposeList();

            _modelsFolder   = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\Models\");
            _texturesFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\BricksMaps\");

            // Load scene when we have the DXDevice ready
            MainDXViewportView.DXSceneInitialized += delegate (object sender, EventArgs args)
            {
                if (MainDXViewportView.DXScene == null) // Probably WPF 3D rendering
                    return;

                CreateSampleScene();

                UpdateEnvironmentMap();
                UpdateLights();
            };

            this.Unloaded += delegate (object sender, RoutedEventArgs args) { Dispose(); };
        }

        private void Dispose()
        {
            _disposables.Dispose();
            _disposables = new DisposeList();
        }
        
        private void CreateSampleScene()
        {
            _physicallyBasedMaterial = new PhysicallyBasedMaterial();

            // We need to dispose the PhysicallyBasedMaterial when this sample is uloaded
            _disposables.Add(_physicallyBasedMaterial);


            UpdateBaseColor();
            UpdateMetalness();
            UpdateRoughness();

            var normalMapShaderResourceView = GetNormalMapShaderResourceView();
            if (normalMapShaderResourceView != null)
                _physicallyBasedMaterial.SetTextureMap(TextureMapTypes.NormalMap, normalMapShaderResourceView, "bricks_normal.png");

            var ambientOcclusionShaderResourceView = AmbientOcclusionShaderResourceView();
            if (ambientOcclusionShaderResourceView != null)
                _physicallyBasedMaterial.SetTextureMap(TextureMapTypes.AmbientOcclusion, ambientOcclusionShaderResourceView, "bricks_ao.png");



            var wpfMaterial = new DiffuseMaterial(Brushes.Red);
            wpfMaterial.SetUsedDXMaterial(_physicallyBasedMaterial);

            ModelPlaceholder.Content = null;
            ModelPlaceholder.Children.Clear();


            var sphereVisual3D = new Ab3d.Visuals.SphereVisual3D()
            {
                CenterPosition = new Point3D(40, 12, 0),
                Radius = 12,
                Segments = 50,
                Material = wpfMaterial,
                FreezeMeshGeometry3D = false,
                UseCachedMeshGeometry3D = false
            };

            ModelPlaceholder.Children.Add(sphereVisual3D);


            var boxVisual3D = new Ab3d.Visuals.BoxVisual3D()
            {
                CenterPosition = new Point3D(-40, 10, 0),
                Size = new Size3D(20, 20, 20),
                Material = wpfMaterial,
                FreezeMeshGeometry3D = false,
                UseCachedMeshGeometry3D = false
            };

            ModelPlaceholder.Children.Add(boxVisual3D);


            var readerObj = new Ab3d.ReaderObj();

            var readModel3D = (GeometryModel3D)readerObj.ReadModel3D(_modelsFolder + "teapot-hires.obj");
            Ab3d.Utilities.ModelUtils.CenterAndScaleModel3D(readModel3D, new Point3D(0, 10, 0), new Size3D(40, 40, 40), true);

            //// This code is called for each GeometryModel3D inside Plane1
            //var tangentVectors = Ab3d.DirectX.Utilities.MeshUtils.CalculateTangentVectors((MeshGeometry3D)readModel3D.Geometry);

            ////// Assign tangent array to the MeshGeometry3D
            //readModel3D.Geometry.SetDXAttribute(DXAttributeType.MeshTangentArray, tangentVectors);



            readModel3D.Material = wpfMaterial;

            //ModelPlaceholder.Content = null;
            ModelPlaceholder.Children.Add(readModel3D.CreateModelVisual3D());



            // Rendering normal (bump) maps require tangent vectors.
            // The following code will generate tangent vectors and assign them to the MeshGeometry3D that form our 3D model.
            // If tangent vectors are not provided, they will be calculated on-demand in the pixel shader (slightly reducing performance).

            Ab3d.Utilities.ModelIterator.IterateGeometryModel3DObjects(ModelPlaceholder, delegate (GeometryModel3D geometryModel3D, Transform3D transform3D)
            {
                // This code is called for each GeometryModel3D inside Plane1
                var tangentVectors = Ab3d.DirectX.Utilities.MeshUtils.CalculateTangentVectors((MeshGeometry3D)geometryModel3D.Geometry);

                // Assign tangent array to the MeshGeometry3D
                geometryModel3D.Geometry.SetDXAttribute(DXAttributeType.MeshTangentArray, tangentVectors);
            });


            Camera1.Distance = 150;

            UpdateLights();
        }

        private ShaderResourceView GetBaseColorShaderResourceView(string textureFileName, out bool hasTransparency, out BlendState recommendedBlendState)
        {
            ShaderResourceView shaderResourceView;

            if (BaseColorTextureCheckBox.IsChecked ?? false)
            {
                TextureInfo textureInfo;
                shaderResourceView = TextureLoader.LoadShaderResourceView(MainDXViewportView.DXScene.DXDevice.Device, _texturesFolder + textureFileName, out textureInfo);
                _disposables.Add(shaderResourceView);

                hasTransparency = textureInfo.HasTransparency;
                recommendedBlendState = MainDXViewportView.DXScene.DXDevice.CommonStates.GetRecommendedBlendState(textureInfo.HasTransparency, textureInfo.HasPremultipliedAlpha);
            }
            else
            {
                shaderResourceView = null;
                hasTransparency = false;
                recommendedBlendState = null;
            }

            return shaderResourceView;
        }

        private ShaderResourceView GetNormalMapShaderResourceView()
        {
            ShaderResourceView loadShaderResourceView;

            if (NormalMapCheckBox.IsChecked ?? false)
            {
                // IMPORTANT:
                // When using TextureLoader.LoadShaderResourceView to load normal texture, we need to set convertTo32bppPRGBA to false to prevent conversion of the texture
                loadShaderResourceView = TextureLoader.LoadShaderResourceView(MainDXViewportView.DXScene.DXDevice.Device, _texturesFolder + "bricks_normal.png", loadDdsIfPresent: false, convertTo32bppPRGBA: false);
                _disposables.Add(loadShaderResourceView);
            }
            else
            {
                loadShaderResourceView = null;
            }

            return loadShaderResourceView;
        }

        private DXCubeMap GetEnvironmentMapCubeMap()
        {
            DXCubeMap usedEnvironmentMapCubeMap;

            if (EnvironmentMapCheckBox.IsChecked ?? false)
            {
                if (_environmentCubeMap == null)
                {
                    string packUriPrefix = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources\\SkyboxTextures\\");

                    //string packUriPrefix = "pack://siteoforigin:,,,/Resources/SkyboxTextures/"; // This throws "InvalidDeployment" exception (internally caught by .Net)
                    //string packUriPrefix = string.Format("pack://application:,,,/{0};component/Resources/SkyboxTextures/", this.GetType().Assembly.GetName().Name); // When textures are defined as Resources

                    // Create DXCubeMap with specifying 6 bitmaps for all sides of the cube
                    _environmentCubeMap = new DXCubeMap(packUriPrefix,
                                                        "CloudyLightRaysRight512.png",
                                                        "CloudyLightRaysLeft512.png",
                                                        "CloudyLightRaysUp512.png",
                                                        "CloudyLightRaysDown512.png",
                                                        "CloudyLightRaysFront512.png",
                                                        "CloudyLightRaysBack512.png");

                    _environmentCubeMap.InitializeResources(MainDXViewportView.DXScene.DXDevice);

                    _disposables.Add(_environmentCubeMap);
                }

                usedEnvironmentMapCubeMap = _environmentCubeMap;
            }
            else
            {
                usedEnvironmentMapCubeMap = null;
            }

            return usedEnvironmentMapCubeMap;
        }

        private ShaderResourceView AmbientOcclusionShaderResourceView()
        {
            ShaderResourceView loadShaderResourceView;

            if (AmbientOcclusionCheckBox.IsChecked ?? false)
            {
                // IMPORTANT:
                // When using TextureLoader.LoadShaderResourceView to load ambient occlusion texture, we need to set convertTo32bppPRGBA to false to prevent conversion of the texture
                loadShaderResourceView = TextureLoader.LoadShaderResourceView(MainDXViewportView.DXScene.Device, _texturesFolder + "bricks_ao.png", loadDdsIfPresent: false, convertTo32bppPRGBA: false);
                _disposables.Add(loadShaderResourceView);
            }
            else
            {
                loadShaderResourceView = null;
            }

            return loadShaderResourceView;
        }

        private void UpdateMetalness()
        {
            _physicallyBasedMaterial.Metalness = (float)MetalnessSlider.Value;
            MetalnessTextBlock.Text = string.Format(System.Globalization.CultureInfo.InvariantCulture, "Metalness: {0:0.00}", _physicallyBasedMaterial.Metalness);
        }

        private void UpdateRoughness()
        {
            _physicallyBasedMaterial.Roughness = (float)RoughnessSlider.Value;
            RoughnessTextBlock.Text = string.Format(System.Globalization.CultureInfo.InvariantCulture, "Roughness: {0:0.00}", _physicallyBasedMaterial.Roughness);
        }


        private void MetalnessSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!this.IsLoaded || MainDXViewportView.DXScene == null)
                return;

            UpdateMetalness();
            MainDXViewportView.DXScene.NotifyChange(DXScene.ChangeNotifications.MaterialPropertiesChanged);
        }

        private void RoughnessSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!this.IsLoaded || MainDXViewportView.DXScene == null)
                return;

            UpdateRoughness();
            MainDXViewportView.DXScene.NotifyChange(DXScene.ChangeNotifications.MaterialPropertiesChanged);
        }

        private void BaseColorTextureCheckBox_OnChecked(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded || MainDXViewportView.DXScene == null)
                return;

            UpdateBaseColor();

            MainDXViewportView.DXScene.NotifyChange(DXScene.ChangeNotifications.MaterialPropertiesChanged);
        }


        private void AmbientOcclusionCheckBox_OnChecked(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded || MainDXViewportView.DXScene == null)
                return;

            var shaderResourceView = AmbientOcclusionShaderResourceView();

            if (shaderResourceView != null)
                _physicallyBasedMaterial.SetTextureMap(TextureMapTypes.AmbientOcclusion, shaderResourceView);
            else
                _physicallyBasedMaterial.RemoveTextureMap(TextureMapTypes.AmbientOcclusion);

            MainDXViewportView.DXScene.NotifyChange(DXScene.ChangeNotifications.MaterialPropertiesChanged);
        }

        private void NormalMapCheckBox_OnChecked(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded || MainDXViewportView.DXScene == null)
                return;


            var normalMapShaderResourceView = GetNormalMapShaderResourceView();

            if (normalMapShaderResourceView != null)
                _physicallyBasedMaterial.SetTextureMap(TextureMapTypes.NormalMap, normalMapShaderResourceView);
            else
                _physicallyBasedMaterial.RemoveTextureMap(TextureMapTypes.NormalMap);

            MainDXViewportView.DXScene.NotifyChange(DXScene.ChangeNotifications.MaterialPropertiesChanged);
        }

        private void EnvironmentMapCheckBox_OnChecked(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded || MainDXViewportView.DXScene == null)
                return;

            UpdateEnvironmentMap();
        }

        private void UpdateEnvironmentMap()
        {
            // Show Environment CUBE MAP
            SkyboxViewport.Visibility = (EnvironmentMapCheckBox.IsChecked ?? false) ? Visibility.Visible : Visibility.Hidden;


            // Set environment map texture to PBR
            var environmentMapCubeMap = GetEnvironmentMapCubeMap();

            if (environmentMapCubeMap != null)
                _physicallyBasedMaterial.SetTextureMap(TextureMapTypes.EnvironmentCubeMap, environmentMapCubeMap.ShaderResourceView);
            else
                _physicallyBasedMaterial.RemoveTextureMap(TextureMapTypes.EnvironmentCubeMap);

            MainDXViewportView.DXScene.NotifyChange(DXScene.ChangeNotifications.MaterialPropertiesChanged);
        }


        private void UpdateBaseColor()
        {
            string textureFileName = "bricks.png";

            bool hasTransparency;
            BlendState recommendedBlendState;
            var baseColorShaderResourceView = GetBaseColorShaderResourceView(textureFileName, out hasTransparency, out recommendedBlendState);

            if (baseColorShaderResourceView != null)
            {
                _physicallyBasedMaterial.SetTextureMap(TextureMapTypes.BaseColor, baseColorShaderResourceView, textureFileName);
                _physicallyBasedMaterial.BlendState = recommendedBlendState;
                _physicallyBasedMaterial.HasTransparency = hasTransparency;

                _physicallyBasedMaterial.BaseColor = Color4.White; // No color filter
            }
            else
            {
                _physicallyBasedMaterial.RemoveTextureMap(TextureMapTypes.BaseColor);
                _physicallyBasedMaterial.BlendState = null;
                _physicallyBasedMaterial.HasTransparency = false;

                _physicallyBasedMaterial.BaseColor = System.Windows.Media.Color.FromRgb(33, 148, 206).ToColor4(); // #2194ce
            }
        }

        private void OnLightSettingsChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            UpdateLights();
        }

        private void UpdateLights()
        {
            TopDirectionalLight.Color = (TopLightCheckBox.IsChecked ?? false) ? Colors.White : Colors.Black; // Setting light color to black is equal as removing it from the scene
            FrontDirectionalLight.Color = (FrontLightCheckBox.IsChecked ?? false) ? Colors.White : Colors.Black;
            AmbientLight1.Color = (AmbientLightCheckBox.IsChecked ?? false) ? System.Windows.Media.Color.FromRgb(85, 85, 85) : Colors.Black; // 0x55 = 85

            Camera1.ShowCameraLight = (CameraLightCheckBox.IsChecked ?? false) ? ShowCameraLightType.Always : ShowCameraLightType.Never;
            Camera1.Refresh();
        }
    }
}