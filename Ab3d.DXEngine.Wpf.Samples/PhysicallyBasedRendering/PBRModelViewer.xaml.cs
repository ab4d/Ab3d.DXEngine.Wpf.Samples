using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Ab3d.Assimp;
using Ab3d.Common.Cameras;
using Ab3d.DirectX;
using Ab3d.DirectX.Materials;
using Ab3d.DXEngine.Wpf.Samples.Common;
using Assimp;
using AssimpMaterial = Assimp.Material;

#if SHARPDX
using SharpDX.Direct3D11;
#endif


namespace Ab3d.DXEngine.Wpf.Samples.PhysicallyBasedRendering
{
    /// <summary>
    /// Interaction logic for PBRModelViewer.xaml
    /// </summary>
    public partial class PBRModelViewer : Page
    {
        private DisposeList _disposables;

        private DXCubeMap _environmentCubeMap;

        private string _loadedFileName;

        private Dictionary<AssimpMaterial, PhysicallyBasedMaterial> _pbrMaterials;

        private Dictionary<string, ShaderResourceView> _texturesCache;


        public PBRModelViewer()
        {
            InitializeComponent();

            _disposables = new DisposeList();

            AssimpLoader.LoadAssimpNativeLibrary();

            _pbrMaterials  = new Dictionary<AssimpMaterial, PhysicallyBasedMaterial>();
            _texturesCache = new Dictionary<string, ShaderResourceView>();

            // Support dragging .obj files to load the 3D models from obj file
            var dragAndDropHelper = new DragAndDropHelper(ViewportBorder, ".*");
            dragAndDropHelper.FileDropped += delegate(object sender, FileDroppedEventArgs e)
            {
                FileNameTextBox.Text = e.FileName;
                FolderPathTextBox.Text = System.IO.Path.GetDirectoryName(e.FileName) ?? "";

                LoadFile(e.FileName, null);
            };

            MainDXViewportView.DXSceneInitialized += delegate
            {
                if (MainDXViewportView.DXScene == null) // Probably WPF 3D rendering
                    return;

                string rootFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory + @"Resources\RobotModel\");
                string fileName = rootFolder + @"Robot_Claw.FBX";

                FileNameTextBox.Text = fileName;
                FolderPathTextBox.Text = "";

                UpdateEnvironmentMap();

                LoadFile(fileName, "");
            };

            this.Loaded += delegate
            {
                UpdateLights();
            };

            this.Unloaded += delegate
            {
                Dispose();
            };
        }

        private void Dispose()
        {
            _texturesCache.Clear();
            _pbrMaterials.Clear();

            _disposables.Dispose();
            _disposables = new DisposeList();
        }

        private void LoadFile(string fileName, string customTexturesFolder)
        {
            if (!System.IO.File.Exists(fileName))
            {
                MessageBox.Show($"File does not exist:\r\n{fileName}");
                return;
            }

            Mouse.OverrideCursor = Cursors.Wait;

            ModelPlaceholder.Content = null;
            ModelPlaceholder.Children.Clear();


            try
            {
                // Dispose existing resources
                Dispose();

                TextureMapsPanel.Children.Clear();

                // Get environment map texture for PBR (when EnvironmentMapCheckBox is unchecked, then environmentCubeMap is null)
                var environmentCubeMap = GetEnvironmentMapCubeMap();


                // Therefore we need to use Assimp importer
                var assimpWpfImporter = new AssimpWpfImporter();

                // Let assimp calculate the tangents that are needed for normal mapping
                assimpWpfImporter.AssimpPostProcessSteps |= PostProcessSteps.CalculateTangentSpace;

                var readModel3D = assimpWpfImporter.ReadModel3D(fileName, texturesPath: customTexturesFolder); // we can also define a textures path if the textures are located in some other directory (this is parameter can be skipped, but is defined here, so you will know that you can use it)

                _loadedFileName = fileName;


                bool findPbrMapsFromFileNames = FindPbrFilesCheckBox.IsChecked ?? false;

                // Using static AssimpPbrHelper.SetupPbrMaterials (defined in this project in Common folder)
                // can greatly simplify setting up PBR materials
                _pbrMaterials = AssimpPbrHelper.SetupPbrMaterials(assimpWpfImporter, MainDXViewportView.DXScene.DXDevice, findPbrMapsFromFileNames, true, environmentCubeMap, _texturesCache);

                if (_pbrMaterials != null)
                {
                    // If any PBR materials were created, then add TextureMapSelection controls for each of them
                    // and add them to _disposables
                    foreach (var physicallyBasedMaterial in _pbrMaterials.Values)
                    {
                        AddTextureMapSelections(assimpWpfImporter.ImportedAssimpScene, physicallyBasedMaterial.Name, physicallyBasedMaterial, customTexturesFolder);
                        _disposables.Add(physicallyBasedMaterial);
                    }
                }


                ModelPlaceholder.Content = readModel3D;

                Camera1.TargetPosition  = readModel3D.Bounds.GetCenterPosition();
                Camera1.Distance        = 1.5 * readModel3D.Bounds.GetDiagonalLength();
                Camera1.ShowCameraLight = ShowCameraLightType.Always;
                Camera1.Refresh();

                // Manually call Refresh to update the scene while we are still showing Wait cursor
                MainDXViewportView.Refresh();

                this.Title = "Ab3d.DXEngine PBR Rendering - " + System.IO.Path.GetFileName(fileName);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void AddTextureMapSelections(Scene assimpScene, string materialName, PhysicallyBasedMaterial physicallyBasedMaterial, string customTexturesFolder = null)
        {
            if (physicallyBasedMaterial == null)
                return;

            var expander = new Expander()
            {
                Header     = materialName,
                Margin     = new Thickness(0, 0, 0, 3),
                IsExpanded = true
            };

            TextureMapsPanel.Children.Add(expander);


            var stackPanel = new StackPanel()
            {
                Orientation = Orientation.Vertical
            };

            expander.Content = stackPanel;


            bool hasDiffuseColor = physicallyBasedMaterial.HasTextureMap(TextureMapTypes.DiffuseColor);
            bool hasMetalness = physicallyBasedMaterial.HasTextureMap(TextureMapTypes.Metalness);
            bool hasRoughnessMetalness = physicallyBasedMaterial.HasTextureMap(TextureMapTypes.RoughnessMetalness);
            bool hasOcclusionRoughnessMetalness = physicallyBasedMaterial.HasTextureMap(TextureMapTypes.OcclusionRoughnessMetalness);
            
            string baseFolder = customTexturesFolder ?? System.IO.Path.GetDirectoryName(_loadedFileName);

            foreach (var supportedTextureMapType in AssimpPbrHelper.PBRSupportedTextureMapTypes)
            {
                // Show only one of DiffuseColor or BaseColor (not both)
                // Show DiffuseColor only if there is a texture map defined for it
                // Also in case OcclusionRoughnessMetalness is defined, then do not show Metalness, Roughness and AmbientOcclusion (and vice-versa)
                if ((!hasDiffuseColor && supportedTextureMapType == TextureMapTypes.DiffuseColor) ||
                    (hasDiffuseColor && supportedTextureMapType == TextureMapTypes.BaseColor) ||
                    (hasMetalness && (supportedTextureMapType == TextureMapTypes.OcclusionRoughnessMetalness || supportedTextureMapType == TextureMapTypes.RoughnessMetalness)) ||
                    (hasRoughnessMetalness && (supportedTextureMapType == TextureMapTypes.Metalness || supportedTextureMapType == TextureMapTypes.Roughness || supportedTextureMapType == TextureMapTypes.OcclusionRoughnessMetalness)) ||
                    (hasOcclusionRoughnessMetalness && (supportedTextureMapType == TextureMapTypes.Metalness || supportedTextureMapType == TextureMapTypes.Roughness || supportedTextureMapType == TextureMapTypes.AmbientOcclusion || supportedTextureMapType == TextureMapTypes.RoughnessMetalness)))
                {
                    continue;
                }

                var textureMapSelectionControl = new TextureMapSelectionControl(physicallyBasedMaterial, supportedTextureMapType, baseFolder);
                textureMapSelectionControl.Margin = new Thickness(23, 0, 0, 7);

                textureMapSelectionControl.MapSettingsChanged += TextureMapSelectionControlOnMapSettingsChanged;
                textureMapSelectionControl.LayoutTransform = new ScaleTransform(0.75, 0.75);

                if (MainDXViewportView.DXScene != null)
                    textureMapSelectionControl.DXDevice = MainDXViewportView.DXScene.DXDevice;

                textureMapSelectionControl.AssimpScene = assimpScene;
                textureMapSelectionControl.TexturesCache = _texturesCache;

                stackPanel.Children.Add(textureMapSelectionControl);
            }
        }

        private void TextureMapSelectionControlOnMapSettingsChanged(object sender, EventArgs e)
        {
            MainDXViewportView.Refresh();
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
            AmbientLight1.Color = (AmbientLightCheckBox.IsChecked ?? false) ? Colors.White : Colors.Black;

            Camera1.ShowCameraLight = (CameraLightCheckBox.IsChecked ?? false) ? ShowCameraLightType.Always : ShowCameraLightType.Never;
            Camera1.Refresh();
        }

        private void LoadFileButton_OnClick(object sender, RoutedEventArgs e)
        {
            Dispose();

            LoadFile(FileNameTextBox.Text, FolderPathTextBox.Text);
        }

        private void OpenFileButton_OnClick(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "All files (*.*)|*.*";
            openFileDialog.Title = "Select model file";

            if ((openFileDialog.ShowDialog() ?? false) && !string.IsNullOrEmpty(openFileDialog.FileName))
            {
                FileNameTextBox.Text = openFileDialog.FileName;
                FolderPathTextBox.Text = System.IO.Path.GetDirectoryName(openFileDialog.FileName) ?? "";

                LoadFile(openFileDialog.FileName, null);
            }
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

            foreach (var physicallyBasedMaterial in _pbrMaterials.Values)
            {
                if (environmentMapCubeMap != null)
                    physicallyBasedMaterial.SetTextureMap(TextureMapTypes.EnvironmentCubeMap, environmentMapCubeMap.ShaderResourceView);
                else
                    physicallyBasedMaterial.RemoveTextureMap(TextureMapTypes.EnvironmentCubeMap);
            }


            MainDXViewportView.DXScene.NotifyChange(DXScene.ChangeNotifications.MaterialPropertiesChanged);
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
    }
}