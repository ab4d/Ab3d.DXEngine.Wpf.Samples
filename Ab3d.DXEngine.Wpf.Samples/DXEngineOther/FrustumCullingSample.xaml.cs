using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Ab3d.Cameras;
using Ab3d.Common.Cameras;
using Ab3d.DirectX;
using Ab3d.Visuals;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Material = System.Windows.Media.Media3D.Material;

#if SHARPDX
using SharpDX;
using Matrix = SharpDX.Matrix;
#endif

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineOther
{
    /// <summary>
    /// Interaction logic for FrustumCullingSample.xaml
    /// </summary>
    public partial class FrustumCullingSample : Page
    {
        // FrustumVisibilityStatus holds bounding box, visibility status and original BoxVisual3D object
        struct FrustumVisibilityStatus
        {
            public BoxVisual3D BoxVisual3D;
            public BoundingBox Bounds;
            private ContainmentType _visibility;

            public ContainmentType Visibility
            {
                get { return _visibility; }
            }

            public FrustumVisibilityStatus(BoxVisual3D boxVisual3D, BoundingBox bounds)
            {
                BoxVisual3D = boxVisual3D;
                Bounds = bounds;

                _visibility = ContainmentType.Contains; // == Visible
            }

            public static void SetVisibility(ref FrustumVisibilityStatus frustumVisibilityStatus, ContainmentType newVisibility)
            {
                frustumVisibilityStatus._visibility = newVisibility;
            }
        }

        private Material _fullyVisibleMaterial;
        private Material _partiallyVisibleMaterial;
        private Material _hiddenMaterial;

        private FrustumVisibilityStatus[] _frustumStatuses;

        public FrustumCullingSample()
        {
            InitializeComponent();

            _fullyVisibleMaterial = new DiffuseMaterial(Brushes.Green);
            _fullyVisibleMaterial.Freeze(); // Freezing material will speed up initialization of objects

            _partiallyVisibleMaterial = new DiffuseMaterial(Brushes.Orange);
            _partiallyVisibleMaterial.Freeze();

            _hiddenMaterial = new DiffuseMaterial(Brushes.Red);
            _hiddenMaterial.Freeze();


            // Setup keyboard support
            this.Focusable = true; // by default Page is not focusable and therefore does not receive keyDown event
            this.PreviewKeyDown += OnPreviewKeyDown; // Use PreviewKeyDown to get arrow keys also (KeyDown event does not get them)
            this.Focus();


            this.Camera1.CameraChanged += delegate(object sender, CameraChangedRoutedEventArgs args)
            {
                UpdateVisibleBoxes();
            };

            this.SizeChanged += delegate(object sender, SizeChangedEventArgs args)
            {
                UpdateVisibleBoxes();
            };


            CreateSceneObjects();
        }

        private void CreateSceneObjects()
        {
            // We store bounding box and current visibility status into a FrustumVisibilityStatus struct.
            // This way the frustum checks are optimized because we do not need to convert from WPF positions to SharpDX Vector3.
            // Also accessing DependencyProperties (CenterPosition, Size) is very slow.
            //
            // Also the organization of all FrustumVisibilityStatus structs in one array will give us
            // very memory cache friendly organization of data.

            var frustumStatuses = new List<FrustumVisibilityStatus>();

            var halfBoxSize = 5;
            var boxSize = new Size3D(halfBoxSize * 2, halfBoxSize * 2, halfBoxSize * 2);

            for (int x = 0; x < 10; x++)
            {
                for (int z = 0; z < 10; z++)
                {
                    for (int y = 0; y < 4; y++)
                    {
                        var centerPosition = new Point3D(-200 + 40 * x, y * 40, -200 + 40 * z);

                        var boxVisual3D = new Ab3d.Visuals.BoxVisual3D()
                        {
                            CenterPosition = centerPosition,
                            Size = boxSize,
                            Material = _fullyVisibleMaterial
                        };

                        BoxesRootVisual3D.Children.Add(boxVisual3D);

                        frustumStatuses.Add(new FrustumVisibilityStatus(boxVisual3D, new BoundingBox(new Vector3((float)centerPosition.X - halfBoxSize, (float)centerPosition.Y - halfBoxSize, (float)centerPosition.Z - halfBoxSize),
                                                                                                     new Vector3((float)centerPosition.X + halfBoxSize, (float)centerPosition.Y + halfBoxSize, (float)centerPosition.Z + halfBoxSize))));
                    }
                }
            }

            // Convert to array. This will allow use to change Visibility field with passing array item by ref.
            _frustumStatuses = frustumStatuses.ToArray();


            Camera1.Refresh();
            UpdateVisibleBoxes();
        }

        private void UpdateVisibleBoxes()
        {
            if (!(CullingCheckBox.IsChecked ?? false) || _frustumStatuses == null || !Camera1.IsValid())
                return;

            //
            // IMPORTANT TIP:
            //
            // When you have many 3D objects, do not check each object if it is visible or not.
            // Instead group the objects into lower number of groups (into ModelVisual3D, Model3DGroup or some other group).
            // Then calculate the bounding box of each group.
            // Finally check each group if it is visible or not and remove the hidden groups from visible objects.


            // Get camera's viewProjection matrix
            System.Windows.Media.Media3D.Matrix3D view, projection;
            Camera1.GetCameraMatrixes(out view, out projection);


            Matrix viewProjection;

            if (Camera1.CameraType == BaseCamera.CameraTypes.PerspectiveCamera)
            {
                // We need to convert the matrix from WPF to SharpDX format
                viewProjection = (view * projection).ToMatrix();
            }
            else
            {
                // For OrthographicCamera the code in SharpDX.BBoundingFrustum expects a left handed projection matrix but WPF work with right handed projection.
                // To make the code work correctly we create the left handed orthographic matrix from Matrix.OrthoLH:
                var leftHandedProjectionMatrix = Matrix.OrthoLH((float)Camera1.CameraWidth, (float)(Camera1.CameraWidth * MainDXViewportView.ActualHeight / MainDXViewportView.ActualWidth), (float)Camera1.NearPlaneDistance, (float)Camera1.FarPlaneDistance);
                
                viewProjection = view.ToMatrix() * leftHandedProjectionMatrix;
            }

            // Create BoundingFrustum from camera view-projection matrix
            var boundingFrustum = new BoundingFrustum(viewProjection);


            // We could also get the ViewProjection from DXScene (but when we are called from CameraChanged we may get an older version)
            //if (MainDXViewportView.DXScene == null)
            //    return;

            //Matrix worldViewProjectionMatrix = MainDXViewportView.DXScene.Camera.GetViewProjection();
            //var boundingFrustum = new SharpDX.BoundingFrustum(worldViewProjectionMatrix);


            bool hasChanged = false;

            for (var i = 0; i < _frustumStatuses.Length; i++) // Use for instead of foreach to avoid creating enumerator object on each call of the method (on every frame)
            {
                var newVisibility = boundingFrustum.Contains(_frustumStatuses[i].Bounds);

                if (newVisibility != _frustumStatuses[i].Visibility)
                {
                    Material newMaterial;

                    switch (newVisibility)
                    {
                        case ContainmentType.Disjoint:
                            newMaterial = _hiddenMaterial;
                            break;

                        case ContainmentType.Contains:
                            newMaterial = _fullyVisibleMaterial;
                            break;

                        case ContainmentType.Intersects:
                            newMaterial = _partiallyVisibleMaterial;
                            break;

                        default:
                            newMaterial = null;
                            break;
                    }

                    _frustumStatuses[i].BoxVisual3D.Material = newMaterial;

                    FrustumVisibilityStatus.SetVisibility(ref _frustumStatuses[i], newVisibility);

                    hasChanged = true;
                }
            }

            if (hasChanged)
                UpdateStatistics();
        }

        private void UpdateStatistics()
        {
            int visibleCount = 0;
            int partiallyVisibleCount = 0;
            int hiddenCount = 0;

            for (var i = 0; i < _frustumStatuses.Length; i++) // Use for instead of foreach to avoid creating enumerator object on each call of the method (on every frame)
            {
                switch (_frustumStatuses[i].Visibility)
                {
                    case ContainmentType.Disjoint:
                        hiddenCount++;
                        break;

                    case ContainmentType.Contains:
                        visibleCount++;
                        break;

                    case ContainmentType.Intersects:
                        partiallyVisibleCount++;
                        break;
                }
            }

            VisibleTextBlockRun.Text = visibleCount.ToString();
            PartiallyVisibleTextBlockRun.Text = partiallyVisibleCount.ToString();
            HiddenTextBlockRun.Text = hiddenCount.ToString();
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.A:
                case Key.Left:
                    // left
                    Camera1.MoveLeft(5);
                    e.Handled = true;

                    break;

                case Key.D:
                case Key.Right:
                    // right
                    Camera1.MoveRight(5);
                    e.Handled = true;

                    break;

                case Key.W:
                case Key.Up:
                    // forward
                    Camera1.MoveForward(10);
                    e.Handled = true;

                    break;

                case Key.S:
                case Key.Down:
                    // backward
                    Camera1.MoveBackward(10);
                    e.Handled = true;

                    break;
            }
        }

        private void CullingCheckBox_OnChecked(object sender, RoutedEventArgs e)
        {
            UpdateVisibleBoxes();
        }
    }
}