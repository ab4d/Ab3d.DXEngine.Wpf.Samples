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
using Ab3d.DirectX;
using Ab3d.Visuals;
using SharpDX;
using RenderingEventArgs = System.Windows.Media.RenderingEventArgs;
using System.Diagnostics;
using System.Runtime.InteropServices;


// This sample shows how to optimize updating MeshGeometry3D data.
// The sample creates two mesh objects and then morphs between the two objects.
//
// This can be easily done with changing the Positions and Normal collections of the MeshGeometry3D object.
// NOTE: 
// Before doing any bigger changes to MeshGeometry3D data, you need to disconnect the collections from MeshGeometry3D.
// For example for Positions this is done with saving Positions into local variable,
// then setting meshGeometry3D.Positions to null,
// then changing the positions in the local variable
// and finally connecting the changed positions back to the MeshGeometry3D.
// Otherwise notifications events in MeshGeometry3D will be fired and this will slow down the update process.
//
// But if updating data still creates performance problems for the application, 
// then it is possible to optimize this with using low level DXEngine objects.
//
// This sample creates a SimpleMesh<PositionNormalTexture> object from MeshGeometry3D.
// The SimpleMesh<PositionNormalTexture> is created with dynamic vertex buffer (DirectX optimization for constantly changing buffer).
// On each change the sample code changes the data in the vertex buffer array (instead of in MeshGeometry3D object).
// After the changes are done the RecreateMesh method is called to update the DirectX vertex buffer.
//
// The main benefit of this approach is that you do not need to change slow WPF Positions and Normals collection
// and that DXEngine do not need to copy those data into its own vertex buffer array.
// In case of using SimpleMesh<PositionNormalTexture> we can update the Positions and Normals directly in the vertex buffer array.
//
// This sample also can create a dynamic vertex buffer.
// When using a dynamic vertex buffer, the existing vertex buffer is reused when updating the data;
// otherwise the existing vertex buffer is disposed and a new vertex buffer is created - this can lead to GPU memory fragmentation.
// The dynamic vertex buffer is created with setting the createDynamicVertexBuffer argument to true when creating the SimpleMesh object.
//
// When using standard WPF objects, the code can automatically start using dynamic vertex buffer when the Positions in MeshGeometry3D are
// changed very often.
// You can also manually force creation of dynamic vertex buffer with setting the CreateDynamicVertexBuffer to true on a GeometryModel3D:
// geometryModel3D.SetDXAttribute(DXAttributeType.CreateDynamicVertexBuffer, true);
// You can also prevent automatically using dynamic vertex buffer with setting CreateDynamicVertexBuffer attribute to false.
//
//
// To even further optimize the code, it is possible to use unsafe code to change position and normal data.
// This can bring a big 50% performance gains.
// See code comments in the AnimateSimpleMesh method for more info.
//
//
// NOTE:
// Before using this low level approach please benchmark your application and check
// if updating buffer is really causing the performance problems.
// If not, then it is advised to use simpler approach with simply changing the data in MeshGeometry3D.

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineAdvanced
{
    /// <summary>
    /// Interaction logic for OptimizedMeshMorphing.xaml
    /// </summary>
    public partial class OptimizedMeshMorphing : Page
    {
        private DisposeList _disposables;

        private Stopwatch _stopwatch;
        private List<double> _meshMorphTimes;
        private List<double> _meshUpdateTimes;
        private List<double> _dxEngineUpdateTimes;

        private MeshGeometry3D _meshGeometry3D;
        private Rect3D _originalMeshGeometryBounds;

        private PositionNormalTexture[] _vertexBufferArray;
        private SimpleMesh<PositionNormalTexture> _simpleMesh;
        private MeshObjectNode _meshObjectNode;
        private float _originalMeshSizeY;

        private DateTime _animationStartTime;
        private TimeSpan _lastRenderingTime;
        private int _lastElapsedSecond;

        private PositionNormal[] _originalMesh;
        private PositionNormal[] _morphedMesh;

        private BoundingBox _originalBoundingBox;
        private BoundingBox _morphedBoundingBox;

        public OptimizedMeshMorphing()
        {
            InitializeComponent();

            _meshMorphTimes = new List<double>();
            _meshUpdateTimes = new List<double>();
            _dxEngineUpdateTimes = new List<double>();

            DXDiagnostics.IsCollectingStatistics = true;

            CreateScene();
            CreateMorphedMesh();

            MainDXViewportView.SceneRendered += delegate
            {
                if (_dxEngineUpdateTimes == null)
                    _dxEngineUpdateTimes = new List<double>();

                if (MainDXViewportView.DXScene != null && MainDXViewportView.DXScene.Statistics != null)
                    _dxEngineUpdateTimes.Add(MainDXViewportView.DXScene.Statistics.UpdateTimeMs);
            };

            CompositionTarget.Rendering += CompositionTargetOnRendering;

            this.Unloaded += delegate(object sender, RoutedEventArgs e)
            {
                CompositionTarget.Rendering -= CompositionTargetOnRendering;

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


            var readerObj = new Ab3d.ReaderObj();
            var readModel3D = readerObj.ReadModel3D(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\Models\dragon_vrip_res3.obj")) as GeometryModel3D;

            if (readModel3D == null)
                return;

            _meshGeometry3D = (MeshGeometry3D)readModel3D.Geometry;

            _originalMeshGeometryBounds = _meshGeometry3D.Bounds;

            // We need to make sure that we have normals defined
            if (_meshGeometry3D.Normals == null || _meshGeometry3D.Normals.Count == 0)
                _meshGeometry3D.Normals = Ab3d.Utilities.MeshUtils.CalculateNormals(_meshGeometry3D);


            if (UseSimpleMeshRadioButton.IsChecked ?? false)
                AddSimpleMesh();
            else
                AddMeshGeometry3D();
        }

        private void AddSimpleMesh()
        {
            // To show _meshGeometry3D with using low level DXEngine object we will do the following:
            // 1) Create a array of PositionNormalTexture data - this will represent a managed vertex buffer array.
            // 2) Create a SimpleMesh<PositionNormalTexture> object that will create an unmanaged vertex buffer from managed vertex buffer.
            // 3) Create a MeshObjectNode (derived from SceneNode) from the SimpleMesh.
            // 4) Create a SceneNodeVisual3D that will allow us to add the MeshObjectNode to the Viewport3D children.


            // 1) Create a array of PositionNormalTexture data - this will represent a managed vertex buffer array.

            int positionsCount = _meshGeometry3D.Positions.Count;

            _vertexBufferArray = new PositionNormalTexture[positionsCount];
            FillVertexBuffer(_vertexBufferArray, _meshGeometry3D.Positions, _meshGeometry3D.Normals, _meshGeometry3D.TextureCoordinates);

            var indexBuffer = new int[_meshGeometry3D.TriangleIndices.Count];
            _meshGeometry3D.TriangleIndices.CopyTo(indexBuffer, 0);


            // 2) Create a SimpleMesh<PositionNormalTexture> object that will create an unmanaged vertex buffer from managed vertex buffer.

            bool createDynamicVertexBuffer = UseDynamicBufferCheckBox.IsChecked ?? false;

            _simpleMesh = new SimpleMesh<PositionNormalTexture>(_vertexBufferArray, 
                                                                indexBuffer, 
                                                                inputLayoutType: InputLayoutType.Position | InputLayoutType.Normal | InputLayoutType.TextureCoordinate,
                                                                name: "SimpleMesh-from-PositionNormalTexture-array",
                                                                createDynamicVertexBuffer: createDynamicVertexBuffer);

            // We can also manually specify the bounds of the mesh
            // If this is not done, then the SimpleMesh will go through all positions and calculate that.
            // But because the bounds are already calculated by MeshGeometry3D, we can just use that value (we only need to convert that to DXEngine's bounds).
            _simpleMesh.Bounds = _meshGeometry3D.Bounds.ToDXEngineBounds();

            _originalMeshSizeY = _simpleMesh.Bounds.BoundingBox.Maximum.Y - _simpleMesh.Bounds.BoundingBox.Minimum.Y;

            _disposables.Add(_simpleMesh);


            var diffuseMaterial = new DiffuseMaterial(Brushes.Silver);
            var dxMaterial = new Ab3d.DirectX.Materials.WpfMaterial(diffuseMaterial);

            _disposables.Add(dxMaterial);


            // 3) Create a MeshObjectNode (derived from SceneNode) from the SimpleMesh.

            _meshObjectNode = new Ab3d.DirectX.MeshObjectNode(_simpleMesh, dxMaterial);
            _meshObjectNode.Name = "MeshObjectNode-from-SimpleMesh";

            _disposables.Add(_meshObjectNode);


            // 4) Create a SceneNodeVisual3D that will allow us to add the MeshObjectNode to the Viewport3D children.

            var sceneNodeVisual3D = new SceneNodeVisual3D(_meshObjectNode);
            

            // Scale and translate the sceneNodeVisual3D and than add it to the scene
            AddVisual3D(sceneNodeVisual3D);
        }

        private void AddMeshGeometry3D()
        {
            // It is much easier to create standard WPF objects to show the _meshGeometry3D
            var silverMaterial = new DiffuseMaterial(Brushes.Silver);

            var geometryModel3D = new GeometryModel3D(_meshGeometry3D, silverMaterial);

            var modelVisual3D = new ModelVisual3D()
            {
                Content = geometryModel3D
            };

            // Scale and translate the modelVisual3D and than add it to the scene
            AddVisual3D(modelVisual3D);
        }

        private void AddVisual3D(Visual3D visual3D)
        {
            // Center the model and scale it to 100
            var meshBounds = _originalMeshGeometryBounds;
            var translate = new TranslateTransform3D(-(meshBounds.X + (meshBounds.SizeX * 0.5)),
                                                     -(meshBounds.Y + (meshBounds.SizeY * 0.5)),
                                                     -(meshBounds.Z + (meshBounds.SizeZ * 0.5)));

            double targetScale = 100;
            double scaleFactor = targetScale / Math.Max(Math.Max(meshBounds.SizeX, meshBounds.SizeY), meshBounds.SizeZ);

            var scale = new ScaleTransform3D(scaleFactor, scaleFactor, scaleFactor);

            var transform3DGroup = new Transform3DGroup();
            transform3DGroup.Children.Add(translate);
            transform3DGroup.Children.Add(scale);

            visual3D.Transform = transform3DGroup;

            MainViewport.Children.Add(visual3D);
        }


        private void CreateMorphedMesh()
        {
            if (_simpleMesh == null)
                return;


            var meshPositions = _meshGeometry3D.Positions;
            var meshNormals = _meshGeometry3D.Normals;

            var positionsCount = meshPositions.Count;

            // First store original positions into _originalMesh
            // This array will provide much faster access to morphed positions then if we would access the data from MeshGeometry3D object.
            var originalMesh = new PositionNormal[positionsCount];

            for (int i = 0; i < positionsCount; i++)
            {
                originalMesh[i].Position = meshPositions[i].ToVector3();
                originalMesh[i].Normal   = meshNormals[i].ToVector3();
            }

            _originalMesh = originalMesh;


            // Now create a copy of the original MeshGeometry3D (this is needed because after adjusting positions we also need to re-calculate normals)
            var morphedMeshGeometry3D = new MeshGeometry3D();

            // Copy positions from original MeshGeometry3D - we also adjust the y position of all positions that are above the center of the mesh

            double yOffset = _originalMeshSizeY;
            double yMiddle = _meshGeometry3D.Bounds.Y + _originalMeshSizeY / 2.0f; // get mesh center y value

            morphedMeshGeometry3D.Positions = null; // Disconnect positions before changing (to prevent change notifications from slowing things down)
            for (int i = 0; i < positionsCount; i++)
            {
                var onePosition = meshPositions[i];

                if (onePosition.Y > yMiddle)
                    onePosition.Y += yOffset;

                meshPositions[i] = onePosition;
            }

            morphedMeshGeometry3D.Positions = meshPositions;

            // Because we have changed the positions, this also changed the normal vectors.
            // We need to calculate normals again
            meshNormals = Ab3d.Utilities.MeshUtils.CalculateNormals(morphedMeshGeometry3D);
            morphedMeshGeometry3D.Normals = meshNormals;


            // After we have a morphed MeshGeometry, we can prepared an optimized list of positions and normals.
            _morphedMesh = new PositionNormal[positionsCount];

            for (int i = 0; i < positionsCount; i++)
                _morphedMesh[i] = new PositionNormal(meshPositions[i].ToVector3(), meshNormals[i].ToVector3());


            // We also need to store original and morphed bounding box (bounds)
            _originalBoundingBox = _meshGeometry3D.Bounds.ToBoundingBox();

            _morphedBoundingBox = new BoundingBox(_originalBoundingBox.Minimum,
                                                  new Vector3(_originalBoundingBox.Maximum.X, _originalBoundingBox.Maximum.Y + (float)yOffset, _originalBoundingBox.Maximum.Z));
        }

        private void CompositionTargetOnRendering(object sender, EventArgs e)
        {
            // Check if the RenderingTime was actually changed (meaning we have new frame).
            // If RenderingTime was not changed then Rendering was called multiple times in one frame - we do not want to measure
            // Based on http://stackoverflow.com/questions/5812384/why-is-frame-rate-in-wpf-irregular-and-not-limited-to-monitor-refresh

            var renderingEventArgs = e as RenderingEventArgs;
            if (renderingEventArgs != null)
            {
                if (renderingEventArgs.RenderingTime == _lastRenderingTime)
                    return; // Still on the same frame

                _lastRenderingTime = renderingEventArgs.RenderingTime;
            }

            if (_animationStartTime == DateTime.MinValue)
            {
                _animationStartTime = DateTime.Now;
                return;
            }

            // Do animation
            if (UseSimpleMeshRadioButton.IsChecked ?? false)
                AnimateSimpleMesh();
            else
                AnimateMeshGeomery3d();
        }

        private void AnimateSimpleMesh()
        {
            //if (_vertexBufferArray == null || _simpleMesh == null || !_simpleMesh.IsInitialized)
            if (_vertexBufferArray == null || _simpleMesh == null)
                    return;

            if (!_simpleMesh.IsInitialized)
            {
                // We cannot call RecreateMesh on _simpleMesh, if it was not yet initialized (InitializeResources method called).
                //
                // _simpleMesh is usually initialized, when its parent SceneNode (_meshObjectNode) is added to the DXScene. 
                // In our case this is done inside DXViewportView's Update method. This method is called
                // inside CompositionTraget.Rendering event handler in DXViewportView class.
                // But if AnimateSimpleMesh is called before the DXViewportView's Update method,
                // then we go inside this if statement.
                // Here we have a few options when _simpleMesh is not yet initialized:
                // - we could simply return and wait until the  _simpleMesh is automatically initialized (before the next call to AnimateSimpleMesh method)
                // - we can force updating DXViewportView with calling Update method:
                //   MainDXViewportView.Update();
                // - we can manually initialize _simpleMesh (and its parent _meshObjectNode).
                //   This is done with the following code:

                if (MainDXViewportView.DXScene != null)
                    _meshObjectNode.InitializeResources(MainDXViewportView.DXScene);
                else
                    return; // UH: DXScene is not yet initialized (we cannot do much now) 
            }

            var elapsedSeconds = (DateTime.Now - _animationStartTime).TotalSeconds;

            // calculate animation factor (from 0 to 1) based on Sin function - repeats in 2*PI seconds
            // 0 - means original mesh
            // 1 - means morphed mesh
            float morphAnimationFactor = (float) (Math.Sin(elapsedSeconds - 0.5 * Math.PI) + 1.0f) * 0.5f;
            

            if (_stopwatch == null)
                _stopwatch = new Stopwatch();

            _stopwatch.Restart();

            var vertexCount = _vertexBufferArray.Length;

            float morphAnimationFactorNeg = 1.0f - morphAnimationFactor;

            for (int i = 0; i < vertexCount; i++)
            {
                // Morph position
                var p1 = _originalMesh[i].Position;
                var p2 = _morphedMesh[i].Position;

                _vertexBufferArray[i].Position = morphAnimationFactorNeg * p1 + morphAnimationFactor * p2;


                // Morph normal
                p1 = _originalMesh[i].Normal;
                p2 = _morphedMesh[i].Normal;

                var newNormal = morphAnimationFactorNeg * p1 + morphAnimationFactor * p2;
                newNormal.Normalize(); // After interpolation, the length of the normal can change so we need to re-normalize it again

                _vertexBufferArray[i].Normal = newNormal;
            }


            // To get the ultimate performance, it is possible to use unsafe code.
            // This is almost 50% faster then the managed code above (on i7 6700).
            // To run this code do the following;
            // - comment the for loop above,
            // - uncomment the code below, 
            // - allow unsafe code in project properties (in build settings)
            //
            // NOTE:
            // With a little hack, similar approach can be used with MeshGeometry3D.
            // In order to use unsafe code efficiently when changing MeshGeometry3D Positions and Normals,
            // you need to first get access to the arrays that are used for Positions Point3DCollection
            // and for Normals Vector3DCollection.
            // To do that you will first need to read private _collection field to get FrugalStructList<T>,
            // then read _listStore on FrugalStructList<T> to get ArrayItemList<T>
            // and finally read _entries to get array of T.
            // Once you get the array of T, you can use pointers to write data much faster then when using 
            // Point3DCollection or Vector3DCollection setters.

            //GCHandle originalMeshHandle = GCHandle.Alloc(_originalMesh, GCHandleType.Pinned);
            //GCHandle morphedMeshHandle = GCHandle.Alloc(_morphedMesh, GCHandleType.Pinned);
            //GCHandle vertexBufferArrayHandle = GCHandle.Alloc(_vertexBufferArray, GCHandleType.Pinned);

            //try
            //{
            //    unsafe
            //    {
            //        float* originalMeshPtr = ((float*)originalMeshHandle.AddrOfPinnedObject().ToPointer());
            //        float* morphedMeshPtr = ((float*)morphedMeshHandle.AddrOfPinnedObject().ToPointer());
            //        float* vertexBufferArrayPtr = ((float*)vertexBufferArrayHandle.AddrOfPinnedObject().ToPointer());

            //        for (int i = 0; i < vertexCount; i++)
            //        {
            //            // Morph position

            //            // var p1 = _originalMesh[i].Position;
            //            float p1x = *originalMeshPtr;
            //            float p1y = *(originalMeshPtr + 1);
            //            float p1z = *(originalMeshPtr + 2);

            //            // var p2 = _morphedMesh[i].Position;
            //            float p2x = *morphedMeshPtr;
            //            float p2y = *(morphedMeshPtr + 1);
            //            float p2z = *(morphedMeshPtr + 2);

            //            // _vertexBufferArray[i].Position = morphAnimationFactorNeg * p1 + morphAnimationFactor * p2;
            //            float px = morphAnimationFactorNeg * p1x + morphAnimationFactor * p2x;
            //            float py = morphAnimationFactorNeg * p1y + morphAnimationFactor * p2y;
            //            float pz = morphAnimationFactorNeg * p1z + morphAnimationFactor * p2z;

            //            *vertexBufferArrayPtr       = px;
            //            *(vertexBufferArrayPtr + 1) = py;
            //            *(vertexBufferArrayPtr + 2) = pz;


            //            // Morph normal
            //            // p1 = _originalMesh[i].Normal;
            //            p1x = *(originalMeshPtr + 3);
            //            p1y = *(originalMeshPtr + 4);
            //            p1z = *(originalMeshPtr + 5);

            //            // p2 = _morphedMesh[i].Normal;
            //            p2x = *(morphedMeshPtr + 3);
            //            p2y = *(morphedMeshPtr + 4);
            //            p2z = *(morphedMeshPtr + 5);

            //            // var newNormal = morphAnimationFactorNeg * p1 + morphAnimationFactor * p2;
            //            px = morphAnimationFactorNeg * p1x + morphAnimationFactor * p2x;
            //            py = morphAnimationFactorNeg * p1y + morphAnimationFactor * p2y;
            //            pz = morphAnimationFactorNeg * p1z + morphAnimationFactor * p2z;

            //            // newNormal.Normalize(); // After interpolation, the length of the normal can change so we need to re-normalize it again

            //            // Multiplying is faster then dividing
            //            // So we divide once and the make three multiplications
            //            // This is actually significantly faster then doing 3 divisions (on i7)
            //            float length = 1.0f / (float)Math.Sqrt(px * px + py * py + pz * pz);

            //            // _vertexBufferArray[i].Normal = newNormal;
            //            *(vertexBufferArrayPtr + 3) = px * length;
            //            *(vertexBufferArrayPtr + 4) = py * length;
            //            *(vertexBufferArrayPtr + 5) = pz * length;


            //            originalMeshPtr += 6;
            //            morphedMeshPtr += 6;
            //            vertexBufferArrayPtr += 8; // IMPORTANT: This works only for array of PositionNormalTexture structs (3 + 3 + 2) * float
            //        }
            //    }
            //}
            //finally
            //{
            //    originalMeshHandle.Free();
            //    morphedMeshHandle.Free();
            //}

            _meshMorphTimes.Add(_stopwatch.Elapsed.TotalMilliseconds);
            _stopwatch.Reset();

            // Calculate the current bounding box (bounds) from interpolating between _originalBoundingBox and _morphedBoundingBox
            Vector3 boundsMin = morphAnimationFactor * _originalBoundingBox.Minimum + (1.0f - morphAnimationFactor) * _morphedBoundingBox.Minimum;
            Vector3 boundsMax = morphAnimationFactor * _originalBoundingBox.Maximum + (1.0f - morphAnimationFactor) * _morphedBoundingBox.Maximum;

            _simpleMesh.Bounds = new Bounds(new BoundingBox(boundsMin, boundsMax));

            // Recreate the DirectX vertex buffer
            _simpleMesh.RecreateMesh(recreateVertexBuffer: true, recreateIndexBuffer: false, updateBounds: false);

            // After mesh has been recreated, we also need to call UpdateMesh on MeshObjectNode
            _meshObjectNode.UpdateMesh();

            _stopwatch.Stop();
            
            _meshUpdateTimes.Add(_stopwatch.Elapsed.TotalMilliseconds);

            // When we are working with SceneNode objects, we need manually notify all the SceneNodes that need to be updated.
            _meshObjectNode.NotifySceneNodeChange(SceneNode.SceneNodeDirtyFlags.MeshVertexBufferDataChanged | SceneNode.SceneNodeDirtyFlags.BoundsChanged);
            _meshObjectNode.NotifyAllParentSceneNodesChange(SceneNode.SceneNodeDirtyFlags.ChildBoundsChanged);


            UpdateStatistics((int)elapsedSeconds);
        }

        private void AnimateMeshGeomery3d()
        {
            var elapsedSeconds = (DateTime.Now - _animationStartTime).TotalSeconds;

            // calculate animation factor (from 0 to 1) based on Sin function - repeats in 2*PI seconds
            // 0 - means original mesh
            // 1 - means morphed mesh
            float morphAnimationFactor = (float)(Math.Sin(elapsedSeconds - 0.5 * Math.PI) + 1.0f) * 0.5f;


            if (_stopwatch == null)
                _stopwatch = new Stopwatch();

            _stopwatch.Restart();

            var vertexCount = _vertexBufferArray.Length;

            // Disconnect Positions and Normals from MeshGeometry3D
            // This eliminates big performance slow down when changing many Positions or Normals

            var positions = _meshGeometry3D.Positions;
            _meshGeometry3D.Positions = null;

            var normals = _meshGeometry3D.Normals;
            _meshGeometry3D.Normals = null;

            float morphAnimationFactorNeg = 1.0f - morphAnimationFactor;

            for (int i = 0; i < vertexCount; i++)
            {
                // Morph position
                var p1 = _originalMesh[i].Position;
                var p2 = _morphedMesh[i].Position;

                positions[i] = (morphAnimationFactorNeg * p1 + morphAnimationFactor * p2).ToWpfPoint3D();


                // Morph normal
                p1 = _originalMesh[i].Normal;
                p2 = _morphedMesh[i].Normal;

                var newNormal = morphAnimationFactorNeg * p1 + morphAnimationFactor * p2;
                newNormal.Normalize(); // After interpolation, the length of the normal can change so we need to re-normalize it again

                normals[i] = newNormal.ToWpfVector3D();
            }

            // NOTE:
            // To get better performance when changing it is possible to use unsafe code.
            // See comments in the AnimateSimpleMesh method for more info.

            // Connect collections back to MeshGeometry3D
            _meshGeometry3D.Positions = positions;
            _meshGeometry3D.Normals = normals;

            _stopwatch.Stop();

            _meshMorphTimes.Add(_stopwatch.Elapsed.TotalMilliseconds);

            UpdateStatistics((int)elapsedSeconds);
        }

        private void UpdateStatistics(int elapsedSeconds)
        {
            if (_lastElapsedSecond == elapsedSeconds)
                return;

            double meshMorphTime = _meshMorphTimes.Average();
            MeshMorphTimeTextBlock.Text = string.Format("Mesh morph time: {0:0.00} ms", meshMorphTime);
            

            // Show time that was needed for _simpleMesh.RecreateMesh call
            double meshUpdateTime;
            if (_meshUpdateTimes.Count > 0)
                meshUpdateTime = _meshUpdateTimes.Average(); // we need to call this inside if, because Average returns exception when the collection is empty
            else
                meshUpdateTime = 0;

            MeshUpdateTimeTextBlock.Text = string.Format("Mesh update time: {0:0.00} ms", meshUpdateTime);


            // We also need to take into account the time that is used by DXEngine to update its objects.
            // This time is significant in case of changing MeshGeometry3D data, because in this case
            // DXEngine needs to update the vertex buffer.
            double dxUpdateTime;
            if (_dxEngineUpdateTimes.Count > 0)
                dxUpdateTime = _dxEngineUpdateTimes.Average();
            else
                dxUpdateTime = 0;

            DXUpdateTimeTextBlock.Text = string.Format("DXEngine update time: {0:0.00} ms", dxUpdateTime);


            _meshMorphTimes.Clear();
            _meshUpdateTimes.Clear();
            _dxEngineUpdateTimes.Clear();

            _lastElapsedSecond = elapsedSeconds;
        }


        private static void FillVertexBuffer(PositionNormalTexture[] vertexBufferArray, Point3DCollection positions, Vector3DCollection normals, PointCollection textureCoordinates)
        {
            int vertexesCount = vertexBufferArray.Length;

            if (vertexesCount > positions.Count)
                throw new ArgumentOutOfRangeException("positions", "vertexBufferArray size is bigger than positions count!");

            if (vertexesCount > normals.Count)
                throw new ArgumentOutOfRangeException("normals", "vertexBufferArray size is bigger than normal count!");

            if (textureCoordinates != null && textureCoordinates.Count > 0 && vertexesCount > textureCoordinates.Count)
                throw new ArgumentOutOfRangeException("textureCoordinates", "vertexBufferArray size is bigger than textureCoordinates count!");


            Point3D onePosition;
            Vector3D oneNormal;

            if (textureCoordinates != null && textureCoordinates.Count > 0)
            {
                System.Windows.Point oneTextureCoordinate;

                // the collections are not arrays - we need to use the managed way
                for (int i = 0; i < vertexesCount; i++)
                {
                    onePosition = positions[i];
                    oneNormal = normals[i];
                    oneTextureCoordinate = textureCoordinates[i];

                    vertexBufferArray[i].Position = new Vector3((float) onePosition.X, (float) onePosition.Y, (float) onePosition.Z);
                    vertexBufferArray[i].Normal = new Vector3((float) oneNormal.X, (float) oneNormal.Y, (float) oneNormal.Z);
                    vertexBufferArray[i].TextureCoordinate = new Vector2((float) oneTextureCoordinate.X, (float) oneTextureCoordinate.Y);
                }
            }
            else
            {
                // the collections are not arrays - we need to use the managed way
                for (int i = 0; i < vertexesCount; i++)
                {
                    onePosition = positions[i];
                    oneNormal = normals[i];

                    vertexBufferArray[i].Position = new Vector3((float)onePosition.X, (float)onePosition.Y, (float)onePosition.Z);
                    vertexBufferArray[i].Normal = new Vector3((float)oneNormal.X, (float)oneNormal.Y, (float)oneNormal.Z);
                }
            }
        }

        private void MeshTypeRadioButtonChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            MainViewport.Children.Clear();

            // Because we have cleared all the MainViewport.Children
            // we have also removed the camera light object.
            // To add it back, we need to manually call Refresh method on the camera.
            Camera1.Refresh();


            // Reset timers
            _meshMorphTimes.Clear();
            _meshUpdateTimes.Clear();
            _dxEngineUpdateTimes.Clear();

            if (UseSimpleMeshRadioButton.IsChecked ?? false)
            {
                AddSimpleMesh();
                UseDynamicBufferCheckBox.IsEnabled = true;
            }
            else
            {
                AddMeshGeometry3D();
                UseDynamicBufferCheckBox.IsEnabled = false;
            }

            // Resert animation
            _animationStartTime = DateTime.Now;
        }

        private void OnUseDynamicBufferCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            if (_simpleMesh != null)
                _simpleMesh.CreateDynamicVertexBuffer = UseDynamicBufferCheckBox.IsChecked ?? false;
        }
    }
}
