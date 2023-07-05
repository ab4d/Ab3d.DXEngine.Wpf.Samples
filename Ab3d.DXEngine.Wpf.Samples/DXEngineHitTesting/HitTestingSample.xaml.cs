using System;
using System.CodeDom;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Media.Media3D;
using Ab3d.Common.Models;
using Ab3d.DirectX;
using Ab3d.DirectX.Common.EventManager3D;
using Ab3d.DirectX.Controls;
using Ab3d.DirectX.Materials;
using Ab3d.DirectX.Models;
using Ab3d.DirectX.Utilities;
using Ab3d.Utilities;
using Ab3d.Visuals;
using SharpDX;
using LineCap = Ab3d.Common.Models.LineCap;
using Material = System.Windows.Media.Media3D.Material;
using Matrix = SharpDX.Matrix;
using MeshUtils = Ab3d.Utilities.MeshUtils;
using Point = System.Windows.Point;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineHitTesting
{
    /// <summary>
    /// Interaction logic for HitTestingSample.xaml
    /// </summary>
    public partial class HitTestingSample : Page
    {
        private DisposeList _disposables;

        private ModelVisual3D _rootModelVisual3D;
        private ModelVisual3D _hitLinesModelVisual3D;
        private SphereVisual3D _sphereVisual3D;
        private ModelVisual3D _grayTeapotModelVisual3D;
        private MeshObjectNode _pyramidMeshObjectNode;
        private MeshObjectNode _blueTeapotMeshObjectNode;
        private MeshObjectNode _greenTeapotMeshObjectNode;
        private InstancedMeshGeometryVisual3D _instancedMeshGeometryVisual3D;

        public HitTestingSample()
        {
            InitializeComponent();

            _disposables = new DisposeList();

            _hitLinesModelVisual3D = new ModelVisual3D();
            MainViewport3D.Children.Add(_hitLinesModelVisual3D);


            CreateTestModels();

            MainDXViewportView.DXSceneDeviceCreated += delegate(object sender, EventArgs args)
            {
                // Display settings from DXHitTestOptions.GenerateOctTreeOnMeshInitialization and DXHitTestOptions.MeshPositionsCountForOctTreeGeneration
                //
                // DXHitTestOptions.GenerateOctTreeOnMeshInitialization documentation:
                // Gets or sets an integer value that specifies number of positions in a mesh (DXMeshGeometry3D) at which a OctTree is generated to speed up hit testing
                // (e.g. if mesh has more positions then a value specified with this property, then OctTree will be generated for the mesh).
                // Default value is 512.
                //
                // 
                // DXHitTestOptions.GenerateOctTreeOnMeshInitialization documentation:
                // When true and when mesh has more positions than MeshPositionsCountForOctTreeGeneration, then the OctTree is generated at mesh initialization time.
                // When false and when mesh has more positions than MeshPositionsCountForOctTreeGeneration, then the OctTree is generated when first HitTest method is called on the mesh.
                // Default value is false.

                string hitTestingOptionsInfo = string.Format("DXScene.DXHitTestOptions:\r\nMeshPositionsCountForOctTreeGeneration: {0}\r\nGenerateOctTreeOnMeshInitialization: {1}\r\n\r\n",
                    MainDXViewportView.DXScene.DXHitTestOptions.MeshPositionsCountForOctTreeGeneration,
                    MainDXViewportView.DXScene.DXHitTestOptions.GenerateOctTreeOnMeshInitialization);

                AddMessage(hitTestingOptionsInfo);
            };

            MainDXViewportView.SceneRendered += MainDXViewportViewOnSceneRendered;

            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                _disposables.Dispose();

                MainDXViewportView.Dispose();
            };
        }

        private void MainDXViewportViewOnSceneRendered(object sender, EventArgs e)
        {
            // This method should be executed only after the first frame is rendered
            MainDXViewportView.SceneRendered -= MainDXViewportViewOnSceneRendered;

            // Do a hit test of all hit objects
            GetAllHitTestObjects();

            // Now animate the camera so that it will show the hit 3D line
            Camera1.RotateTo(40, -15, 1000);
        }


        private void GetClosestHitObject()
        {
            var dxScene = MainDXViewportView.DXScene;

            if (dxScene == null) // NO DXEngine rendering
                return;


            _hitLinesModelVisual3D.Children.Clear();

            // After the 3D lines are cleared we need to call Update method to update SceneNodes before we call HitTest.
            // If this is not done manually, the SceneNodes would be updated before next rendering but after the HitTest and we could get hit the 3D line.
            MainDXViewportView.Update();


            // DXHitTestOptions.MeshPositionsCountForOctTreeGeneration:
            // Gets or sets an integer value that specifies number of positions in a mesh (DXMeshGeometry3D) at which a OctTree is generated to speed up hit testing
            // (e.g. if mesh has more positions then a value specified with this property, then OctTree will be generated for the mesh).
            // Default value is 512.
            //dxScene.DXHitTestOptions.MeshPositionsCountForOctTreeGeneration = 1;

            //dxScene.DXHitTestOptions.GetOnlyFrontFacingTriangles = false;

            // Hit test center of the MainDXViewportView (we could also use mouse position)
            var positionInViewport3D = new Point(MainDXViewportView.ActualWidth / 2, MainDXViewportView.ActualHeight / 2);
            var pickRay = dxScene.GetRayFromCamera((int)positionInViewport3D.X, (int)positionInViewport3D.Y);

            // It is also possible to specify a hit test filter callback to skip testing some SceneNodes
            //dxScene.DXHitTestOptions.HitTestFilterCallback = delegate(SceneNode sceneNode)
            //{
            //    if (sceneNode == ...)
            //        return DXHitTestOptions.HitTestFilterResult.ContinueSkipSelfAndChildren;

            //    return DXHitTestOptions.HitTestFilterResult.Continue;
            //};

            var dxRayHitTestResult = dxScene.GetClosestHitObject(pickRay);


            if (dxRayHitTestResult == null)
            {
                AddMessage("DXScene hit test result: no object hit");
            }
            else
            {
                string message = string.Format("Ray.Start: {0:0.0}; Ray.Direction: {1:0.00}\r\nDXScene hit test result:\r\n  PointHit: {2:0.0};   (distance: {3:0})\r\n  SceneNode Id: {4}",
                        pickRay.Position,
                        pickRay.Direction,
                        dxRayHitTestResult.HitPosition,
                        dxRayHitTestResult.DistanceToRayOrigin,
                        dxRayHitTestResult.HitSceneNode.Id);

                if (!string.IsNullOrEmpty(dxRayHitTestResult.HitSceneNode.Name))
                    message += " ('" + dxRayHitTestResult.HitSceneNode.Name + "')";

                AddMessage(message);

                AddHitLineArrow(pickRay, dxRayHitTestResult, dxRayHitTestResult.HitPosition.ToWpfPoint3D());
            }


            // NOTE:
            // If you want to manually do a hit testing on a mesh data with vertex and index buffer,
            // then you can use the Ab3d.DirectX.HitTester.HitTest method.
            // The method takes a Ray, various types of vertex buffers, index buffer and a few flags and returns a hit test result.
        }

        private void GetAllHitTestObjects()
        {
            var dxScene = MainDXViewportView.DXScene;

            if (dxScene == null) // NO DXEngine rendering
                return;



            _hitLinesModelVisual3D.Children.Clear();

            // After the 3D lines are cleared we need to call Update method to update SceneNodes before we call HitTest.
            // If this is not done manually, the SceneNodes would be updated before next rendering but after the HitTest and we could get hit the 3D line.
            MainDXViewportView.Update();


            // DXHitTestOptions.MeshPositionsCountForOctTreeGeneration:
            // Gets or sets an integer value that specifies number of positions in a mesh (DXMeshGeometry3D) at which a OctTree is generated to speed up hit testing
            // (e.g. if mesh has more positions then a value specified with this property, then OctTree will be generated for the mesh).
            // Default value is 512.
            //dxScene.DXHitTestOptions.MeshPositionsCountForOctTreeGeneration = 1;

            // When GetOnlyFrontFacingTriangles is true, then only triangles that are facing the camera will be hit
            dxScene.DXHitTestOptions.GetOnlyFrontFacingTriangles = OnlyFrontTrianglesCheckBox.IsChecked ?? false;

            // By default multiple hit results can be returned per one SceneNode.
            // This can be changed by setting GetOnlyOneHitPerSceneNode to true.
            //dxScene.DXHitTestOptions.GetOnlyOneHitPerSceneNode = false;

            // It is also possible to specify a hit test filter callback to skip testing some SceneNodes
            //dxScene.DXHitTestOptions.HitTestFilterCallback = delegate(SceneNode sceneNode)
            //{
            //    if (sceneNode == ...)
            //        return DXHitTestOptions.HitTestFilterResult.ContinueSkipSelfAndChildren;

            //    return DXHitTestOptions.HitTestFilterResult.Continue;
            //};

            // Hit test center of the MainDXViewportView (we could also use mouse position)
            var positionInViewport3D = new Point(MainDXViewportView.ActualWidth / 2, MainDXViewportView.ActualHeight / 2);
            var pickRay = dxScene.GetRayFromCamera((int)positionInViewport3D.X, (int)positionInViewport3D.Y);


            List<DXRayHitTestResult> allHitTests = dxScene.GetAllHitObjects(pickRay);

            if (allHitTests.Count == 0)
            {
                AddMessage("No object hit\r\n");
            }
            else
            {
                var sb = new StringBuilder();
                sb.AppendFormat("Ray.Start: {0:0.0};\r\nRay.Direction: {1:0.00}\r\n{2} hit results:\r\n",
                    pickRay.Position, pickRay.Direction, allHitTests.Count);

                for (var i = 0; i < allHitTests.Count; i++)
                {
                    var hitTest = allHitTests[i];
                    sb.AppendFormat("{0}. PointHit: {1:0.0};  Dist: {2:0};   SceneNode Id: {3}",
                        i + 1,
                        hitTest.HitPosition,
                        hitTest.DistanceToRayOrigin,
                        hitTest.HitSceneNode.Id);

                    if (!string.IsNullOrEmpty(hitTest.HitSceneNode.Name))
                        sb.AppendFormat(" ('{0}')", hitTest.HitSceneNode.Name);

                    var dxRayHitTestWpfModel3DResult = hitTest as DXRayHitTestWpfModel3DResult;
                    if (dxRayHitTestWpfModel3DResult != null && dxRayHitTestWpfModel3DResult.HitGeometryModel3D != null)
                    {
                        var name = dxRayHitTestWpfModel3DResult.HitGeometryModel3D.GetName();

                        if (!string.IsNullOrEmpty(name))
                        {
                            sb.Append("  GeometryModel3D.Name: ").Append(name);
                        }
                    }


                    sb.AppendLine();

                    //if (_showHitArrows)
                    //{
                    var wireCrossVisual3D = new Ab3d.Visuals.WireCrossVisual3D()
                    {
                        Position = hitTest.HitPosition.ToWpfPoint3D(),
                        LineColor = Colors.Red,
                        LineThickness = 1,
                        LinesLength = 10
                    };

                    _hitLinesModelVisual3D.Children.Add(wireCrossVisual3D);
                    //}
                }

                sb.AppendLine();

                // Count how many meshes have OctTree objects generated
                sb.AppendFormat("OctTrees count: {0}\r\n", CountOctTrees(MainDXViewportView.DXScene.RootNode));

                AddMessage(sb.ToString());
            }

            var lineVisual3D = new Ab3d.Visuals.LineVisual3D()
            {
                StartPosition = pickRay.Position.ToWpfPoint3D(),
                EndPosition = pickRay.Position.ToWpfPoint3D() + pickRay.Direction.ToWpfVector3D() * dxScene.Camera.FarPlaneDistance,
                LineColor = Colors.Green,
                LineThickness = 1,
                EndLineCap = LineCap.ArrowAnchor
            };

            _hitLinesModelVisual3D.Children.Add(lineVisual3D);
        }



        private void CreateTestModels()
        {
            _rootModelVisual3D = new ModelVisual3D();
            MainViewport3D.Children.Add(_rootModelVisual3D);



            // SphereVisual3D
            _sphereVisual3D = new Ab3d.Visuals.SphereVisual3D()
            {
                CenterPosition = new Point3D(-50, 0, -50),
                Radius         = 30,
                Material       = new DiffuseMaterial(Brushes.Silver)
            };

            _sphereVisual3D.SetName("SphereVisual3D");

            _rootModelVisual3D.Children.Add(_sphereVisual3D);


            var readerObj = new ReaderObj();
            var teapotModel = readerObj.ReadModel3D(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\Models\teapot-hires.obj"));
            
            Ab3d.Utilities.ModelUtils.CenterAndScaleModel3D(teapotModel, centerPosition: new Point3D(50, 0, -50), finalSize: new Size3D(80, 80, 80), preserveAspectRatio: true);

            _grayTeapotModelVisual3D = new ModelVisual3D()
            {
                Content = teapotModel
            };

            teapotModel.SetName("teapot Model3D");
            _grayTeapotModelVisual3D.SetName("teapot ModelVisual3D");

            _rootModelVisual3D.Children.Add(_grayTeapotModelVisual3D);


            // InstancedMeshGeometryVisual3D
            var boxMesh3D = new Ab3d.Meshes.BoxMesh3D(new Point3D(0, 0, 0), new Size3D(6, 6, 6), 1, 1, 1);

            InstanceData[] instancedData = DXEnginePerformance.InstancedMeshGeometry3DTest.CreateInstancesData(center: new Point3D(-50, 0, 50), 
                                                                                                               size: new Size3D(80, 50, 80), 
                                                                                                               modelScaleFactor: 1, 
                                                                                                               xCount: 5, 
                                                                                                               yCount: 1, 
                                                                                                               zCount: 5, 
                                                                                                               useTransparency: false);

            _instancedMeshGeometryVisual3D = new InstancedMeshGeometryVisual3D(boxMesh3D.Geometry);
            _instancedMeshGeometryVisual3D.InstancesData = instancedData;

            _instancedMeshGeometryVisual3D.SetName("InstancedMeshGeometryVisual3D");
            _rootModelVisual3D.Children.Add(_instancedMeshGeometryVisual3D);



            // MeshObjectNode and SceneNodeVisual3D
            var meshGeometry3D = new Ab3d.Meshes.PyramidMesh3D(new Point3D(50, -20, 50), new Size3D(80, 40, 80)).Geometry;
            var dxMeshGeometry3D = new Ab3d.DirectX.Models.DXMeshGeometry3D(meshGeometry3D);

            // We could manually generate OctTree here (but we wait until OctTree is generated when needed in hit-testing)
            //dxMeshGeometry3D.OctTree = dxMeshGeometry3D.CreateOctTree(maxNodeLevel: 4, expandChildBoundingBoxes: 0.2f);

            var standardMaterial = new StandardMaterial()
            {
                DiffuseColor = Colors.Gold.ToColor3()
            };

            _pyramidMeshObjectNode = new Ab3d.DirectX.MeshObjectNode(dxMeshGeometry3D, standardMaterial);

            _disposables.Add(dxMeshGeometry3D);
            _disposables.Add(_pyramidMeshObjectNode);

            var sceneNodeVisual3D = new SceneNodeVisual3D(_pyramidMeshObjectNode);
            sceneNodeVisual3D.SetName("SceneNodeVisual3D");
            _rootModelVisual3D.Children.Add(sceneNodeVisual3D);


            var geometryTeapotModel3D = teapotModel as GeometryModel3D;
            if (geometryTeapotModel3D != null)
            {
                // Use teapot mesh to generate DXEngine's GeometryMesh and SimpleMesh<PositionNormalTexture>.
                // First copy mesh data into arrays that can be used to generate GeometryMesh and SimpleMesh.
                var teapotMeshGeometry3D = (MeshGeometry3D)geometryTeapotModel3D.Geometry;

                var wpfPositions = teapotMeshGeometry3D.Positions;
                var wpfNormals = teapotMeshGeometry3D.Normals;
                var wpfTextureCoordinates = teapotMeshGeometry3D.TextureCoordinates;

                var positionsCount = wpfPositions.Count;

                if (wpfNormals == null || wpfNormals.Count == 0)
                    wpfNormals = MeshUtils.CalculateNormals(teapotMeshGeometry3D);
                

                var dxPositions          = new Vector3[positionsCount];
                var dxNormals            = new Vector3[positionsCount];
                var dxTextureCoordinates = new Vector2[positionsCount];
                var dxVertexBufferArray  = new PositionNormalTexture[positionsCount];

                for (int i = 0; i < positionsCount; i++)
                {
                    var position          = wpfPositions[i].ToVector3();
                    var normal            = wpfNormals[i].ToVector3();
                    var textureCoordinate = wpfTextureCoordinates[i].ToVector2();

                    dxPositions[i]          = position;
                    dxNormals[i]            = normal;
                    dxTextureCoordinates[i] = textureCoordinate;

                    dxVertexBufferArray[i] = new PositionNormalTexture(position, normal, textureCoordinate);
                }

                var wpfTriangleIndices = teapotMeshGeometry3D.TriangleIndices;
                var triangleIndicesCount = wpfTriangleIndices.Count;

                var dxTriangleIndices = new int[triangleIndicesCount];
                for (int i = 0; i < triangleIndicesCount; i++)
                    dxTriangleIndices[i] = wpfTriangleIndices[i];


                // Add MeshObjectNode with GeometryMesh
                var teapotGeometryMesh = new GeometryMesh(dxPositions, dxNormals, dxTextureCoordinates, dxTriangleIndices, "TeapotGeometryMesh");
                _disposables.Add(teapotGeometryMesh);

                // We could manually generate OctTree here (but we wait until OctTree is generated when needed in hit-testing)
                //teapotGeometryMesh.OctTree = teapotGeometryMesh.CreateOctTree(maxNodeLevel: 4, expandChildBoundingBoxes: 0.2f);

                _blueTeapotMeshObjectNode = new Ab3d.DirectX.MeshObjectNode(teapotGeometryMesh, new StandardMaterial(Colors.LightBlue.ToColor3()));
                _blueTeapotMeshObjectNode.Transform = new Transformation(Matrix.Translation(150, -30, 60));
                _disposables.Add(_blueTeapotMeshObjectNode);

                var teapot1SceneNodeVisual3D = new SceneNodeVisual3D(_blueTeapotMeshObjectNode);
                teapot1SceneNodeVisual3D.SetName("Teapot1SceneNodeVisual3D");
                _rootModelVisual3D.Children.Add(teapot1SceneNodeVisual3D);


                // Add MeshObjectNode with SimpleMesh<PositionNormalTexture>
                var teapotSimpleMesh = new SimpleMesh<PositionNormalTexture>(dxVertexBufferArray, 
                                                                             dxTriangleIndices,
                                                                             inputLayoutType: InputLayoutType.Position | InputLayoutType.Normal | InputLayoutType.TextureCoordinate,
                                                                             name: "TeapotSimpleMesh");
                _disposables.Add(teapotSimpleMesh);

                // We could manually generate OctTree here (but we wait until OctTree is generated when needed in hit-testing)
                //teapotSimpleMesh.OctTree = teapotSimpleMesh.CreateOctTree(maxNodeLevel: 4, expandChildBoundingBoxes: 0.2f);

                _greenTeapotMeshObjectNode = new Ab3d.DirectX.MeshObjectNode(teapotSimpleMesh, new StandardMaterial(Colors.LightGreen.ToColor3()));
                _greenTeapotMeshObjectNode.Transform = new Transformation(Matrix.Translation(150, -30, -30));
                _disposables.Add(_greenTeapotMeshObjectNode);

                var teapot2SceneNodeVisual3D = new SceneNodeVisual3D(_greenTeapotMeshObjectNode);
                teapot1SceneNodeVisual3D.SetName("Teapot2SceneNodeVisual3D");
                _rootModelVisual3D.Children.Add(teapot2SceneNodeVisual3D);
            }
        }

        private void AddMessage(string message)
        {
            ResultTextBox.Text = message + Environment.NewLine + ResultTextBox.Text;
        }

        private void AddHitLineArrow(Ray ray, DXRayHitTestResult hitResult, Point3D? referenceHitPosition = null, Point3D? rayEndPosition = null)
        {
            Ab3d.Visuals.LineVisual3D lineVisual3D;
            Point3D                   endPosition;

            if (hitResult != null)
                endPosition = hitResult.HitPosition.ToWpfPoint3D();
            else
                endPosition = rayEndPosition ?? Camera1.TargetPosition + Camera1.Offset;

            if (hitResult != null)
            {
                lineVisual3D = new Ab3d.Visuals.LineVisual3D()
                {
                    StartPosition = ray.Position.ToWpfPoint3D(),
                    EndPosition   = endPosition,
                    LineColor     = Colors.Green,
                    LineThickness = 1,
                    EndLineCap    = LineCap.ArrowAnchor
                };


                if (referenceHitPosition != null)
                {
                    var wireCrossVisual3D = new Ab3d.Visuals.WireCrossVisual3D()
                    {
                        Position      = referenceHitPosition.Value,
                        LineColor     = Colors.Red,
                        LineThickness = 1,
                        LinesLength   = 5
                    };

                    _hitLinesModelVisual3D.Children.Add(wireCrossVisual3D);
                }
            }
            else
            {
                lineVisual3D = new Ab3d.Visuals.LineVisual3D()
                {
                    StartPosition = ray.Position.ToWpfPoint3D(),
                    EndPosition   = endPosition,
                    LineColor     = Colors.Green,
                    LineThickness = 1
                };
            }

            _hitLinesModelVisual3D.Children.Add(lineVisual3D);
        }


        private void GetClosestObjectButton_OnClick(object sender, RoutedEventArgs e)
        {
            GetClosestHitObject();
        }

        private void GetAllHitObjectsButton_OnClick(object sender, RoutedEventArgs e)
        {
            GetAllHitTestObjects();
        }

        private void OnSphereIsHitTestVisibleCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            bool isHitTestVisible = SphereIsHitTestVisibleCheckBox.IsChecked ?? false;
            SetIsHitTestVisible(_sphereVisual3D, isHitTestVisible);
        }
        
        private void OnGrayTeapotIsHitTestVisibleCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            bool isHitTestVisible = GrayTeapotIsHitTestVisibleCheckBox.IsChecked ?? false;
            SetIsHitTestVisible(_grayTeapotModelVisual3D, isHitTestVisible);
        }
        
        private void OnPyramidIsHitTestVisibleCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            _pyramidMeshObjectNode.IsHitTestVisible = PyramidIsHitTestVisibleCheckBox.IsChecked ?? false;
        }
        
        private void OnBoxesIsHitTestVisibleCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            bool isHitTestVisible = BoxesIsHitTestVisibleCheckBox.IsChecked ?? false;
            SetIsHitTestVisible(_instancedMeshGeometryVisual3D, isHitTestVisible);
        }

        private void OnBlueTeapotIsHitTestVisibleCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            _blueTeapotMeshObjectNode.IsHitTestVisible = BlueTeapotIsHitTestVisibleCheckBox.IsChecked ?? false;
        }

        private void OnGreenTeapotIsHitTestVisibleCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            _greenTeapotMeshObjectNode.IsHitTestVisible = GreenTeapotIsHitTestVisibleCheckBox.IsChecked ?? false;
        }

        private void SetIsHitTestVisible(ModelVisual3D modelVisual3D, bool isHitTestVisible)
        {
            var sceneNode = MainDXViewportView.GetSceneNodeForWpfObject(modelVisual3D);

            if (sceneNode == null)
                return;

            sceneNode.IsHitTestVisible = isHitTestVisible;
        }

        private void OnGenerateOctTreeCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            if (GenerateOctTreeCheckBox.IsChecked ?? false)
            {
                MainDXViewportView.DXScene.DXHitTestOptions.MeshPositionsCountForOctTreeGeneration = 512; // default value

                // We could manually generate all OctTrees by the code below.
                // But this is not needed because OcTree will be automatically generated when object will be hit tested (when ray will cross the object's bounding box)
                //int count = 0;
                //GenerateOctTree(MainDXViewportView.DXScene.RootNode, ref count);
            }
            else
            {
                MainDXViewportView.DXScene.DXHitTestOptions.MeshPositionsCountForOctTreeGeneration = int.MaxValue; // never generate OctTree

                int count = 0;
                ClearOctTree(MainDXViewportView.DXScene.RootNode, ref count);

                AddMessage(string.Format("Removed {0} OctTree objects\r\n", count));
            }
        }

        private void ClearOctTree(SceneNode sceneNode, ref int count)
        {
            var meshObjectNode = sceneNode as MeshObjectNode;
            if (meshObjectNode != null)
            {
                var octTreeMesh = meshObjectNode.Mesh as IOctTreeMesh;
                if (octTreeMesh != null && octTreeMesh.OctTree != null)
                {
                    octTreeMesh.OctTree = null;
                    count++;
                }
            }
            else
            {
                var wpfGeometryModel3DNode = sceneNode as WpfGeometryModel3DNode;
                if (wpfGeometryModel3DNode != null && wpfGeometryModel3DNode.DXMesh != null && wpfGeometryModel3DNode.DXMesh.OctTree != null)
                {
                    wpfGeometryModel3DNode.DXMesh.OctTree = null;
                    count++;
                }
            }

            foreach (var childNode in sceneNode.ChildNodes)
                ClearOctTree(childNode, ref count);
        }
        
        private void GenerateOctTree(SceneNode sceneNode, ref int count)
        {
            var meshObjectNode = sceneNode as MeshObjectNode;
            if (meshObjectNode != null)
            {
                var octTreeMesh = meshObjectNode.Mesh as IOctTreeMesh;
                if (octTreeMesh != null && octTreeMesh.OctTree == null)
                {
                    octTreeMesh.OctTree = octTreeMesh.CreateOctTree(MainDXViewportView.DXScene.DXHitTestOptions.OctTreeMaxNodeLevel, MainDXViewportView.DXScene.DXHitTestOptions.OctTreeExpandChildBoundingBoxes);
                    count++;
                }
            }
            else
            {
                var wpfGeometryModel3DNode = sceneNode as WpfGeometryModel3DNode;
                if (wpfGeometryModel3DNode != null && wpfGeometryModel3DNode.DXMesh != null && wpfGeometryModel3DNode.DXMesh.OctTree == null)
                {
                    wpfGeometryModel3DNode.DXMesh.OctTree = wpfGeometryModel3DNode.DXMesh.CreateOctTree(MainDXViewportView.DXScene.DXHitTestOptions.OctTreeMaxNodeLevel, MainDXViewportView.DXScene.DXHitTestOptions.OctTreeExpandChildBoundingBoxes);
                    count++;
                }
            }

            foreach (var childNode in sceneNode.ChildNodes)
                GenerateOctTree(childNode, ref count);
        }
        
        private int CountOctTrees(SceneNode sceneNode)
        {
            int count = 0;

            var meshObjectNode = sceneNode as MeshObjectNode;
            if (meshObjectNode != null)
            {
                var octTreeMesh = meshObjectNode.Mesh as IOctTreeMesh;
                if (octTreeMesh != null && octTreeMesh.OctTree != null)
                    count = 1;
            }
            else
            {
                var wpfGeometryModel3DNode = sceneNode as WpfGeometryModel3DNode;
                if (wpfGeometryModel3DNode != null && wpfGeometryModel3DNode.DXMesh != null && wpfGeometryModel3DNode.DXMesh.OctTree != null)
                    count = 1;
            }

            foreach (var childNode in sceneNode.ChildNodes)
                count += CountOctTrees(childNode);

            return count;
        }
    }
}
