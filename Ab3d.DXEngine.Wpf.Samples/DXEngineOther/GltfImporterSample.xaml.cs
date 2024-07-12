using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Ab3d.DirectX;
using Ab3d.DXEngine.Wpf.Samples.Common;
using Ab3d.Visuals;
using Ab4d.DXEngine.glTF;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineOther
{
    /// <summary>
    /// Interaction logic for GltfImporterSample.xaml
    /// </summary>
    public partial class GltfImporterSample : Page
    {
        private const string InitialFileName = @"Resources\Models\voyager.gltf";

        private glTFImporter _glTfImporter;
        //private MeshGeometry3D _mesh;

        public GltfImporterSample()
        {
            InitializeComponent();


            ConvertSimpleInfoControl.InfoText = 
@"When checked then simple glTF's PhysicallyBasedMaterials (PBR)
(have MetallicFactor set to 0 and do not have MetallicRoughness texture)
are converted into StandardMaterial objects.";

            ConvertAllInfoControl.InfoText = 
@"When checked then all glTF's PhysicallyBasedMaterials (PBR)
are converted into StandardMaterial objects.
To test this import a gltf file that use PBR material.";


            var dragAndDropHelper = new DragAndDropHelper(this, ".*");
            dragAndDropHelper.FileDropped += (sender, args) => LoadModel(args.FileName);


            MainDXViewportView.DXSceneInitialized += delegate (object sender, EventArgs args)
            {
                _glTfImporter = new glTFImporter(MainDXViewportView.DXScene.DXDevice);
            
                var fileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, InitialFileName);
                LoadModel(fileName);
            };

            // IMPORTANT:
            // It is very important to call Dispose method on DXSceneView after the control is not used anymore (see help file for more info)
            this.Unloaded += (sender, args) => MainDXViewportView.Dispose();
        }

        private void LoadModel(string fileName)
        {
            if (_glTfImporter == null)
                return;


            MainViewport.Children.Clear();


            _glTfImporter.ConvertSimplePhysicallyBasedMaterialsToStandardMaterials = ConvertSimpleCheckBox.IsChecked ?? false;
            _glTfImporter.ConvertAllPhysicallyBasedMaterialsToStandardMaterials = ConvertAllCheckBox.IsChecked ?? false;

            if (LogCheckBox.IsChecked ?? false)
            {
                if (!_glTfImporter.LogInfoMessages)
                {
                    _glTfImporter.LogInfoMessages = true;
                    _glTfImporter.LoggerCallback = (logLevel, logMessage) => Debug.WriteLine(logLevel + ": " + logMessage);
                }
            }
            else
            {
                if (_glTfImporter.LogInfoMessages)
                {
                    _glTfImporter.LogInfoMessages = false;
                    _glTfImporter.LoggerCallback = null;
                }
            }
            

            var sceneNode = _glTfImporter.Import(fileName);

            var sceneNodeVisual3D = new SceneNodeVisual3D(sceneNode);

            MainViewport.Children.Add(sceneNodeVisual3D);

            if (sceneNode.Bounds != null)
            {
                Camera1.Distance = sceneNode.Bounds.GetDiagonalLength() * 1.5;
                Camera1.TargetPosition = sceneNode.Bounds.GetCenterPosition().ToWpfPoint3D();
            }
        }

        private void LoadButton_OnClick(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.InitialDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources\\Models");

            openFileDialog.Filter = "glTF file (*.gltf;*.glb)|*.gltf;*.glb";
            openFileDialog.Title = "Select glTF file";

            if ((openFileDialog.ShowDialog() ?? false) && !string.IsNullOrEmpty(openFileDialog.FileName))
                LoadModel(openFileDialog.FileName);
        }
    }
}