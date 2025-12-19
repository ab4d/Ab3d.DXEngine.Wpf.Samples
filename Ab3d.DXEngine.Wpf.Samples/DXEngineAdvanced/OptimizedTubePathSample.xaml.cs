using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Ab3d.DirectX;
using Ab3d.DirectX.Effects;
using Ab3d.Visuals;
using InstanceData = Ab3d.DirectX.InstanceData;
using Point = System.Windows.Point;

#if SHARPDX
using SharpDX;
#endif

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineAdvanced
{
    // 3D tube path object can be used instead of 3D lines.
    // An advantage of tube path objects is that they do not require geometry shader to generate the
    // line mesh geometry from line positions. 
    // With tube path objects, the mesh geometry is generated at initialization time.
    //
    // The standard way to do that is to use TubePathVisual3D from Ab3d.PowerToys library.
    // But when we want to generate a lot of tube paths,
    // this approach takes a lot of time because first all WPF 3D objects need to be generated and 
    // then those objects need to be converted into DXEngine objects.
    //
    // This sample provides alternative ways to show many tube path objects.
    //
    // Therefore it is much faster to generate DXEngine's SceneNode objects without any WPF object.
    // This is much faster.
    //
    // The sample also provides code that shows how to use instancing to show tube paths.
    // But here this is a slower solution then to use a fixed geometry.
    // To see this option uncomment call to the AddInstancedSpirals method (in constructor).
    //
    //
    // Results on i7 6700 (4 cores with hyper-threading):
    //
    // Method                           Time to first frame
    // WPF's TubePathVisual3D           5500 ms
    // MeshObjectNode (single thread)   220 ms                   
    // MeshObjectNode (multi-threaded)  110 ms

    /// <summary>
    /// Interaction logic for OptimizedTubePathSample.xaml
    /// </summary>
    public partial class OptimizedTubePathSample : Page
    {
        private float[] _sinuses;
        private float[] _cosines;
        private int _lastSegmentsCount;
        private SolidColorEffect _solidColorEffect;

        private DisposeList _disposables;
        private Stopwatch _stopwatch;

        public OptimizedTubePathSample()
        {
            InitializeComponent();


            _disposables = new DisposeList();

            MainDXViewportView.DXSceneDeviceCreated += delegate(object sender, EventArgs args)
            {
                _solidColorEffect = MainDXViewportView.DXScene.DXDevice.EffectsManager.GetEffect<SolidColorEffect>();
                _disposables.Add(_solidColorEffect);

                _stopwatch = new Stopwatch();
                _stopwatch.Start();

                AddSpirals_MeshObjectNode(10, 10, 5000, useMultiThreading: true);

                // Uncomment to see how the tubes are created in a standard Ab3d.PowerToys way.
                //AddSpirals_TubePathVisual3D(10, 10, 5000);

                // To see how to use instancing to draw tube paths, uncomment the following line.
                // Note: In this case is instancing slower then rendering a fixed geometry because the task for the GPU is much more complicated in case of instancing.
                //AddInstancedSpirals(10, 10, 5000);
            };

            // Subscribe to get event when the first frame is rendered
            MainDXViewportView.SceneRendered += MainDXViewportViewOnSceneRendered;

            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                _disposables.Dispose();
            };
        }

        private void MainDXViewportViewOnSceneRendered(object sender, EventArgs e)
        {
            if (_stopwatch != null)
            {
                _stopwatch.Stop();

                // Show results:
                // NOTE: The first time this sample is run the results are not as good as possible.
                //       Go to other sample and then come back to get the actual results.

#if DEBUG
                System.Diagnostics.Debug.WriteLine($"Time to first frame: {_stopwatch.Elapsed.TotalMilliseconds:0.00} ms");
#else
                MessageBox.Show($"Time to first frame: {_stopwatch.Elapsed.TotalMilliseconds:0.00} ms");
#endif
            }

            MainDXViewportView.SceneRendered -= MainDXViewportViewOnSceneRendered;
        }


        // This method uses low level DXEngine objects to create tube paths.
        private void AddSpirals_MeshObjectNode(int xCount, int yCount, int spiralLength, bool useMultiThreading)
        {
            float circleRadius = 10;
            int spiralCircles = spiralLength / 20; // One circle in the spiral is created from 20 lines

            var dxMaterial = new Ab3d.DirectX.Materials.StandardMaterial()
            {
                DiffuseColor  = Color3.Black,
                EmissiveColor = Color3.White,
                Effect        = _solidColorEffect
            };

            _disposables.Add(dxMaterial);


            float xStart = -xCount * circleRadius * 1.5f;
            float yStart = -yCount * circleRadius * 1.5f;


            if (useMultiThreading)
            {
                // On i7 6700 with 4 cores with hyper-threading the multi-threaded code path is almost 100% faster then single threaded solution.
                var initializedMeshes = new MeshBase[xCount, yCount];

                var dxDevice = MainDXViewportView.DXScene.DXDevice;

                Parallel.For(0, xCount * yCount, xy =>
                {
                    int x = (int) xy / yCount;
                    int y = (int) xy % yCount;


                    var spiralPositions = CreateSpiralPositions(startPosition: new Vector3(x * circleRadius * 3 + xStart, y * circleRadius * 3 + yStart, 0),
                                                                circleXDirection: new Vector3(1, 0, 0),
                                                                circleYDirection: new Vector3(0, 1, 0),
                                                                oneSpiralCircleDirection: new Vector3(0, 0, -10),
                                                                circleRadius: circleRadius,
                                                                segmentsPerCircle: 20,
                                                                circles: spiralCircles);

                    MeshBase tubePathMesh = CreateTubePathMesh(spiralPositions, radius: 1.0f, segmentsCount: 8, isTubeClosed: true, tubeColor: Color4.White);

                    // Create DirectX resources in the background thread (this creates buffers on the GPU and send data there from the main memory)
                    tubePathMesh.InitializeResources(dxDevice);

                    // Save the mesh
                    initializedMeshes[x, y] = tubePathMesh;
                });

                // Now most of the work was done in multi-threaded way.
                // So we only need to create the MeshObjectNode and add that to the Scene. This needs to be done on the UI thread. 
                MainViewport.BeginInit();
                MainViewport.Children.Clear();

                for (int x = 0; x < xCount; x++)
                {
                    for (int y = 0; y < yCount; y++)
                    {
                        var tubePathMesh = initializedMeshes[x, y];
                        var meshObjectNode = new Ab3d.DirectX.MeshObjectNode(tubePathMesh, dxMaterial);

                        var tubePathVisual3D = new SceneNodeVisual3D(meshObjectNode);

                        // IMPORTANT:
                        //
                        // In this performance demo we create new spiral positions and new tubePathMesh for each spiral.
                        // But because the spirals are the same, we could create only one spiral positions and one tubePathMesh
                        // and then use that tubePathMesh to create multiple MeshObjectNode and SceneNodeVisual3D objects 
                        // each of them with its Transform property set - as shown in the line below.
                        //
                        // Sharing one mesh would provide much better performance and lower memory usage, 
                        // but for this demo we want to simulate cration of huge tube paths in the background thread.
                        //
                        //tubePathVisual3D.Transform = new TranslateTransform3D(x * circleRadius * 3 + xStart, y * circleRadius * 3 + yStart, 0);

                        _disposables.Add(tubePathMesh); // We did not add that in the background thread (we would need locking for that) so we need to do that now
                        _disposables.Add(meshObjectNode);

                        MainViewport.Children.Add(tubePathVisual3D);
                    }
                }

                MainViewport.EndInit();
            }

            else
            {
                // No multi-threading
                MainViewport.BeginInit();
                MainViewport.Children.Clear();

                for (int x = 0; x < xCount; x++)
                {
                    for (int y = 0; y < yCount; y++)
                    {
                        var spiralPositions2 = CreateSpiralPositions(startPosition: new Point3D(x * circleRadius * 3 + xStart, y * circleRadius * 3 + yStart, 0),
                                            circleXDirection: new Vector3D(1, 0, 0),
                                            circleYDirection: new Vector3D(0, 1, 0),
                                            oneSpiralCircleDirection: new Vector3D(0, 0, -10),
                                            circleRadius: circleRadius,
                                            segmentsPerCircle: 20,
                                            circles: spiralCircles);

                        var spiralPositions = spiralPositions2.Select(p => p.ToVector3()).ToArray();


                        //var spiralPositions = CreateSpiralPositions(startPosition: new Vector3(x * circleRadius * 3 + xStart, y * circleRadius * 3 + yStart, 0),
                        //                                            circleXDirection: new Vector3(1, 0, 0),
                        //                                            circleYDirection: new Vector3(0, 1, 0),
                        //                                            oneSpiralCircleDirection: new Vector3(0, 0, -10),
                        //                                            circleRadius: circleRadius,
                        //                                            segmentsPerCircle: 20,
                        //                                            circles: spiralCircles);

                        var tubePathMesh = CreateTubePathMesh(spiralPositions, radius: 2, segmentsCount: 8, isTubeClosed: true, tubeColor: Color4.White);
                        
                        var meshObjectNode = new Ab3d.DirectX.MeshObjectNode(tubePathMesh, dxMaterial);

                        var tubePathVisual3D = new SceneNodeVisual3D(meshObjectNode);
                        //tubePathVisual3D.Transform = new TranslateTransform3D(x * circleRadius * 3 + xStart, y * circleRadius * 3 + yStart, 0);

                        lock (this)
                            _disposables.Add(meshObjectNode);

                        MainViewport.Children.Add(tubePathVisual3D);
                    }
                }

                MainViewport.EndInit();
            }
        }

        // This method uses standard Ab3d.PowerToys TubePathVisual3D to create tube paths
        private void AddSpirals_TubePathVisual3D(int xCount, int yCount, int spiralLength)
        {
            float circleRadius = 10;
            int spiralCircles = spiralLength / 20; // One circle in the spiral is created from 20 lines

            double xStart = -xCount * circleRadius * 1.5;
            double yStart = -yCount * circleRadius * 1.5;

            MainViewport.BeginInit();

            MainViewport.Children.Clear();


            var materialGroup = new MaterialGroup();
            materialGroup.Children.Add(new DiffuseMaterial(Brushes.Black));
            materialGroup.Children.Add(new EmissiveMaterial(Brushes.White));

            for (int x = 0; x < xCount; x++)
            {
                for (int y = 0; y < yCount; y++)
                {
                    var spiralPositions = CreateSpiralPositions(startPosition: new Point3D(x * circleRadius * 3 + xStart, y * circleRadius * 3 + yStart, 0),
                                                                circleXDirection: new Vector3D(1, 0, 0),
                                                                circleYDirection: new Vector3D(0, 1, 0),
                                                                oneSpiralCircleDirection: new Vector3D(0, 0, -10),
                                                                circleRadius: circleRadius,
                                                                segmentsPerCircle: 20,
                                                                circles: spiralCircles);

                    var tubePathVisual3D = new TubePathVisual3D()
                    {
                        PathPositions = new Point3DCollection(spiralPositions),
                        Radius = 2,
                        Segments = 8,
                        GenerateTextureCoordinates = false,
                    };

                    tubePathVisual3D.Material = materialGroup;
                    //tubePathVisual3D.Transform = new TranslateTransform3D(x * circleRadius * 3 + xStart, y * circleRadius * 3 + yStart, 0);

                    MainViewport.Children.Add(tubePathVisual3D);
                }
            }

            MainViewport.EndInit();
        }

        private MeshBase CreateTubePathMesh(Vector3[] pathPositions, float radius, int segmentsCount, bool isTubeClosed, Color4 tubeColor)
        {
            int pathPositionsCount = pathPositions.Length;


            // Preallocate the positions, indices, normals and textures collections - this prevents resizing the collection when the elements are added to them
            int totalPositionsCount = segmentsCount * pathPositionsCount;

            int shownSegmentsCount        = pathPositionsCount - 1;
            int totalTriangleIndicesCount = shownSegmentsCount * segmentsCount * 2 * 3; // 2 triangles for each segment * No. of shown segments * 3 indices per triangle
            if (isTubeClosed)
                totalTriangleIndicesCount += (segmentsCount - 2) * 2 * 3; // Add closing triangles (for triangle strip)


            var vertexBuffer = new PositionNormal[totalPositionsCount];
            var indexBuffer  = new int[totalTriangleIndicesCount];

            AddTubePathMesh(pathPositions, radius, segmentsCount, isTubeClosed, vertexBuffer, indexBuffer, vertexBufferStartIndex: 0, indexBufferStartIndex: 0);

            var simpleMesh = new SimpleMesh<PositionNormal>(vertexBuffer,
                                                            indexBuffer,
                                                            inputLayoutType: InputLayoutType.Position | InputLayoutType.Normal);

            // Quickly calculate mesh BoundingBox
            // If this is not done here, then BoundingBox is calculated in SimpleMesh with checking all the tube path's positions.
            // To prevent checking all the positions, we can simplify the calculation of bounding box with using
            // all path positions and then extending this for radius into all directions.
            // This will create slightly bigger bounding box but this is not a problem.
            var positionsBoundingBox = BoundingBox.FromPoints(pathPositions);
            simpleMesh.Bounds = new Bounds(new Vector3(positionsBoundingBox.Minimum.X - radius, positionsBoundingBox.Minimum.Y - radius, positionsBoundingBox.Minimum.Z - radius),
                                           new Vector3(positionsBoundingBox.Maximum.X + radius, positionsBoundingBox.Maximum.Y + radius, positionsBoundingBox.Maximum.Z + radius));


            // lock access to _disposables because this method may be executed in multiple threads
            lock (this)
            {
                _disposables.Add(simpleMesh);
            }

            return simpleMesh;
        }

        private void AddTubePathMesh(Vector3[] pathPositions, float radius, int segmentsCount, bool isTubeClosed, PositionNormal[] vertexBuffer, int[] indexBuffer, int vertexBufferStartIndex, int indexBufferStartIndex)
        {
            if (_lastSegmentsCount != segmentsCount)
            { 
                // lock because this method may be executed in multiple threads 
                lock (this)
                {
                    if (_lastSegmentsCount != segmentsCount)
                    {
                        _sinuses = new float[segmentsCount];
                        _cosines = new float[segmentsCount];

                        double angle = 0;
                        double angleStep = 2 * Math.PI / segmentsCount;

                        for (int i = 0; i < segmentsCount; i++)
                        {
                            _sinuses[i] = (float)Math.Sin(angle) * radius;
                            _cosines[i] = (float)Math.Cos(angle) * radius;

                            angle += angleStep;
                        }

                        _lastSegmentsCount = segmentsCount;
                    }
                }
            }


            int pathPositionsCount = pathPositions.Length;
            int totalPositionsCount = segmentsCount * pathPositionsCount;

            Vector3 pathPosition1 = pathPositions[0];
            Vector3 pathPosition2 = pathPositions[1];

            Vector3 lastDirection = new Vector3();


            // Calculate the perpendicular vectors for the first segment
            // First get the direction of the 
            Vector3 pathSegmentDirection = pathPositions[1] - pathPosition1;
            pathSegmentDirection.Normalize();

            // Now we calculate the vectors that are perpendicular (90 degrees) to the averageSegmentDirection
            // For heightDirection = (0, 1, 0) the values are:
            // p1 = (1, 0, 0) - right
            // p2 = (0, 0, -1) - into the screen
            Vector3 p1, p2; // two perpendicular vectors
            GetPerpendicularVectors(pathSegmentDirection, out p1, out p2);


            int vertexBufferIndex = vertexBufferStartIndex;
            int indexBufferIndex  = indexBufferStartIndex;

            for (int i = 0; i < pathPositionsCount; i++)
            {
                // Get the used direction for segment - this is the average of directions of the previous and the next segment (except for first and last segment)
                Vector3 averageSegmentDirection;

                if (i == 0)
                {
                    // first segment: use the direction of the first segment except when the path is closed - the we need to average the first and last segment
                    averageSegmentDirection = pathSegmentDirection;
                }
                else if (i == pathPositionsCount - 1)
                {
                    averageSegmentDirection = lastDirection;
                }
                else
                {
                    // inner segments: average the direction of the previous and the next segment
                    pathPosition2 = pathPositions[i + 1];

                    pathSegmentDirection = pathPosition2 - pathPosition1;
                    pathSegmentDirection.Normalize();

                    // The direction of the inter-section circle is the average direction of the previous and next segment
                    averageSegmentDirection = (lastDirection + pathSegmentDirection) * 0.5f;
                }


                // Calculate the new perpendicular vectors (p1 and p2)
                // We should not use the MathUtils.GetPerpendicularVectors because this could lead to sudden "flip" of a vector
                // Therefore we reuse the previous p1 and p2 vectors and use the new averageSegmentDirection to update the p1 and p2 values
                p1 = Vector3.Cross(p2, averageSegmentDirection);

                var p1LengthSquared = p1.X * p1.X + p1.Y * p1.Y + p1.Z * p1.Z; 

                if (p1LengthSquared > 1e-6)
                {
                    p2 = Vector3.Cross(averageSegmentDirection, p1);
                }
                else if (p1LengthSquared <= 1e-6)
                {
                    // This could happen for the following data:
                    //pathPositions = new Point3DCollection(new Point3D[]
                    //{
                    //    new Point3D(0, 0, 0),
                    //    new Point3D(10, 0, 0),
                    //    new Point3D(10, 0, 10),
                    //    new Point3D(-10, 0, 10)
                    //});

                    GetPerpendicularVectors(averageSegmentDirection, out p1, out p2);
                }

                p1.Normalize();
                p2.Normalize();


                for (int j = 0; j < segmentsCount; j++)
                {
                    float sin = _sinuses[j];
                    float cos = _cosines[j];

                    float x = sin * p1.X - cos * p2.X;
                    float y = sin * p1.Y - cos * p2.Y;
                    float z = sin * p1.Z - cos * p2.Z;

                    var onePosition = new Vector3(pathPosition1.X + x, pathPosition1.Y + y, pathPosition1.Z + z);

                    var oneNormal = new Vector3(x, y, z);
                    oneNormal.Normalize();

                    vertexBuffer[vertexBufferIndex] = new PositionNormal(onePosition, oneNormal);
                    vertexBufferIndex++;
                }

                pathPosition1 = pathPosition2;
                lastDirection = pathSegmentDirection;
            }

            if (isTubeClosed)
            {
                // Fill the start circle with simple triangle strip
                for (int i = 1; i < segmentsCount - 1; i++)
                {
                    indexBuffer[indexBufferIndex]     = 0;
                    indexBuffer[indexBufferIndex + 1] = i + 1;
                    indexBuffer[indexBufferIndex + 2] = i;

                    indexBufferIndex += 3;
                }
            }

            // Setup triangle indices
            int startPos1 = 0;             // Start position on the previous circle
            int startPos2 = segmentsCount; // Start position on this circle

            // Code for no texture coordinates
            for (int i = 0; i < pathPositionsCount - 1; i++)
            {
                int pos1 = startPos1;
                int pos2 = startPos2;

                for (int j = 1; j < segmentsCount; j++)
                {
                    indexBuffer[indexBufferIndex]     = pos1;
                    indexBuffer[indexBufferIndex + 1] = pos1 + 1;
                    indexBuffer[indexBufferIndex + 2] = pos2;

                    indexBuffer[indexBufferIndex + 3] = pos1 + 1;
                    indexBuffer[indexBufferIndex + 4] = pos2 + 1;
                    indexBuffer[indexBufferIndex + 5] = pos2;

                    pos1++;
                    pos2++;
                    indexBufferIndex += 6;
                }

                // No texture coordinates
                indexBuffer[indexBufferIndex]     = pos1;
                indexBuffer[indexBufferIndex + 1] = startPos1;
                indexBuffer[indexBufferIndex + 2] = pos2;

                indexBuffer[indexBufferIndex + 3] = pos2;
                indexBuffer[indexBufferIndex + 4] = startPos1;
                indexBuffer[indexBufferIndex + 5] = startPos2;

                startPos1 += segmentsCount;
                startPos2 += segmentsCount;
                indexBufferIndex += 6;
            }

            if (isTubeClosed)
            {
                // Fill the end circle with simple triangle strip
                int lastCircleIndex = totalPositionsCount - segmentsCount;
                for (int i = 1; i < segmentsCount - 1; i++)
                {
                    indexBuffer[indexBufferIndex]     = lastCircleIndex;
                    indexBuffer[indexBufferIndex + 1] = lastCircleIndex + i;
                    indexBuffer[indexBufferIndex + 2] = lastCircleIndex + i + 1;

                    indexBufferIndex += 3;
                }
            }
        }

        private Vector3[] CreateSpiralPositions(Vector3 startPosition, Vector3 circleXDirection, Vector3 circleYDirection, Vector3 oneSpiralCircleDirection, float circleRadius, int segmentsPerCircle, int circles)
        {
            List<Vector2> oneCirclePositions = new List<Vector2>(segmentsPerCircle);

            float angleStep = (float)(2 * Math.PI / segmentsPerCircle);
            float angle     = 0;

            for (int i = 0; i < segmentsPerCircle; i++)
            {
                // Get x any y position on a flat plane
                float xPos = (float)Math.Sin(angle);
                float yPos = (float)Math.Cos(angle);

                angle += angleStep;

                var newPoint = new Vector2(xPos * circleRadius, yPos * circleRadius);
                oneCirclePositions.Add(newPoint);
            }


            var allPositions = new Vector3[segmentsPerCircle * circles];

            Vector3 onePositionDirection = oneSpiralCircleDirection / segmentsPerCircle;
            Vector3 currentCenterPoint   = startPosition;

            int index = 0;

            for (int i = 0; i < circles; i++)
            {
                for (int j = 0; j < segmentsPerCircle; j++)
                {
                    float xCircle = oneCirclePositions[j].X;
                    float yCircle = oneCirclePositions[j].Y;

                    var point3D = new Vector3(currentCenterPoint.X + (xCircle * circleXDirection.X) + (yCircle * circleYDirection.X),
                                              currentCenterPoint.Y + (xCircle * circleXDirection.Y) + (yCircle * circleYDirection.Y),
                                              currentCenterPoint.Z + (xCircle * circleXDirection.Z) + (yCircle * circleYDirection.Z));

                    allPositions[index] = point3D;

                    index++;
                    currentCenterPoint += onePositionDirection;
                }
            }

            return allPositions;
        }




        private void CreateInstancedTubePath()
        {
            // Create curve through those points
            var controlPoints = new Point3D[]
            {
                new Point3D(-200, 50, 150),
                new Point3D(0, 50, 20),
                new Point3D(150, 50, 0),
                new Point3D(150, 50, 200),
                new Point3D(250, 50, 200)
            };

            // To create curve through specified points we must first convert the points into a bezierCurve (curve that has tangents defined for each point).
            // The curve is created with 10 points per segments - 10 points between two control points.
            var bezierCurve = Ab3d.Utilities.BezierCurve.CreateFromCurvePositions(controlPoints);
            var curvePositions = bezierCurve.CreateBezierCurve(positionsPerSegment: 10);


            // Convert Point3D to Vector3
            var dxCurvePositions = curvePositions.Select(p => p.ToVector3()).ToList();

            // Setup InstanceData
            var instanceData = new InstanceData[curvePositions.Count];


            // NOTE:
            // When a tube path is created from a lot of positions,
            // then it is worth to transform the following loop into Parallel.For loop.

            var startPosition = dxCurvePositions[0];

            for (int i = 1; i < dxCurvePositions.Count; i++)
            {
                var endPosition = dxCurvePositions[i];

                var direction = endPosition - startPosition;
                var length = direction.Length(); // length will be used for x scale

                var scale = new Vector3(length, 1, 1);

                //direction.Normalize(); // Because we already have length, we can quickly normalize with simply dividing by length
                direction /= length;

                instanceData[i].World = Common.MatrixUtils.GetMatrixFromNormalizedDirection(direction, startPosition, scale);
                instanceData[i].DiffuseColor = Color4.White;

                startPosition = endPosition;
            }

            // Create tube mesh with normalized radius (=1) and from (0,0,0) to (1,0,0).
            // This mesh is then transformed to connect positions along the path
            var tubeLineMesh3D = new Ab3d.Meshes.TubeLineMesh3D(startPosition: new Point3D(0, 0, 0),
                                                                endPosition: new Point3D(1, 0, 0),
                                                                radius: 1,
                                                                segments: 8,
                                                                generateTextureCoordinates: false).Geometry;

            // Create InstancedMeshGeometryVisual3D
            var instancedMeshGeometryVisual3D = new InstancedMeshGeometryVisual3D(tubeLineMesh3D);
            instancedMeshGeometryVisual3D.InstancesData = instanceData;

            // Set IsSolidColorMaterial to render tube instances with solid color without any shading based on lighting
            instancedMeshGeometryVisual3D.IsSolidColorMaterial = true;

            // Add instancedMeshGeometryVisual3D to the scene
            MainViewport.Children.Add(instancedMeshGeometryVisual3D);
        }

        private InstancedMeshGeometryVisual3D AddInstancedTubePath(Point3DCollection spiralPositionCollection, Color4 tubeColor)
        {
            // Setup InstanceData
            var instanceData = new InstanceData[spiralPositionCollection.Count];

            // Convert to list of Vector3
            var dxCurvePositions = spiralPositionCollection.Select(p => p.ToVector3()).ToList();


            // NOTE:
            // When a tube path is created from a lot of positions,
            // then it is worth to transform the following loop into Parallel.For loop.

            var startPosition = dxCurvePositions[0];

            for (int i = 1; i < dxCurvePositions.Count; i++)
            {
                var endPosition = dxCurvePositions[i];

                var direction = endPosition - startPosition;
                var length = direction.Length(); // length will be used for x scale

                var scale = new Vector3(length, 1, 1);

                //direction.Normalize(); // Because we already have length, we can quickly normalize with simply dividing by length
                direction /= length;

                instanceData[i].World = Common.MatrixUtils.GetMatrixFromNormalizedDirection(direction, startPosition, scale);
                instanceData[i].DiffuseColor = Color4.White;

                startPosition = endPosition;
            }

            // Create tube mesh with normalized radius (=1) and from (0,0,0) to (1,0,0).
            // This mesh is then transformed to connect positions along the path
            var tubeLineMesh3D = new Ab3d.Meshes.TubeLineMesh3D(startPosition: new Point3D(0, 0, 0),
                                                                endPosition: new Point3D(1, 0, 0),
                                                                radius: 1,
                                                                segments: 8,
                                                                generateTextureCoordinates: false).Geometry;

            // Create InstancedMeshGeometryVisual3D
            var instancedMeshGeometryVisual3D = new InstancedMeshGeometryVisual3D(tubeLineMesh3D);
            instancedMeshGeometryVisual3D.InstancesData = instanceData;

            // Set IsSolidColorMaterial to render tube instances with solid color without any shading based on lighting
            instancedMeshGeometryVisual3D.IsSolidColorMaterial = true;

            // Add instancedMeshGeometryVisual3D to the scene
            MainViewport.Children.Add(instancedMeshGeometryVisual3D);

            return instancedMeshGeometryVisual3D;
        }


        private void AddInstancedSpirals(int xCount, int yCount, int spiralLength)
        {
            double circleRadius = 10;
            int spiralCircles = spiralLength / 20; // One circle in the spiral is created from 20 lines

            var spiralPositions = CreateSpiralPositions(startPosition: new Point3D(0, 0, 0),
                                                        circleXDirection: new Vector3D(1, 0, 0),
                                                        circleYDirection: new Vector3D(0, 1, 0),
                                                        oneSpiralCircleDirection: new Vector3D(0, 0, -10),
                                                        circleRadius: circleRadius,
                                                        segmentsPerCircle: 20,
                                                        circles: spiralCircles);

            var spiralPositionCollection = new Point3DCollection(spiralPositions);

            double xStart = -xCount * circleRadius * 3 / 2;
            double yStart = -yCount * circleRadius * 3 / 2;

            MainViewport.BeginInit();

            MainViewport.Children.Clear();


            var materialGroup = new MaterialGroup();
            materialGroup.Children.Add(new DiffuseMaterial(Brushes.Black));
            materialGroup.Children.Add(new EmissiveMaterial(Brushes.White));

            for (int x = 0; x < xCount; x++)
            {
                for (int y = 0; y < yCount; y++)
                {
                    // PERFORMANCE NOTICE:
                    // The AddSpiralVisual3D creates one PolyLineVisual3D for each sphere.
                    // When creating many PolyLineVisual3D objects, the performance would be significantly improved if instead of many PolyLineVisual3D objects,
                    // all the spheres would be rendered with only one MultiPolyLineVisual3D. 
                    // This would allow rendering all the 3D lines with only one draw call.

                    var instancedMeshGeometryVisual3D = AddInstancedTubePath(spiralPositionCollection, Color4.White);
                    instancedMeshGeometryVisual3D.Transform = new TranslateTransform3D(x * circleRadius * 3 + xStart, y * circleRadius * 3 + yStart, 0);

                    //var tubePathVisual3D = new TubePathVisual3D()
                    //{
                    //    PathPositions = spiralPositionCollection,
                    //    Radius = 2,
                    //    Segments = 8,
                    //    GenerateTextureCoordinates = false,
                    //};

                    //tubePathVisual3D.Material = materialGroup;

                    //tubePathVisual3D.Transform = new TranslateTransform3D(x * circleRadius * 3 + xStart, y * circleRadius * 3 + yStart, 0);

                    //MainViewport.Children.Add(tubePathVisual3D);
                }
            }

            MainViewport.EndInit();
        }


        private List<Point3D> CreateSpiralPositions(Point3D startPosition, Vector3D circleXDirection, Vector3D circleYDirection, Vector3D oneSpiralCircleDirection, double circleRadius, int segmentsPerCircle, int circles)
        {
            List<Point> oneCirclePositions = new List<Point>(segmentsPerCircle);

            double angleStep = 2 * Math.PI / segmentsPerCircle;
            double angle     = 0;

            for (int i = 0; i < segmentsPerCircle; i++)
            {
                // Get x any y position on a flat plane
                double xPos = Math.Sin(angle);
                double yPos = Math.Cos(angle);

                angle += angleStep;

                var newPoint = new Point(xPos * circleRadius, yPos * circleRadius);
                oneCirclePositions.Add(newPoint);
            }


            var allPositions = new List<Point3D>(segmentsPerCircle * circles);

            Vector3D onePositionDirection = oneSpiralCircleDirection / segmentsPerCircle;
            Point3D  currentCenterPoint   = startPosition;

            for (int i = 0; i < circles; i++)
            {
                for (int j = 0; j < segmentsPerCircle; j++)
                {
                    double xCircle = oneCirclePositions[j].X;
                    double yCircle = oneCirclePositions[j].Y;

                    var point3D = new Point3D(currentCenterPoint.X + (xCircle * circleXDirection.X) + (yCircle * circleYDirection.X),
                                              currentCenterPoint.Y + (xCircle * circleXDirection.Y) + (yCircle * circleYDirection.Y),
                                              currentCenterPoint.Z + (xCircle * circleXDirection.Z) + (yCircle * circleYDirection.Z));

                    allPositions.Add(point3D);

                    currentCenterPoint += onePositionDirection;
                }
            }

            return allPositions;
        }

        private void CameraRotationButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (Camera1.IsRotating)
                Camera1.StopRotation();
            else
                Camera1.StartRotation(45, 0);
        }


        /// <summary>
        /// Calculate two vectors that are perpendicular to the inputVector. Both calculated vectors are normalized.
        /// </summary>
        /// <param name="inputVector">input vector</param>
        /// <param name="p1">first perpendicular vector (normalized)</param>
        /// <param name="p2">second perpendicular vector (normalized)</param>
        public static void GetPerpendicularVectors(Vector3 inputVector, out Vector3 p1, out Vector3 p2)
        {
            // Calculate the vectors that are perpendicular (90 degress) to the arrowVector
            // We do this with using two vector and than use the one that has greater angle between the vector and arrowVector

            // Dot product is = length(a) * length(b) * cos(fi)
            // With creating two dot products we can get the vector that has greater angle

            Vector3 v1, v2; // two orthogonal vectors
            Vector3 normalizedDirection = inputVector;
            normalizedDirection.Normalize();

            v1 = new Vector3(0, 1, 0);
            double dot1 = Vector3.Dot(normalizedDirection, v1);

            v2 = new Vector3(0, 0, 1);
            double dot2 = Vector3.Dot(normalizedDirection, v2);

            if (Math.Abs(dot1) < 0.0001 && Math.Abs(dot2) < 0.0001) // both dot products are zero (or almost zero)
            {
                // This can mean two thing:
                // 1) arrowVector is orthogonal to both v1 and v2 - in the direction (-1,0,0) - (1,0,0) or opposite
                // 2) arrowVector length is 0 (Start and end position are the same)
                // NOTE that arrowVector.Y and arrowVector.Z are zero (or almost zero)

                if (Math.Abs(normalizedDirection.X) < 0.00001) // x is also zero (Start and end position are the same)
                {
                    p1 = p2 = new Vector3(0, 0, 0);
                    return;
                }

                // If we are here than: arrowVector is orthogonal to both v1 and v2
                p1 = v1;
                p2 = v2;

                p1.Normalize();
                p2.Normalize();

                if (normalizedDirection.X < 0)
                    p1 = -v1; // We need to negate the direction of v1 (otherwise the order or triangles would be broken - back material would be visible instead of material)
            }
            else
            {
                if (Math.Abs(dot1) < Math.Abs(dot2)) // cos: smaller dot product means bigger angle - we need bigger angle
                    p1 = Vector3.Cross(normalizedDirection, v1);
                else
                    p1 = Vector3.Cross(normalizedDirection, v2);

                p1.Normalize();

                // Now when we have first perpendicular vector we can simple calculate the other

                p2 = Vector3.Cross(normalizedDirection, p1);
                p2.Normalize();
            }
        }
    }
}