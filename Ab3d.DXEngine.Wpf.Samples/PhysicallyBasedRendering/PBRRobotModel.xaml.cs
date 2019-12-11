using System;
using System.Collections.Generic;
using System.IO;
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
using System.Windows.Threading;
using Ab3d.Assimp;
using Ab3d.Common;
using Ab3d.Common.Cameras;
using Ab3d.DirectX;
using Ab3d.DirectX.Controls;
using Ab3d.DirectX.Effects;
using Ab3d.DirectX.Materials;
using Assimp;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Material = System.Windows.Media.Media3D.Material;
using AssimpMaterial = Assimp.Material;


// Robot model license:
//
// The robot model may be used only as part of the demonstration for Ab3d.PowerToys or Ab3d.DXEngine libraries
// and must not be used for any other commercial or non-commercial purpose!
//
// Copyright(c) 2018 by AB4D d.o.o.


// Credits:
//
// Sugarbricks Studio
// Brankova 21 Belgrade
// https: //www.facebook.com/Sugarbricks/
// https: //www.upwork.com/agencies/~01369bbf16f9f63f1c
// 
// Design/3d modeling by:
// Grgur Stojanovic
// Tundenizer @gmail.com
// 
// Texturing by:
// Miljan Jelic
// miljanjelic0 @gmail.com


// The textures from this folder are scaled down to reduce the size of the installer.
//   
// To check the robot model with full resulution when using 4k textures download the full detailed model from:
// https://www.ab4d.com/GetFile.ashx?fileName=RobotModel-4k.zip


namespace Ab3d.DXEngine.Wpf.Samples.PhysicallyBasedRendering
{
    /// <summary>
    /// Interaction logic for PBRRobotModel.xaml
    /// </summary>
    public partial class PBRRobotModel : Page
    {
        private string _rootFolder;

        private string _texturesSubfolder = @"Robot_{0}_Maps";
        private string _robotModelFileName = "Robot_{0}.FBX";


        public enum RobotParts
        {
            None,
            Main,
            Claw,
            Saw,
            Panel
        }


        private DisposeList _disposables;

        private Dictionary<string, string[]> _folderImageFiles;

        private Dictionary<AssimpMaterial, PhysicallyBasedMaterial> _dxMaterials;
        private Dictionary<string, ShaderResourceView> _texturesCache;
        private Dictionary<TextureMapTypes, string> _textureFiles;

        private Dictionary<string, Model3D> _namedObjects;

        private AxisAngleRotation3D _baseRotation;
        private AxisAngleRotation3D _arm1Rotation;
        private AxisAngleRotation3D _arm2Rotation;
        private AxisAngleRotation3D _arm3Rotation;
        private AxisAngleRotation3D _toolRotation;

        private AxisAngleRotation3D _clawLeftRotation;
        private AxisAngleRotation3D _clawRightRotation;

        private AxisAngleRotation3D _sawRotation;

        private GeometryModel3D _shownPanelGeometryModel3D;


        public PBRRobotModel()
        {
            InitializeComponent();

            _disposables = new DisposeList();

            AssimpLoader.LoadAssimpNativeLibrary();

            _dxMaterials   = new Dictionary<AssimpMaterial, PhysicallyBasedMaterial>();
            _texturesCache = new Dictionary<string, ShaderResourceView>();

            _textureFiles   = new Dictionary<TextureMapTypes, string>();

            // Load scene when we have the DXDevice ready
            MainDXViewportView.DXSceneInitialized += delegate (object sender, EventArgs args)
            {
                if (MainDXViewportView.DXScene == null) // Probably WPF 3D rendering
                    return;

                _rootFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory + @"Resources\RobotModel\");

                LoadRobotPart(RobotParts.Main);
                LoadRobotPart(RobotParts.Claw);
            };




            UpdateLights();

            this.Unloaded += delegate (object sender, RoutedEventArgs args) { Dispose(); };
        }

        private void Dispose()
        {
            _disposables.Dispose();
            _disposables = new DisposeList();
        }

        private void LoadRobotPart(RobotParts robotPart)
        {
            if (robotPart == RobotParts.None)
                return;

            string partName = robotPart.ToString();

            string fileName = _rootFolder + string.Format(_robotModelFileName, partName);
            string texturesFolder = _rootFolder + string.Format(_texturesSubfolder, partName);

            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                if (robotPart == RobotParts.Main)
                {
                    ModelPlaceholder.Content = null;
                    ModelPlaceholder.Children.Clear();

                    InfoTextBlock.Text = "";
                }

                Dictionary<string, Model3D> namedObjects;
                var readModel3D = LoadModel3D(fileName, texturesFolder, out namedObjects);

                if (readModel3D == null)
                    return;

                
                // Add RotateTransform3D objects to robot parts
                AddRobotTransformations(robotPart, namedObjects);


                if (robotPart == RobotParts.Main)
                {                   
                    // Show the model
                    ModelPlaceholder.Content = readModel3D;

                    // Setup camera
                    Camera1.TargetPosition = readModel3D.Bounds.GetCenterPosition();
                    Camera1.Distance       = 1.0 * readModel3D.Bounds.GetDiagonalLength();

                    // Manually call Refresh to update the scene (create DirectX resources) while we are still showing Wait cursor
                    MainDXViewportView.Refresh();

                    _namedObjects = namedObjects;
                }
                else
                {
                    Model3D toolConnectorModel3D;
                    if (_namedObjects == null || !_namedObjects.TryGetValue("Helper_Extras", out toolConnectorModel3D))
                        return;

                    var toolConnectorModel3DGroup = (Model3DGroup)toolConnectorModel3D;
                    toolConnectorModel3DGroup.Children.Clear();

                    Ab3d.Utilities.ModelUtils.PositionModel3D(readModel3D, new Point3D(0, 0, 0), PositionTypes.Bottom);
                    toolConnectorModel3DGroup.Children.Add(readModel3D);

                    //var geometryModel3D = Ab3d.Models.Line3DFactory.CreateWireCross3D(new Point3D(0, 0, 0), 1000, 2, Colors.Orange, MainViewport);
                    //toolConnectorModel3DGroup.Children.Add(geometryModel3D);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("Error loading robot part {0}:\r\n{1}", robotPart, ex.Message));
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void AddRobotTransformations(RobotParts robotPart, Dictionary<string, Model3D> namedObjects)
        {
            try
            {
                Model3DGroup model3DGroup;

                if (robotPart == RobotParts.Main)
                {
                    model3DGroup = namedObjects["Helper_Base"] as Model3DGroup;
                    _baseRotation = new AxisAngleRotation3D(new System.Windows.Media.Media3D.Vector3D(0, 0, 1), BaseRotationSlider.Value);
                    InsertTransformation(model3DGroup, new RotateTransform3D(_baseRotation));

                    model3DGroup  = namedObjects["Helper_Arm1"] as Model3DGroup;
                    _arm1Rotation = new AxisAngleRotation3D(new System.Windows.Media.Media3D.Vector3D(1, 0, 0), Arm1RotationSlider.Value);
                    InsertTransformation(model3DGroup, new RotateTransform3D(_arm1Rotation));

                    model3DGroup  = namedObjects["Helper_Arm2"] as Model3DGroup;
                    _arm2Rotation = new AxisAngleRotation3D(new System.Windows.Media.Media3D.Vector3D(1, 0, 0), Arm2RotationSlider.Value);
                    InsertTransformation(model3DGroup, new RotateTransform3D(_arm2Rotation));

                    model3DGroup  = namedObjects["Helper_Arm3"] as Model3DGroup;
                    _arm3Rotation = new AxisAngleRotation3D(new System.Windows.Media.Media3D.Vector3D(1, 0, 0), Arm3RotationSlider.Value);
                    InsertTransformation(model3DGroup, new RotateTransform3D(_arm3Rotation));


                    // The robot tools are added to the "Helper_Extras" Model3DGroup.
                    model3DGroup = namedObjects["Helper_Extras"] as Model3DGroup; 

                    // The original 3D model was created with coordinate system where Z axis is us (instead of Y up).
                    // When the model was exported to fbx, the exported added a transformation to convert the model to Y up coordinate system.
                    // If we now load another fbx file (robot tool) and add it to the last robot arm part,
                    // the tool will have the same coordinate system change transformation, but because we would add that to 
                    // already transformed coordinate system, this would not work well.
                    // Therefore we need to add a transformation that will allow negate the coordinate system change transformation in the added file:

                    var matrixTransform3D = new MatrixTransform3D(new Matrix3D(1, 0, 0, 0,
                                                                               0, 0, 1, 0,
                                                                               0, -1, 0, 0,
                                                                               0, 0, 0, 1));

                    var transform3DGroup = new Transform3DGroup();
                    transform3DGroup.Children.Add(matrixTransform3D);

                    _toolRotation = new AxisAngleRotation3D(new System.Windows.Media.Media3D.Vector3D(0, 0, 1), ToolRotationSlider.Value);
                    transform3DGroup.Children.Add(new RotateTransform3D(_toolRotation));

                    InsertTransformation(model3DGroup, transform3DGroup);
                }
                else if (robotPart == RobotParts.Claw)
                {
                    model3DGroup = namedObjects["Helper_Claw_Left"] as Model3DGroup;
                    _clawLeftRotation = new AxisAngleRotation3D(new System.Windows.Media.Media3D.Vector3D(0, 1, 0), 0);
                    InsertTransformation(model3DGroup, new RotateTransform3D(_clawLeftRotation));

                    model3DGroup = namedObjects["Helper_Claw_Right"] as Model3DGroup;
                    _clawRightRotation = new AxisAngleRotation3D(new System.Windows.Media.Media3D.Vector3D(0, -1, 0), 0);
                    InsertTransformation(model3DGroup, new RotateTransform3D(_clawRightRotation));
                }
                else if (robotPart == RobotParts.Saw)
                {
                    model3DGroup = namedObjects["Helper_SawBlade"] as Model3DGroup;
                    _sawRotation = new AxisAngleRotation3D(new System.Windows.Media.Media3D.Vector3D(1, 0, 0), 0);
                    InsertTransformation(model3DGroup, new RotateTransform3D(_sawRotation));
                }
                else if (robotPart == RobotParts.Panel)
                {
                    _shownPanelGeometryModel3D = namedObjects["Robot_Panel_Screen"] as GeometryModel3D;

                    if (_shownPanelGeometryModel3D != null)
                        _shownPanelGeometryModel3D.Material = new DiffuseMaterial(Brushes.Black);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error setting robot transformations:\r\n" + ex.Message);
            }
        }

        private Model3D LoadModel3D(string fileName, string customTexturesFolder, out Dictionary<string, Model3D> namedObjects)
        {
            if (!System.IO.File.Exists(fileName))
            {
                MessageBox.Show($"File does not exist:\r\n{fileName}");
                namedObjects = null;

                return null;
            }


            // Therefore we need to use Assimp importer
            var assimpWpfImporter = new AssimpWpfImporter();

            // Let assimp calculate the tangents that are needed for normal mapping
            assimpWpfImporter.AssimpPostProcessSteps = PostProcessSteps.CalculateTangentSpace;

            var assimpScene = assimpWpfImporter.ReadFileToAssimpScene(fileName);

            var assimpWpfConverter = new AssimpWpfConverter();
            assimpWpfConverter.RemoveEmptyModel3DGroups = false; // Prevent removing "Helper_Extras" Model3DGroup - placholder that is used for tools

            var readModel3D = assimpWpfConverter.ConvertAssimpModel(assimpScene, System.IO.Path.GetDirectoryName(fileName));


            // Update WPF materials with PhysicallyBasedMaterial
            UpdateMaterials(fileName, customTexturesFolder, assimpScene, assimpWpfConverter, useStrictFileNameMatch: true, supportDDSTextures: true);


            // Convert ObjectNames to NamedObjects
            namedObjects = new Dictionary<string, Model3D>(assimpWpfConverter.ObjectNames.Count);
            foreach (var keyValuePair in assimpWpfConverter.ObjectNames)
            {
                var model3D = keyValuePair.Key as Model3D;

                if (model3D == null || keyValuePair.Value == null)
                    continue;

                namedObjects[keyValuePair.Value] = model3D;
            }


            return readModel3D;
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
            //AmbientLight1.Color = (AmbientLightCheckBox.IsChecked ?? false) ? System.Windows.Media.Color.FromRgb(85, 85, 85) : Colors.Black; // 0x55 = 85
            AmbientLight1.Color = (AmbientLightCheckBox.IsChecked ?? false) ? System.Windows.Media.Color.FromRgb(255, 255, 255) : Colors.Black; // 0x55 = 85

            Camera1.ShowCameraLight = (CameraLightCheckBox.IsChecked ?? false) ? ShowCameraLightType.Always : ShowCameraLightType.Never;
            Camera1.Refresh();
        }



        private void UpdateMaterials(string fileName, string customTexturesFolder, Scene assimpScene, Ab3d.Assimp.AssimpWpfConverter assimpWpfConverter, bool useStrictFileNameMatch, bool supportDDSTextures)
        {
            string filePath = System.IO.Path.GetDirectoryName(fileName);

            _textureFiles.Clear();
            _texturesCache.Clear();
            _dxMaterials.Clear();


            foreach (var assimpMaterial in assimpScene.Materials)
                CreatePbrMaterial(assimpMaterial, filePath, customTexturesFolder, useStrictFileNameMatch, supportDDSTextures);

            
            // Go through all assimp meshes and for each 
            foreach (var assimpMesh in assimpScene.Meshes)
            {
                Log($"Mesh: {assimpMesh.Name ?? ""}:");

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
                if (assimpMaterial == null || !assimpMaterial.HasTextureDiffuse)
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

            //FillTextureRadioButtons();
        }

        private PhysicallyBasedMaterial CreatePbrMaterial(AssimpMaterial assimpMaterial, string filePath, string customTexturesFolder, bool useStrictFileNameMatch, bool supportDDSTextures)
        {
            PhysicallyBasedMaterial physicallyBasedMaterial;
            if (_dxMaterials.TryGetValue(assimpMaterial, out physicallyBasedMaterial))
            {
                Log($"  Material: {assimpMaterial.Name ?? ""} (already defined)");
                return physicallyBasedMaterial;
            }

            if (!assimpMaterial.HasTextureDiffuse)
            {
                Log($"  Material {assimpMaterial.Name ?? ""} does not define a diffuse texture");
                return null;
            }


            Log($"  Material {assimpMaterial.Name ?? ""}:");

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
                return null;
            }


            if (_folderImageFiles == null)
                _folderImageFiles = new Dictionary<string, string[]>();

            string[] allFilesInFolder;

            if (!_folderImageFiles.TryGetValue(folderName, out allFilesInFolder))
            {
                allFilesInFolder = System.IO.Directory.GetFiles(folderName, "*.*", SearchOption.TopDirectoryOnly);
                _folderImageFiles.Add(folderName, allFilesInFolder);
            }

            
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
                return null;
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
                    physicallyBasedMaterial = new PhysicallyBasedMaterial();

                    foreach (var oneTextureFile in _textureFiles)
                    {
                        var textureType = oneTextureFile.Key;
                        var oneFileName = oneTextureFile.Value;

                        ShaderResourceView shaderResourceView;

                        if (!_texturesCache.TryGetValue(oneFileName, out shaderResourceView))
                        {
                            var convertTo32bppPRGBA = (textureType == TextureMapTypes.BaseColor ||
                                                       textureType == TextureMapTypes.Albedo ||
                                                       textureType == TextureMapTypes.DiffuseColor);

                            shaderResourceView = Ab3d.DirectX.TextureLoader.LoadShaderResourceView(MainDXViewportView.DXScene.DXDevice.Device, oneFileName, loadDdsIfPresent: false, convertTo32bppPRGBA: convertTo32bppPRGBA);
                            
                            physicallyBasedMaterial.TextureMaps.Add(new TextureMapInfo((Ab3d.DirectX.Materials.TextureMapTypes)textureType, shaderResourceView, null, oneFileName));

                            _texturesCache.Add(oneFileName, shaderResourceView);
                        }
                    }

                    _dxMaterials.Add(assimpMaterial, physicallyBasedMaterial);
                    _disposables.Add(physicallyBasedMaterial);
                }
            }

            return physicallyBasedMaterial;
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

        private void ToolNameRadioButtonCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            RobotParts robotPart;

            if (ClawsRadioButton.IsChecked ?? false)
                robotPart = RobotParts.Claw;
            else if (SawRadioButton.IsChecked ?? false)
                robotPart = RobotParts.Saw;
            else if (ShowPanelRadioButton.IsChecked ?? false)
                robotPart = RobotParts.Panel;
            else
                robotPart = RobotParts.None;

            LoadRobotPart(robotPart);
        }

        private void BaseRotationSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_baseRotation == null)
                return;

            _baseRotation.Angle = BaseRotationSlider.Value;
        }

        private void Arm1RotationSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_arm1Rotation == null)
                return;

            _arm1Rotation.Angle = Arm1RotationSlider.Value;
        }

        private void Arm2RotationSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_arm2Rotation == null)
                return;

            _arm2Rotation.Angle = Arm2RotationSlider.Value;
        }

        private void Arm3RotationSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_arm3Rotation == null)
                return;

            _arm3Rotation.Angle = Arm3RotationSlider.Value;
        }

        private void ToolRotationSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_toolRotation == null)
                return;

            _toolRotation.Angle = ToolRotationSlider.Value;
        }

        private void ClawsSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_clawLeftRotation == null || _clawRightRotation == null)
                return;

            var angle = ClawsSlider.Value * 0.2;

            _clawLeftRotation.Angle = angle;
            _clawRightRotation.Angle = angle;
        }

        private void SawSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_sawRotation == null)
                return;

            _sawRotation.Angle = SawSlider.Value;
        }

        private void OnShowPanelLogoCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_shownPanelGeometryModel3D == null)
                return;

            if (ShowPanelLogoCheckBox.IsChecked ?? false)
            {
                string pngLogoFileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources/ab4d-logo-220x220.png");

                var imageBrush = new ImageBrush();
                imageBrush.ImageSource = new BitmapImage(new Uri(pngLogoFileName));

                _shownPanelGeometryModel3D.Material = new DiffuseMaterial(imageBrush);
            }
            else
            {
                _shownPanelGeometryModel3D.Material = new DiffuseMaterial(Brushes.Black);
            }
        }


        // Insert the specified transform3D before any already specified transformation in model3D
        public static void InsertTransformation(Model3D model3D, Transform3D transform3D)
        {
            if (transform3D == null)
                return;

            if (model3D == null)
                throw new ArgumentNullException("model3D");

            var currentTransform = model3D.Transform;
            if (currentTransform == null)
            {
                // No Transform yet => just add the new transform3D
                model3D.Transform = transform3D;
            }
            else
            {
                // We will add the transform3D to existing or new Transform3DGroup
                var transform3DGroup = currentTransform as Transform3DGroup;
                if (transform3DGroup == null)
                {
                    transform3DGroup = new Transform3DGroup();
                    transform3DGroup.Children.Add(currentTransform); // Add existing Transform to new Transform3DGroup

                    model3D.Transform = transform3DGroup;
                }

                transform3DGroup.Children.Insert(0, transform3D);
            }
        }
    }
}
