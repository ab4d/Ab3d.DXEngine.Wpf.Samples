using System;
using System.Collections.Generic;
using System.IO;
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
using System.Windows.Threading;
using Ab3d.Assimp;
using Ab3d.Common.Cameras;
using Ab3d.DirectX;
using Ab3d.DirectX.Controls;
using Ab3d.DirectX.Effects;
using Ab3d.DirectX.Materials;
using Ab3d.DXEngine.Wpf.Samples.Common;
using Assimp;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Material = System.Windows.Media.Media3D.Material;
using AssimpMaterial = Assimp.Material;


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

        private Dictionary<string, string[]> _folderImageFiles;

        private Dictionary<AssimpMaterial, PhysicallyBasedMaterial> _dxMaterials;

        private Dictionary<string, ShaderResourceView> _texturesCache;
        private Dictionary<TextureMapTypes, string> _textureFiles;


        public PBRModelViewer()
        {
            InitializeComponent();

            _disposables = new DisposeList();

            AssimpLoader.LoadAssimpNativeLibrary();

            _dxMaterials   = new Dictionary<AssimpMaterial, PhysicallyBasedMaterial>();
            _texturesCache = new Dictionary<string, ShaderResourceView>();
            _textureFiles  = new Dictionary<TextureMapTypes, string>();

            // Support dragging .obj files to load the 3D models from obj file
            var dragAndDropHelper = new DragAndDropHelper(ViewportBorder, ".*");
            dragAndDropHelper.FileDropped += delegate(object sender, FileDroppedEventArgs e)
            {
                FileNameTextBox.Text = e.FileName;
                FolderPathTextBox.Text = System.IO.Path.GetDirectoryName(e.FileName);

                LoadFile(e.FileName, null);
            };

            MainDXViewportView.DXSceneInitialized += delegate (object sender, EventArgs args)
            {
                if (MainDXViewportView.DXScene == null) // Probably WPF 3D rendering
                    return;

                string rootFolder     = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory + @"Resources\RobotModel\");
                string fileName       = rootFolder + @"Robot_Claw.FBX";
                string texturesFolder = rootFolder + @"Robot_Claw_Maps\";

                FileNameTextBox.Text = fileName;
                FolderPathTextBox.Text = texturesFolder;

                LoadFile(fileName, texturesFolder);
                UpdateEnvironmentMap();
            };

            this.Loaded += delegate(object sender, RoutedEventArgs args)
            {
                UpdateLights();
            };

            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                Dispose();
            };
        }

        private void Dispose()
        {
            _textureFiles.Clear();
            _texturesCache.Clear();
            _dxMaterials.Clear();

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
            //LightsModelPlaceholder.Children.Clear();

            InfoTextBlock.Text = "";
            Log("Start opening file: " + fileName);


            try
            {
                //// ReaderObj that comes with Ab3d.PowerToys cannot read normal map textures
                //var readerObj = new Ab3d.ReaderObj();
                //readerObj.BitmapCacheOption = BitmapCacheOption.None; // Do not load textures by ReaderObj
                //Model3D readModel3D = readerObj.ReadModel3D(fileName);

                //UpdateMaterials(readModel3D);


                // Therefore we need to use Assimp importer
                var assimpWpfImporter = new AssimpWpfImporter();

                // Let assimp calculate the tangents that are needed for normal mapping
                assimpWpfImporter.AssimpPostProcessSteps |= PostProcessSteps.CalculateTangentSpace;

                //var readModel3D = assimpWpfImporter.ReadModel3D(fileName, texturesPath: null); // we can also define a textures path if the textures are located in some other directory (this is parameter can be skipped, but is defined here so you will know that you can use it)

                var assimpScene = assimpWpfImporter.ReadFileToAssimpScene(fileName);

                if (assimpScene == null)
                {
                    Log("Cannot load 3D model probably because file format is not recognized!");
                    return;
                }

                var assimpWpfConverter = new AssimpWpfConverter();
                var readModel3D = assimpWpfConverter.ConvertAssimpModel(assimpScene, System.IO.Path.GetDirectoryName(fileName));


                _loadedFileName = fileName;

                // Convert standard WPF materials into PhysicallyBasedMaterial objects
                UpdateMaterials(fileName, customTexturesFolder, assimpScene, assimpWpfConverter, useStrictFileNameMatch: true, supportDDSTextures: true);


                ModelPlaceholder.Content = readModel3D;

                Camera1.TargetPosition = readModel3D.Bounds.GetCenterPosition();
                Camera1.Distance       = 1.5 * readModel3D.Bounds.GetDiagonalLength();

                Log(string.Format("\r\nFile '{0}' loaded\r\nCenter position: {1:0};    Size: {2:0}",
                    System.IO.Path.GetFileName(fileName),
                    readModel3D.Bounds.GetCenterPosition(),
                    readModel3D.Bounds.Size));


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

        private void AddTextureMapSelections(string materialName, PhysicallyBasedMaterial physicallyBasedMaterial)
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


            //if (!string.IsNullOrEmpty(materialName))
            //{
            //    var textBlock = new TextBlock()
            //    {
            //        Text       = materialName,
            //        FontWeight = FontWeights.Bold,
            //        Margin = new Thickness(0, 0, 0, 3)
            //    };

            //    TextureMapsPanel.Children.Add(textBlock);
            //}

            bool hasDiffuseColor = physicallyBasedMaterial.HasTextureMap(TextureMapTypes.DiffuseColor);
            string baseFolder = System.IO.Path.GetDirectoryName(_loadedFileName);

            foreach (var supportedTextureMapType in KnownTextureFiles.PBRSupportedTextureMapTypes)
            {
                // Show only one of DiffuseColor or BaseColor (not both)
                // Show DiffuseColor only if there is a texture map defined for it
                if ((supportedTextureMapType == TextureMapTypes.DiffuseColor && !hasDiffuseColor) ||
                    (supportedTextureMapType == TextureMapTypes.BaseColor && hasDiffuseColor))
                {
                    continue;
                }

                var textureMapSelectionControl = new TextureMapSelectionControl(physicallyBasedMaterial, supportedTextureMapType, baseFolder);
                textureMapSelectionControl.Margin = new Thickness(23, 0, 0, 7);

                textureMapSelectionControl.MapSettingsChanged += TextureMapSelectionControlOnMapSettingsChanged;
                textureMapSelectionControl.LayoutTransform = new ScaleTransform(0.75, 0.75);

                if (MainDXViewportView.DXScene != null)
                    textureMapSelectionControl.DXDevice = MainDXViewportView.DXScene.DXDevice;

                //TextureMapsPanel.Children.Add(textureMapSelectionControl);
                stackPanel.Children.Add(textureMapSelectionControl);
            }
        }

        private void TextureMapSelectionControlOnMapSettingsChanged(object sender, EventArgs e)
        {
            MainDXViewportView.Refresh();
        }

        private void Log(string message)
        {
            //System.Diagnostics.Debug.WriteLine(message);

            InfoTextBlock.Text += message + Environment.NewLine;
            InfoTextBlock.ScrollToEnd();
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

        private void UpdateMaterials(string fileName, string customTexturesFolder, Scene assimpScene, Ab3d.Assimp.AssimpWpfConverter assimpWpfConverter, bool useStrictFileNameMatch, bool supportDDSTextures)
        {
            string filePath = System.IO.Path.GetDirectoryName(fileName);

            // Dispose existing resources
            Dispose();

            TextureMapsPanel.Children.Clear();


            Log("Materials:");
            foreach (var assimpMaterial in assimpScene.Materials)
                CreatePbrMaterial(assimpMaterial, filePath, customTexturesFolder, useStrictFileNameMatch, supportDDSTextures);



            // Go through all assimp meshes and for each 
            //Log("Meshes:");
            foreach (var assimpMesh in assimpScene.Meshes)
            {
                //Log($"  {assimpMesh.Name ?? "<undefined mesh name>"}");

                var geometryModel3D = assimpWpfConverter.GetGeometryModel3DForAssimpMesh(assimpMesh);

                if (geometryModel3D == null)
                    continue; // This should not happen but just in case do a check to prevent null reference exception


                var wpfMaterial = geometryModel3D.Material;

                if (wpfMaterial == null)
                    continue;

                // NOTE: We do not check the back material - the DiffuseSpecularNormalMapEffect does not support rendering them (we would need to update the shader to invert normals to support that).

                var assimpMaterial = assimpWpfConverter.GetAssimpMaterialForWpfMaterial(wpfMaterial);

                // PBR material must have a diffuse texture defined (this can be also used as albedo or base color in PBR)
                // The diffuse texture file name will be used as a base file name for looking at other textures that are needed for PBR
                if (assimpMaterial == null)
                    continue;



                PhysicallyBasedMaterial physicallyBasedMaterial;
                if (_dxMaterials.TryGetValue(assimpMaterial, out physicallyBasedMaterial))
                {
                    // ... and tangent data
                    SetMeshTangentData(assimpMesh, geometryModel3D);

                    // Finally call SetUsedDXMaterial on WPF material.
                    // This will tell DXEngine to use the diffuseSpecularNormalMapMaterial instead of creating a standard WpfMaterial.
                    if (wpfMaterial.GetUsedDXMaterial(MainDXViewportView.DXScene.DXDevice) == null)
                        wpfMaterial.SetUsedDXMaterial(physicallyBasedMaterial);
                }
                else
                {
                    Log($"  Material: {assimpMaterial.Name ?? ""} does not have a PhysicallyBasedMaterial defined");
                }
            }

            Log("\r\nAssimp scene nodes:");
            LogAssimpNodes(assimpScene.RootNode, indentString: "  ");
        }

        private void LogAssimpNodes(Node assimpSceneNode, string indentString)
        {
            string nodeDescription;

            if (!string.IsNullOrEmpty(assimpSceneNode.Name))
                nodeDescription = assimpSceneNode.Name;
            else
                nodeDescription = "<undefined node name>";


            Log(indentString + nodeDescription);

            if (assimpSceneNode.ChildCount > 0)
            {
                string childIndentString = indentString + "  ";
                foreach (var child in assimpSceneNode.Children)
                    LogAssimpNodes(child, childIndentString);
            }
        }

        private PhysicallyBasedMaterial CreatePbrMaterial(AssimpMaterial assimpMaterial, string filePath, string customTexturesFolder, bool useStrictFileNameMatch, bool supportDDSTextures)
        {
            PhysicallyBasedMaterial physicallyBasedMaterial;
            if (_dxMaterials.TryGetValue(assimpMaterial, out physicallyBasedMaterial)) // Is PhysicallyBasedMaterial already creared
            {
                Log($"  Material: {assimpMaterial.Name ?? ""} (already defined)");
                return physicallyBasedMaterial;
            }

            //if (!assimpMaterial.HasTextureDiffuse)
            //{
            //    Log($"  Material {assimpMaterial.Name ?? ""} does not define a diffuse texture");
            //    return null;
            //}


            Log($"  Material {assimpMaterial.Name ?? ""}:");


            physicallyBasedMaterial = new PhysicallyBasedMaterial();

            // When materials has diffuse texture defined, then we also try to find other PBR textures
            if (assimpMaterial.HasTextureDiffuse)
                AddPBRTextures(assimpMaterial, filePath, customTexturesFolder, useStrictFileNameMatch, supportDDSTextures, physicallyBasedMaterial);

            
            // Set BaseColor based on the DiffuseColor 
            if (assimpMaterial.HasColorDiffuse)
                physicallyBasedMaterial.BaseColor = new Color4(assimpMaterial.ColorDiffuse.R, assimpMaterial.ColorDiffuse.G, assimpMaterial.ColorDiffuse.B, assimpMaterial.ColorDiffuse.A);

            // When there is no Metalness texture defined, then set Metalness to zero - use plastic
            if (!physicallyBasedMaterial.HasTextureMap(TextureMapTypes.Metalness))
                physicallyBasedMaterial.Metalness = 0;


            _disposables.Add(physicallyBasedMaterial);
            _dxMaterials.Add(assimpMaterial, physicallyBasedMaterial);

            AddTextureMapSelections(assimpMaterial.Name, physicallyBasedMaterial);

            return physicallyBasedMaterial;
        }

        private void AddPBRTextures(AssimpMaterial assimpMaterial, string filePath, string customTexturesFolder, bool useStrictFileNameMatch, bool supportDDSTextures, PhysicallyBasedMaterial physicallyBasedMaterial)
        {
            //PhysicallyBasedMaterial physicallyBasedMaterial;
            string diffuseTextureFileName = assimpMaterial.TextureDiffuse.FilePath;

            if (!string.IsNullOrEmpty(customTexturesFolder))
                diffuseTextureFileName = System.IO.Path.Combine(customTexturesFolder, System.IO.Path.GetFileName(diffuseTextureFileName));
            else if (!System.IO.Path.IsPathRooted(diffuseTextureFileName))
                diffuseTextureFileName = System.IO.Path.Combine(filePath, diffuseTextureFileName);

            string folderName = System.IO.Path.GetDirectoryName(diffuseTextureFileName);

            if (!System.IO.Directory.Exists(folderName))
            {
                Log($"  Folder for diffuse texture does not exist: {folderName ?? ""}:");
                return;
            }


            if (_folderImageFiles == null)
                _folderImageFiles = new Dictionary<string, string[]>();

            string[] allFilesInFolder;

            if (!_folderImageFiles.TryGetValue(folderName, out allFilesInFolder))
            {
                allFilesInFolder = System.IO.Directory.GetFiles(folderName, "*.*", SearchOption.TopDirectoryOnly);
                _folderImageFiles.Add(folderName, allFilesInFolder);
            }


            //TextureMapTypes textureMapType = KnownTextureFiles.GetTextureType(diffuseTextureFileName);

            string fileNameWithoutKnownSuffix = KnownTextureFiles.GetFileNameWithoutKnownSuffix(diffuseTextureFileName);

            // Get material files that start with the diffuse texture file name without a suffix
            List<string> materialFiles;

            if (useStrictFileNameMatch)
                materialFiles = allFilesInFolder.Where(f => fileNameWithoutKnownSuffix == KnownTextureFiles.GetFileNameWithoutKnownSuffix(f)).ToList();
            else
                materialFiles = allFilesInFolder.Where(f => f.IndexOf(fileNameWithoutKnownSuffix, 0, StringComparison.OrdinalIgnoreCase) != -1).ToList();


            _textureFiles.Clear();

            if (materialFiles.Count == 0)
            {
                Log($"   Folder ({folderName}) for {assimpMaterial.Name ?? ""} material does not define any texture files");
                return;
            }
            else
            {
                bool hasDiffuseTexture = false;
                foreach (var materialFile in materialFiles)
                {
                    if (!TextureLoader.IsSupportedFile(materialFile, supportDDSTextures)) // Skip unsupported files
                        continue;

                    var textureMapType = KnownTextureFiles.GetTextureType(materialFile);
                    if (textureMapType == TextureMapTypes.Unknown)
                    {
                        if (!hasDiffuseTexture)
                            textureMapType = TextureMapTypes.DiffuseColor; // First unknown file type is considered to be diffuse texture file
                        else
                            continue; // Unknown file type
                    }

                    bool isDiffuseTexture = (textureMapType == TextureMapTypes.DiffuseColor ||
                                             textureMapType == TextureMapTypes.Albedo ||
                                             textureMapType == TextureMapTypes.BaseColor);

                    string existingTextureFileName;
                    if (_textureFiles.TryGetValue(textureMapType, out existingTextureFileName))
                    {
                        // Map for this texture type already exist
                        var existingFileExtension = System.IO.Path.GetExtension(existingTextureFileName);
                        if (existingFileExtension != null && existingFileExtension.Equals(".dds", StringComparison.OrdinalIgnoreCase))
                            continue; // DDS texture already found for this texture type - we will use existing dds texture
                    }


                    hasDiffuseTexture |= isDiffuseTexture;

                    _textureFiles.Add(textureMapType, materialFile);

                    Log("    " + textureMapType + ": " + System.IO.Path.GetFileName(materialFile));
                }

                if (_textureFiles.Count > 0)
                {
                    foreach (var oneTextureFile in _textureFiles)
                    {
                        var textureType = oneTextureFile.Key;
                        var oneFileName = oneTextureFile.Value;

                        ShaderResourceView shaderResourceView;

                        if (!_texturesCache.TryGetValue(oneFileName, out shaderResourceView))
                        {
                            var isBaseColor = (textureType == TextureMapTypes.BaseColor ||
                                               textureType == TextureMapTypes.Albedo ||
                                               textureType == TextureMapTypes.DiffuseColor);

                            // To load a texture from file, you can use the TextureLoader.LoadShaderResourceView (this supports loading standard image files and also loading dds files).
                            // This method returns a ShaderResourceView and it can also set a textureInfo parameter that defines some of the properties of the loaded texture (bitmap size, dpi, format, hasTransparency).
                            TextureInfo textureInfo;
                            shaderResourceView = Ab3d.DirectX.TextureLoader.LoadShaderResourceView(MainDXViewportView.DXScene.Device, 
                                                                                                   oneFileName,
                                                                                                   loadDdsIfPresent: true,
                                                                                                   convertTo32bppPRGBA: isBaseColor,
                                                                                                   generateMipMaps: true,
                                                                                                   textureInfo: out textureInfo);

                            physicallyBasedMaterial.TextureMaps.Add(new TextureMapInfo((Ab3d.DirectX.Materials.TextureMapTypes) textureType, shaderResourceView, null, oneFileName));

                            if (isBaseColor)
                            {
                                // Get recommended BlendState based on HasTransparency and HasPreMultipliedAlpha values.
                                // Possible values are: CommonStates.Opaque, CommonStates.PremultipliedAlphaBlend or CommonStates.NonPremultipliedAlphaBlend.
                                var recommendedBlendState = MainDXViewportView.DXScene.DXDevice.CommonStates.GetRecommendedBlendState(textureInfo.HasTransparency, textureInfo.HasPremultipliedAlpha);

                                physicallyBasedMaterial.BlendState      = recommendedBlendState;
                                physicallyBasedMaterial.HasTransparency = textureInfo.HasTransparency;
                            }


                            _texturesCache.Add(oneFileName, shaderResourceView);
                        }
                    }
                }
            }
        }

        private static void SetMeshTangentData(Mesh assimpMesh, GeometryModel3D geometryModel3D)
        {
            var assimpTangents = assimpMesh.Tangents;

            if (assimpTangents != null && assimpTangents.Count > 0)
            {
                var count      = assimpTangents.Count;
                var dxTangents = new SharpDX.Vector3[count];

                for (int i = 0; i < count; i++)
                    dxTangents[i] = new SharpDX.Vector3(assimpTangents[i].X, assimpTangents[i].Y, assimpTangents[i].Z);

                // Tangent values are stored with the MeshGeometry3D object.
                // This is done with using DXAttributeType.MeshTangentArray:
                geometryModel3D.Geometry.SetDXAttribute(DXAttributeType.MeshTangentArray, dxTangents);
            }
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
                FolderPathTextBox.Text = System.IO.Path.GetDirectoryName(openFileDialog.FileName);

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

            foreach (var physicallyBasedMaterial in _dxMaterials.Values)
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
