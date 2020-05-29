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

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineHitTesting
{
    /// <summary>
    /// Interaction logic for DXEventManager3DSample.xaml
    /// </summary>
    public partial class DXEventManager3DSample : Page
    {
        private DXEventManager3D _dxEventManager3D;

        private Visual3D _selectedVisual3D;
        private GeometryModel3D _selectedGeometryModel3D;

        private Point _lastMouseDownPosition;

        private InstancedMeshGeometryVisual3D _instancedMeshGeometryVisual3D;
        private int _selectedInstanceIndex;

        private WireCrossVisual3D _wireCrossVisual3D;

        public DXEventManager3DSample()
        {
            InitializeComponent();


            Box1Visual3D.SetName("Box1Visual3D");
            BoxesGroupVisual3D.SetName("BoxesGroupVisual3D");
            Box2Visual3D.SetName("Box2Visual3D");
            Box3Visual3D.SetName("Box3Visual3D");
            Box4Visual3D.SetName("Box4Visual3D");
            Line1.SetName("Line1");
            Line2.SetName("Line2");
            Rectangle1.SetName("Rectangle1");


            var meshGeometry3D = new Ab3d.Meshes.SphereMesh3D(centerPosition: new Point3D(0, 0, 0), radius: 5, segments: 20, generateTextureCoordinates: false).Geometry;

            // The following method prepare InstanceData array with data for each instance (WorldMatrix and Color)
            InstanceData[] instancedData = DXEnginePerformance.InstancedMeshGeometry3DTest.CreateInstancesData(center: new Point3D(0, 0, 0),
                                                                                                               size: new Size3D(200, 50, 50),
                                                                                                               modelScaleFactor: 1,
                                                                                                               xCount: 10,
                                                                                                               yCount: 3,
                                                                                                               zCount: 3,
                                                                                                               useTransparency: false);


            // Create InstancedGeometryVisual3D with selected meshGeometry and InstancesData
            _instancedMeshGeometryVisual3D = new InstancedMeshGeometryVisual3D(meshGeometry3D);
            _instancedMeshGeometryVisual3D.InstancesData = instancedData;
            _instancedMeshGeometryVisual3D.SetName("InstancedMeshGeometryVisual3D");
            _instancedMeshGeometryVisual3D.Transform = new TranslateTransform3D(0, 50, 0);

            MainViewport3D.Children.Add(_instancedMeshGeometryVisual3D);

            _selectedInstanceIndex = -1;


            var model3DGroup = new Model3DGroup();
            model3DGroup.SetName("Model3DGroup");

            var frozenModel3DGroup = new Model3DGroup();
            frozenModel3DGroup.SetName("FrozenModel3DGroup");

            var box1Material = new DiffuseMaterial(Brushes.LightPink);
            var box2Material = new DiffuseMaterial(Brushes.LightCyan);

            var boxModel3D = Ab3d.Models.Model3DFactory.CreateBox(new Point3D(-100, 2, 25), new Size3D(60, 4, 50), box2Material);
            frozenModel3DGroup.Children.Add(boxModel3D);

            var pyramidModel3D = Ab3d.Models.Model3DFactory.CreatePyramid(new Point3D(-100, 2, 20), new Size3D(20, 20, 20), box2Material);
            frozenModel3DGroup.Children.Add(pyramidModel3D);

            for (int i = 0; i < 5; i++)
            {
                var geometryModel3D = new GeometryModel3D(meshGeometry3D, box1Material);
                geometryModel3D.Transform = new TranslateTransform3D(-130 + i * 15, 5, 80);
                geometryModel3D.SetName("GroupedSphere_" + i.ToString());

                model3DGroup.Children.Add(geometryModel3D);

                geometryModel3D = new GeometryModel3D(meshGeometry3D, box2Material);
                geometryModel3D.Transform = new TranslateTransform3D(-130 + i * 15, 10, 50);
                geometryModel3D.SetName("FrozenGroupedSphere_" + i.ToString());

                frozenModel3DGroup.Children.Add(geometryModel3D);
            }

            
            frozenModel3DGroup.Freeze();

            MainViewport3D.Children.Add(model3DGroup.CreateModelVisual3D());
            MainViewport3D.Children.Add(frozenModel3DGroup.CreateModelVisual3D());

            _dxEventManager3D = new DXEventManager3D(MainDXViewportView);

            RegisterEventSource(Box1Visual3D);
            RegisterEventSource(BoxesGroupVisual3D);
            RegisterEventSource(Line1);
            RegisterEventSource(Line2);
            RegisterEventSource(Rectangle1);
            RegisterEventSource(_instancedMeshGeometryVisual3D);

            RegisterEventSource(model3DGroup);
            RegisterEventSource(frozenModel3DGroup);


            // Prevent TransparentPlaneVisual3D to be used by hit-testing
            _dxEventManager3D.RegisterExcludedVisual3D(TransparentPlaneVisual3D);


            this.PreviewMouseMove += delegate(object sender, MouseEventArgs args)
            {
                var position = args.GetPosition(this);
                MousePositionTextBlock.Text = $"Mouse pos: {position.X:0} {position.Y:0}";
            };

            this.PreviewMouseDown += delegate(object sender, MouseButtonEventArgs e)
            {
                var position = e.GetPosition(this);
                if (position == _lastMouseDownPosition)
                    return;

                System.Diagnostics.Debug.WriteLine("Mouse position: " + position.ToString());

                _lastMouseDownPosition = position;
            };


            // IMPORTANT:
            // It is very important to call Dispose method on DXSceneView after the control is not used any more (see help file for more info)
            this.Unloaded += (sender, args) => MainDXViewportView.Dispose();
        }

        private void RegisterEventSource(Visual3D visual3D)
        {
            var eventSource3D = new Ab3d.DirectX.Utilities.VisualEventSource3D();
            eventSource3D.TargetVisual3D = visual3D;
            eventSource3D.MouseEnter += eventSource3D_MouseEnter;
            eventSource3D.MouseLeave += eventSource3D_MouseLeave;
            eventSource3D.MouseMove += eventSource3D_MouseMove;
            eventSource3D.MouseClick += movableEventSource3D_MouseClick;

            _dxEventManager3D.RegisterEventSource3D(eventSource3D);
        }

        private void RegisterEventSource(Model3D model3D)
        {
            var eventSource3D = new Ab3d.DirectX.Utilities.ModelEventSource3D();
            eventSource3D.TargetModel3D = model3D;
            eventSource3D.MouseEnter += eventSource3D_MouseEnter;
            eventSource3D.MouseLeave += eventSource3D_MouseLeave;
            eventSource3D.MouseMove += eventSource3D_MouseMove;
            eventSource3D.MouseClick += movableEventSource3D_MouseClick;

            _dxEventManager3D.RegisterEventSource3D(eventSource3D);
        }

        private void ChangeColor(Ab3d.DirectX.Common.EventManager3D.Mouse3DEventArgs e, System.Windows.Media.Color color)
        {
            var wpfGeometryModel3DNode = e.RayHitResult.HitSceneNode as WpfGeometryModel3DNode;
            if (wpfGeometryModel3DNode != null)
            {
                wpfGeometryModel3DNode.GeometryModel3D.Material = new DiffuseMaterial(new SolidColorBrush(color));
            }
            else
            {
                var hitVisual3D = e.RayHitResult.HitSceneNode.GetVisual3D();
                ChangeColor(hitVisual3D, color);
            }
        }

        private void ChangeInstanceColor(int instanceIndex, System.Windows.Media.Color color)
        {
            if (_selectedInstanceIndex == -1)
                return;

            _instancedMeshGeometryVisual3D.InstancesData[instanceIndex].DiffuseColor = color.ToColor4();
            _instancedMeshGeometryVisual3D.Update(instanceIndex, 1, updateBounds: false);
        }

        private void ChangeColor(Visual3D visual3D, System.Windows.Media.Color color)
        {
            var baseModelVisual3D = visual3D as BaseModelVisual3D;
            if (baseModelVisual3D != null)
            {
                baseModelVisual3D.Material = new DiffuseMaterial(new SolidColorBrush(color));
                return;
            }

            var baseLineVisual3D = visual3D as BaseLineVisual3D;
            if (baseLineVisual3D != null)
            {
                baseLineVisual3D.LineColor = color;
                return;
            }

            var modelVisual3D = visual3D as ModelVisual3D;
            if (modelVisual3D != null && modelVisual3D.Children.Count > 0)
            {
                foreach (var childVisual3D in modelVisual3D.Children)
                    ChangeColor(childVisual3D, color);

                return;
            }
        }

        void eventSource3D_MouseEnter(object sender, Ab3d.DirectX.Common.EventManager3D.Mouse3DEventArgs e)
        {
            if (e.RayHitResult != null)
            {
                Log("MouseEnter: " + e.RayHitResult.HitSceneNode);

                // We need to save the hit SceneNode.
                // This way we will be able to change the material back in the eventSource3D_MouseLeave event handler.
                _selectedVisual3D = e.RayHitResult.HitSceneNode.GetVisual3D();

                var wpfGeometryModel3DNode = e.RayHitResult.HitSceneNode as WpfGeometryModel3DNode;
                if (wpfGeometryModel3DNode != null)
                    _selectedGeometryModel3D = wpfGeometryModel3DNode.GeometryModel3D;
                else
                    _selectedGeometryModel3D = null;


                var dxRayInstancedHitTestResult = e.RayHitResult as DXRayInstancedHitTestResult;
                if (dxRayInstancedHitTestResult != null)
                    _selectedInstanceIndex = dxRayInstancedHitTestResult.HitInstanceIndex;


                if (_wireCrossVisual3D == null)
                {
                    _wireCrossVisual3D = new WireCrossVisual3D()
                    {
                        LineColor = Colors.DeepPink,
                        LineThickness = 2,
                        LinesLength = 10
                    };
                }

                _wireCrossVisual3D.Position = e.RayHitResult.HitPosition.ToWpfPoint3D();

                if (!MainViewport3D.Children.Contains(_wireCrossVisual3D))
                    MainViewport3D.Children.Add(_wireCrossVisual3D);
            }
            else
            {
                _selectedVisual3D = null;
                _selectedGeometryModel3D = null;
                _selectedInstanceIndex = -1;

                Log("MouseEnter: <null>");
                return;
            }

            if (_selectedInstanceIndex != -1)
                ChangeInstanceColor(_selectedInstanceIndex, Colors.Red);
            else
                ChangeColor(e, Colors.Red);
        }

        private void eventSource3D_MouseMove(object sender, Mouse3DEventArgs e)
        {
            if (e.RayHitResult == null)
                HitPositionTextBlock.Text = null;
            else
                HitPositionTextBlock.Text = string.Format("Hit pos: {0:0}", e.RayHitResult.HitPosition);
        }

        void eventSource3D_MouseLeave(object sender, Ab3d.DirectX.Common.EventManager3D.Mouse3DEventArgs e)
        {
            Log("MouseLeave: " + (e.RayHitResult != null ? e.RayHitResult.HitSceneNode.ToString() : "<null>"));

            // Note that the e.RayHitResult.HitSceneNode is set to the new hit object (object that is hit when we left the previously entered object)
            // Therefore we cannot use this to change the color.

            if (_selectedInstanceIndex != -1)
                ChangeInstanceColor(_selectedInstanceIndex, Colors.Yellow);
            else if (_selectedGeometryModel3D != null)
                _selectedGeometryModel3D.Material = new DiffuseMaterial(new SolidColorBrush(Colors.Yellow));
            else if (_selectedVisual3D != null)
                ChangeColor(_selectedVisual3D, Colors.Yellow);
            

            _selectedVisual3D = null;
            _selectedInstanceIndex = -1;

            if (_wireCrossVisual3D != null)
                MainViewport3D.Children.Remove(_wireCrossVisual3D);
        }

        void movableEventSource3D_MouseClick(object sender, Ab3d.DirectX.Common.EventManager3D.MouseButton3DEventArgs e)
        {
            MessageBox.Show("Clicked on " + e.HitEventSource3D.Name ?? "");
        }

        private void Log(string message)
        {
            System.Diagnostics.Debug.WriteLine(message);
        }
    }
}
