using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
using Ab3d.Common.Models;
using Ab3d.DirectX;
using Ab3d.DirectX.Models;
using Ab3d.DirectX.Utilities;
using Ab3d.Utilities;
using Ab3d.Visuals;
using SharpDX;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Matrix = SharpDX.Matrix;
using Path = System.IO.Path;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineOther
{
    /// <summary>
    /// Interaction logic for ClosestDistanceSample.xaml
    /// </summary>
    public partial class ClosestDistanceSample : Page, ICompositionRenderingSubscriber
    {
        private MeshCollider _meshCollider;

        private BoundingBox _animationBoundingBox;

        private Vector3 _testPosition;

        private DateTime _startTime;
        private DateTime _lastUpdateTime;

        private TranslateTransform3D _secondMeshTranslate;

        private WireCrossVisual3D _wireCrossVisual3D;
        private LineVisual3D _closestLineVisual3D;

        private DXRayHitTestResult _lastClosestPositionResult;

        public ClosestDistanceSample()
        {
            InitializeComponent();

            CreateMainMesh();

            float r = 100;
            _animationBoundingBox = new BoundingBox(new Vector3(-r, -r, -r), new Vector3(r, r, r));

            this.Loaded += delegate (object o, RoutedEventArgs args)
            {
                //CreateSecondMesh();
                EnsureWireCross();

                _startTime = DateTime.Now;

                // Use CompositionRenderingHelper to subscribe to CompositionTarget.Rendering event
                // This is much safer because in case we forget to unsubscribe from Rendering, the CompositionRenderingHelper will unsubscribe us automatically
                // This allows to collect this class will Grabage collector and prevents infinite calling of Rendering handler.
                // After subscribing the ICompositionRenderingSubscriber.OnRendering method will be called on each CompositionTarget.Rendering event
                CompositionRenderingHelper.Instance.Subscribe(this);
            };

            this.Unloaded += (sender, args) => MainDXViewportView.Dispose();
        }

        private void CreateMainMesh()
        {
            // Create a simple torus mesh
            var torusKnotMesh3D = new Ab3d.Meshes.TorusKnotMesh3D(centerPosition: new Point3D(0, 0, 0), p: 1, q: 3, r1: 50, r2: 20, r3: 10, uSegments: 150, vSegments: 10, calculateNormals: true).Geometry;

            // Rotate torus by 90 degrees
            var transform3D = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), 90));
            torusKnotMesh3D = Ab3d.Utilities.MeshUtils.TransformMeshGeometry3D(torusKnotMesh3D, transform3D);

            // Create an instance of MeshCollider from WPF's MeshGeometry3D
            // It can be also created from DXEngine's Mesh
            _meshCollider = new MeshCollider(torusKnotMesh3D);

            // Show the mesh:
            var geometryModel3D = new GeometryModel3D(torusKnotMesh3D, new DiffuseMaterial(Brushes.Silver));

            MainMeshVisual3D.Children.Add(geometryModel3D.CreateContentVisual3D());


            var wireframeLinePositions = Ab3d.Models.WireframeFactory.CreateWireframeLinePositions(torusKnotMesh3D);

            var multiLineVisual3D = new MultiLineVisual3D()
            {
                Positions = wireframeLinePositions,
                LineColor = Colors.Black,
                LineThickness = 0.5,
            };

            multiLineVisual3D.SetDXAttribute(DXAttributeType.LineDepthBias, 0.5f);

            SecondMeshVisual3D.Children.Add(multiLineVisual3D);
        }

        private void CreateSecondMesh()
        {
            _secondMeshTranslate = new TranslateTransform3D();

            var boxVisual3D = new BoxVisual3D()
            {
                CenterPosition = new Point3D(0, 0, 0),
                Size = new Size3D(40, 20, 40),
                Material = new DiffuseMaterial(Brushes.LightBlue),
                Transform = _secondMeshTranslate
            };

            SecondMeshVisual3D.Children.Add(boxVisual3D);
        }

        void ICompositionRenderingSubscriber.OnRendering(EventArgs e)
        {
            var now = DateTime.Now; 

            // Update statistics only 10 times per second
            if ((now - _lastUpdateTime).TotalSeconds > 0.1)
            {
                if (_lastClosestPositionResult == null)
                {
                    DistanceTextBlock.Text = "";
                    PositionIndexTextBlock.Text = "";
                }
                else
                {
                    DistanceTextBlock.Text = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.0}", _lastClosestPositionResult.DistanceToRayOrigin);

                    // IMPORTANT: TriangleIndex is not set to index of triangle but to index of the position in the positions array (or vertex buffer array) 
                    PositionIndexTextBlock.Text = _lastClosestPositionResult.TriangleIndex.ToString();
                }

                _lastUpdateTime = now;
            }


            double elapsedSeconds = (DateTime.Now - _startTime).TotalSeconds;

            float x, y, z;

            x = _animationBoundingBox.Center.X;
            y = _animationBoundingBox.Center.Y;
            z = _animationBoundingBox.Center.Z;

            x += (float)Math.Sin(elapsedSeconds) * _animationBoundingBox.Size.X * 0.5f;
            y += (float)Math.Cos(elapsedSeconds * 0.5) * _animationBoundingBox.Size.Y * 0.5f;
            z += (float)Math.Cos(elapsedSeconds * 0.3) * _animationBoundingBox.Size.Z * 0.5f;
            
            _testPosition = new Vector3(x, y, z);

            if (_secondMeshTranslate != null)
            {
                _secondMeshTranslate.OffsetX = x;
                _secondMeshTranslate.OffsetY = y;
                _secondMeshTranslate.OffsetZ = z;
            }

            EnsureWireCross();
            _wireCrossVisual3D.Position = _testPosition.ToWpfPoint3D();


            // GetClosestPosition returns a DXRayHitTestResult with HitPosition set to the closest position on the mesh,
            // DistanceToRayOrigin is set to the distance from the specified position to the position on the mesh and
            // TriangleIndex is set to the position index in the Positions array.
            // IMPORTANT: TriangleIndex is not set to index of triangle but to index of the position in the positions array (or vertex buffer array)
            _lastClosestPositionResult = _meshCollider.GetClosestPosition(_testPosition);


            EnsureClosestLine();
            
            if (_lastClosestPositionResult != null)
            {
                _closestLineVisual3D.StartPosition = _testPosition.ToWpfPoint3D();
                _closestLineVisual3D.EndPosition = _lastClosestPositionResult.HitPosition.ToWpfPoint3D();
            }
        }

        private void EnsureWireCross()
        {
            if (_wireCrossVisual3D != null)
                return;

            _wireCrossVisual3D = new WireCrossVisual3D()
            {
                Position = _testPosition.ToWpfPoint3D(),
                LineColor = Colors.Blue,
                LineThickness = 2,
                LinesLength = 20,
            };

            SecondMeshVisual3D.Children.Add(_wireCrossVisual3D);
        }

        private void EnsureClosestLine()
        {
            if (_closestLineVisual3D != null)
                return;

            _closestLineVisual3D = new LineVisual3D()
            {
                StartPosition = _testPosition.ToWpfPoint3D(),
                EndPosition = new Point3D(0, 0, 0),
                LineColor = Colors.Blue,
                LineThickness = 2,
                EndLineCap = LineCap.ArrowAnchor
            };

            SecondMeshVisual3D.Children.Add(_closestLineVisual3D);
        }
    }
}
