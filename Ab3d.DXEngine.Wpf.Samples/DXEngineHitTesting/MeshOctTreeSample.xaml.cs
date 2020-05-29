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
using Ab3d.DirectX.Models;
using Ab3d.DirectX.Utilities;
using Ab3d.Utilities;
using Ab3d.Visuals;
using SharpDX;
using Material = System.Windows.Media.Media3D.Material;
using Point = System.Windows.Point;

// OctTree is a data structure that organizes the triangles in 3D space into multiple levels of
// OctTreeNode objects so that the search of a triangle or a check for triangle ray intersection is very efficient.
// Each OctTreeNodes divide its space into 8 child OctTreeNodes.
// See also: https://en.wikipedia.org/wiki/Octree
//
// OctTrees are used in DXEngine for very efficient hit testing of complex MeshGeometry3D objects.
//
// OctTree generation for hit testing (when calling DXScene.GetClosestHitObject or DXScene.GetAllHitObjects methods) 
// is controlled by DXScene.DXHitTestOptions.MeshPositionsCountForOctTreeGeneration property.
// This property gets or sets an integer value that specifies number of positions in a mesh (DXMeshGeometry3D)
// at which a OctTree is generated to speed up hit testing (e.g. if mesh has more positions then a value specified with this property,
// then OctTree will be generated for the mesh). Default value is 512.
//
// This sample shows how to manually create OctTree (this can be also done in background thread).

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineHitTesting
{
    /// <summary>
    /// Interaction logic for MeshOctTreeSample.xaml
    /// </summary>
    public partial class MeshOctTreeSample : Page
    {
        private const int MaxNodeLevels = 4; // This should be determined by the number of triangles (more triangles bigger max level)

        private ModelVisual3D _octTreeLinesModelVisual3D;
        private ModelVisual3D _hitLinesModelVisual3D;

        private DXMeshGeometry3D _dxMesh;

        private DirectX.MeshOctTree _meshOctTree;

        private GeometryModel3D _readModel3D;

        public MeshOctTreeSample()
        {
            InitializeComponent();

            _octTreeLinesModelVisual3D = new ModelVisual3D();
            MainViewport3D.Children.Add(_octTreeLinesModelVisual3D);

            _hitLinesModelVisual3D = new ModelVisual3D();
            MainViewport3D.Children.Add(_hitLinesModelVisual3D);

            CreateScene();

            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                if (_dxMesh != null)
                {
                    _dxMesh.Dispose();
                    _dxMesh = null;
                }

                MainDXViewportView.Dispose();
            };
        }

        private void CreateScene()
        {
            var readerObj   = new ReaderObj();
            _readModel3D = readerObj.ReadModel3D(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\Models\teapot-hires.obj")) as GeometryModel3D;

            if (_readModel3D != null)
            {
                var meshGeometry3D = (MeshGeometry3D)_readModel3D.Geometry;

                _readModel3D.Material = new DiffuseMaterial(new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 200, 200, 200)));
                _readModel3D.BackMaterial = _readModel3D.Material;

                Ab3d.Utilities.ModelUtils.CenterAndScaleModel3D(_readModel3D, new Point3D(0, 0, 0), new Size3D(100, 100, 100), preserveAspectRatio: true);

                meshGeometry3D = Ab3d.Utilities.MeshUtils.TransformMeshGeometry3D(meshGeometry3D, _readModel3D.Transform);

                var modelVisual3D = _readModel3D.CreateModelVisual3D();
                MainViewport3D.Children.Add(modelVisual3D);

                MainDXViewportView.DXSceneInitialized += delegate (object sender, EventArgs args)
                {
                    if (MainDXViewportView.DXScene == null)
                        return; // WPF 3D rendering
                    
                    _dxMesh = new DXMeshGeometry3D(meshGeometry3D);
                    _dxMesh.InitializeResources(MainDXViewportView.DXScene.DXDevice);

                    RecreateOctTree();
                };
            }

            _hitLinesModelVisual3D = new ModelVisual3D();
            MainViewport3D.Children.Add(_hitLinesModelVisual3D);

            Camera1.Distance = 200;
        }

        private void RecreateOctTree()
        {
            if (_dxMesh == null)
                return;

            float expandChildBoundingBoxes = (ExpandBoundingBoxesCheckBox.IsChecked ?? false) ? 0.2f : 0f;

            _meshOctTree = _dxMesh.CreateOctTree(MaxNodeLevels, expandChildBoundingBoxes);

            ShowBoundingBoxes();

            var nodeStatistics = _meshOctTree.GetNodeStatistics();
            AddMessage("MeshOctTree nodes statistics:\r\n" + nodeStatistics);
        }

        private void ShowBoundingBoxes()
        {
            _octTreeLinesModelVisual3D.Children.Clear();

            var colors = new System.Windows.Media.Color[] { Colors.Red, Colors.Green, Colors.Blue, Colors.Black };

            bool showActualBoundingBox = ActualBoundingBoxCheckBox.IsChecked ?? false;

            int startNodeLevel = 2;
            for (int i = startNodeLevel; i <= MaxNodeLevels; i++)
            {
                var boundingBoxs = _meshOctTree.CollectBoundingBoxesInLevel(i, showActualBoundingBox);

                foreach (var boundingBox in boundingBoxs)
                {
                    var wireBoxVisual3D = new Ab3d.Visuals.WireBoxVisual3D()
                    {
                        CenterPosition = boundingBox.ToRect3D().GetCenterPosition(),
                        Size = boundingBox.ToRect3D().Size,
                        LineColor = colors[(i - startNodeLevel) % (colors.Length)],
                        LineThickness = 2
                    };

                    _octTreeLinesModelVisual3D.Children.Add(wireBoxVisual3D);
                }

            }
        }

        private void AddMessage(string message)
        {
            ResultTextBox.Text = message + Environment.NewLine + ResultTextBox.Text;
        }


        private void HitTestButton_OnClick(object sender, RoutedEventArgs e)
        {
            var dxScene = MainDXViewportView.DXScene;
            if (dxScene == null)
                return;

            // Hit test center of the MainDXViewportView (we could also use mouse position)
            var positionInViewport3D = new Point(MainDXViewportView.ActualWidth / 2, MainDXViewportView.ActualHeight / 2);
            var pickRay              = dxScene.GetRayFromCamera((int)positionInViewport3D.X, (int)positionInViewport3D.Y);

            var hitTestContext = new DXHitTestContext(dxScene);
            var octTreeHitResult = _meshOctTree.HitTest(ref pickRay, hitTestContext);


            _hitLinesModelVisual3D.Children.Clear();

            if (octTreeHitResult == null)
            {
                AddMessage("MeshOctTree hit test result: no object hit\r\n");
            }
            else
            {
                AddMessage(string.Format("MeshOctTree hit test result (Ray.Start: {0:0.0}; Ray.Direction: {1:0.00}):\r\n  PointHit: {2:0.0};   (distance: {3:0})\r\n",
                    pickRay.Position,
                    pickRay.Direction,
                    octTreeHitResult.HitPosition,
                    octTreeHitResult.DistanceToRayOrigin));

                var lineVisual3D = new Ab3d.Visuals.LineVisual3D()
                {
                    StartPosition = pickRay.Position.ToWpfPoint3D(),
                    EndPosition   = octTreeHitResult.HitPosition.ToWpfPoint3D(),
                    LineColor     = Colors.Orange,
                    LineThickness = 1,
                    EndLineCap    = LineCap.ArrowAnchor
                };

                _hitLinesModelVisual3D.Children.Add(lineVisual3D);
            }
        }

        private void OnOctTreeSettingsChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            RecreateOctTree();
        }
    }
}
