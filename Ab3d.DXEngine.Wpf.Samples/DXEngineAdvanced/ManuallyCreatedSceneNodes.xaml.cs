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
using Ab3d.DirectX.Materials;
using Ab3d.DirectX.Models;
using Ab3d.Visuals;
using SharpDX;
using SharpDX.Direct3D11;
using Buffer = System.Buffer;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineAdvanced
{
    /// <summary>
    /// Interaction logic for ManuallyCreatedSceneNodes.xaml
    /// </summary>
    public partial class ManuallyCreatedSceneNodes : Page
    {
        private DisposeList _disposables;
        private WireCrossVisual3D _wireCrossVisual3D;

        public ManuallyCreatedSceneNodes()
        {
            InitializeComponent();

            
            // Wait until the DirectX device is created and then create the sample objects
            MainDXViewportView.DXSceneDeviceCreated += delegate(object sender, EventArgs args)
            {
                CreateScene();
                ResetCamera();
            };

            MainDXViewportView.SceneRendered += MainDXViewportViewOnSceneRendered;


            // Show 3D WireCrossVisual3D at the location of camera rotation
            MouseCameraController1.CameraRotateStarted += delegate(object sender, EventArgs args)
            {
                if (_wireCrossVisual3D != null)
                {
                    MainViewport.Children.Remove(_wireCrossVisual3D);
                    _wireCrossVisual3D = null;
                }

                Point3D rotationCenter;

                if (Camera1.RotationCenterPosition.HasValue)
                    rotationCenter = Camera1.RotationCenterPosition.Value;
                else
                    rotationCenter = Camera1.TargetPosition;


                _wireCrossVisual3D = new WireCrossVisual3D()
                {
                    LineColor     = Colors.Red,
                    LineThickness = 3,
                    LinesLength   = 100,
                    Position      = rotationCenter
                };

                MainViewport.Children.Add(_wireCrossVisual3D);
            };

            MouseCameraController1.CameraRotateEnded += delegate(object sender, EventArgs args)
            {
                if (_wireCrossVisual3D != null)
                {
                    MainViewport.Children.Remove(_wireCrossVisual3D);
                    _wireCrossVisual3D = null;
                }
            };


            this.Unloaded += delegate(object sender, RoutedEventArgs e)
            {
                if (_disposables != null)
                {
                    _disposables.Dispose();
                    _disposables = null;
                }

                MainDXViewportView.Dispose();
            };
        }

        private void CreateScene()
        {
            // IMPORTANT:
            // Before the Form is closed, we need to dispose all the DXEngine objects that we created (all that implement IDisposable).
            // This means that all materials, Mesh objects and SceneNodes need to be disposed.
            // To make this easier, we can use the DisposeList collection that will hold IDisposable objects.
            _disposables = new DisposeList();


            //
            // 1)
            // 
            // The easiest way to add 3D models to DXEngine's scene is to add WPF's Visual3D objects to Viewport3D.Children collection:

            var pyramidVisual3D = new Ab3d.Visuals.PyramidVisual3D()
            {
                BottomCenterPosition = new Point3D(-100, 0, 0),
                Size = new Size3D(80, 50, 80),
                Material = new DiffuseMaterial(Brushes.Blue)
            };

            pyramidVisual3D.SetName("PyramidVisual3D");

            MainViewport.Children.Add(pyramidVisual3D);


            // We could also start from PyramidMesh3D and then create GeometryModel3D and ModelVisual3D
            //var pyramidMeshGeometry3D = new Ab3d.Meshes.PyramidMesh3D(new Point3D(100, 0, 0), new Size3D(80, 50, 80)).Geometry;

            //if (pyramidMeshGeometry3D.Normals.Count == 0)
            //    pyramidMeshGeometry3D.Normals = Ab3d.Utilities.MeshUtils.CalculateNormals(pyramidMeshGeometry3D);

            //var geometryModel3D = new GeometryModel3D(pyramidMeshGeometry3D, diffuseMaterial);
            //var modelVisual3D = new ModelVisual3D()
            //{
            //    Content = geometryModel3D
            //};

            //MainViewport.Children.Add(modelVisual3D);


            
            // DXEngine internally converts WPF objects into SceneNodes.
            // You can get the string that describes the SceneNodes with opening Visual Studio Immediate Window and execting the following:
            // MainDXViewportView.DXScene.DumpSceneNodes();
            //
            // Usually this is the best was to define the 3D scene.
            //
            // But if you have very complex objects with a lot of positions, it might be good to create the SceneNodes manually.
            // This allows faster initialization because WPF 3D objects are not created.
            // Also all the memory used by WPF 3D objects can be freed.
            //
            // Because WPF uses double type for Point3D and Vector3D types instead of float as in DirectX and DXEngine,
            // the memory size required for a 3D objects in WPF is almost twice the size of what is required in DXEngine.
            // 
            // For example if your object has 100.000 positions, the the memory requirements are the following:
            //
            // In WPF:
            // Positions:           100.000 * 3 (x,y,z) * 8 (8 bytes for one double value) = 2.400.000 bytes
            // Normals:             100.000 * 3 (x,y,z) * 8 (8 bytes for one double value) = 2.400.000 bytes
            // Texture coordinates: 100.000 * 2 (u,y) * 8 (8 bytes for one double value)   = 1.600.000 bytes
            // Triangle indices:    100.000 * 4 (4 bytes for one Int32)                    =   400.000 bytes (the actual number of triangle indices may be different - depends on how many positions are shared between triangles)
            // TOTAL:                                                                      = 6.800.000 bytes = 6.7 MB
            // 
            // In DXEngine:
            // Positions:           100.000 * 3 (x,y,z) * 4 (4 bytes for one float value) = 1.200.000 bytes
            // Normals:             100.000 * 3 (x,y,z) * 4 (4 bytes for one float value) = 1.200.000 bytes
            // Texture coordinates: 100.000 * 2 (u,y) * 4 (4 bytes for one float value)   =   800.000 bytes
            // Triangle indices:    100.000 * 4 (4 bytes for one Int32)                   =   400.000 bytes
            // TOTAL:                                                                     = 3.600.000 bytes = 3.5 MB
            //
            // Usually both objects need to be initialized (takes CPU time) and are stored in memory.
            //
            //
            // When the DXEngine's SceneNodes are manually created, the WPF objects can be cleared from memory 
            // or event the SceneNodes can be created without the intermediate WPF objects.
            //
            // One the SceneNode is created it can be added to the scene with using SceneNodeVisual3D.
            // This is a Visual3D and can be added to the Viewport3D.Children collection.
            // The object also provides a way to add Transformation to the SceneNode.
            //
            // A disadvantage of creating SceneNodes is that such objects cannot be shown when WPF 3D rendering is used (for example in case when DXEngine falls back to WPF 3D rendering because of problems with DirectX initialization).
            // Another disadvantage is that it is more complicated to create and modify SceneNodes.
            //
            // Usually, when memory usage is not problematic, it is better to use standard WPF 3D objects.

            // 
            // 2)
            //
            // Create MeshObjectNode from GeometryMesh with providing arrays (IList<T>) for positions, normals, textureCoordinates and triangleIndices:

            Vector3[] positions;
            Vector3[] normals;
            Vector2[] textureCoordinates;
            int[] triangleIndices;

            // Get Pyramid mesh data
            GetObjectDataArrays(out positions, out normals, out textureCoordinates, out triangleIndices);


            // The easiest way to create DXEngine's material is to use Ab3d.DirectX.Materials.WpfMaterial that takes a WPF material and converts it into DXEngine's material
            var diffuseMaterial = new DiffuseMaterial(Brushes.Green);
            var dxMaterial = new Ab3d.DirectX.Materials.WpfMaterial(diffuseMaterial);

            _disposables.Add(dxMaterial);

            // Create SceneNode
            // First create GeometryMesh object from the mesh arrays
            var geometryMesh = new Ab3d.DirectX.GeometryMesh(positions, normals, textureCoordinates, triangleIndices, "PyramidMesh3D");

            _disposables.Add(geometryMesh);

            // NOTE:
            // We could also create GeometryMesh from WPF's MeshGeometry with help from DXMeshGeometry3D:
            //var wpfPyramidMesh = new Meshes.PyramidMesh3D(bottomCenterPosition: new System.Windows.Media.Media3D.Point3D(0, 0, 0),
            //                                              size: new System.Windows.Media.Media3D.Size3D(30, 20, 10));

            //var geometryMesh = new Ab3d.DirectX.Models.DXMeshGeometry3D(wpfPyramidMesh.Geometry, "PyramidMesh");


            // Use GeometryMesh to create MeshObjectNode (SceneNode from GeometryMesh object)
            var meshObjectNode = new Ab3d.DirectX.MeshObjectNode(geometryMesh, dxMaterial);
            meshObjectNode.Name = "MeshObjectNode-from-GeometryMesh";

            _disposables.Add(meshObjectNode);

            // Use SceneNodeVisual3D to show SceneNode in DXViewportView
            var sceneNodeVisual3D = new SceneNodeVisual3D(meshObjectNode);
            //sceneNodeVisual3D.Transform = new TranslateTransform3D(0, 0, 0);

            MainViewport.Children.Add(sceneNodeVisual3D);


            //
            // 3)
            //
            // Create MeshObjectNode from SimpleMesh<T> with providing VertexBufferArray and IndexBufferArray:
            // This option provides faster initialization, because the VertexBufferArray is already generated and it can be directly used to create DirectX vertex buffer.
            // In the previous sample the VertexBufferArray was generated in the GeometryMesh from positions, normals, textureCoordinates arrays.
            //
            // If you can store your 3D models in disk (or some other location) in a form of VertexBuffer and IndexBuffer,
            // then this is the fastes way to initialize 3D objects.

            //
            // 3a)
            //
            // The standard way to create a SimpleMesh is to use the PositionNormalTexture or some other struct that defines the data for one array:

            PositionNormalTexture[] vertexBuffer;
            int[] indexBuffer;
            GetVertexAndIndexBuffer(out vertexBuffer, out indexBuffer);

            var simpleMesh = new SimpleMesh<PositionNormalTexture>(vertexBuffer, 
                                                                   indexBuffer, 
                                                                   inputLayoutType: InputLayoutType.Position | InputLayoutType.Normal | InputLayoutType.TextureCoordinate,
                                                                   name: "SimpleMesh-from-PositionNormalTexture-array");

            _disposables.Add(simpleMesh);

            diffuseMaterial = new DiffuseMaterial(Brushes.Red);
            dxMaterial = new Ab3d.DirectX.Materials.WpfMaterial(diffuseMaterial);

            _disposables.Add(dxMaterial);

            meshObjectNode = new Ab3d.DirectX.MeshObjectNode(simpleMesh, dxMaterial);
            meshObjectNode.Name = "MeshObjectNode-from-SimpleMesh";

            _disposables.Add(meshObjectNode);

            sceneNodeVisual3D = new SceneNodeVisual3D(meshObjectNode);
            sceneNodeVisual3D.Transform = new TranslateTransform3D(100, 0, 0);

            MainViewport.Children.Add(sceneNodeVisual3D);



            //
            // 3b)
            //
            // It is also possible to create SimpleMesh with a base type - for example float (for example if we read data from file).
            // In this case we need to set the ArrayStride property.
            //
            // A drawback of using a non-standard vertex buffer (Vector3, PositionNormalTexture, PositionNormal or PositionTexture)
            // is that such mesh does not support hit testing. 
            // In this sample this is demonstrated with camera rotation around mouse hit object - it is not possible to rotate around SimpleMesh<float>.

            float[] floatVertexBuffer;
            GetFloatVertexAndIndexBuffer(out floatVertexBuffer, out indexBuffer);

            var floatSimpleMesh = new SimpleMesh<float>(floatVertexBuffer, 
                                                        indexBuffer, 
                                                        inputLayoutType: InputLayoutType.Position | InputLayoutType.Normal | InputLayoutType.TextureCoordinate,
                                                        name: "SimpleMesh-from-float-array");

            _disposables.Add(floatSimpleMesh);

            // IMPORTANT:
            // When we do not use PositionNormalTexture or PositionNormal, the DXEngine cannot calculate Bounds of the SimpleMesh for us.
            // In this case we need to calculate and specify Bounds manually:
            // Defined bounds for the following mesh: new Ab3d.Meshes.PyramidMesh3D(new Point3D(0, 0, 0), new Size3D(80, 50, 80))
            floatSimpleMesh.Bounds = new Bounds(new BoundingBox(minimum: new Vector3(-40, -25, -40), maximum: new Vector3(40, 25, 40)));

            // Because we created SimpleMesh with a base type (float), 
            // we need to specify how many array elements define one Vertex.
            // This is 8 in our case: 3 (position x,y,z) + 3 (normal x,y,z) + 2 (texture coordinate u,v) = 8
            floatSimpleMesh.ArrayStride = 8;


            diffuseMaterial = new DiffuseMaterial(Brushes.Orange);
            dxMaterial = new Ab3d.DirectX.Materials.WpfMaterial(diffuseMaterial);

            _disposables.Add(dxMaterial);

            meshObjectNode = new Ab3d.DirectX.MeshObjectNode(floatSimpleMesh, dxMaterial);
            meshObjectNode.Name = "MeshObjectNode-from-FloatSimpleMesh";

            _disposables.Add(meshObjectNode);

            sceneNodeVisual3D = new SceneNodeVisual3D(meshObjectNode);
            sceneNodeVisual3D.Transform = new TranslateTransform3D(200, 0, 0);

            MainViewport.Children.Add(sceneNodeVisual3D);



            //
            // 3c)
            //
            // Instead of float array elements, it is also possible to use byte array to create SimpleMesh.
            //
            // As before, a drawback of using a non-standard vertex buffer (Vector3, PositionNormalTexture, PositionNormal or PositionTexture)
            // is that such mesh does not support hit testing. 
            // In this sample this is demonstrated with camera rotation around mouse hit object - it is not possible to rotate around SimpleMesh<float>.

            byte[] byteVertexBuffer;
            GetByteVertexAndIndexBuffer(out byteVertexBuffer, out indexBuffer);

            var byteSimpleMesh = new SimpleMesh<byte>(byteVertexBuffer,
                                                      indexBuffer, 
                                                      inputLayoutType: InputLayoutType.Position | InputLayoutType.Normal | InputLayoutType.TextureCoordinate,
                                                      name: "SimpleMesh-from-byte-array");

            _disposables.Add(byteSimpleMesh);

            // IMPORTANT:
            // When we do not use PositionNormalTexture or PositionNormal, the DXEngine cannot calculate Bounds of the SimpleMesh for us.
            // In this case we need to calculate and specify Bounds manually:
            // Defined bounds for the following mesh: new Ab3d.Meshes.PyramidMesh3D(new Point3D(0, 0, 0), new Size3D(80, 50, 80))
            byteSimpleMesh.Bounds = new Bounds(new BoundingBox(minimum: new Vector3(-40, -25, -40), maximum: new Vector3(40, 25, 40)));

            // Because we created SimpleMesh with a base type (byte), 
            // we need to specify how many array elements define one Vertex.
            // This is 32 in our case: 8 (8x float value) * 4 (4 bytes for one float) = 32
            byteSimpleMesh.ArrayStride = 32;


            diffuseMaterial = new DiffuseMaterial(Brushes.Yellow);
            dxMaterial = new Ab3d.DirectX.Materials.WpfMaterial(diffuseMaterial);

            _disposables.Add(dxMaterial);

            meshObjectNode = new Ab3d.DirectX.MeshObjectNode(byteSimpleMesh, dxMaterial);
            meshObjectNode.Name = "MeshObjectNode-from-ByteSimpleMesh";

            _disposables.Add(meshObjectNode);

            sceneNodeVisual3D = new SceneNodeVisual3D(meshObjectNode);
            sceneNodeVisual3D.Transform = new TranslateTransform3D(300, 0, 0);

            MainViewport.Children.Add(sceneNodeVisual3D);


            //
            // 4)
            // 
            // When a frozen Model3DGroup is added to the DXViewportView, it is converted into the WpfOptimizedModel3DGroupNode (derived from SceneNode).
            // In this case both WPF and DXEngine's 3D objects data are stored in memory.
            //
            // To release the WPF 3D objects data, it is possible to create the WpfOptimizedModel3DGroupNode manually and
            // then clear the used WPF 3D objects.
            // This can be done with setting the AutomaticallyClearWpfObjectsAfterInitialization property on WpfOptimizedModel3DGroupNode to true,
            // or by calling the ClearWpfObjects method on WpfOptimizedModel3DGroupNode.

            string dragonModelFileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources\\Models\\dragon_vrip_res3.obj");

            var readerObj = new Ab3d.ReaderObj();
            Model3D readModel3D = readerObj.ReadModel3D(dragonModelFileName);

            double scale = 100 / readModel3D.Bounds.SizeX; // Scale the model to 100 SizeX
            readModel3D.Transform = new ScaleTransform3D(scale, scale, scale);

            var model3DGroup = readModel3D as Model3DGroup;
            if (model3DGroup == null)
            {
                model3DGroup = new Model3DGroup();
                model3DGroup.Children.Add(readModel3D);
            }

            model3DGroup.Freeze();


            var wpfOptimizedModel3DGroupNode = new Ab3d.DirectX.Models.WpfOptimizedModel3DGroupNode(model3DGroup, name: "Frozen Model3DGroup");
            wpfOptimizedModel3DGroupNode.AutomaticallyClearWpfObjectsAfterInitialization = true; // This will clear the WPF 3D models that are referenced by WpfOptimizedModel3DGroupNode when the DirectX objects are created

            _disposables.Add(wpfOptimizedModel3DGroupNode);

            sceneNodeVisual3D = new SceneNodeVisual3D(wpfOptimizedModel3DGroupNode);
            sceneNodeVisual3D.Transform = new TranslateTransform3D(-100, -20, -100);

            MainViewport.Children.Add(sceneNodeVisual3D);


            //
            // 5) 
            //
            // The following code shows how to load texture with using TextureLoader

            if (MainDXViewportView.DXScene != null)
            {
                var planeGeometry3D = new Ab3d.Meshes.PlaneMesh3D(new Point3D(0, 0, 0), new Vector3D(0, 1, 0), new Vector3D(1, 0, 0), new Size(80, 80), 1, 1).Geometry;
                var dxMeshGeometry3D = new DXMeshGeometry3D(planeGeometry3D);
                _disposables.Add(dxMeshGeometry3D);

                // Load texture file into ShaderResourceView (in our case we load dds file; but we could also load png file)
                string textureFileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources/ab4d-logo-220x220.dds");


                // The easiest way to load image file and in the same time create a material with the loaded texture is to use the CreateStandardTextureMaterial method.
                var standardMaterial = Ab3d.DirectX.TextureLoader.CreateStandardTextureMaterial(MainDXViewportView.DXScene.DXDevice, textureFileName);

                // We need to manually dispose the created StandardMaterial and ShaderResourceView
                _disposables.Add(standardMaterial);
                _disposables.Add(standardMaterial.DiffuseTextures[0]);


                // If we want more control over the material creation process, we can use the following code:

                //// To load a texture from file, you can use the TextureLoader.LoadShaderResourceView (this supports loading standard image files and also loading dds files).
                //// This method returns a ShaderResourceView and it can also set a textureInfo parameter that defines some of the properties of the loaded texture (bitmap size, dpi, format, hasTransparency).
                //TextureInfo textureInfo;
                //var loadedShaderResourceView = Ab3d.DirectX.TextureLoader.LoadShaderResourceView(MainDXViewportView.DXScene.Device,
                //                                                                                 textureFileName,
                //                                                                                 out textureInfo);
                //_disposables.Add(loadedShaderResourceView);

                //// Get recommended BlendState based on HasTransparency and HasPreMultipliedAlpha values.
                //// Possible values are: CommonStates.Opaque, CommonStates.PremultipliedAlphaBlend or CommonStates.NonPremultipliedAlphaBlend.
                //var recommendedBlendState = MainDXViewportView.DXScene.DXDevice.CommonStates.GetRecommendedBlendState(textureInfo.HasTransparency, textureInfo.HasPremultipliedAlpha);

                //// Now we can create a DXEngine's StandardMaterial
                //var standardMaterial = new StandardMaterial()
                //{
                //    // Set ShaderResourceView into array of diffuse textures
                //    DiffuseTextures = new ShaderResourceView[] {loadedShaderResourceView},
                //    TextureBlendState = recommendedBlendState,

                //    HasTransparency = textureInfo.HasTransparency,

                //    // When showing texture, the DiffuseColor represents a color mask - each color from texture is multiplied with DiffuseColor (White preserves the original color)
                //    DiffuseColor = Colors.White.ToColor3()
                //};

                //_disposables.Add(standardMaterial);


                meshObjectNode = new Ab3d.DirectX.MeshObjectNode(dxMeshGeometry3D, standardMaterial);
                meshObjectNode.Name = "MeshObjectNode-from-PlaneMesh3D";

                _disposables.Add(meshObjectNode);

                sceneNodeVisual3D = new SceneNodeVisual3D(meshObjectNode);
                sceneNodeVisual3D.Transform = new TranslateTransform3D(0, 0, 100);

                MainViewport.Children.Add(sceneNodeVisual3D);
            }



            // Add PointLight
            var pointLight = new PointLight(Colors.White, new Point3D(100, 500, 0));
            MainViewport.Children.Add(pointLight.CreateModelVisual3D());

            Camera1.ShowCameraLight = ShowCameraLightType.Never;
        }


        private void MainDXViewportViewOnSceneRendered(object sender, EventArgs eventArgs)
        {
            // Call this method only after the first frame is rendered
            MainDXViewportView.SceneRendered -= MainDXViewportViewOnSceneRendered;

            if (MainDXViewportView.DXScene == null) // In WPF 3D rendering
                return;

            // Show generated SceneNodes
            SceneNodesTextBox.Text = "DXEngine's SceneNodes:\r\n\r\n" + MainDXViewportView.DXScene.GetSceneNodesDumpString(showBounds: true, showTransform: true, showDirtyFlags: false); // all the parameters are optional

            // You could also set breakpoint here and then call the following in the Visual Studio Immediate Window:
            MainDXViewportView.DXScene.DumpSceneNodes(showBounds: true, showTransform: true, showDirtyFlags: false);
        }


        private static void GetObjectDataArrays(out Vector3[] positions, out Vector3[] normals, out Vector2[] textureCoordinates, out int[] triangleIndices)
        {
            //// The following commented code is used to generate the arrays definition below:
            //var pyramidMeshGeometry3D = new Ab3d.Meshes.PyramidMesh3D(new Point3D(0, 0, 0), new Size3D(80, 50, 80)).Geometry;

            //if (pyramidMeshGeometry3D.Normals.Count == 0)
            //    pyramidMeshGeometry3D.Normals = Ab3d.Utilities.MeshUtils.CalculateNormals(pyramidMeshGeometry3D);

            //string meshArraysTest = GetMeshArraysText(pyramidMeshGeometry3D);


            // Pyramid mesh data (prepared with the commented code above):
            positions = new Vector3[]
            {
                new Vector3(0f, 50f, 0f),
                new Vector3(0f, 50f, 0f),
                new Vector3(0f, 50f, 0f),
                new Vector3(0f, 50f, 0f),
                new Vector3(-40f, 0f, -40f),
                new Vector3(-40f, 0f, -40f),
                new Vector3(-40f, 0f, -40f),
                new Vector3(40f, 0f, -40f),
                new Vector3(40f, 0f, -40f),
                new Vector3(40f, 0f, -40f),
                new Vector3(40f, 0f, 40f),
                new Vector3(40f, 0f, 40f),
                new Vector3(40f, 0f, 40f),
                new Vector3(-40f, 0f, 40f),
                new Vector3(-40f, 0f, 40f),
                new Vector3(-40f, 0f, 40f),
            };

            normals = new Vector3[]
            {
                new Vector3(0f, 0.624695047554424f, -0.78086880944303f),
                new Vector3(0.78086880944303f, 0.624695047554424f, 0f),
                new Vector3(0f, 0.624695047554424f, 0.78086880944303f),
                new Vector3(-0.78086880944303f, 0.624695047554424f, 0f),
                new Vector3(0f, 0.624695047554424f, -0.78086880944303f),
                new Vector3(-0.78086880944303f, 0.624695047554424f, 0f),
                new Vector3(0f, -1f, 0f),
                new Vector3(0f, 0.624695047554424f, -0.78086880944303f),
                new Vector3(0.78086880944303f, 0.624695047554424f, 0f),
                new Vector3(0f, -1f, 0f),
                new Vector3(0.78086880944303f, 0.624695047554424f, 0f),
                new Vector3(0f, 0.624695047554424f, 0.78086880944303f),
                new Vector3(0f, -1f, 0f),
                new Vector3(0f, 0.624695047554424f, 0.78086880944303f),
                new Vector3(-0.78086880944303f, 0.624695047554424f, 0f),
                new Vector3(0f, -1f, 0f),
            };

            textureCoordinates = new Vector2[]
            {
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
            };

            triangleIndices = new int[]
            {
                0, 7, 4,
                1, 10, 8,
                2, 13, 11,
                3, 5, 14,
                6, 9, 15,
                9, 12, 15,
            };
        }


        private static void GetVertexAndIndexBuffer(out PositionNormalTexture[] vertexBuffer, out int[] indexBuffer)
        {
            vertexBuffer = new PositionNormalTexture[] {
                /*                        Position                     Normal                                   TextureCoordinate */
                new PositionNormalTexture(new Vector3(0f, 50f, 0f),    new Vector3(0.0000f, 0.6247f, -0.7809f), new Vector2(0.5f, 0.5f)),
                new PositionNormalTexture(new Vector3(0f, 50f, 0f),    new Vector3(0.7809f, 0.6247f, 0.0000f),  new Vector2(0.5f, 0.5f)),
                new PositionNormalTexture(new Vector3(0f, 50f, 0f),    new Vector3(0.0000f, 0.6247f, 0.7809f),  new Vector2(0.5f, 0.5f)),
                new PositionNormalTexture(new Vector3(0f, 50f, 0f),    new Vector3(-0.7809f, 0.6247f, 0.0000f), new Vector2(0.5f, 0.5f)),
                new PositionNormalTexture(new Vector3(-40f, 0f, -40f), new Vector3(0.0000f, 0.6247f, -0.7809f), new Vector2(0f, 0f)),
                new PositionNormalTexture(new Vector3(-40f, 0f, -40f), new Vector3(-0.7809f, 0.6247f, 0.0000f), new Vector2(0f, 0f)),
                new PositionNormalTexture(new Vector3(-40f, 0f, -40f), new Vector3(0.0000f, -1.0000f, 0.0000f), new Vector2(0f, 0f)),
                new PositionNormalTexture(new Vector3(40f, 0f, -40f),  new Vector3(0.0000f, 0.6247f, -0.7809f), new Vector2(1f, 0f)),
                new PositionNormalTexture(new Vector3(40f, 0f, -40f),  new Vector3(0.7809f, 0.6247f, 0.0000f),  new Vector2(1f, 0f)),
                new PositionNormalTexture(new Vector3(40f, 0f, -40f),  new Vector3(0.0000f, -1.0000f, 0.0000f), new Vector2(1f, 0f)),
                new PositionNormalTexture(new Vector3(40f, 0f, 40f),   new Vector3(0.7809f, 0.6247f, 0.0000f),  new Vector2(1f, 1f)),
                new PositionNormalTexture(new Vector3(40f, 0f, 40f),   new Vector3(0.0000f, 0.6247f, 0.7809f),  new Vector2(1f, 1f)),
                new PositionNormalTexture(new Vector3(40f, 0f, 40f),   new Vector3(0.0000f, -1.0000f, 0.0000f), new Vector2(1f, 1f)),
                new PositionNormalTexture(new Vector3(-40f, 0f, 40f),  new Vector3(0.0000f, 0.6247f, 0.7809f),  new Vector2(0f, 1f)),
                new PositionNormalTexture(new Vector3(-40f, 0f, 40f),  new Vector3(-0.7809f, 0.6247f, 0.0000f), new Vector2(0f, 1f)),
                new PositionNormalTexture(new Vector3(-40f, 0f, 40f),  new Vector3(0.0000f, -1.0000f, 0.0000f), new Vector2(0f, 1f)),
            };

            indexBuffer = new int[]
            {
                0, 7, 4,
                1, 10, 8,
                2, 13, 11,
                3, 5, 14,
                6, 9, 15,
                9, 12, 15,
            };
        }

        private static void GetFloatVertexAndIndexBuffer(out float[] floatVertexBuffer, out int[] indexBuffer)
        {
            floatVertexBuffer = new float[] {
            /*   Position            Normal                         TextureCoordinate */
                 0f, 50f, 0f,        0.0000f, 0.6247f, -0.7809f,    0.5f, 0.5f,
                 0f, 50f, 0f,        0.7809f, 0.6247f, 0.0000f,     0.5f, 0.5f,
                 0f, 50f, 0f,        0.0000f, 0.6247f, 0.7809f,     0.5f, 0.5f,
                 0f, 50f, 0f,       -0.7809f, 0.6247f, 0.0000f,     0.5f, 0.5f,
                -40f, 0f, -40f,      0.0000f, 0.6247f, -0.7809f,    0f, 0f,
                -40f, 0f, -40f,     -0.7809f, 0.6247f, 0.0000f,     0f, 0f,
                -40f, 0f, -40f,      0.0000f, -1.0000f, 0.0000f,    0f, 0f,
                 40f, 0f, -40f,      0.0000f, 0.6247f, -0.7809f,    1f, 0f,
                 40f, 0f, -40f,      0.7809f, 0.6247f, 0.0000f,     1f, 0f,
                 40f, 0f, -40f,      0.0000f, -1.0000f, 0.0000f,    1f, 0f,
                 40f, 0f, 40f,       0.7809f, 0.6247f, 0.0000f,     1f, 1f,
                 40f, 0f, 40f,       0.0000f, 0.6247f, 0.7809f,     1f, 1f,
                 40f, 0f, 40f,       0.0000f, -1.0000f, 0.0000f,    1f, 1f,
                -40f, 0f, 40f,       0.0000f, 0.6247f, 0.7809f,     0f, 1f,
                -40f, 0f, 40f,      -0.7809f, 0.6247f, 0.0000f,     0f, 1f,
                -40f, 0f, 40f,       0.0000f, -1.0000f, 0.0000f,    0f, 1f,
            };

            indexBuffer = new int[]
            {
                0, 7, 4,
                1, 10, 8,
                2, 13, 11,
                3, 5, 14,
                6, 9, 15,
                9, 12, 15,
            };
        }

        private static void GetByteVertexAndIndexBuffer(out byte[] byteVertexBuffer, out int[] indexBuffer)
        {
            float[] floatVertexBuffer;
            GetFloatVertexAndIndexBuffer(out floatVertexBuffer, out indexBuffer);

            // Copy float array into byte array
            byteVertexBuffer = new byte[floatVertexBuffer.Length * 4];
            Buffer.BlockCopy(floatVertexBuffer, 0, byteVertexBuffer, 0, byteVertexBuffer.Length);
        }

        // This methods generates the arrays text
        private static string GetMeshArraysText(MeshGeometry3D mesh)
        {
            var sb = new StringBuilder();

            sb.AppendLine("var positions = new Vector3[] {");

            foreach (var meshPosition in mesh.Positions)
                sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "    new Vector3({0}f, {1}f, {2}f),\r\n", meshPosition.X, meshPosition.Y, meshPosition.Z);

            sb.AppendLine("};");
            sb.AppendLine();


            if (mesh.Normals != null && mesh.Normals.Count > 0)
            {
                sb.AppendLine("var normals = new Vector3[] {");

                foreach (var normal in mesh.Normals)
                    sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "    new Vector3({0}f, {1}f, {2}f),\r\n", normal.X, normal.Y, normal.Z);

                sb.AppendLine("};");
            }
            else
            {
                sb.AppendLine("var normals = new List<Vector3>();"); // Just create an empty normals list
            }

            sb.AppendLine();


            if (mesh.TextureCoordinates != null && mesh.TextureCoordinates.Count > 0)
            {
                sb.AppendLine("var textureCoordinates = new Vector2[] {");

                foreach (var textureCoordinate in mesh.TextureCoordinates)
                    sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "    new Vector2({0}f, {1}f),\r\n", textureCoordinate.X, textureCoordinate.Y);

                sb.AppendLine("};");
            }
            else
            {
                sb.AppendLine("var textureCoordinates = new List<Vector2>();"); // Just create an empty normals list
            }

            sb.AppendLine();


            if (mesh.TriangleIndices != null && mesh.TriangleIndices.Count > 0)
            {
                sb.AppendLine("var triangleIndices = new int[] {");

                for (int i = 0; i < mesh.TriangleIndices.Count; i+=3)
                    sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "    {0}, {1}, {2},\r\n", mesh.TriangleIndices[i], mesh.TriangleIndices[i + 1], mesh.TriangleIndices[i + 2]);

                sb.AppendLine("};");
            }
            else
            {
                sb.AppendLine("var triangleIndices = new List<Int32>();"); // Just create an empty normals list
            }


            // Create VertexBufferArray with PositionNormalTexture elements:

            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("var vertexBufferArray = new PositionNormalTexture[] {");
            // NOTE:
            // This code requires that Positions, Normals and TextureCoordinates collections have the same number of elements
            int positonsCount = mesh.Positions.Count;
            for (int i = 0; i < positonsCount; i++)
            {
                sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                                "    new PositionNormalTexture(new Vector3({0}f, {1}f, {2}f), new Vector3({3:0.0000}f, {4:0.0000}f, {5:0.0000}f), new Vector2({6}f, {7}f)),\r\n",
                                mesh.Positions[i].X, mesh.Positions[i].Y, mesh.Positions[i].Z,
                                mesh.Normals[i].X, mesh.Normals[i].Y, mesh.Normals[i].Z,
                                mesh.TextureCoordinates[i].X, mesh.TextureCoordinates[i].Y);
            }

            sb.AppendLine("};");
            
            
            // Create VertexBufferArray with float elements:

            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("var vertexBufferFloatArray = new float[] {");
            // NOTE:
            // This code requires that Positions, Normals and TextureCoordinates collections have the same number of elements
            for (int i = 0; i < positonsCount; i++)
            {
                sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                                "    {0}f, {1}f, {2}f,     {3:0.0000}f, {4:0.0000}f, {5:0.0000}f,     {6}f, {7}f,\r\n",
                                mesh.Positions[i].X, mesh.Positions[i].Y, mesh.Positions[i].Z,
                                mesh.Normals[i].X, mesh.Normals[i].Y, mesh.Normals[i].Z,
                                mesh.TextureCoordinates[i].X, mesh.TextureCoordinates[i].Y);
            }

            sb.AppendLine("};");


            return sb.ToString();
        }

        private void FitIntoViewButton_OnClick(object sender, RoutedEventArgs e)
        {
            Camera1.FitIntoView();
        }

        private void ResetCameraButton_OnClick(object sender, RoutedEventArgs e)
        {
            ResetCamera();
        }

        private void ResetCamera()
        {
            Camera1.TargetPosition = new Point3D(0, 20, 0);
            Camera1.RotationCenterPosition = null;

            Camera1.Heading = 50;
            Camera1.Attitude = -20;
            Camera1.Distance = 600;
        }
    }
}
