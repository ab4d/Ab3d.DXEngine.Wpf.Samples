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
using Ab3d.Animation;
using Ab3d.Common;
using Ab3d.DirectX;
using Ab3d.Utilities;
using Ab3d.Visuals;
using SharpDX;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineAdvanced
{
    // This sample shows how to create one vertex buffer and one index buffer for many mesh geometries
    // and how it is possible to specify different materials for different for
    // parts of this combined mesh.
    // 
    // This can be achieved with using SubMeshes in SimpleMesh.
    // The SimpleMesh will define one big vertex buffer and one big index buffer.
    // The SimpleMesh can define multiple SubMeshes.
    // Each SubMesh define a material index and a subset of triangle indices (from index buffer)
    // that is rendered with this SubMesh. The subset is defined by StartIndexLocation and IndexCount.
    //
    // The sample also shows how to change the StartIndexLocation and IndexCount
    // at runtime to change which parts use which material.
    // This is the most efficient way of changing the material.
    //
    // Note that when SubMeshes are changed you need to call UpdateMesh method on the MeshObjectNode.
    //
    // Additional note:
    // The same sample as here can be also created with using instancing and InstancedMeshGeometry3D.
    // This would make the code much simpler and in some cases with better performance.
    // But the purpose of this sample was to simply demonstrate how to create and change SubMeshes.
    // Also, with SubMeshes and SimpleMesh it is possible to create a vertex and index buffer
    // that is created from multiple different meshes.
    // 
    // When a Model3DGroup object is frozen, the DXEngine uses similar technique as used here.
    // It combines all the child GeometryModel3D objects into one vertex and index buffer
    // and then for each material that is used in GeometryModel3D objects it creates its own SubMesh.
    // This way the frozen Model3DGroup object can be rendered in the most efficient way.
    // This is done with the WpfOptimizedModel3DGroupNode.


    /// <summary>
    /// Interaction logic for SubMeshesSample.xaml
    /// </summary>
    public partial class SubMeshesSample : Page, ICompositionRenderingSubscriber
    {
        // Sample parameters:
        private const double AnimationSpeedFactor = 0.3; // when 1 then the whole sinus animation is played in 1 second

        private const int XCount = 30;
        private const int YCount = 16;
        private const int ZCount = 30;

        private const float BoxSize = 10;
        private const float BoxesMargin = 10;



        private MeshObjectNode _meshObjectNode;

        private SimpleMesh<PositionNormalTexture> _multiMaterialMesh;

        private int _oneMeshTriangleIndicesCount;

        private DateTime _animationStartTime;

        private DisposeList _disposables;

        private int _indexBufferLength;
        private int _firstColorIndex;
        private int _secondColorIndex;

        private Random _rnd = new Random();


        public SubMeshesSample()
        {
            InitializeComponent();

            _disposables = new DisposeList();

            CreateScene();

            StartAnimation();


            this.Unloaded += delegate (object sender, RoutedEventArgs e)
            {
                CompositionRenderingHelper.Instance.Unsubscribe(this);

                _disposables.Dispose();

                MainDXViewportView.Dispose();
            };
        }

        private void StartAnimation()
        {
            _animationStartTime = DateTime.Now;

            // Use CompositionRenderingHelper from Ab3d.PowerToys to safely subscribe to
            // CompositionTarget.Rendering event that is called after each WPF rendering. 
            // This prevent to pin the subscribed object (this) to a static event in the WPF.
            CompositionRenderingHelper.Instance.Subscribe(this);
        }

        public void OnRendering(EventArgs e)
        {
            Animate();
        }

        private void Animate()
        {
            var elapsedTime = DateTime.Now - _animationStartTime;

            var elapsedSeconds = elapsedTime.TotalSeconds;

            var factor1 = Math.Sin(elapsedSeconds * Math.PI * 2 * AnimationSpeedFactor);
            var factor2 = Math.Sin(elapsedSeconds * Math.PI * 2 * AnimationSpeedFactor);


            int newFirstColorIndex  = _firstColorIndex  + (int)((factor1 * _firstColorIndex) / _oneMeshTriangleIndicesCount) * _oneMeshTriangleIndicesCount;
            int newSecondColorIndex = _secondColorIndex + (int)((factor2 * _firstColorIndex) / _oneMeshTriangleIndicesCount) * _oneMeshTriangleIndicesCount;

            _multiMaterialMesh.SubMeshes[0].StartIndexLocation = 0;
            _multiMaterialMesh.SubMeshes[0].IndexCount         = newFirstColorIndex;

            _multiMaterialMesh.SubMeshes[1].StartIndexLocation = newFirstColorIndex;
            _multiMaterialMesh.SubMeshes[1].IndexCount         = newSecondColorIndex - newFirstColorIndex;

            _multiMaterialMesh.SubMeshes[2].StartIndexLocation = newSecondColorIndex;
            _multiMaterialMesh.SubMeshes[2].IndexCount         = _indexBufferLength - newSecondColorIndex;

            _meshObjectNode.UpdateMesh();
        }

        private void CreateScene()
        {
            var boxMeshGeometry3D = new Ab3d.Meshes.BoxMesh3D(new Point3D(0, 0, 0), new Size3D(BoxSize, BoxSize, BoxSize), 1, 1, 1).Geometry;

            _oneMeshTriangleIndicesCount = boxMeshGeometry3D.TriangleIndices.Count;


            PositionNormalTexture[] vertexBuffer;
            int[] indexBuffer;

            CreateMultiMeshBuffer(center: new Vector3(0, 0, 0), 
                                  size: new Vector3(XCount * (BoxSize + BoxesMargin), YCount * (BoxSize + BoxesMargin), ZCount * (BoxSize + BoxesMargin)), 
                                  xCount: XCount, yCount: YCount, zCount: ZCount, 
                                  meshGeometry3D: boxMeshGeometry3D,
                                  vertexBuffer: out vertexBuffer, 
                                  indexBuffer: out indexBuffer);

            _multiMaterialMesh = new SimpleMesh<PositionNormalTexture>(vertexBuffer, indexBuffer,
                                                                inputLayoutType: InputLayoutType.Position | InputLayoutType.Normal | InputLayoutType.TextureCoordinate);


            _indexBufferLength = indexBuffer.Length;

            // i1 is at 1/4 of the height of the box
            _firstColorIndex = (int)(_indexBufferLength / 4);

            // i2 is at 3/4 of the height
            _secondColorIndex = _firstColorIndex * 3;

            _multiMaterialMesh.SubMeshes = new SubMesh[]
            {
                new SubMesh("SubMesh1") { MaterialIndex = 0, StartIndexLocation = 0,                 IndexCount = _firstColorIndex },
                new SubMesh("SubMesh2") { MaterialIndex = 1, StartIndexLocation = _firstColorIndex , IndexCount = _secondColorIndex - _firstColorIndex },
                new SubMesh("SubMesh3") { MaterialIndex = 2, StartIndexLocation = _secondColorIndex, IndexCount = _indexBufferLength - _secondColorIndex },
            };

            _disposables.Add(_multiMaterialMesh);


            var materials = new Ab3d.DirectX.Material[]
            {
                new Ab3d.DirectX.Materials.StandardMaterial() { DiffuseColor = Colors.DimGray.ToColor3() },
                new Ab3d.DirectX.Materials.StandardMaterial() { DiffuseColor = Colors.Silver.ToColor3() },
                new Ab3d.DirectX.Materials.StandardMaterial() { DiffuseColor = Colors.Gold.ToColor3() },
            };

            _meshObjectNode = new Ab3d.DirectX.MeshObjectNode(_multiMaterialMesh, materials);

            _disposables.Add(_meshObjectNode);

            // Use SceneNodeVisual3D to show SceneNode in DXViewportView
            var sceneNodeVisual3D = new SceneNodeVisual3D(_meshObjectNode);

            MainViewport.Children.Add(sceneNodeVisual3D);
        }

        public static void CreateMultiMeshBuffer(Vector3 center, Vector3 size, int xCount, int yCount, int zCount, MeshGeometry3D meshGeometry3D,
                                                 out PositionNormalTexture[] vertexBuffer, out int[] indexBuffer)
        {
            // Because we will iterate through Positions, Normals and other collections multiple times,
            // we copy the data into simple arrays because accessing simple arrays is significantly faster then Point3DCollections and other collection
            // (the reason for that is that in each getter in Point3DCollections there is a check if we are on the correct thread - the one that created the collection).
            var positionsCount = meshGeometry3D.Positions.Count;

            var positions = new Point3D[positionsCount];
            meshGeometry3D.Positions.CopyTo(positions, 0);


            if (meshGeometry3D.Normals.Count < positionsCount) // Each position must have its Normal
                throw new Exception("Invalid Normals");

            var normals = new Vector3D[positionsCount];
            meshGeometry3D.Normals.CopyTo(normals, 0);


            // If we do not use textures, we can skip TextureCoordinates
            Point[] textureCoordinates;

            if (meshGeometry3D.TextureCoordinates != null && meshGeometry3D.TextureCoordinates.Count > 0)
            {
                if (meshGeometry3D.TextureCoordinates.Count < positionsCount)
                    throw new Exception("Invalid TextureCoordinates");

                textureCoordinates = new System.Windows.Point[positionsCount];
                meshGeometry3D.TextureCoordinates.CopyTo(textureCoordinates, 0);
            }
            else
            {
                textureCoordinates = null;
            }


            var triangleIndicesCount = meshGeometry3D.TriangleIndices.Count;
            var triangleIndices = new Int32[triangleIndicesCount];
            meshGeometry3D.TriangleIndices.CopyTo(triangleIndices, 0);



            float xStep = (float)(size.X / xCount);
            float yStep = (float)(size.Y / yCount);
            float zStep = (float)(size.Z / zCount);

            vertexBuffer = new PositionNormalTexture[positionsCount * xCount * yCount * zCount];
            indexBuffer  = new int[triangleIndicesCount * xCount * yCount * zCount];

            int vertexBufferIndex = 0;
            int indexBufferIndex = 0;

            for (int y = 0; y < yCount; y++)
            {
                float yPos = (float)(center.Y - (size.Y / 2.0) + (y * yStep));

                for (int z = 0; z < zCount; z++)
                {
                    float zPos = (float)(center.Z - (size.Z / 2.0) + (z * zStep));

                    for (int x = 0; x < xCount; x++)
                    {
                        float xPos = (float)(center.X - (size.X / 2.0) + (x * xStep));


                        // Save index for the start of the mesh
                        int vertexBufferStartIndex = vertexBufferIndex;

                        if (textureCoordinates != null)
                        {
                            for (int positionIndex = 0; positionIndex < positionsCount; positionIndex++)
                            {
                                // TODO: We could further optimize this with converting positions, normals and textureCoordinates to Vector3 arrays before entering the loops
                                vertexBuffer[vertexBufferIndex] = new PositionNormalTexture(
                                                                            new Vector3((float)positions[positionIndex].X + xPos, (float)positions[positionIndex].Y + yPos, (float)positions[positionIndex].Z + zPos),
                                                                            normals[positionIndex].ToVector3(),
                                                                            new Vector2((float)textureCoordinates[positionIndex].X, (float)textureCoordinates[positionIndex].Y));

                                vertexBufferIndex++;
                            }
                        }
                        else
                        {
                            for (int positionIndex = 0; positionIndex < positionsCount; positionIndex++)
                            {
                                vertexBuffer[vertexBufferIndex] = new PositionNormalTexture(
                                                                            new Vector3((float)positions[positionIndex].X + xPos, (float)positions[positionIndex].Y + yPos, (float)positions[positionIndex].Z + zPos),
                                                                            normals[positionIndex].ToVector3(),
                                                                            new Vector2(0, 0));

                                vertexBufferIndex++;
                            }
                        }


                        for (int instanceIndex = 0; instanceIndex < triangleIndicesCount; instanceIndex++)
                        {
                            indexBuffer[indexBufferIndex] = triangleIndices[instanceIndex] + vertexBufferStartIndex;
                            indexBufferIndex++;
                        }
                    }
                }
            }
        }

        private void ChangeColorsButton_OnClick(object sender, RoutedEventArgs e)
        {
            // It is also possible to change the materials of the SumMeshes at runtime.

            ((Ab3d.DirectX.Materials.StandardMaterial)_meshObjectNode.Materials[0]).DiffuseColor = GetRandomColor().ToColor3();
            ((Ab3d.DirectX.Materials.StandardMaterial)_meshObjectNode.Materials[1]).DiffuseColor = GetRandomColor().ToColor3();
            ((Ab3d.DirectX.Materials.StandardMaterial)_meshObjectNode.Materials[2]).DiffuseColor = GetRandomColor().ToColor3();

            // After that you need to call UpdateMaterial method.
            _meshObjectNode.UpdateMaterial();
        }

        private Color GetRandomColor()
        {
            return Color.FromRgb((byte) _rnd.Next(255), (byte) _rnd.Next(255), (byte) _rnd.Next(255));
        }
    }
}
