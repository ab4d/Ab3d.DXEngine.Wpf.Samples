using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Ab3d.Cameras;
using Ab3d.Common.Cameras;
using Ab3d.Controls;
using Ab3d.DirectX;
using Ab3d.DirectX.Controls;
using Ab3d.DXEngine.Wpf.Samples.Controls;
using Ab3d.DXEngine.Wpf.Samples.DXEnginePerformance;
using Ab3d.Utilities;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    /// <summary>
    /// Interaction logic for TransparencySortingPerformanceSample.xaml
    /// </summary>
    public partial class TransparencySortingPerformanceSample : Page
    {
        private const int XCount = 6;
        private const int ZCount = 6;
        private const int YCount = 20;
        
        private TestScene[] _testScenes;
        
        public TransparencySortingPerformanceSample()
        {
            InitializeComponent();

            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                CreateScenes();
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

            this.Unloaded += delegate (object sender, RoutedEventArgs args)
            {
                if (_testScenes != null)
                {
                    foreach (var testScene in _testScenes)
                        testScene.Dispose();
                }
            };
        }

        private void CreateScenes()
        { 
            TitleTextBlock.Text = string.Format("Ab3d.DXEngine transparency sorting performance sample (showing {0} boxes)", XCount * YCount * ZCount);

            _testScenes = new TestScene[3];

            _testScenes[0] = new TestScene("No transparency sorting", useDXEngine: true,  useTransparencySorter: false, useDXEngineSorting: false, 
                                           infoText: "Objects are rendered in the order in which they were added to the scene. In this case some objects are not visible through transparent objects (if this is not seen then wait for camera to rotate around).");

            _testScenes[1] = new TestScene("Ab3d.PowerToys TransparencySorter", useDXEngine: true,  useTransparencySorter: true,  useDXEngineSorting: false,
                                           infoText: "Using TransparencySorter from Ab3d.PowerToys library sorts the objects in Model3DGroup so that the objects in its Children are sorted by camera distance - first objects that are farthest away from the camera.\nThis method works correctly but is not fast because sorting WPF objects is slow and also because the order of objects is changed, the Update part of the Ab3d.DXEngine rendering takes much more time.\nWhat is more, it is not possible sort objects from different ModelVisual3D, Model3DGroup objects and SceneNode objects that are manually created (not created from WPF objects).");

            _testScenes[2] = new TestScene("Ab3d.DXEngine transparency sorting", useDXEngine: true,  useTransparencySorter: false, useDXEngineSorting: true, 
                                           infoText: "Using transparency sorting from Ab3d.DXEngine is much more efficient then using TransparencySorter from Ab3d.PowerToys library. It sorts the objects inside the DXViewportView.TransparentRenderingQueue. Sorting is highly optimized and also does not require additional work in the Update phase.\n\nNote that by default transparency sorting is disabled!\nTo enable it set DXScene.IsTransparencySortingEnabled property to true.\nTo adjust the sorted order, you can use the DXScene.TransparentRenderingQueue.SortingCompleted event.");

            for (int i = 0; i < 3; i++)
            {
                var sceneFrameworkElement = _testScenes[i].CreateScene();
                Grid.SetRow(sceneFrameworkElement, 1);
                Grid.SetColumn(sceneFrameworkElement, i);

                RootGrid.Children.Add(sceneFrameworkElement);

                _testScenes[i].CameraChanged += OnCameraChanged;
            }

            _testScenes[0].StartAnimation();
        }

        private void OnCameraChanged(object sender, CameraChangedRoutedEventArgs e)
        {
            var testScene = sender as TestScene;
            var changedCamera = testScene.MainCamera;

            for (var i = 0; i < _testScenes.Length; i++)
            {
                if (_testScenes[i] == testScene)
                    continue;

                _testScenes[i].SyncCamera(changedCamera);
            }
        }

        class TestScene
        {
            private readonly string _title;
            private readonly string _infoText;
            private readonly bool _useDXEngine;
            private readonly bool _useTransparencySorter;
            private readonly bool _useDXEngineSorting;
            private readonly string _subtitle;

            private CheckBox _checkAllBoundingBoxCornersCheckBox;

            private TransparencySorter _transparencySorter;

            private Stopwatch _stopwatch;

            private double _noSortPrepareTime;
            private double _totalSortTime;
            private double _totalUpdateTime;
            private double _totalPrepareTime;
            private double _totalDrawTime;
            private int _framesCount;
            private int _lastFrameSecond;

            private bool _isSyncingCamera;
            private DXViewportView _dxViewportView;
            private TextBlock _reportTextBlock;

            private MouseCameraController _mouseCameraController;


            public Viewport3D MainViewport3D { get; private set; }
            public TargetPositionCamera MainCamera { get; private set; }

            public event BaseCamera.CameraChangedRoutedEventHandler CameraChanged;

            public TestScene(string title, bool useDXEngine, bool useTransparencySorter, bool useDXEngineSorting, string subtitle = null, string infoText = null)
            {
                _title = title;
                _useDXEngine = useDXEngine;
                _useTransparencySorter = useTransparencySorter;
                _useDXEngineSorting = useDXEngineSorting;
                _subtitle = subtitle;
                _infoText = infoText;
            }

            public Grid CreateScene()
            {
                FrameworkElement subtitleElement = null;
                InfoControl titleInfoControl = null;

                var rootGrid = new Grid();
                var topStackPanel = new StackPanel()
                {
                    Orientation = Orientation.Vertical,
                    Margin = new Thickness(10, 5, 0, 0)
                };


                var titleTextBlock = new TextBlock()
                {
                    Text       = _title,
                    FontSize   = 16,
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                if (!string.IsNullOrEmpty(_infoText))
                {
                    titleInfoControl = new InfoControl()
                    {
                        InfoText = _infoText,
                        InfoWidth = 500,
                        Margin = new Thickness(5, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                }

                var titleStackPanel = new StackPanel()
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 2, 0, 2)
                };


                _reportTextBlock = new TextBlock()
                {
                    Text       = "",
                    FontSize   = 10,
                    Margin     = new Thickness(0, 5, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    TextWrapping = TextWrapping.Wrap
                };

                var rootBorder = new Border()
                {
                    Background = Brushes.Transparent,
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(2, 2, 2, 2),
                    Margin = new Thickness(1, 2, 1, 2)
                };

                MainViewport3D = new Viewport3D();

                var rootModel3DGroup = Create3DScene(new Point3D(0, -300, 0), XCount, YCount, ZCount, 30);
                
                MainCamera = new TargetPositionCamera()
                {
                    TargetViewport3D = MainViewport3D,
                    TargetPosition   = new Point3D(0, 0, 0),
                    Heading          = 130, // start from back where sorting errors are most obvious
                    Attitude         = -50,
                    Distance         = 500,
                    ShowCameraLight  = ShowCameraLightType.Always
                };

                MainCamera.CameraChanged += OnMainCameraChanged;

                _mouseCameraController = new MouseCameraController()
                {
                    TargetCamera           = MainCamera,
                    EventsSourceElement    = rootBorder,
                    RotateCameraConditions = MouseCameraController.MouseAndKeyboardConditions.LeftMouseButtonPressed,
                    MoveCameraConditions   = MouseCameraController.MouseAndKeyboardConditions.Disabled // disable mouse move
                };

                if (_useDXEngine)
                {
                    _dxViewportView = new DXViewportView();
                    _dxViewportView.Viewport3D = MainViewport3D;
                    
                    rootBorder.Child = _dxViewportView;

                    if (_useDXEngineSorting)
                    {
                        var checkBoxStackPanel = new StackPanel()
                        {
                            Orientation = Orientation.Horizontal,
                            Margin = new Thickness(0, 2, 0, 2)
                        };

                        _checkAllBoundingBoxCornersCheckBox = new CheckBox()
                        {
                            Content = "CheckAllBoundingBoxCorners",
                            IsChecked = false,
                            VerticalAlignment = VerticalAlignment.Center
                        };

                        _checkAllBoundingBoxCornersCheckBox.Checked += OnCheckAllBoundingBoxCornersCheckedChanged;
                        _checkAllBoundingBoxCornersCheckBox.Unchecked += OnCheckAllBoundingBoxCornersCheckedChanged;


                        var infoControl = new InfoControl()
                        {
                            InfoText = "When checked, then all bounding box corners are checked to get distance to the camera (this is more accurate but slower).\r\nWhen false (by default) then center of the bounding box is used to get the distance to the camera (this is less accurate but significantly faster).\r\nSee 'Transparency sorting types' sample for more info.",
                            InfoWidth = 500,
                            Margin = new Thickness(5, 0, 0, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        };

                        checkBoxStackPanel.Children.Add(_checkAllBoundingBoxCornersCheckBox);
                        checkBoxStackPanel.Children.Add(infoControl);

                        subtitleElement = checkBoxStackPanel;
                    }


                    // Always disable transparency sorting on start
                    // If _useDXEngineSorting the IsTransparencySortingEnabled will be set to true after 
                    // the first rendering statistics will be collected (see CollectStatistics method for more info)
                    _dxViewportView.IsTransparencySortingEnabled = false;

                    // In the versions before v4.5 the IsTransparencySortingEnabled was available only on DXScene,
                    // so you need to subscribe to DXSceneDeviceCreated to get access to the created DXScene:
                    //_dxViewportView.DXSceneDeviceCreated += delegate (object sender, EventArgs e)
                    //{
                    //    _dxViewportView.DXScene.IsTransparencySortingEnabled = false;
                    //};

                    _dxViewportView.SceneRendered += delegate(object sender, EventArgs e)
                    {
                        CollectStatistics();
                    };

                    // To manually change the order of objects after they have been sorted use SortingCompleted event:
                    //_dxViewportView.DXScene.TransparentRenderingQueue.SortingCompleted += delegate(object sender, RenderingQueueSortingCompletedEventArgs e)
                    //{
                    //    // Here it is possible to change the order of item with changing the indexes in the e.SortedIndexes array.
                    //    // IMPORTANT:
                    //    // To get objects count use e.RenderablePrimitives.Count and not e.SortedIndexes.Length as it may be too big!
                    //};

                    Ab3d.DirectX.DXDiagnostics.IsCollectingStatistics = true;
                }
                else
                {
                    rootBorder.Child = MainViewport3D;
                }

                if (_useTransparencySorter)
                    _transparencySorter = new TransparencySorter(rootModel3DGroup, MainCamera);

                MainCamera.Refresh();

                titleStackPanel.Children.Add(titleTextBlock);
                if (titleInfoControl != null)
                    titleStackPanel.Children.Add(titleInfoControl);

                topStackPanel.Children.Add(titleStackPanel);

                if (subtitleElement != null)
                    topStackPanel.Children.Add(subtitleElement);

                topStackPanel.Children.Add(_reportTextBlock);


                rootGrid.Children.Add(rootBorder);
                rootGrid.Children.Add(topStackPanel);

                return rootGrid;
            }

            private void OnCheckAllBoundingBoxCornersCheckedChanged(object sender, RoutedEventArgs e)
            {
                if (_dxViewportView == null || _dxViewportView.DXScene == null || _checkAllBoundingBoxCornersCheckBox == null)
                    return;

                foreach (var cameraDistanceSortedRenderingQueue in _dxViewportView.DXScene.RenderingQueues.OfType<CameraDistanceSortedRenderingQueue>())
                    cameraDistanceSortedRenderingQueue.CheckAllBoundingBoxCorners = _checkAllBoundingBoxCornersCheckBox.IsChecked ?? false;
            }

            private void CollectStatistics()
            {
                if (_dxViewportView == null || _dxViewportView.DXScene == null || _dxViewportView.DXScene.Statistics == null)
                    return;

                if (_useDXEngineSorting && _noSortPrepareTime == 0)
                {
                    // PrepareRenderTimeMs is used to collect time that is needed to
                    // clear the buffers, initialize render targets and states, sort rendering queues by material or camera distance.
                    // So when _useDXEngineSorting is true, then in the first frame we do not do any sorting yet,
                    // so that we can collect the time that is needed for other tasks except sorting.
                    // After we have that time, we can enable sorting.
                    // This way we will be able to get the time that is needed for sorting only.
                    _noSortPrepareTime = _dxViewportView.DXScene.Statistics.PrepareRenderTimeMs;
                    _dxViewportView.DXScene.IsTransparencySortingEnabled = true;
                }

                _totalPrepareTime += _dxViewportView.DXScene.Statistics.PrepareRenderTimeMs;
                _totalUpdateTime  += _dxViewportView.DXScene.Statistics.UpdateTimeMs;
                _totalDrawTime  += _dxViewportView.DXScene.Statistics.DrawRenderTimeMs;

                _framesCount++;

                // Update report text once a second (check if the second value has changed)
                int frameSecond = DateTime.Now.Second;
                if (frameSecond == _lastFrameSecond)
                    return;


                double sortTime;

                if (_transparencySorter != null)
                    sortTime = _totalSortTime;
                else if (_useDXEngineSorting)
                    sortTime = _totalPrepareTime - _noSortPrepareTime;
                else
                    sortTime = 0;

                string reportText;

                if (_subtitle != null)
                    reportText = _subtitle + Environment.NewLine;
                else
                    reportText = "";

                if (sortTime > 0)
                    reportText += string.Format("Sort time: {0:0.00} ms\r\n", sortTime / _framesCount);
                else
                    reportText += "";

                reportText += string.Format(
                    "Update time: {0:0.00} ms\r\nDraw time: {1:0.00} ms\r\nTOTAL: {2:0.00} ms",
                    _totalUpdateTime / _framesCount,
                    _totalDrawTime / _framesCount,
                    (sortTime + _totalUpdateTime + _totalDrawTime) / _framesCount);

                _reportTextBlock.Text = reportText;

                _framesCount = 0;
                _totalSortTime = 0;
                _totalPrepareTime = 0;
                _totalUpdateTime = 0;
                _totalDrawTime = 0;

                _lastFrameSecond = frameSecond;
            }

            private void OnMainCameraChanged(object sender, CameraChangedRoutedEventArgs e)
            {
                if (_transparencySorter != null)
                {
                    if (_stopwatch == null)
                        _stopwatch = new Stopwatch();

                    _stopwatch.Restart();

                    _transparencySorter.Sort(TransparencySorter.SortingModeTypes.ByCameraDistance);

                    _stopwatch.Stop();
                    _totalSortTime += _stopwatch.Elapsed.TotalMilliseconds;
                }


                if (_isSyncingCamera)
                    return;

                OnCameraChanged(e);
            }

            public void SyncCamera(TargetPositionCamera sourceCamera)
            {
                _isSyncingCamera = true; // Do not trigger CameraChanged event

                MainCamera.BeginInit();

                MainCamera.Heading  = sourceCamera.Heading;
                MainCamera.Attitude = sourceCamera.Attitude;
                MainCamera.Distance = sourceCamera.Distance;
                MainCamera.Offset   = sourceCamera.Offset;

                MainCamera.EndInit();

                _isSyncingCamera = false;
            }

            private Model3DGroup Create3DScene(Point3D center, int xCount, int yCount, int zCount, float modelSize)
            {
                var semiTransparentBrush = new SolidColorBrush(Color.FromArgb(32, 26,  161, 226)); // alpha = 1/8; color: #1aa1e2
                var diffuseMaterial      = new DiffuseMaterial(semiTransparentBrush);
                diffuseMaterial.Freeze(); // This will significantly speed up the creation of objects

                var modelSizeWithMargin = modelSize * 1.2;
                var size = new Size3D(xCount * modelSizeWithMargin, yCount * modelSizeWithMargin, zCount * modelSizeWithMargin);
                var instancedData = InstancedMeshGeometry3DTest.CreateInstancesData(center, size, modelSize, xCount, yCount, zCount, false);

                var model3DGroup = new Model3DGroup();
                var boxMesh3D    = new Ab3d.Meshes.BoxMesh3D(new Point3D(0, 0, 0), new Size3D(1, 1, 1), 1, 1, 1).Geometry;

                for (int i = 0; i < instancedData.Length; i++)
                {
                    var geometryModel3D = new GeometryModel3D(boxMesh3D, diffuseMaterial);
                    geometryModel3D.BackMaterial = diffuseMaterial;
                    geometryModel3D.Transform = new MatrixTransform3D(instancedData[i].World.ToWpfMatrix3D());

                    model3DGroup.Children.Add(geometryModel3D);
                }

                var modelVisual3D = new ModelVisual3D();
                modelVisual3D.Content = model3DGroup;

                MainViewport3D.Children.Add(modelVisual3D);

                return model3DGroup;
            }

            public void StartAnimation()
            {
                MainCamera.StartRotation(15, 0);
            }

            public void StopAnimation()
            {
                MainCamera.StopRotation();
            }

            protected void OnCameraChanged(CameraChangedRoutedEventArgs e)
            {
                if (CameraChanged != null)
                    CameraChanged(this, e);
            }

            public void Dispose()
            {
                if (_dxViewportView != null)
                {
                    _dxViewportView.Dispose();
                    _dxViewportView = null;
                }
            }
        }
    }
}
