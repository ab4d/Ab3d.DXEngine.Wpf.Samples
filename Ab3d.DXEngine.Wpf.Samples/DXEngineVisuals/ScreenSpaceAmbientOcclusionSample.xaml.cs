using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Ab3d.Assimp;
using Ab3d.Common.Cameras;
using Ab3d.DirectX;

#if SHARPDX
using SharpDX;
#endif

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    /// <summary>
    /// Interaction logic for ScreenSpaceAmbientOcclusionSample.xaml
    /// </summary>
    public partial class ScreenSpaceAmbientOcclusionSample : Page
    {
        private string _fileName;

        private AmbientLight _sceneAmbientLight;
        private DirectionalLight _backLight;
        private DirectionalLight _sideLight;

        private AssimpWpfImporter _assimpWpfImporter;

        private ScreenSpaceAmbientOcclusionRenderingProvider _ssaoRenderingProvider;
        private float[] _sharpenPowerValues;
        private RenderTextureRenderingStep _renderSsaoTextureRenderingStep;

        public ScreenSpaceAmbientOcclusionSample()
        {
            InitializeComponent();

            
            MapSizeComboBox.ItemsSource = new string[] { "128 x 128", "256 x 256", "512 x 512", "1024 x 1024", "10%", "25%", "50%", "100%" };
            MapSizeComboBox.SelectedIndex = 6;

            _sharpenPowerValues = new float[] { 0.2f, 0.5f, 1, 2, 4, 6, 8, 16 };
            SharpenPowerComboBox.ItemsSource = _sharpenPowerValues;
            SharpenPowerComboBox.SelectedIndex = 4;
            
            BlurCountComboBox.ItemsSource = new int[] { 0, 1, 2, 3, 4, 5, 6 };
            BlurCountComboBox.SelectedIndex = 4;


            AssimpLoader.LoadAssimpNativeLibrary();

            MainDXViewportView.DXSceneInitialized += (sender, args) =>
            {
                if (MainDXViewportView.DXScene == null) // if not wpf 3d rendering
                    return;

                _ssaoRenderingProvider = new ScreenSpaceAmbientOcclusionRenderingProvider();
                MainDXViewportView.DXScene.InitializeShadowRendering(_ssaoRenderingProvider);

                // upper right corner will display original the SSAO texture
                if (_ssaoRenderingProvider.SsaoShaderResourceView != null)
                {
                    // SsaoShaderResourceView has R_16 format (one channel with 16 bit floats value) - render that as grayscale
                    _renderSsaoTextureRenderingStep = new RenderTextureRenderingStep(RenderTextureRenderingStep.TextureChannelsCount.OneChannelGrayscaleRendering, "Show SSAO texture");
                    _renderSsaoTextureRenderingStep.TargetViewport = new ViewportF(0.68f, 0.02f, 0.3f, 0.3f); // upper right corner; size: 30% of the screen width and height

                    // Preserve the original factors and offsets (can be used to adjust how the values are converted into colors)
                    //_renderSsaoTextureRenderingStep.Factors = new Vector4(1, 1, 1, 0);
                    //_renderSsaoTextureRenderingStep.Offsets = new Vector4(0, 0, 0, 1);

                    _renderSsaoTextureRenderingStep.BeforeRunningStep += (o, eventArgs) =>
                    {
                        _renderSsaoTextureRenderingStep.SourceTexture = _ssaoRenderingProvider.SsaoShaderResourceView;
                    };

                    MainDXViewportView.DXScene.RenderingSteps.AddBefore(MainDXViewportView.DXScene.DefaultCompleteRenderingStep, _renderSsaoTextureRenderingStep);
                }

                SetupScene();
            };

            this.Unloaded += delegate (object sender, RoutedEventArgs args)
            {
                if (_ssaoRenderingProvider != null)
                {
                    _ssaoRenderingProvider.Dispose();
                    _ssaoRenderingProvider = null;
                }

                if (_assimpWpfImporter != null)
                {
                    _assimpWpfImporter.Dispose(); // Dispose unmanaged resource
                    _assimpWpfImporter = null;
                }

                MainDXViewportView.Dispose();
            };
        }

        
        private void SetupScene()
        {
            SampleObjectsRootVisual3D.Children.Clear();

            // Create an instance of AssimpWpfImporter
            _assimpWpfImporter = new AssimpWpfImporter();

            string fileName = AppDomain.CurrentDomain.BaseDirectory + @"Resources\Models\house with trees.3ds";
            var readModel3D = _assimpWpfImporter.ReadModel3D(fileName, texturesPath: null); // we can also define a textures path if the textures are located in some other directory (this is parameter can be skipped, but is defined here so you will know that you can use it)
            
            if (readModel3D == null)
            {
                MessageBox.Show("Cannot read file");
                return;
            }

            SampleObjectsRootVisual3D.Children.Add(readModel3D.CreateModelVisual3D());

            

            if (_fileName != fileName) // Reset camera only when the file is loaded for the first time
            {
                _fileName = fileName;

                Camera1.TargetPosition = readModel3D.Bounds.GetCenterPosition();
                Camera1.Distance = readModel3D.Bounds.GetDiagonalLength() * 1.2;
            }


            string dragonModelFileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources\\Models\\dragon_vrip_res3.obj");
            var dragonModel3D = _assimpWpfImporter.ReadModel3D(dragonModelFileName, texturesPath: null); // we can also define a textures path if the textures are located in some other directory (this is parameter can be skipped, but is defined here so you will know that you can use it)

            if (dragonModel3D != null)
            {
                // Note: IsCastingShadow and IsReceivingShadow are not supported by SSAO
                //dragonModel3D.SetDXAttribute(DXAttributeType.IsCastingShadow, false);

                Ab3d.Utilities.ModelUtils.CenterAndScaleModel3D(dragonModel3D, new Point3D(-230, 40, 200), new Size3D(200, 80, 100), preserveAspectRatio: true);
                SampleObjectsRootVisual3D.Children.Add(dragonModel3D.CreateModelVisual3D());
            }


            UpdateAmbientLight();
            UpdateSceneLights();

            UpdateSsaoSettings(renderNextFrame: false);
        }


        private void UpdateAmbientLight()
        {
            if (AmbientLightSlider == null)
                return;

            if (_sceneAmbientLight == null)
            {
                _sceneAmbientLight = new AmbientLight();
                LightsModel3DGroup.Children.Add(_sceneAmbientLight);
            }

            var color = (byte)(2.55 * AmbientLightSlider.Value); // Minimum="0" Maximum="100" => 0 .. 255
            _sceneAmbientLight.Color = System.Windows.Media.Color.FromRgb(color, color, color);
        }

        private void UpdateSceneLights()
        {
            Camera1.ShowCameraLight = (CameraLightCheckBox.IsChecked ?? false) ? ShowCameraLightType.Always : ShowCameraLightType.Never;

            if (BackLightCheckBox.IsChecked ?? false)
            {
                if (_backLight == null)
                    _backLight = new DirectionalLight(Colors.White, new System.Windows.Media.Media3D.Vector3D(-1, 0, 0));

                if (!LightsModel3DGroup.Children.Contains(_backLight))
                    LightsModel3DGroup.Children.Add(_backLight);
            }
            else
            {
                if (_backLight != null && LightsModel3DGroup.Children.Contains(_backLight))
                    LightsModel3DGroup.Children.Remove(_backLight);
            }
            
            if (SideLightCheckBox.IsChecked ?? false)
            {
                if (_sideLight == null)
                    _sideLight = new DirectionalLight(Colors.White, new System.Windows.Media.Media3D.Vector3D(0, 0, -1));

                if (!LightsModel3DGroup.Children.Contains(_sideLight))
                    LightsModel3DGroup.Children.Add(_sideLight);
            }
            else
            {
                if (_sideLight != null && LightsModel3DGroup.Children.Contains(_sideLight))
                    LightsModel3DGroup.Children.Remove(_sideLight);
            }
        }

        
        private void AmbientLightSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!this.IsLoaded)
                return;

            UpdateAmbientLight();
        }

        private void OnLightSettingsChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            UpdateSceneLights();
        }
        
        private void UpdateSsaoSettings(bool renderNextFrame)
        {
            if (_ssaoRenderingProvider == null)
                return;

            _ssaoRenderingProvider.IsEnabled = SSAOCheckBox.IsChecked ?? false;

            _renderSsaoTextureRenderingStep.IsEnabled = _ssaoRenderingProvider.IsEnabled && (ShowPreviewCheckBox.IsChecked ?? false);

            _ssaoRenderingProvider.OcclusionRadius = (float)OcclusionRadiusSlider.Value;

            var selectedMapSizeText = (string)MapSizeComboBox.SelectedItem;
            UpdateSsaoMapSize(selectedMapSizeText);

            _ssaoRenderingProvider.SharpenPower = _sharpenPowerValues[SharpenPowerComboBox.SelectedIndex];
            _ssaoRenderingProvider.SsaoTextureBlurCount = BlurCountComboBox.SelectedIndex;

            if (renderNextFrame)
                MainDXViewportView.Refresh();
        }

        private void UpdateSsaoMapSize(string selectedText)
        {
            float size;
            bool isSizePercent;

            int pos = selectedText.IndexOf('x');

            if (pos != -1)
            {
                string sizeText = selectedText.Substring(0, pos - 1);
                size = Int32.Parse(sizeText);
                isSizePercent = false;
            }
            else
            {
                string percentText = selectedText.Substring(0, selectedText.Length - 1); // remove '%'
                int percentInt = Int32.Parse(percentText);

                size = (float)percentInt / 100;
                isSizePercent = true;
            }

            _ssaoRenderingProvider.ShadowTextureWidth = size;
            _ssaoRenderingProvider.ShadowTextureHeight = size;
            _ssaoRenderingProvider.IsShadowTextureSizePercent = isSizePercent;
        }

        private void OnSSAOCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            UpdateSsaoSettings(renderNextFrame: true);
        }

        private void OcclusionRadiusSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSsaoSettings(renderNextFrame: true);
        }

        private void OnSsaoSettingsChanged(object sender, RoutedEventArgs e)
        {
            UpdateSsaoSettings(renderNextFrame: true);
        }
    }
}
