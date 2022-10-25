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
    /// Interaction logic for MeshColliderSample.xaml
    /// </summary>
    public partial class MeshColliderSample : Page
    {
        private float KEYBOARD_MOVEMENT_STEP = 3.0f; // how much the mesh is moved when an arrow key is pressed

        public enum CollisionTypes
        {
            None = 0,
            Position3D,
            BoundingBoxNoCorners,
            BoundingBoxWithCorners,
            SphereMesh,
            SimpleBoxMesh,
            ComplexBoxMesh
        }

        private MeshCollider _meshCollider;
        
        private int _originalMeshPositionsCount;

        private MeshGeometry3D _collidingMesh;
        private TranslateTransform3D _collidingMeshTransform;

        private Vector3 _collisionPosition;
        private Rect3D _collisionRect3D;

        private CollisionTypes _currentCollisionType;
        private WireCrossVisual3D _collisionPositionWireCrossVisual3D;

        private Stopwatch _stopwatch;
        private WireBoxVisual3D _collisionWireBoxVisual3D;

        public MeshColliderSample()
        {
            InitializeComponent();

            CreateMainMesh();

            _currentCollisionType = CollisionTypes.Position3D;

            ShowCollisionPosition();

            MainDXViewportView.DXSceneInitialized += (sender, args) =>
            {
                TestCollision();
                TestCollision(); // Call again to get accurate elapsed time results (on the first call the code needs to be compiled and therefore it takes much longer to execute)
            };

            this.Unloaded += (sender, args) => MainDXViewportView.Dispose();


            this.Focusable = true;                                          // by default Page is not focusable and therefore does not receive keyDown event
            this.PreviewKeyDown += CollisionDetectionSample_PreviewKeyDown; // Use PreviewKeyDown to get arrow keys also (KeyDown event does not get them)
            this.Focus();
        }

        private void CreateMainMesh()
        {
            // Create a simple torus mesh
            var torusKnotMesh3D = new Ab3d.Meshes.TorusKnotMesh3D(centerPosition: new Point3D(0, 0, 0), p: 0, q: 1, r1: 50, r2: 20, r3: 30, uSegments: 100, vSegments: 30, calculateNormals: true).Geometry;

            // MeshCollider does not support transforming the mesh that is used to create the MeshCollider.
            // Therefore the mesh need to be transformed by the user, for example:
            //var transform3D = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), 30));
            //torusKnotMesh3D = Ab3d.Utilities.MeshUtils.TransformMeshGeometry3D(torusKnotMesh3D, transform3D);

            // Create an instance of MeshCollider from WPF's MeshGeometry3D
            // It can be also created from DXEngine's Mesh
            _meshCollider = new MeshCollider(torusKnotMesh3D);


            // Store positions count
            _originalMeshPositionsCount = torusKnotMesh3D.Positions.Count;
            
            // Show the mesh:
            var geometryModel3D = new GeometryModel3D(torusKnotMesh3D, new DiffuseMaterial(Brushes.Silver));

            MainMeshVisual3D.Children.Add(geometryModel3D.CreateContentVisual3D());

            UpdateInfoText();
        }

        private void TestCollision()
        {
            bool hasIntersection;
            
            if (_stopwatch == null)
                _stopwatch = new Stopwatch();
            
            _stopwatch.Restart();

            switch (_currentCollisionType)
            {
                case CollisionTypes.Position3D:
                    hasIntersection = _meshCollider.IsInsideMesh(_collisionPosition);
                    break;

                case CollisionTypes.BoundingBoxNoCorners:
                    hasIntersection = _meshCollider.HasIntersection(_collisionRect3D, checkEachCorner: false);
                    break;
                
                case CollisionTypes.BoundingBoxWithCorners:
                    hasIntersection = _meshCollider.HasIntersection(_collisionRect3D, checkEachCorner: true);
                    break;

                case CollisionTypes.SphereMesh:
                case CollisionTypes.SimpleBoxMesh:
                case CollisionTypes.ComplexBoxMesh:
                    hasIntersection = _meshCollider.HasIntersection(_collidingMesh, _collidingMeshTransform);
                    break;

                default:
                    hasIntersection = false;
                    break;
            }

            _stopwatch.Stop();

            TimeInfoTextBlock.Text = string.Format(System.Globalization.CultureInfo.InvariantCulture, "Collision test time: {0:0.00}ms", _stopwatch.Elapsed.TotalMilliseconds);

            ShowCollisionResult(hasIntersection);
        }
        
        private void ShowCollisionResult(bool isCollision)
        {
            if (isCollision)
            {
                CollisionResultTextBlock.Text = "Intersect";
                CollisionResultTextBlock.Background = Brushes.LightGreen;
            }
            else
            {
                CollisionResultTextBlock.Text = "No intersection";
                CollisionResultTextBlock.Background = Brushes.Red;
            }
        }
        
        private void UpdateInfoText()
        {
            string infoText = string.Format(System.Globalization.CultureInfo.InvariantCulture, "Mesh1 positions count: {0:#,##0}", _originalMeshPositionsCount);

            if (_collidingMesh != null)
                infoText += string.Format(System.Globalization.CultureInfo.InvariantCulture, "\r\nMesh2 positions count: {0:#,##0}", _collidingMesh.Positions.Count);

            MeshInfoTextBlock.Text = infoText;
        }
        
        private void CollisionTypeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            SecondMeshVisual3D.Children.Clear();

            _collisionPositionWireCrossVisual3D = null;
            _collisionWireBoxVisual3D = null;
            _collidingMesh = null;
            _collidingMeshTransform = null;


            _currentCollisionType = (CollisionTypes)(CollisionTypeComboBox.SelectedIndex + 1); // index -1 => 0 (None); 0 => 1 (Position3D)

            switch (_currentCollisionType)
            {
                case CollisionTypes.Position3D:
                    ShowCollisionPosition();
                    break;
                
                case CollisionTypes.BoundingBoxNoCorners:
                case CollisionTypes.BoundingBoxWithCorners:
                    ShowCollisionRect3D();
                    break;

                case CollisionTypes.SphereMesh:
                case CollisionTypes.SimpleBoxMesh:
                case CollisionTypes.ComplexBoxMesh:
                    ShowCollisionMesh();
                    break;
            }

            UpdateInfoText();

            if (_currentCollisionType == CollisionTypes.BoundingBoxNoCorners)
            {
                WarningTextBlock.Text = "Checking bounding box without corners is the fastest collision check because only two bounding boxes are checked for intersection. The results of that check are the least accurate.";
                WarningTextBlock.Visibility = Visibility.Visible;
            }
            else if (_currentCollisionType == CollisionTypes.BoundingBoxWithCorners)
            {
                WarningTextBlock.Text = "Checking bounding box corners checks if center position and each corner of the bounding box is intersecting with the mesh. This tests only 9 positions so it is very fast but is not very accurate.";
                WarningTextBlock.Visibility = Visibility.Visible;
            }
            else if(_currentCollisionType == CollisionTypes.SimpleBoxMesh)
            {
                WarningTextBlock.Text = "Because collision detection is done by checking each mesh position, a simple box (1x1x1) may produce inaccurate results. Use mesh with more positions to improve accuracy (see next mesh type).";
                WarningTextBlock.Visibility = Visibility.Visible;
            }
            else
            {
                WarningTextBlock.Visibility = Visibility.Collapsed;
            }

            if (this.IsLoaded)
                TestCollision();
        }

        private void ShowCollisionMesh()
        {
            if (_collidingMesh != null)
                return;

            switch (_currentCollisionType)
            {
                case CollisionTypes.SphereMesh:
                    _collidingMesh = new Ab3d.Meshes.SphereMesh3D(centerPosition: new Point3D(0, 0, 0), radius: 20, segments: 20).Geometry;
                    break;

                case CollisionTypes.SimpleBoxMesh:
                    // Simple box is created with one segment for each side (1 x 1 x 1)
                    // This may lead to inaccurate results when testing collision because only edge positions are tested
                    _collidingMesh = new Ab3d.Meshes.BoxMesh3D(centerPosition: new Point3D(0, 0, 0), size: new Size3D(40, 20, 80), xSegments: 1, ySegments: 1, zSegments: 1).Geometry;
                    break;

                case CollisionTypes.ComplexBoxMesh:
                    // Complex box is created with many segment for each side (10 x 4 x 10)
                    // This greatly improves the results of the collision detection
                    _collidingMesh = new Ab3d.Meshes.BoxMesh3D(centerPosition: new Point3D(0, 0, 0), size: new Size3D(40, 20, 80), xSegments: 10, ySegments: 4, zSegments: 10).Geometry;
                    break;

                default:
                    _collidingMesh = null;
                    break;
            }
            

            if (_collidingMesh == null)
                return;

            var geometryModel3D = new GeometryModel3D(_collidingMesh, new DiffuseMaterial(Brushes.LightBlue));

            _collidingMeshTransform = new TranslateTransform3D();
            geometryModel3D.Transform = _collidingMeshTransform;

            SecondMeshVisual3D.Children.Add(geometryModel3D.CreateContentVisual3D());


            var wireframeLinePositions = Ab3d.Models.WireframeFactory.CreateWireframeLinePositions(_collidingMesh);

            var multiLineVisual3D = new MultiLineVisual3D()
            {
                Positions = wireframeLinePositions,
                LineColor = Colors.Black,
                LineThickness = 1,
                Transform = _collidingMeshTransform
            };

            multiLineVisual3D.SetDXAttribute(DXAttributeType.LineDepthBias, 0.1f);

            SecondMeshVisual3D.Children.Add(multiLineVisual3D);
        }

        private void ShowCollisionPosition()
        {
            if (_collisionPositionWireCrossVisual3D != null)
                return;
            
            _collisionPositionWireCrossVisual3D = new WireCrossVisual3D()
            {
                Position = new Point3D(0, 0, 0),
                LineColor = Colors.Blue,
                LineThickness = 2,
                LinesLength = 80
            };

            SecondMeshVisual3D.Children.Add(_collisionPositionWireCrossVisual3D);

            _collisionPosition = _collisionPositionWireCrossVisual3D.Position.ToVector3();
        }
        
        private void ShowCollisionRect3D()
        {
            if (_collisionWireBoxVisual3D != null)
                return;

            _collisionWireBoxVisual3D = new WireBoxVisual3D()
            {
                CenterPosition = new Point3D(0, 0, 0),
                Size = new Size3D(40, 15, 30),
                LineColor = Colors.Blue,
                LineThickness = 2,
            };

            SecondMeshVisual3D.Children.Add(_collisionWireBoxVisual3D);

            UpdateCollisionRect3D();
        }

        private void UpdateCollisionRect3D()
        {
            if (_collisionWireBoxVisual3D == null)
                return;

            _collisionRect3D = new Rect3D(_collisionWireBoxVisual3D.CenterPosition.X - _collisionWireBoxVisual3D.Size.X * 0.5,
                                          _collisionWireBoxVisual3D.CenterPosition.Y - _collisionWireBoxVisual3D.Size.Y * 0.5,
                                          _collisionWireBoxVisual3D.CenterPosition.Z - _collisionWireBoxVisual3D.Size.Z * 0.5,
                                          _collisionWireBoxVisual3D.Size.X,
                                          _collisionWireBoxVisual3D.Size.Y,
                                          _collisionWireBoxVisual3D.Size.Z);
        }


        void CollisionDetectionSample_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.A:
                case Key.Left:
                    // left
                    MoveMesh(-KEYBOARD_MOVEMENT_STEP, 0, 0);
                    e.Handled = true;
                    break;

                case Key.D:
                case Key.Right:
                    // right
                    MoveMesh(KEYBOARD_MOVEMENT_STEP, 0, 0);
                    e.Handled = true;
                    break;

                case Key.W:
                case Key.Up:
                    // forward
                    MoveMesh(0, 0, -KEYBOARD_MOVEMENT_STEP);
                    e.Handled = true;
                    break;

                case Key.S:
                case Key.Down:
                    // backward
                    MoveMesh(0, 0, KEYBOARD_MOVEMENT_STEP);
                    e.Handled = true;
                    break;
                
                case Key.PageUp:
                    // forward
                    MoveMesh(0, KEYBOARD_MOVEMENT_STEP, 0);
                    e.Handled = true;
                    break;

                case Key.PageDown:
                    // backward
                    MoveMesh(0, -KEYBOARD_MOVEMENT_STEP, 0);
                    e.Handled = true;
                    break;
            }
        }

        private void MoveMesh(float dx, float dy, float dz)
        {
            switch (_currentCollisionType)
            {
                case CollisionTypes.Position3D:
                    if (_collisionPositionWireCrossVisual3D != null)
                    {
                        _collisionPositionWireCrossVisual3D.Position += new Vector3D(dx, dy, dz);
                        _collisionPosition = _collisionPositionWireCrossVisual3D.Position.ToVector3();
                    }
                    break;

                case CollisionTypes.BoundingBoxNoCorners:
                case CollisionTypes.BoundingBoxWithCorners:
                    if (_collisionWireBoxVisual3D != null)
                    {
                        _collisionWireBoxVisual3D.CenterPosition += new Vector3D(dx, dy, dz);
                        UpdateCollisionRect3D();
                    }
                    break;

                case CollisionTypes.SphereMesh:
                case CollisionTypes.SimpleBoxMesh:
                case CollisionTypes.ComplexBoxMesh:
                    if (_collidingMeshTransform != null)
                    {
                        _collidingMeshTransform.OffsetX += dx;
                        _collidingMeshTransform.OffsetY += dy;
                        _collidingMeshTransform.OffsetZ += dz;
                    }
                    break;
            }

            TestCollision();
        }
    }
}
