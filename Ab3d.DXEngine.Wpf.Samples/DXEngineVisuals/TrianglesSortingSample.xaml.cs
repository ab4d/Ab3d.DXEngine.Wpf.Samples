using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
using Ab3d.Common.Cameras;
using Ab3d.DirectX;
using Ab3d.DirectX.Controls;
using Ab3d.DirectX.Models;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    /// <summary>
    /// Interaction logic for TrianglesSortingSample.xaml
    /// </summary>
    public partial class TrianglesSortingSample : Page
    {
        private GeometryModel3D _geometryModel1;
        private GeometryModel3D _geometryModel2;

        private WpfGeometryModel3DNode _wpfGeometryModel3DNode1;

        private int _sortCount;

        private readonly string[] _fileNames = new string[] { "teapot-hires.obj", "dragon_vrip_res3.obj" };

        public TrianglesSortingSample()
        {
            InitializeComponent();


            MainDXViewportView.DXSceneDeviceCreated += delegate (object sender, EventArgs args)
            {
                MainDXViewportView.DXScene.IsTransparencySortingEnabled = true;
                MainDXViewportView.DXScene.DXDevice.CommonStates.DefaultTransparentDepthStencilState = MainDXViewportView.DXScene.DXDevice.CommonStates.DepthReadWrite;
            };

            MainDXViewportView.DXSceneInitialized += delegate (object sender, EventArgs args)
            {
                if (MainDXViewportView.DXScene != null && (IsSortingCheckBox.IsChecked ?? false))
                {
                    // Call DXScene.Update to update WorldMatrices of all the objects (this is required to correctly sort position the sorted triangles in space)
                    MainDXViewportView.DXScene.Update();

                    SortTriangles();
                }
            };

            Camera1.CameraChanged += delegate (object sender, CameraChangedRoutedEventArgs args)
            {
                if (IsSortingCheckBox.IsChecked ?? false)
                    SortTriangles();
            };


            ShowModel(modelIndex: 0);


            // IMPORTANT:
            // It is very important to call Dispose method on DXSceneView after the control is not used any more (see help file for more info)
            this.Unloaded += (sender, args) => MainDXViewportView.Dispose();
        }

        private void SortTriangles()
        {
            // Before sorting the triangles we need to get the DXEngine's SceneNode
            if (_wpfGeometryModel3DNode1 == null)
                _wpfGeometryModel3DNode1 = MainDXViewportView.GetSceneNodeForWpfObject(_geometryModel1) as WpfGeometryModel3DNode;

            if (_wpfGeometryModel3DNode1 != null)
            {
                // SortTrianglesByCameraDistance method will sort triangles (update IndexBuffer in the _wpfGeometryModel3DNode1.DXMesh)
                // so that the triangles that are farther away from the camera will be rendered first.
                //
                // It is also possible to sort triangles in the DXEngine's meshes that are defined by the SimpleMesh<T> object.

                var cameraPosition = Camera1.GetCameraPosition().ToVector3();
                bool isSorted = _wpfGeometryModel3DNode1.SortTrianglesByCameraDistance(cameraPosition, transformCamera: true);

                if (isSorted) // If the camera is not changed enough then the triangles order may not be changed
                {
                    _sortCount++;
                    InfoTextBlock.Text = $"Sorted {_sortCount} times";
                }
            }
        }

        private MeshGeometry3D GetMesh(int modelIndex)
        {
            MeshGeometry3D mesh;

            if (modelIndex == 0)
            {
                mesh = new Ab3d.Meshes.TorusKnotMesh3D(centerPosition: new Point3D(0, 0, 0),
                                                       p: 5,
                                                       q: 3,
                                                       r1: 40,
                                                       r2: 20,
                                                       r3: 7,
                                                       uSegments: 300,
                                                       vSegments: 30,
                                                       calculateNormals: true).Geometry;
            }
            else if (modelIndex < 3)
            {
                string fileName = _fileNames[modelIndex - 1];
                mesh = LoadMeshFromObjFile(fileName);
            }
            else
            {
                mesh = null;
            }

            return mesh;
        }

        private void ShowModel(int modelIndex)
        {
            var mesh1 = GetMesh(modelIndex);

            if (mesh1 == null)
                return;

            var mesh2 = mesh1.Clone(); // Clone the mesh to preserve its original triangles indices (shown in the lower model)

            
            MainViewport.Children.Clear();

            var material = new DiffuseMaterial(new SolidColorBrush(Colors.DarkGray) { Opacity = 0.5 });

            _geometryModel1 = new GeometryModel3D(mesh1, material);
            _geometryModel1.BackMaterial = material;
            Ab3d.Utilities.ModelUtils.CenterAndScaleModel3D(_geometryModel1, new Point3D(0, 60, 0), new Size3D(1000, 100, 1000));

            MainViewport.Children.Add(_geometryModel1.CreateModelVisual3D());

            _wpfGeometryModel3DNode1 = null; // reset the DXEngine's SceneNode that is created from GeometryModel3D


            _geometryModel2 = new GeometryModel3D(mesh2, material);
            _geometryModel2.BackMaterial = material;
            Ab3d.Utilities.ModelUtils.CenterAndScaleModel3D(_geometryModel2, new Point3D(0, -60, 0), new Size3D(1000, 100, 1000));

            MainViewport.Children.Add(_geometryModel2.CreateModelVisual3D());
        }

        private MeshGeometry3D LoadMeshFromObjFile(string fileName)
        {
            if (!System.IO.Path.IsPathRooted(fileName))
                fileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources\\Models\\" + fileName);

            var readerObj = new Ab3d.ReaderObj();
            var readModel3D = readerObj.ReadModel3D(fileName) as GeometryModel3D;

            if (readModel3D == null)
                return null;

            return readModel3D.Geometry as MeshGeometry3D;
        }

        private void SortButton_OnClick(object sender, RoutedEventArgs e)
        {
            // Manually sort the triangles
            SortTriangles();
        }

        private void OnIsSortingCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            SortButton.IsEnabled = !(IsSortingCheckBox.IsChecked ?? false);
        }

        private void ModelComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            ShowModel(modelIndex: ModelComboBox.SelectedIndex);
        }
    }
}
