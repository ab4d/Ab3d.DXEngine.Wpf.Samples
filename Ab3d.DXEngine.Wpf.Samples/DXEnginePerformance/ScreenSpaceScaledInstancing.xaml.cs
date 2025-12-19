using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Ab3d.Controls;
using Ab3d.DirectX;
using Ab3d.DXEngine.Wpf.Samples.Common;
using Ab3d.Visuals;
using Point = System.Windows.Point;

#if SHARPDX
using SharpDX;
#endif

namespace Ab3d.DXEngine.Wpf.Samples.DXEnginePerformance
{
    /// <summary>
    /// Interaction logic for ScreenSpaceScaledInstancing.xaml
    /// </summary>
    public partial class ScreenSpaceScaledInstancing : Page
    {
        private MeshTypes _currentMeshType = MeshTypes.Sphere;
        private float _screenSize = 20f;

        private enum MeshTypes
        {
            Box,
            Sphere,
            Arrow
        };

        private InstancedMeshGeometryVisual3D _instancedMeshGeometryVisual3D;

        private int _selectedInstanceIndex;
        private Color4 _savedInstanceColor;
        private Color4 _selectedColor;

        private Point3D[] _instanceWorldPositions;
        private Point[] _instanceScreenPositions;


        public ScreenSpaceScaledInstancing()
        {
            InitializeComponent();

            ScreenSizeComboBox.ItemsSource = new float[] {1, 5, 10, 20, 50};
            ScreenSizeComboBox.SelectedItem = _screenSize;

            ObjectsTypeComboBox.ItemsSource = Enum.GetNames(typeof(MeshTypes));
            ObjectsTypeComboBox.SelectedItem = _currentMeshType.ToString();

            MouseDistanceComboBox.ItemsSource = new double[] {0.5, 1, 5, 10, 15, 20, 25, 30};
            MouseDistanceComboBox.SelectedItem = (double)(_screenSize / 2);

            MouseCameraController1.RotationCursor = null;

            CameraControllerInfo1.AddCustomInfoLine(0, MouseCameraController.MouseAndKeyboardConditions.MiddleMouseButtonPressed, "Zoom in / out");


            _selectedColor = Colors.Red.ToColor4();

            MainDXViewportView.DXSceneDeviceCreated += (sender, args) => CreateInstancedObjects();

            this.MouseMove += (sender, args) => DoHitTesting();

            Camera1.CameraChanged += (sender, args) => DoHitTesting();
        }

        private void CreateInstancedObjects()
        {
            MainViewport.Children.Clear();


            _currentMeshType = (MeshTypes)Enum.Parse(typeof(MeshTypes), (string) ObjectsTypeComboBox.SelectedItem);
            _screenSize = (float) ScreenSizeComboBox.SelectedItem;

            MeshGeometry3D mesh;
            
            switch (_currentMeshType)
            {
                case MeshTypes.Box:
                    mesh = new Ab3d.Meshes.BoxMesh3D(new Point3D(0, 0, 0), new Size3D(1, 1, 1), 1, 1, 1).Geometry;
                    break;

                case MeshTypes.Sphere:
                    mesh = new Ab3d.Meshes.SphereMesh3D(new Point3D(0, 0, 0), 0.5, 30).Geometry;
                    break;

                case MeshTypes.Arrow:
                    mesh = new Ab3d.Meshes.ArrowMesh3D(new Point3D(-0.5, 0, 0), new Point3D(0.5, 0, 0), 0.1, 0.2, 45, 30, false).Geometry;
                    break;

                default:
                    mesh = null;
                    break;
            }

            var instancedData = GetInstancedData(_currentMeshType, _screenSize);


            _instancedMeshGeometryVisual3D = new InstancedMeshGeometryVisual3D(mesh);
            _instancedMeshGeometryVisual3D.InstancesData = instancedData;

            // To enable screen-space scaling, set the UseScreenSpaceScaling to true.
            //
            // IMPORTANT:
            // For this to work correctly, the size of the instance mesh must be 1 and the center of the mesh must be at (0, 0, 0).
            // In this case the mesh will be scaled correctly to the screen size that is defined in the instance matrix's scale component.
            _instancedMeshGeometryVisual3D.UseScreenSpaceScaling = UseScreenSpaceScaleCheckBox.IsChecked ?? false;

            MainViewport.Children.Add(_instancedMeshGeometryVisual3D);

            _instanceWorldPositions = null; // Reset array of instance positions that is used for finding the closest instance in screen coordinates
            _selectedInstanceIndex = -1;

            // Because we have cleared all MainViewport.Children, this also removed the camera's light. 
            // Call Refresh to recreate the light
            Camera1.Refresh();
        }


        private void DoHitTesting()
        {
            if (MainDXViewportView.DXScene == null || !MainDXViewportView.DXScene.IsInitialized || (NoHitTestingRadioButton.IsChecked ?? false))
                return;

            var mousePosition = Mouse.GetPosition(ViewportBorder);

            if (ScreenSpaceHitTestingRadioButton.IsChecked ?? false)
                UpdateClosestInstance(mousePosition);
            else if (WorldSpaceTestingRadioButton.IsChecked ?? false)
                UpdateHitInstance(mousePosition);
        }

        private void UpdateClosestInstance(Point mousePosition)
        {
            var instancesData = _instancedMeshGeometryVisual3D.InstancesData;
            int count = instancesData.Length;

            if (_instanceWorldPositions == null || _instanceWorldPositions.Length != count)
            {
                _instanceWorldPositions = new Point3D[count];

                for (int i = 0; i < count; i++)
                    _instanceWorldPositions[i] = new Point3D(instancesData[i].World.M41, instancesData[i].World.M42, instancesData[i].World.M43);
            }

            if (_instanceScreenPositions == null || _instanceScreenPositions.Length != count)
                _instanceScreenPositions = new Point[count];


            // Points3DTo2D also support parallel calculations (for lots of positions the perf gains are significant).
            // Tests show that it is worth using parallel algorithm when number of positions is more the 300 (but this may be highly CPU dependent)
            bool useParallelFor = count > 300;

            var success = Camera1.Points3DTo2D(_instanceWorldPositions, _instanceScreenPositions, _instancedMeshGeometryVisual3D.Transform, useParallelFor);

            if (!success)
                return;


            double minSquaredDistance = double.MaxValue;
            int minIndex = -1;

            for (int i = 0; i < count; i++)
            {
                double distanceSquared = (_instanceScreenPositions[i] - mousePosition).LengthSquared;
                if (distanceSquared < minSquaredDistance)
                {
                    minSquaredDistance = distanceSquared;
                    minIndex = i;
                }
            }

            

            if (minIndex != -1)
            {
                double actualDistance = Math.Sqrt(minSquaredDistance);

                double minRequiredDistance = (double) MouseDistanceComboBox.SelectedItem;

                if (actualDistance < minRequiredDistance)
                    SelectInstance(minIndex);
                else
                    DeselectInstance();
            }
            else
            {
                DeselectInstance();
            }
        }

        private void UpdateHitInstance(Point mousePosition)
        {
            if (MainDXViewportView == null || MainDXViewportView.DXScene == null)
                return;


            // Hit test center of the MainDXViewportView (we could also use mouse position)
            var pickRay = MainDXViewportView.DXScene.GetRayFromCamera((int)mousePosition.X, (int)mousePosition.Y);

            var dxRayHitTestResult = MainDXViewportView.DXScene.GetClosestHitObject(pickRay) as DXRayInstancedHitTestResult;

            if (dxRayHitTestResult == null || dxRayHitTestResult.HitInstanceIndex == -1)
                DeselectInstance();
            else
                SelectInstance(dxRayHitTestResult.HitInstanceIndex);
        }

        private void SelectInstance(int instanceIndex)
        {
            if (_selectedInstanceIndex == instanceIndex)
                return; // Already selected

            if (_selectedInstanceIndex != -1)
                DeselectInstance();

            _savedInstanceColor = _instancedMeshGeometryVisual3D.InstancesData[instanceIndex].DiffuseColor;

            _instancedMeshGeometryVisual3D.InstancesData[instanceIndex].DiffuseColor = _selectedColor;
            _instancedMeshGeometryVisual3D.Update(instanceIndex, 1, updateBounds: false);

            _selectedInstanceIndex = instanceIndex;
        }

        private void DeselectInstance()
        {
            if (_selectedInstanceIndex == -1)
                return;

            _instancedMeshGeometryVisual3D.InstancesData[_selectedInstanceIndex].DiffuseColor = _savedInstanceColor;
            _instancedMeshGeometryVisual3D.Update(_selectedInstanceIndex, 1, updateBounds: false);

            _selectedInstanceIndex = -1;
        }


        private InstanceData[] GetInstancedData(MeshTypes currentMeshType, float screenSize)
        {
            InstanceData[] instancedData;

            if (currentMeshType == MeshTypes.Arrow)
            {
                instancedData = CreateRotatedInstancesData(new Point3D(0, 0, 0), new Size3D(1000, 1000, 2000),
                                                           modelScaleFactor: screenSize,
                                                           xCount: 5, yCount: 5, zCount: 5,
                                                           rotationTargetPosition: new Point3D(0, 0, 0));
            }
            else
            {
                instancedData = DXEnginePerformance.InstancedMeshGeometry3DTest.CreateInstancesData(new Point3D(0, -100, -1000), new Size3D(500, 0, 3000),
                                                                                                    modelScaleFactor: screenSize,
                                                                                                    xCount: 5, yCount: 1, zCount: 8, 
                                                                                                    useTransparency: false);
            }

            return instancedData;
        }


        public static InstanceData[] CreateRotatedInstancesData(Point3D center, Size3D size, float modelScaleFactor, int xCount, int yCount, int zCount, Point3D rotationTargetPosition)
        {
            var instancedData = new InstanceData[xCount * yCount * zCount];

            float xStep = xCount <= 1 ? 0 : (float)(size.X / (xCount - 1));
            float yStep = yCount <= 1 ? 0 : (float)(size.Y / (yCount - 1));
            float zStep = zCount <= 1 ? 0 : (float)(size.Z / (zCount - 1));

            Vector3 targetPosition = rotationTargetPosition.ToVector3();

            int i = 0;
            for (int y = 0; y < yCount; y++)
            {
                float yPos = (float)(center.Y - (size.Y / 2.0) + (y * yStep));

                for (int z = 0; z < zCount; z++)
                {
                    float zPos = (float)(center.Z - (size.Z / 2.0) + (z * zStep));

                    for (int x = 0; x < xCount; x++)
                    {
                        float xPos = (float)(center.X - (size.X / 2.0) + (x * xStep));

                        var position = new Vector3(xPos, yPos, zPos);
                        var direction = targetPosition - position;
                        direction.Normalize();

                        float usedScaleFactor;
                        if (modelScaleFactor < 0)
                            usedScaleFactor = i + 1;
                        else
                            usedScaleFactor = modelScaleFactor;
                        Vector3 scale = new Vector3(usedScaleFactor, usedScaleFactor, usedScaleFactor);

                        MatrixUtils.GetMatrixFromDirection(direction, position, scale, out instancedData[i].World);
                        
                        instancedData[i].DiffuseColor = Colors.Yellow.ToColor4();
                        
                        i++;
                    }
                }
            }

            return instancedData;
        }



        private void OnUseScreenSpaceScaleCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded || _instancedMeshGeometryVisual3D == null)
                return;

            _instancedMeshGeometryVisual3D.UseScreenSpaceScaling = UseScreenSpaceScaleCheckBox.IsChecked ?? false;
        }

        private void ScreenSizeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            _screenSize = (float)ScreenSizeComboBox.SelectedItem;
            MouseDistanceComboBox.SelectedItem = (double)(_screenSize / 2);

            // Recreate the scene
            CreateInstancedObjects();
        }

        private void ObjectsTypeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            // Recreate the scene
            CreateInstancedObjects();

            if (_currentMeshType == MeshTypes.Arrow)
            {
                Camera1.Heading     = -60;
                Camera1.Distance    = 4000;
                Camera1.CameraWidth = 2000;
            }
        }
    }
}