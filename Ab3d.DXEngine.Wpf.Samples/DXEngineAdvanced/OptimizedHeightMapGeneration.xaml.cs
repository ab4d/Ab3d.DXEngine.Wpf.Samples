using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
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
using Ab3d.Meshes;
using Ab3d.Visuals;
using SharpDX;
using Material = Ab3d.DirectX.Material;
using Point = System.Windows.Point;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineAdvanced
{
    // This sample shows how to generate height maps with generating vertex buffer and index buffer directly
    // (not with first generating WPF 3D objects and then converting that into DirectX objects).
    // The sample first generates a very simple mesh that can be shown immediately,
    // then a background worked is started that generates the big height map and the required DirectX objects in the background thread.
    // When the data are prepared, then the new SceneNodes are created in the main UI thread.

    /// <summary>
    /// Interaction logic for OptimizedHeightMapGeneration.xaml
    /// </summary>
    public partial class OptimizedHeightMapGeneration : Page
    {
        private const bool AddDelay = true; // Add some Thread.Sleep during generation of height data to simulate big cpu load and to still make the sample run on slower computers

        private const int XCount = 3000;
        private const int YCount = 2000;

        private const float MaxHeightMapHeight = 100;

        private DisposeList _disposables;

        private BackgroundWorker _backgroundWorker;
        private Material _simplePositionColorMaterial;
        private Material _dxMaterial;


        //private int _xCount = 10000;
        //private int _yCount = 2000;
        // Performance values for the 10000 x 2000 height map on NVIDIA 1080:
        // DirectXOverlay: render time 0.25 ms (4000 FPS)
        // DirectXImage - with back face rendering (bottom of the height map): render time around 14 ms (70 FPS) 
        //              - without back face rendering:                         render time around 7 ms (140 FPS) 

        public OptimizedHeightMapGeneration()
        {
            InitializeComponent();


            TitleTextBlock.Text += string.Format(" ({0}x{1} height values)", XCount, YCount);

            // First create very simple height map that can be created immediately.
            // After that we will wait until DXScene is initialized and then start a background worker
            // that will generate the mesh in the background. When this is done, we will update the 3D scene.


            _disposables = new DisposeList();

            int xCount = XCount / 100;
            int yCount = YCount / 100;

            float[,] simpleHeightData;
            Color4[] simplePositionColorsArray;

            //GenerateSimpleHeightData(XCount, YCount, out heightData, out positionColorsArray);
            //GenerateRandomHeightData(XCount, YCount, out heightData, out positionColorsArray);
            GenerateSinusHeightData(xCount, yCount, null, out simpleHeightData, out simplePositionColorsArray);

            var simpleHeightMapMesh = GenerateHeightMapMesh(simpleHeightData, dxDevice: null);
            _simplePositionColorMaterial = GeneratePositionColorMaterial(simplePositionColorsArray, dxDevice: null);

            _disposables.Add(simpleHeightMapMesh);
            _disposables.Add(_simplePositionColorMaterial);

            GenerateHeightMapSceneNodes(simpleHeightMapMesh, _simplePositionColorMaterial);

            //GenerateHeightMapObject(heightData, positionColorsArray);


            MainDXViewportView.DXSceneInitialized += delegate(object sender, EventArgs args)
            {
                if (MainDXViewportView.DXScene == null)
                    return; // WPF 3D rendering

                MeshBase heightMapMesh = null;
                _dxMaterial = null;

                var dxDevice = MainDXViewportView.DXScene.DXDevice;

                _backgroundWorker = new BackgroundWorker()
                {
                    WorkerSupportsCancellation = true,
                    WorkerReportsProgress = true
                };

                _backgroundWorker.DoWork += delegate(object o, DoWorkEventArgs eventArgs)
                {
                    // 1)
                    // Generate height map data in the background.

                    float[,] heightData;
                    Color4[] positionColorsArray;

                    //GenerateSimpleHeightData(XCount, YCount, out heightData, out positionColorsArray);
                    //GenerateRandomHeightData(XCount, YCount, out heightData, out positionColorsArray);
                    GenerateSinusHeightData(XCount, YCount, _backgroundWorker, out heightData, out positionColorsArray);

                    if (_backgroundWorker.CancellationPending)
                        return;

                    // 2)
                    // Generate the mesh object and initialize it with dxDevice 
                    // This will generate DirectX resources and send them to GPU
                    heightMapMesh = GenerateHeightMapMesh(heightData, dxDevice);

                    if (_backgroundWorker.CancellationPending)
                        return;

                    _backgroundWorker.ReportProgress(95);

                    if (_dxMaterial != null)
                        _dxMaterial.Dispose();

                    // 3)
                    // Generate material with position color data and sent that to GPU
                    _dxMaterial = GeneratePositionColorMaterial(positionColorsArray, dxDevice);

                    _backgroundWorker.ReportProgress(100);
                };

                _backgroundWorker.ProgressChanged += delegate(object o, ProgressChangedEventArgs eventArgs)
                {
                    GenerationProgressBar.Value = eventArgs.ProgressPercentage;
                };

                _backgroundWorker.RunWorkerCompleted += delegate(object o, RunWorkerCompletedEventArgs eventArgs)
                {
                    // Clean and dispose existing models
                    RootContentVisual3D.Children.Clear();
                    _disposables.Dispose();

                    // Create new DisposeList
                    _disposables = new DisposeList();
                    _disposables.Add(simpleHeightMapMesh);
                    _disposables.Add(_simplePositionColorMaterial);

                    // Generate SceneNode with new heightMapMesh and dxMaterial.
                    // Note that this is a very fast operation
                    if (heightMapMesh != null && _dxMaterial != null)
                        GenerateHeightMapSceneNodes(heightMapMesh, _dxMaterial);

                    if (MainDXViewportView.IsDisposed)
                        return;

                    _backgroundWorker = null;
                    GenerationProgressBar.Visibility = Visibility.Collapsed;

                    MainDXViewportView.Refresh();
                };

                GenerationProgressBar.Value = 0;
                GenerationProgressBar.Visibility = Visibility.Visible;

                _backgroundWorker.RunWorkerAsync();
            };

            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                if (_backgroundWorker != null)
                    _backgroundWorker.CancelAsync();

                if (_dxMaterial != null)
                {
                    _dxMaterial.Dispose();
                    _dxMaterial = null;
                }
                
                if (_simplePositionColorMaterial != null)
                {
                    _simplePositionColorMaterial.Dispose();
                    _simplePositionColorMaterial = null;
                }

                _disposables.Dispose();
                MainDXViewportView.Dispose();
            };
        }

        private void GenerateSimpleHeightData(int xCount, int yCount, out float[,] heightData, out Color4[] positionColorsArray)
        {
            heightData = new float[xCount, yCount]; // This will initialize everything to 0

            int positionsCount = xCount * yCount;
            positionColorsArray = new Color4[positionsCount];

            var singleColor = Colors.Green.ToColor4();
            for (int i = 0; i < positionsCount; i++)
                positionColorsArray[i] = singleColor;
        }

        private void GenerateRandomHeightData(int xCount, int yCount, out float[,] heightData, out Color4[] positionColorsArray)
        {
            int positionsCount = xCount * yCount;

            heightData = new float[xCount, yCount]; // This will initialize everything to 0
            positionColorsArray = new Color4[positionsCount];

            var rnd = new Random();

            int positionIndex = 0;

            for (int x = 0; x < xCount; x++)
            {
                for (int y = 0; y < yCount; y++)
                {
                    var height = (float)rnd.NextDouble(); // random from 0 to 1

                    heightData[x, y] = height;
                    positionColorsArray[positionIndex] = new Color4(height, 0, 1 - height, 1); // height = blue => black; height = 1 => red

                    positionIndex++;
                }
            }
        }

        private void GenerateSinusHeightData(int xCount, int yCount, BackgroundWorker backgroundWorker, out float[,] heightData, out Color4[] positionColorsArray)
        {
            int lastPercent = 0;
            int positionsCount = xCount * yCount;

            heightData = new float[xCount, yCount]; // This will initialize everything to 0
            positionColorsArray = new Color4[positionsCount];

            int positionIndex = 0;

            // 3 waves in each direction
            double xFactor = 6 * Math.PI / xCount;
            double yFactor = 6 * Math.PI / yCount;

            for (int x = 0; x < xCount; x++)
            {
                for (int y = 0; y < yCount; y++)
                {
                    float height = (float)Math.Sin(x * xFactor) * (float)Math.Sin(y * yFactor);
                    height = height * 0.5f + 0.5f; // put in range from 0 to 1

                    heightData[x, y] = height;
                    positionColorsArray[positionIndex] = new Color4(height, 0, 1 - height, 1); // height = blue => black; height = 1 => red

                    positionIndex++;
                }

                if (backgroundWorker != null)
                {
                    int percent = 90 * x / xCount; // we assume that 90% of the time is spent to generate the data for the float array. 10% is spent to create DirectX resources and send that to GPU.

                    if (lastPercent != percent)
                    {
                        if (AddDelay)
                            Thread.Sleep(10);

                        backgroundWorker.ReportProgress(percent);

                        lastPercent = percent;
                    }
                }
            }
        }

        private SimpleMesh<PositionNormalTexture> GenerateHeightMapMesh(float[,] heightData, DXDevice dxDevice)
        {
            PositionNormalTexture[] vertexBuffer;
            int[] indexBuffer;

            int xCount = heightData.GetUpperBound(0) + 1;
            int yCount = heightData.GetUpperBound(1) + 2;

            CreateHeightVertexAndIndexBuffer(heightData,
                                             centerPosition: new Vector3(0, 0, 0),
                                             size: new Vector3(1000, MaxHeightMapHeight, 1000.0f * (float) yCount / (float) xCount),
                                             vertexBuffer: out vertexBuffer,
                                             indexBuffer: out indexBuffer);

            var heightMapMesh = new SimpleMesh<PositionNormalTexture>(vertexBuffer,
                                                                      indexBuffer,
                                                                      inputLayoutType: InputLayoutType.Position | InputLayoutType.Normal | InputLayoutType.TextureCoordinate,
                                                                      name: "HeightSimpleMesh");

            if (_backgroundWorker != null && _backgroundWorker.CancellationPending)
                return null;

            // If DXDevice is already initialized, then we can also initialize (create DirectX resources) for the SimpleMesh.
            // This will create the DirectX resources and send them to the GPU
            if (dxDevice != null && dxDevice.Device != null)
                heightMapMesh.InitializeResources(dxDevice);

            return heightMapMesh; 
        }

        private Ab3d.DirectX.Material GeneratePositionColorMaterial(Color4[] positionColorsArray, DXDevice dxDevice)
        {
            Ab3d.DirectX.Material dxMaterial;

            if (positionColorsArray != null)
            {
                dxMaterial = new Ab3d.DirectX.Materials.VertexColorMaterial()
                {
                    PositionColors = positionColorsArray, // The PositionColors property is used to specify colors for each vertex
                    CreateDynamicBuffer = false, // We will not update the colors frequently

                    // To show specular effect set the specular data here:
                    //SpecularPower = 16,
                    //SpecularColor = Color3.White,
                    //HasSpecularColor = true
                };
            }
            else
            {
                // Solid color material:
                var diffuseMaterial = new DiffuseMaterial(Brushes.Green);

                // Texture material:
                //var imageBrush = new ImageBrush();
                //imageBrush.ImageSource = new BitmapImage(new Uri("pack://application:,,,/Resources/GrassTexture.jpg"));
                //var diffuseMaterial = new DiffuseMaterial(imageBrush);

                dxMaterial = new Ab3d.DirectX.Materials.WpfMaterial(diffuseMaterial);
            }

            // If DXDevice is already initialized, then we can also initialize (create DirectX resources) for the SimpleMesh.
            // This will create the DirectX resources and send them to the GPU
            if (dxDevice != null)
                dxMaterial.InitializeResources(dxDevice);

            return dxMaterial;
        }



        //private void GenerateHeightMapObject(float[,] heightData, Color4[] positionColorsArray)
        private void GenerateHeightMapSceneNodes(MeshBase heightMapMesh, Ab3d.DirectX.Material dxMaterial)
        {
            var meshObjectNode = new Ab3d.DirectX.MeshObjectNode(heightMapMesh, dxMaterial);
            meshObjectNode.Name = "HeightMeshObjectNode";

            _disposables.Add(meshObjectNode);

            var sceneNodeVisual3D = new SceneNodeVisual3D(meshObjectNode);
            RootContentVisual3D.Children.Add(sceneNodeVisual3D);


            // If you also want to render back faces of the height map you need to create another MeshObjectNode and set its IsBackFaceMaterial to true.
            // You can reuse the mesh. But this still requires almost twice the GPU power.
            var backDiffuseMaterial = new DiffuseMaterial(Brushes.Gray);
            var backDXMaterial = new Ab3d.DirectX.Materials.WpfMaterial(backDiffuseMaterial);

            meshObjectNode = new Ab3d.DirectX.MeshObjectNode(heightMapMesh, backDXMaterial);
            meshObjectNode.IsBackFaceMaterial = true;
            meshObjectNode.Name = "HeightBackMeshObjectNode";

            _disposables.Add(meshObjectNode);

            sceneNodeVisual3D = new SceneNodeVisual3D(meshObjectNode);
            RootContentVisual3D.Children.Add(sceneNodeVisual3D);
        }


        #region Helper classes (taken from internal Ab3d.DXEngine and Ab3d.PowerToys code)

        private void CreateHeightVertexAndIndexBuffer(float[,] heightData, Vector3 centerPosition, Vector3 size, out PositionNormalTexture[] vertexBuffer, out int[] indexBuffer)
        {
            int xCount = heightData.GetUpperBound(0) + 1; // NOTE: This is not length but max element index (so length is GetUpperBound() + 1)
            int yCount = heightData.GetUpperBound(1) + 1;

            float x_step = size.X / ((float)(xCount - 1));
            float z_step = size.Z / ((float)(yCount - 1));


            vertexBuffer = new PositionNormalTexture[xCount * yCount];

            float x_pos = centerPosition.X - (size.X / 2.0f);
            float yScale = size.Y;
            float yOffset = centerPosition.Y; // if one value is 0, then it is positioned at the _centerPosition.Y (positive values are above center; negative values below center)

            float xCount1 = (float)(xCount - 1);
            float yCount1 = (float)(yCount - 1);

            int index = 0;

            for (int x = 0; x < xCount; x++)
            {
                float z_pos = centerPosition.Z - (size.Z / 2.0f);
                float xTexture = ((float)x) / xCount1;

                for (int y = 0; y < yCount; y++)
                {
                    vertexBuffer[index].Position = new Vector3(x_pos, heightData[x, y] * yScale + yOffset, z_pos);
                    vertexBuffer[index].TextureCoordinate = new Vector2(xTexture, (float)y / yCount1);

                    index++;

                    z_pos += z_step;
                }

                x_pos += x_step;
            }



            indexBuffer = new int[(xCount - 1) * (yCount - 1) * 6];
            index = 0;

            for (int x = 0; x < xCount - 1; x++)
            {
                for (int y = 0; y < yCount - 1; y++)
                {
                    if ((x + y) % 2 == 1)
                    {
                        indexBuffer[index]     = y + (x * yCount);
                        indexBuffer[index + 1] = (y + 1) + x * yCount;
                        indexBuffer[index + 2] = (y) + ((x + 1) * yCount);

                        indexBuffer[index + 3] = y + ((x + 1) * yCount);
                        indexBuffer[index + 4] = (y + 1) + ((x) * yCount);
                        indexBuffer[index + 5] = (y + 1) + ((x + 1) * yCount);
                    }
                    else
                    {
                        indexBuffer[index]     = y + (x * yCount);
                        indexBuffer[index + 1] = (y + 1) + x * yCount;
                        indexBuffer[index + 2] = (y + 1) + ((x + 1) * yCount);

                        indexBuffer[index + 3] = y + (x * yCount);
                        indexBuffer[index + 4] = (y + 1) + ((x + 1) * yCount);
                        indexBuffer[index + 5] = y + ((x + 1) * yCount);
                    }

                    index += 6;
                }
            }

            
            // Calculate normals in the vertexBuffer
            CalculateNormals(vertexBuffer, indexBuffer, normalize: true);
        }



        // NOTE: Because we normalize the normals at the end and sum the cross products, the normals from triangles with bigger area have more weight
        // If we would normalize each cross product, than the area would not affect the final normal (but this gives worse results and is much slower)

        // This method is more than 10 times faster than old CalculateNormals:
        // For sphere with 251001 positions and 1500000 indices (500 segments) the times are:
        // old calculate normals: 386.174ms
        // new calculate normals: 32.236ms
        public static void CalculateNormals(PositionNormalTexture[] vertexBuffer, int[] indices, bool normalize)
        {
            if (vertexBuffer == null || indices == null)
                return;

            int indicesCount = indices.Length;
            indicesCount -= indicesCount % 3; // snap to 3

            int vertexCount = vertexBuffer.Length;

            if (indicesCount == 0 || vertexCount == 0)
                return;


            GCHandle vertexBufferHandle = GCHandle.Alloc(vertexBuffer, GCHandleType.Pinned);
            GCHandle indicesHandle = GCHandle.Alloc(indices, GCHandleType.Pinned);

            try
            {
                unsafe
                {
                    float* vertexBufferPtr = ((float*)vertexBufferHandle.AddrOfPinnedObject().ToPointer());
                    int* indicesPtr = ((int*)indicesHandle.AddrOfPinnedObject().ToPointer());

                    // First zero normals
                    float* savedVertexBufferPtr = vertexBufferPtr;

                    for (int i = 0; i < vertexCount; i++)
                    {
                        *(vertexBufferPtr + 3) = 0.0f;
                        *(vertexBufferPtr + 4) = 0.0f;
                        *(vertexBufferPtr + 5) = 0.0f;

                        vertexBufferPtr += PositionNormalTexture.SizeInFloats;
                    }

                    float* p1Ptr, p2Ptr, p3Ptr;
                    int p1Index, p2Index, p3Index;
                    float x1, y1, z1; // p1
                    float x2, y2, z2; // p2
                    float x3, y3, z3; // p3
                    float v1x, v1y, v1z; // p2 - p1
                    float v2x, v2y, v2z; // p3 - p1
                    float nx, ny, nz; // normal

                    vertexBufferPtr = savedVertexBufferPtr;

                    for (int i = 0; i < indicesCount; i += 3)
                    {
                        p1Index = *indicesPtr;
                        p2Index = *(indicesPtr + 1);
                        p3Index = *(indicesPtr + 2);

                        if (p1Index > vertexCount || p2Index > vertexCount || p3Index > vertexCount) // UH: Broken indices array - prevent pointer access violation
                        {
#if DEBUG
                            throw new Exception("Indices array value out of range");
#else
                            // The indices array is broken - it has elements that are out of bounds
                            // Try to fix that with setting invalid indices to 0
                            *indicesPtr = 0;
                            *(indicesPtr + 1) = 0;
                            *(indicesPtr + 2) = 0;

                            p1Index = 0;
                            p2Index = 0;
                            p3Index = 0;
#endif
                        }


                        indicesPtr += 3;

                        //p1 = meshPositions[p1Index];
                        //p2 = meshPositions[p2Index];
                        //p3 = meshPositions[p3Index];

                        p1Ptr = vertexBufferPtr + p1Index * PositionNormalTexture.SizeInFloats;
                        x1 = *p1Ptr;
                        y1 = *(p1Ptr + 1);
                        z1 = *(p1Ptr + 2);

                        p2Ptr = vertexBufferPtr + p2Index * PositionNormalTexture.SizeInFloats;
                        x2 = *p2Ptr;
                        y2 = *(p2Ptr + 1);
                        z2 = *(p2Ptr + 2);

                        p3Ptr = vertexBufferPtr + p3Index * PositionNormalTexture.SizeInFloats;
                        x3 = *p3Ptr;
                        y3 = *(p3Ptr + 1);
                        z3 = *(p3Ptr + 2);


                        //oneNormal = Vector3.Cross(p2 - p1, p3 - p1);

                        v1x = x2 - x1;
                        v1y = y2 - y1;
                        v1z = z2 - z1;

                        v2x = x3 - x1;
                        v2y = y3 - y1;
                        v2z = z3 - z1;

                        //result = new Vector3(
                        //    (left.Y * right.Z) - (left.Z * right.Y),
                        //    (left.Z * right.X) - (left.X * right.Z),
                        //    (left.X * right.Y) - (left.Y * right.X));

                        nx = (v1y * v2z) - (v1z * v2y);
                        ny = (v1z * v2x) - (v1x * v2z);
                        nz = (v1x * v2y) - (v1y * v2x);


                        //normals[p1Index] += oneNormal;
                        //normals[p2Index] += oneNormal;
                        //normals[p3Index] += oneNormal;

                        *(p1Ptr + 3) += nx;
                        *(p1Ptr + 4) += ny;
                        *(p1Ptr + 5) += nz;

                        *(p2Ptr + 3) += nx;
                        *(p2Ptr + 4) += ny;
                        *(p2Ptr + 5) += nz;

                        *(p3Ptr + 3) += nx;
                        *(p3Ptr + 4) += ny;
                        *(p3Ptr + 5) += nz;
                    }

                    if (normalize)
                    {
                        float length, lengthInv;

                        for (int i = 0; i < vertexCount; i++)
                        {
                            // read one normal
                            nx = *(vertexBufferPtr + 3);
                            ny = *(vertexBufferPtr + 4);
                            nz = *(vertexBufferPtr + 5);

                            length = (float)Math.Sqrt((double)(nx * nx + ny * ny + nz * nz));

                            if (length == 0.0f)
                            {
                                nx = ny = nz = float.NaN;
                            }
                            else
                            {
                                lengthInv = 1.0f / length; // Multiply is much faster than device - therefore we device once and than multiply

                                nx *= lengthInv;
                                ny *= lengthInv;
                                nz *= lengthInv;
                            }

                            // Write back
                            *(vertexBufferPtr + 3) = nx;
                            *(vertexBufferPtr + 4) = ny;
                            *(vertexBufferPtr + 5) = nz;

                            vertexBufferPtr += PositionNormalTexture.SizeInFloats;
                        }
                    }
                }
            }
            finally
            {
                indicesHandle.Free();
                vertexBufferHandle.Free();
            }
        }

        #endregion
    }
}
