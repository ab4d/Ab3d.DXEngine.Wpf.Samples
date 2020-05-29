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
using Material = System.Windows.Media.Media3D.Material;
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
        private ModelVisual3D _teapotModelVisual3D;
        private MeshObjectNode _pyramidMeshObjectNode;
        private InstancedMeshGeometryVisual3D _instancedMeshGeometryVisual3D;

        public HitTestingSample()
        {
            InitializeComponent();

            _disposables = new DisposeList();

            _hitLinesModelVisual3D = new ModelVisual3D();
            MainViewport3D.Children.Add(_hitLinesModelVisual3D);


            CreateTestModels();
            
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
                sb.AppendFormat("Ray.Start: {0:0.0}; Ray.Direction: {1:0.00}\r\n{2} hit results:\r\n",
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
                        LinesLength = 5
                    };

                    _hitLinesModelVisual3D.Children.Add(wireCrossVisual3D);
                    //}
                }

                sb.AppendLine();

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

            _teapotModelVisual3D = new ModelVisual3D()
            {
                Content = teapotModel
            };

            teapotModel.SetName("teapot Model3D");
            _teapotModelVisual3D.SetName("teapot ModelVisual3D");

            _rootModelVisual3D.Children.Add(_teapotModelVisual3D);


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
        
        private void OnTeapotIsHitTestVisibleCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            bool isHitTestVisible = TeapotIsHitTestVisibleCheckBox.IsChecked ?? false;
            SetIsHitTestVisible(_teapotModelVisual3D, isHitTestVisible);
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

        private void SetIsHitTestVisible(ModelVisual3D modelVisual3D, bool isHitTestVisible)
        {
            var sceneNode = MainDXViewportView.GetSceneNodeForWpfObject(modelVisual3D);

            if (sceneNode == null)
                return;

            sceneNode.IsHitTestVisible = isHitTestVisible;
        }
    }
}
