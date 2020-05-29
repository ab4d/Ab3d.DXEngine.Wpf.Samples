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
using Ab3d.DirectX;
using Ab3d.Visuals;
using SharpDX;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineAdvanced
{
    /// <summary>
    /// Interaction logic for ObjectSelectionWithSubMeshes.xaml
    /// </summary>
    public partial class ObjectSelectionWithSubMeshes : Page
    {
        private MeshObjectNode _meshObjectNode;

        private SimpleMesh<PositionNormalTexture> _multiMaterialMesh;
        
        private int _oneMeshTriangleIndicesCount;

        private DisposeList _disposables;

        private SharpDX.Point _lastMousePosition;
        private int _lastSelectedSphereIndex;
        private MeshOctTree _octTree;

        public ObjectSelectionWithSubMeshes()
        {
            InitializeComponent();

            _disposables = new DisposeList();
            CreateScene();

            SetupHitTesting();

            this.Unloaded += delegate (object sender, RoutedEventArgs e)
            {
                _disposables.Dispose();
                MainDXViewportView.Dispose();
            };
        }

        private void CreateScene()
        {
            int xCount = 40;
            int yCount = 1;
            int zCount = 40;

            float sphereRadius = 10;
            float sphereMargin = 10;

            var sphereMeshGeometry3D = new Ab3d.Meshes.SphereMesh3D(new Point3D(0, 0, 0), sphereRadius, 10).Geometry;

            _oneMeshTriangleIndicesCount = sphereMeshGeometry3D.TriangleIndices.Count;


            PositionNormalTexture[] vertexBuffer;
            int[] indexBuffer;

            var size = new Vector3(xCount * (sphereRadius + sphereMargin), yCount * (sphereRadius + sphereMargin), zCount * (sphereRadius + sphereMargin));

            SubMeshesSample.CreateMultiMeshBuffer(center: new Vector3(0, 0, 0),
                                                  size: size,
                                                  xCount: xCount, yCount: yCount, zCount: zCount,
                                                  meshGeometry3D: sphereMeshGeometry3D,
                                                  vertexBuffer: out vertexBuffer,
                                                  indexBuffer: out indexBuffer);

            _multiMaterialMesh = new SimpleMesh<PositionNormalTexture>(vertexBuffer, indexBuffer,
                                                                       inputLayoutType: InputLayoutType.Position | InputLayoutType.Normal | InputLayoutType.TextureCoordinate);


            // Create all 3 SubMeshes at the beginning.
            // Though at first only the first SubMesh will be rendered (the other two have IndexCount set to 0),
            // this will allow us to simply change the StartIndexLocation and IndexCount of the SubMeshes
            // to show selected part without adding or removing any SubMesh (this would regenerate the RenderingQueues).
            // This way the selection is almost a no-op (only changing a few integer values and rendering the scene again).
            _multiMaterialMesh.SubMeshes = new SubMesh[]
            {
                // First sub-mesh will render triangles from the first to the start of selection (or all triangles if there is no selection)
                new SubMesh("MainSubMesh1")     { MaterialIndex = 0, StartIndexLocation = 0, IndexCount = indexBuffer.Length },

                // Second sub-mesh will render triangles after the selection (this one follows the first on to preserve the same material)
                new SubMesh("MainSubMesh2")     { MaterialIndex = 0, StartIndexLocation = 0, IndexCount = 0 },

                // The third sub-mesh will render selected triangles and will use the second material for that.
                new SubMesh("SelectionSubMesh") { MaterialIndex = 1, StartIndexLocation = 0, IndexCount = 0 },
            };

            _disposables.Add(_multiMaterialMesh);

            // Create MeshOctTree from vertexBuffer.
            // This will significantly improve hit testing performance (check this with uncommenting the dxScene.GetClosestHitObject call in OnMouseMouse method).
            _octTree = new MeshOctTree(vertexBuffer, indexBuffer);


            var materials = new Ab3d.DirectX.Material[]
            {
                new Ab3d.DirectX.Materials.StandardMaterial() { DiffuseColor = Colors.Green.ToColor3() },
                new Ab3d.DirectX.Materials.StandardMaterial() { DiffuseColor = Colors.Red.ToColor3() }
            };

            _meshObjectNode = new Ab3d.DirectX.MeshObjectNode(_multiMaterialMesh, materials);

            _disposables.Add(_meshObjectNode);

            // Use SceneNodeVisual3D to show SceneNode in DXViewportView
            var sceneNodeVisual3D = new SceneNodeVisual3D(_meshObjectNode);

            MainViewport.Children.Add(sceneNodeVisual3D);
        }

        private void SelectSphere(int selectedSphereIndex)
        {
            if (selectedSphereIndex < 0)
            {
                // No sphere selected => render all the spheres with the first SubMesh
                _multiMaterialMesh.SubMeshes[0].StartIndexLocation = 0;
                _multiMaterialMesh.SubMeshes[0].IndexCount         = _multiMaterialMesh.IndexCount;

                _multiMaterialMesh.SubMeshes[1].StartIndexLocation = 0;
                _multiMaterialMesh.SubMeshes[1].IndexCount         = 0;

                _multiMaterialMesh.SubMeshes[2].StartIndexLocation = 0;
                _multiMaterialMesh.SubMeshes[2].IndexCount         = 0;
            }
            else
            {
                int selectedSphereTriangleIndex = selectedSphereIndex * _oneMeshTriangleIndicesCount;

                // Setup StartIndexLocation and IndexCount so that
                // first and second SubMesh will render non-selected spheres.
                // The third SubMesh will render selected sphere.
                _multiMaterialMesh.SubMeshes[0].StartIndexLocation = 0;
                _multiMaterialMesh.SubMeshes[0].IndexCount         = selectedSphereTriangleIndex;

                _multiMaterialMesh.SubMeshes[1].StartIndexLocation = selectedSphereTriangleIndex + _oneMeshTriangleIndicesCount;
                _multiMaterialMesh.SubMeshes[1].IndexCount         = _multiMaterialMesh.IndexCount - selectedSphereTriangleIndex - _oneMeshTriangleIndicesCount;

                _multiMaterialMesh.SubMeshes[2].StartIndexLocation = selectedSphereTriangleIndex;
                _multiMaterialMesh.SubMeshes[2].IndexCount         = _oneMeshTriangleIndicesCount;
            }

            _meshObjectNode.UpdateMesh();
        }


        private void SetupHitTesting()
        {
            _lastSelectedSphereIndex =  -1;
            ViewportBorder.MouseMove += OnMouseMove;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            var dxScene = MainDXViewportView.DXScene;

            if (dxScene == null)
                return;

            var mousePosition = e.GetPosition(ViewportBorder);

            int xPos = (int)mousePosition.X;
            int yPos = (int)mousePosition.Y;

            if (xPos == _lastMousePosition.X && yPos == _lastMousePosition.Y)
                return;


            _lastMousePosition = new SharpDX.Point(xPos, yPos);

            var mouseRay = dxScene.GetRayFromCamera(xPos, yPos);


            // Using OctTree significantly improve hit testing performance
            // Check this with uncommenting the following line (and commenting the use of OctTree):

            //var hitResult = dxScene.GetClosestHitObject(mouseRay);
            var hitResult = _octTree.HitTest(ref mouseRay, new DXHitTestContext(dxScene));


            int selectedSphereIndex;

            if (hitResult == null)
                selectedSphereIndex = -1;
            else
                selectedSphereIndex = (hitResult.TriangleIndex * 3) / _oneMeshTriangleIndicesCount;

            if (selectedSphereIndex == _lastSelectedSphereIndex)
                return;

            SelectSphere(selectedSphereIndex);
        }
    }
}
