using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using Ab3d.Cameras;
using Ab3d.Common.Cameras;
using Ab3d.Controls;
using Ab3d.DirectX;
using Ab3d.DirectX.Client.Diagnostics;
using Ab3d.DirectX.Controls;
using Ab3d.DirectX.Effects;
using Ab3d.DirectX.Materials;
using Ab3d.DirectX.Models;
using Ab3d.Meshes;
using Ab3d.Visuals;
using SharpDX;
using Color = SharpDX.Color;

// NOTE:
// This sample shows many 3D boxes that are defined by MeshObjectNode objects.
// A much more optimal way to show that many 3D boxes is to use object instancing with InstancedMeshGeometryVisual3D,
// but in this sample we need to simulate rendering of a very complex scene with many different SceneNodes objects (each with its own MeshGeometry3D).
//
// The sample can also show many instances of ScreenSpaceLineNode to simulate showing many different 3D lines.
// It would be much better to use a single ScreenSpaceLineNode and set all line positions to that object.

namespace Ab3d.DXEngine.Wpf.Samples.DXEnginePerformance
{
    /// <summary>
    /// Interaction logic for MultiThreadingSample.xaml
    /// </summary>
    public partial class MultiThreadingSample : Page
    {
        private DXViewportView _mainDXViewportView;
        private Viewport3D _mainViewport3D;

        private int _lastSecond = -1;
        private int _framesCount = 0;

        private int _lastFPS;
        private List<double> _drawRenderTimes;
        private List<double> _completeRenderTimes;
        private List<double> _totalRenderTimes;

        private DisposeList _disposables;

        private int _lastSceneGenerationTimeInTicks;

        private SceneNodeVisual3D _sceneNodeVisual3D;

        private PerformanceAnalyzer _performanceAnalyzer;

        public enum ObjectsTypes
        {
            None = 0,
            SingleColorBoxes,
            MultiColorBoxes,
            SingleColorLines,
            MultiColorLines,
            MultiColorBoxesAndLines
        }

        public enum LightingMode
        {
            None = 0,
            AmbientLight,
            DirectionalLight,
            PointLight,
            PointLights16,
        }

        private LightingMode _currentLightingMode;
        private ObjectsTypes _currentObjectsType;

        private Window _parentWindow;
        private int _objectsCount;
        private double _lastTotalRenderTime;


        private int _benchmarkStepFramesCount;
        private DateTime _benchmarkStepStartTime;
        private StringBuilder _benchmarkResults;

        private List<double> _benchmarkTotalRenderTimes;
        private Model3DGroup _lightsGroup;
        private TargetPositionCamera _camera1;
        private MouseCameraController _mouseCameraController;


        public MultiThreadingSample()
        {
            InitializeComponent();

            IsCachingCommandListsInfoControl.InfoText =
@"When checked then Ab3d.DXEngine records all DirectX commands into DirectX CommandLists and
reuses the command lists on next frame if only camera and light properties have changed.

In this case only the new camera data is loaded into the graphics card and then all the previously rendered
CommandLists are replayed. This way the rendering performance is greatly improved because the render time is almost zero 
(this is the time to prepare and process the DirectX commands by the Ab3d.DXEngine and by the DirectX and drives).

Note that command list caching works only for non-transparent standard geometry objects with WpfMaterial or StandardMaterial.

In this sample you can see command list caching in action with observing the DrawRenderTime.
When command list caching is used, then this time is very small because there is no need to set all DirectX
rendering states and invoke DirectX draw calls. The time for that can be observed when caching is disabled.";

            IsBackgroundWaitingInfoControl.InfoText =
@"This CheckBox is enabled only when using DirectXImage PresentationType. In this presentation type the DXEngine needs to wait until the graphics card finishes rendering a frame.
After that the DXEngine can inform the WPF's D3DImage control to invalidate the shared texture.

This CheckBox sets the IsBackgroundWaitingUntilRendered property on the DXViewportView.

When this CheckBox is unchecked, then waiting is done on the main UI thread (this is shown in increased CompleteRenderTime time).
When this CheckBox is checked, then waiting is done in the background tread and when the frame is ready then the D3DImage is invalidated on the main UI thread.";


            _currentLightingMode = LightingMode.DirectionalLight;
            _currentObjectsType = ObjectsTypes.MultiColorBoxes;

            // Create DXViewportView in code behind because we need to support changing PresentationType and
            // this requires that the DXViewportView is recreated from scratch.
            CreateDXViewportView(isDirectXOverlay: PresentationTypeComboBox.SelectedIndex == 0);

            int maxBackgroundThreadsCount = Environment.ProcessorCount - 1;
            ThreadsCountSlider.Maximum = maxBackgroundThreadsCount; 
            ThreadsCountSlider.Value = maxBackgroundThreadsCount;

            ObjectsTypeComboBox.ItemsSource = Enum.GetNames(typeof(ObjectsTypes));
            ObjectsTypeComboBox.SelectedValue = _currentObjectsType.ToString();

            var possibleObjectCounts = new int[] {100, 200, 500, 1000, 2500, 5000, 10000, 20000, 40000, 80000, 160000, 320000};
            ObjectsCountComboBox.ItemsSource = possibleObjectCounts;
            ObjectsCountComboBox.SelectedIndex = Array.IndexOf(possibleObjectCounts, 20000);

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                CreateTestScene(possibleObjectCounts[ObjectsCountComboBox.SelectedIndex]);

                _camera1.StartRotation(45, 0);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

            _drawRenderTimes     = new List<double>();
            _completeRenderTimes = new List<double>();
            _totalRenderTimes    = new List<double>();

            
            // Enable collecting statistics
            DXDiagnostics.IsCollectingStatistics = true;

            StatisticsTitleInfoControl.InfoText =
@"DrawRenderTime: shows time that is needed to draw all objects for a frames (calling DirectX Draw methods and any needed state change methods). Those operations are done in multiple threads so this time is significantly reduced when using multiple threads.

CompleteRenderTime: shows time that is needed to complete the rendering (resolve anti-aliasing, resolve stereoscopic images, calling Present on SwapChain (when using DirectXOverlay) or waiting for the graphics card to finish rendering the frame (when using DirectXImage)). When using DirectXImage this time can increase significantly when using multi-threading because graphics card cannot render as fast as the draw commands are issued.

TotalTimeTextBlock: shows time that is spend in DXEngine to render one frame. Based on this time a theoretical FPS (frames per second) is calculated. It shows how many frames per second could be show when there were no other limitations (for example WPF's frames limit).

WPF FPS: shows number of frames per second in this WPF application (WPF has a cap on 60 frames per second).";


            this.Loaded += delegate(object sender, RoutedEventArgs args)
            {
                _parentWindow = Window.GetWindow(this);

                if (_parentWindow != null)
                    _parentWindow.PreviewKeyDown += ParentWindowOnPreviewKeyDown;
            };

            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                if (_disposables != null)
                {
                    _disposables.Dispose();
                    _disposables = null;
                }

                if (_parentWindow != null)
                    _parentWindow.PreviewKeyDown -= ParentWindowOnPreviewKeyDown;

                DisposeDXViewportView();
            };
        }

        private void DisposeDXViewportView()
        {
            if (_mainDXViewportView == null)
                return;

            if (_camera1 != null)
            {
                _camera1.StopRotation();
                RootGrid.Children.Remove(_camera1);

                _camera1 = null;
            }

            if (_mouseCameraController != null)
            {
                RootGrid.Children.Remove(_mouseCameraController);
                _mouseCameraController = null;
            }


            _mainDXViewportView.SceneRendered -= MainDxViewportViewOnSceneRendered;

            _mainDXViewportView.Dispose();
            _mainDXViewportView = null;
        }

        private void CreateDXViewportView(bool isDirectXOverlay)
        {
            _mainViewport3D = new Viewport3D();

            _lightsGroup = new Model3DGroup();
            _mainViewport3D.Children.Add(_lightsGroup.CreateModelVisual3D());

            UpdateLightingMode();

            _camera1 = new TargetPositionCamera()
            {
                TargetPosition   = new Point3D(0, 0, 0),
                Heading          = 30,
                Attitude         = -10,
                Distance         = 1500,
                ShowCameraLight  = ShowCameraLightType.Never,
                TargetViewport3D = _mainViewport3D
            };

            _mouseCameraController = new MouseCameraController()
            {
                RotateCameraConditions = MouseCameraController.MouseAndKeyboardConditions.LeftMouseButtonPressed,
                MoveCameraConditions   = MouseCameraController.MouseAndKeyboardConditions.LeftMouseButtonPressed | MouseCameraController.MouseAndKeyboardConditions.ControlKey,
                EventsSourceElement    = ViewportBorder,
                TargetCamera           = _camera1
            };

            RootGrid.Children.Add(_camera1);
            RootGrid.Children.Add(_mouseCameraController);


            _mainDXViewportView = new DXViewportView(_mainViewport3D)
            {
                BackgroundColor  = Colors.White,
                PresentationType = isDirectXOverlay ? DXView.PresentationTypes.DirectXOverlay : DXView.PresentationTypes.DirectXImage
            };

            var maxBackgroundThreadsCount = (int)ThreadsCountSlider.Value;
            _mainDXViewportView.DXSceneDeviceCreated += delegate(object sender, EventArgs args)
            {
                _mainDXViewportView.DXScene.MaxBackgroundThreadsCount = maxBackgroundThreadsCount;
                _mainDXViewportView.DXScene.IsCachingCommandLists = IsCachingCommandListsCheckBox.IsChecked ?? false;
            };

            _mainDXViewportView.SceneRendered += MainDxViewportViewOnSceneRendered;

            ViewportBorder.Child = _mainDXViewportView;

            //_camera1.StartRotation(40, 0);


            // Notify MainWindow about a new MainDXViewportView - so we can open DiagnosticsWindow
            if (_parentWindow != null)
            {
                ((MainWindow) _parentWindow).UnsubscribeLastShownDXViewportView();
                ((MainWindow) _parentWindow).SubscribeDXViewportView(_mainDXViewportView);

                ((MainWindow) _parentWindow).MaxBackgroundThreadsCount = maxBackgroundThreadsCount; // prevent setting MaxBackgroundThreadsCount from the setting dialog
            }
        }

        private void ParentWindowOnPreviewKeyDown(object sender, KeyEventArgs args)
        {
            if (_mainDXViewportView.DXScene == null)
                return;

            if (args.Key == Key.Return || args.Key == Key.Enter)
            {
                IsCachingCommandListsCheckBox.IsChecked = !(IsCachingCommandListsCheckBox.IsChecked ?? false);
                args.Handled = true;
            }
            else if (args.Key == Key.Down)
            {
                if (ThreadsCountSlider.Value > 0)
                    ThreadsCountSlider.Value --; // The MainDXViewportView.DXScene.MaxBackgroundThreadsCount will be updated in the ValueChanged handler

                args.Handled = true;
            }
            else if (args.Key == Key.Up)
            {
                if (ThreadsCountSlider.Value < ThreadsCountSlider.Maximum)
                    ThreadsCountSlider.Value ++; // The MainDXViewportView.DXScene.MaxBackgroundThreadsCount will be updated in the ValueChanged handler

                args.Handled = true;
            }
            else if (args.Key == Key.PageDown)
            {
                if (ObjectsCountComboBox.SelectedIndex > 0)
                    ObjectsCountComboBox.SelectedIndex--;

                args.Handled = true;
            }
            else if (args.Key == Key.PageUp)
            {
                int eventAge = Math.Abs(args.Timestamp - _lastSceneGenerationTimeInTicks);
                if (eventAge > 0 && // prevent increasing the size too much (if user clicks PageUp while models are being created)
                    ObjectsCountComboBox.SelectedIndex < ObjectsCountComboBox.Items.Count)
                {
                    ObjectsCountComboBox.SelectedIndex++;
                    _lastSceneGenerationTimeInTicks = Environment.TickCount;
                }

                args.Handled = true;
            }

            UpdateStatistics();
        }

        private void CreateTestScene(int totalModelsCount)
        {
            Mouse.OverrideCursor = Cursors.Wait;

            // Creating Model3DGroup takes too much time when a lot of objects are created.
            // Therefore we are creating SceneNodes (MeshObjectNode) directly.

            //var model3DGroup = CreateModel3DGroup(boxMesh, new Point3D(0, 0, 0), new Size3D(400, 100, 400), 5, 40, 40, 40);
            //var model3DGroup = CreateModel3DGroup(boxMesh, new Point3D(0, 0, 0), new Size3D(400, 200, 400), 5, 40, 20, 40);
            //var model3DGroup = CreateModel3DGroup(boxMesh, new Point3D(0, 0, 0), new Size3D(400, 200, 400), 10, 10, 5, 10);
            //modelsCount = model3DGroup.Children.Count;

            //MainViewport.Children.Add(model3DGroup.CreateModelVisual3D());

            try
            {
                if (_disposables != null)
                    _disposables.Dispose();

                if (_sceneNodeVisual3D != null)
                {
                    _mainViewport3D.Children.Remove(_sceneNodeVisual3D);

                    _sceneNodeVisual3D.SceneNode.Dispose();
                    _sceneNodeVisual3D = null;
                }


                _disposables = new DisposeList();

                var boxMesh = new BoxMesh3D(new Point3D(0, 0, 0), new Size3D(1, 1, 1), 1, 1, 1).Geometry;

                int modelsXZCount = totalModelsCount < 2500 ? 10 : 50;
                int modelsYCount = totalModelsCount / (modelsXZCount * modelsXZCount);

                bool useSingleColor = _currentObjectsType == ObjectsTypes.SingleColorBoxes || _currentObjectsType == ObjectsTypes.SingleColorLines;

                SceneNode sceneNode;

                if (_currentObjectsType == ObjectsTypes.MultiColorBoxes || _currentObjectsType == ObjectsTypes.SingleColorBoxes)
                {
                    sceneNode = CreateBoxSceneNodes(boxMesh, new Point3D(0, 0, 0), new Size3D(500, modelsYCount * 10, 500), 5, useSingleColor, modelsXZCount, modelsYCount, modelsXZCount);
                }
                else if (_currentObjectsType == ObjectsTypes.MultiColorLines || _currentObjectsType == ObjectsTypes.SingleColorLines)
                {
                    sceneNode = CreateLineSceneNodes(new Point3D(0, 0, 0), new Size3D(500, modelsYCount * 10, 500), useSingleColor, modelsXZCount, modelsYCount, modelsXZCount, _disposables);
                }
                else if (_currentObjectsType == ObjectsTypes.MultiColorBoxesAndLines)
                {
                    sceneNode = new SceneNode();
                    var sceneNode1 = CreateBoxSceneNodes(boxMesh, new Point3D(0, -modelsYCount * 2.5 - 10, 0), new Size3D(500, modelsYCount * 5, 500), 5, useSingleColor, modelsXZCount, modelsYCount / 2, modelsXZCount);
                    var sceneNode2 = CreateLineSceneNodes(        new Point3D(0,  modelsYCount * 2.5 + 10, 0), new Size3D(500, modelsYCount * 5, 500),    useSingleColor, modelsXZCount, modelsYCount / 2, modelsXZCount, _disposables);

                    sceneNode.AddChild(sceneNode1);
                    sceneNode.AddChild(sceneNode2);
                }
                else
                {
                    sceneNode = null;
                }

                if (sceneNode == null)
                    return;

                _objectsCount = totalModelsCount;

                _sceneNodeVisual3D = new SceneNodeVisual3D(sceneNode);
                _mainViewport3D.Children.Add(_sceneNodeVisual3D);

                _mainDXViewportView.Refresh();
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void MainDxViewportViewOnSceneRendered(object sender, EventArgs e)
        {
            if (_performanceAnalyzer != null)
                return; // Do not collect statistics there while running benchmark

            RenderingStatistics renderingStatistics;
            if (_mainDXViewportView.DXScene != null)
                renderingStatistics = _mainDXViewportView.DXScene.Statistics;
            else
                renderingStatistics = null;


            int second = DateTime.Now.Second;

            if (second != _lastSecond)
            {
                _lastFPS = _framesCount;
                UpdateStatistics();


                _framesCount = 0;
                _drawRenderTimes.Clear();
                _completeRenderTimes.Clear();
                _totalRenderTimes.Clear();

                _lastSecond = second;
            }
            else
            {
                _framesCount++;

                if (renderingStatistics != null)
                {
                    _drawRenderTimes.Add(renderingStatistics.DrawRenderTimeMs);
                    _completeRenderTimes.Add(renderingStatistics.CompleteRenderTimeMs);
                    _totalRenderTimes.Add(renderingStatistics.TotalRenderTimeMs);
                }
            }
        }

        private void UpdateStatistics()
        {
            if (_totalRenderTimes == null || _totalRenderTimes.Count == 0 || 
                _performanceAnalyzer != null) // when running benchmark test we do not need to update statistics because they are not shown
            {
                return;
            }

            DrawRenderTimeTextBlock.Text = string.Format("{0:0.00} ms", _drawRenderTimes.Average());
            CompleteRenderTimeTextBlock.Text = string.Format("{0:0.00} ms", _completeRenderTimes.Average());

            _lastTotalRenderTime = _totalRenderTimes.Average();

            TotalTimeTextBlock.Text = string.Format("{0:0.00} ms => {1:0.0} theoretical FPS", _lastTotalRenderTime, 1000.0 / _lastTotalRenderTime);
            FpsTextBlock.Text = _lastFPS.ToString();
        }

        private void UpdateLightingMode()
        {
            _currentLightingMode = (LightingMode) (int) (LightsComboBox.SelectedIndex);
            SetLightingMode(_currentLightingMode);
        }

        private void SetLightingMode(LightingMode lightingMode)
        {
            _lightsGroup.Children.Clear();

            if (lightingMode == LightingMode.None)
                return;


            var ambientLight = new AmbientLight();

            if (lightingMode == LightingMode.AmbientLight)
            {
                // 100% ambient light => using SolidColorShader
                ambientLight.Color = Colors.White;
                _lightsGroup.Children.Add(ambientLight);
            }
            else if (lightingMode == LightingMode.DirectionalLight)
            {
                // DirectionalLight + ambient light => using DirectionalLightShader
                ambientLight.Color = Colors.DimGray;
                _lightsGroup.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-1, -1, 1)));
            }
            else if (lightingMode == LightingMode.PointLight)
            {
                // PointLight + ambient light => using SuperShader
                ambientLight.Color = Colors.DimGray;
                _lightsGroup.Children.Add(new PointLight(Colors.White, new Point3D(0, 1000, 0)));
            }
            else if (lightingMode == LightingMode.PointLights16)
            {
                // PointLight + ambient light => using SuperShader
                ambientLight.Color = Colors.DimGray;

                for (int x = 0; x < 4; x++)
                {
                    for (int y = 0; y < 4; y++)
                    {
                        _lightsGroup.Children.Add(new PointLight(Colors.White, new Point3D(-300 + x * 200, 1000, -300 + y * 200)));
                    }
                }
            }

            _lightsGroup.Children.Add(ambientLight);
        }

        public static SceneNode CreateBoxSceneNodes(MeshGeometry3D mesh, Point3D center, Size3D size, float modelScaleFactor, bool useSingleColor, int xCount, int yCount, int zCount)
        {
            var rootSceneNode = new SceneNode();

            var dxMeshGeometry3D = new DXMeshGeometry3D(mesh);

            float xStep = (float)(size.X / xCount);
            float yStep = (float)(size.Y / yCount);
            float zStep = (float)(size.Z / zCount);

            var singleColorStandardMaterial = new StandardMaterial()
            {
                DiffuseColor = Colors.Orange.ToColor3()
            };

            for (int z = 0; z < zCount; z++)
            {
                float zPos = (float)(center.Z - (size.Z / 2.0) + (z * zStep));
                float zPercent = (float)z / (float)zCount;

                for (int y = 0; y < yCount; y++)
                {
                    float yPos = (float)(center.Y - (size.Y / 2.0) + (y * yStep));
                    float yPercent = (float)y / (float)yCount;

                    for (int x = 0; x < xCount; x++)
                    {
                        float xPos = (float)(center.X - (size.X / 2.0) + (x * xStep));

                        var matrix = new SharpDX.Matrix(modelScaleFactor, 0, 0, 0,
                                                        0, modelScaleFactor, 0, 0,
                                                        0, 0, modelScaleFactor, 0,
                                                        xPos, yPos, zPos, 1);

                        StandardMaterial usedMaterial;

                        if (useSingleColor)
                        {
                            // Using single color does not improve performance because for each rendered box we need to
                            // update constant buffer with different transformation matrix. 
                            // However, the performance would be improved by almost 30% if all the boxes would have the
                            // same color and transformation. But then we would need to provide many different meshes
                            // and this would greatly decrease initialization time and also slightly decrease performance.
                            // See CreateLineSceneNodes for sample with lines when single color improves performance.
                            usedMaterial = singleColorStandardMaterial;
                        }
                        else
                        {
                            usedMaterial = new StandardMaterial()
                            {
                                DiffuseColor = new Color3((float) x / (float) xCount, yPercent, zPercent)
                            };
                        }

                        // NOTE:
                        // This sample shows many 3D boxes that are defined by MeshObjectNode objects.
                        // A much more optimal way to show that many 3D boxes is to use object instancing with InstancedMeshGeometryVisual3D,
                        // but in this sample we need to simulate rendering of a very complex scene with many different SceneNodes objects (each with its own MeshGeometry3D).

                        var meshObjectNode = new Ab3d.DirectX.MeshObjectNode(dxMeshGeometry3D, usedMaterial);
                        meshObjectNode.Transform = new Transformation(matrix);

                        rootSceneNode.AddChild(meshObjectNode);
                    }
                }
            }

            return rootSceneNode;
        }

        public static SceneNode CreateLineSceneNodes(Point3D center, Size3D size, bool useSingleColor, int xCount, int yCount, int zCount, DisposeList disposables)
        {
            var rootSceneNode = new SceneNode();

            float xStep = (float)(size.X / xCount);
            float yStep = (float)(size.Y / yCount);
            float zStep = (float)(size.Z / zCount);

            float xHalfLineSize = xStep * 0.3f;
            //float yHalfLineSize = xStep * 0.3f;
            float zHalfLineSize = xStep * 0.3f;

            var singleColorLineMaterial = new LineMaterial()
            {
                LineColor = Colors.Orange.ToColor4(),
                LineThickness = 2
            };

            if (disposables != null)
                disposables.Add(singleColorLineMaterial);



            for (int z = 0; z < zCount; z++)
            {
                float zPos = (float)(center.Z - (size.Z / 2.0) + (z * zStep));
                float zPercent = (float)z / (float)zCount;

                for (int y = 0; y < yCount; y++)
                {
                    float yPos = (float)(center.Y - (size.Y / 2.0) + (y * yStep));
                    float yPercent = (float)y / (float)yCount;

                    for (int x = 0; x < xCount; x++)
                    {
                        float xPos = (float)(center.X - (size.X / 2.0) + (x * xStep));

                        var linePositions = new Vector3[]
                        {
                            new Vector3(xPos - xHalfLineSize, yPos, zPos - zHalfLineSize),
                            new Vector3(xPos + xHalfLineSize, yPos, zPos + zHalfLineSize),
                            new Vector3(xPos - xHalfLineSize, yPos, zPos + zHalfLineSize),
                            new Vector3(xPos + xHalfLineSize, yPos, zPos - zHalfLineSize),
                        };

                        LineMaterial usedLineMaterial;

                        if (useSingleColor)
                        {
                            // Using single color improved performance by 30% because we do not need to update per-object constant buffer for each line.
                            usedLineMaterial = singleColorLineMaterial;
                        }
                        else
                        {
                            usedLineMaterial = new LineMaterial()
                            {
                                LineColor     = new Color4((float) x / (float) xCount, yPercent, zPercent, 1),
                                LineThickness = 2
                            };

                            if (disposables != null)
                                disposables.Add(usedLineMaterial);
                        }

                        // NOTE:
                        // The sample can also show many instances of ScreenSpaceLineNode to simulate showing many different 3D lines.
                        // It would be much better to use a single ScreenSpaceLineNode and set all line positions to that object.

                        var screenSpaceLineNode = new ScreenSpaceLineNode(linePositions, isLineStrip: false, isLineClosed: false, lineMaterial: usedLineMaterial);

                        rootSceneNode.AddChild(screenSpaceLineNode);
                    }
                }
            }

            return rootSceneNode;
        }

        public static Model3DGroup CreateModel3DGroup(MeshGeometry3D mesh, Point3D center, Size3D size, float modelScaleFactor, int xCount, int yCount, int zCount)
        {
            var model3DGroup = new Model3DGroup();

            float xStep = (float)(size.X / xCount);
            float yStep = (float)(size.Y / yCount);
            float zStep = (float)(size.Z / zCount);

            int i = 0;
            for (int z = 0; z < zCount; z++)
            {
                float zPos = (float)(center.Z - (size.Z / 2.0) + (z * zStep));

                for (int y = 0; y < yCount; y++)
                {
                    float yPos = (float)(center.Y - (size.Y / 2.0) + (y * yStep));

                    float yPercent = (float)y / (float)yCount;

                    for (int x = 0; x < xCount; x++)
                    {
                        float xPos = (float)(center.X - (size.X / 2.0) + (x * xStep));

                        var matrix = new Matrix3D(modelScaleFactor, 0, 0, 0,
                            0, modelScaleFactor, 0, 0,
                            0, 0, modelScaleFactor, 0,
                            xPos, yPos, zPos, 1);

                        var material = new DiffuseMaterial(new SolidColorBrush(System.Windows.Media.Color.FromRgb((byte)(255 * (float)x / (float)xCount), 255, (byte)(yPercent * 255))));

                        var model3D = new GeometryModel3D(mesh, material);
                        model3D.Transform = new MatrixTransform3D(matrix);

                        model3DGroup.Children.Add(model3D);

                        i++;
                    }
                }
            }

            return model3DGroup;
        }

        private void ThreadsCountSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_mainDXViewportView != null && _mainDXViewportView.DXScene != null)
            {
                _mainDXViewportView.DXScene.MaxBackgroundThreadsCount = (int) ThreadsCountSlider.Value;
                _mainDXViewportView.Refresh();
            }
        }

        private void OnIsCachingCommandListsCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_mainDXViewportView != null && _mainDXViewportView.DXScene != null)
            {
                _mainDXViewportView.DXScene.IsCachingCommandLists = IsCachingCommandListsCheckBox.IsChecked ?? false;
                _mainDXViewportView.Refresh();
            }
        }
        
        private void OnIsWaitingInBackgroundCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_mainDXViewportView != null)
            {
                _mainDXViewportView.IsWaitingInBackgroundUntilRendered = IsWaitingInBackgroundCheckBox.IsChecked ?? false;
                _mainDXViewportView.Refresh();
            }
        }

        private void ObjectsCountComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            int modelsCount = (int) ObjectsCountComboBox.SelectedItem;
            CreateTestScene(modelsCount);
        }


        private void ObjectsTypeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            string selectedObjectTypesText = (string)ObjectsTypeComboBox.SelectedValue;
            _currentObjectsType = (ObjectsTypes)Enum.Parse(typeof(ObjectsTypes), selectedObjectTypesText);

            CreateTestScene(_objectsCount);
        }

        private void PresentationTypeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            bool isDirectXOverlay = PresentationTypeComboBox.SelectedIndex == 0;

            var isCameraRotating = _camera1.IsRotating;

            // Recreate the DXViewportView and the scene
            DisposeDXViewportView();
            CreateDXViewportView(isDirectXOverlay);
            CreateTestScene(_objectsCount);

            IsWaitingInBackgroundCheckBox.IsEnabled = !isDirectXOverlay;

            if (isCameraRotating)
                _camera1.StartRotation(45, 0);
        }

        private void LightsComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            UpdateLightingMode();
        }

        private void StartStopTestButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (IsCachingCommandListsCheckBox.IsChecked ?? false)
            {
                var result = MessageBox.Show("Uncheck the 'IsCachingCommandLists' CheckBox before starting the benchmark?\r\n\r\nIf this is not done, then there is almost no work to be done by DXEngine and the benchmark will not show any interesting results.", "Starting benchmark", MessageBoxButton.YesNo, MessageBoxImage.Stop);
                if (result == MessageBoxResult.Yes)
                    IsCachingCommandListsCheckBox.IsChecked = false;
            }

            if (_performanceAnalyzer == null)
            {
                if (OptionsGrid.Visibility == Visibility.Visible)
                {
                    // Start the test
                    StartBenchmark();
                }
                else
                {
                    // Hide test results
                    StartStopTestButton.Content = "Start benchmark";

                    OptionsGrid.Visibility   = Visibility.Visible;
                    InfoTextBlock.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                // Abort the test
                StopBenchmark();
            }
        }

        private void StartBenchmark()
        {
            if (_mainDXViewportView.DXScene == null)
                return; // Probably WPF 3D rendering

            StartStopTestButton.Content = "Abort benchmark";

            OptionsGrid.Visibility   = Visibility.Collapsed;
            InfoTextBlock.Visibility = Visibility.Visible;

            _mouseCameraController.IsEnabled = false;

            _totalRenderTimes.Clear();

            _performanceAnalyzer = new PerformanceAnalyzer(_mainDXViewportView);

            
            // get only system info
            string systemInfo = _performanceAnalyzer.GetResultsText(addSystemInfo: true, addDXEngineInfo: true, addGarbageCollectionsInfo: false, addTotals: false, addStateChangesStatistics: false, addFirstFrameStatisticsWhenAvailable: false, addRenderingStatistics: false, addMultiThreadingStatistics: false);

            int maxThreadsCount = (int)ThreadsCountSlider.Maximum;
            InfoTextBlock.Text = $"Starting benchmark ({_mainDXViewportView.PresentationType} with {_objectsCount} objects)\r\nwith changing MaxBackgroundThreadsCount from 0 to {ThreadsCountSlider.Maximum:0}.\r\n\r\n{systemInfo}\r\n====================\r\nStarted benchmark 1/{maxThreadsCount+1} with disabled multi-threading ...\r\n\r\n";

            InfoTextBlock.UpdateLayout();
            InfoTextBlock.ScrollToEnd();


            SetLightingMode(LightingMode.DirectionalLight);
            ThreadsCountSlider.Value = 0;

            _benchmarkResults = new StringBuilder();
            _benchmarkTotalRenderTimes = new List<double>();

            _benchmarkStepFramesCount = 0;
            _mainDXViewportView.SceneRendered += OnBenchmarkSceneRendered;
        }

        private void StopBenchmark()
        {
            _performanceAnalyzer = null; // This will also signal that we are not executing the benchmark any more
            StartStopTestButton.Content = "Hide results";

            _mouseCameraController.IsEnabled = true;

            _mainDXViewportView.SceneRendered -= OnBenchmarkSceneRendered;


            _framesCount = 0;
            _drawRenderTimes.Clear();
            _completeRenderTimes.Clear();
            _totalRenderTimes.Clear();


            // Show results:
            AddInfoText("\r\nBenchmark completed\r\n");

            if (_benchmarkResults != null && _benchmarkTotalRenderTimes.Count > 1)
            {
                AddInfoText("PERFORMANCE SUMMARY:");
                AddInfoText("Background threads count and total render time:");

                for (int i = 0; i < _benchmarkTotalRenderTimes.Count; i++)
                {
                    string infoText = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0,2}  {1,5:0.00} ms", i, _benchmarkTotalRenderTimes[i]);

                    if (i > 0)
                        infoText += string.Format(System.Globalization.CultureInfo.InvariantCulture, " ({0:0.00} times faster)", _benchmarkTotalRenderTimes[0] / _benchmarkTotalRenderTimes[i]);

                    AddInfoText(infoText);
                }
            }
        }
        
        private void OnBenchmarkSceneRendered(object sender, EventArgs e)
        {
            var now = DateTime.Now;

            if (_benchmarkStepFramesCount == 0)
            {
                // Starting one benchmark step
                _performanceAnalyzer.StartCollectingStatistics();
                _benchmarkStepStartTime = now;
            }
            else if (_mainDXViewportView.DXScene.Statistics != null) // just in case check that Statistics is not null (this should not happen because we set DXDiagnostics.IsCollectingStatistics to true)
            {
                // else collect TotalRenderTimeMs
                _totalRenderTimes.Add(_mainDXViewportView.DXScene.Statistics.TotalRenderTimeMs);
            }


            // Complete one benchmark step after 3 seconds or after 100 frames
            if ((now - _benchmarkStepStartTime).TotalSeconds > 3 || _benchmarkStepFramesCount > 100)
            {
                _performanceAnalyzer.StopCollectingStatistics();

                double totalRenderTimes;

                if (_totalRenderTimes.Count > 0)
                {
                    totalRenderTimes = _totalRenderTimes.Average();
                    _totalRenderTimes.Clear();
                }
                else
                {
                    totalRenderTimes = 0;
                }

                _benchmarkTotalRenderTimes.Add(totalRenderTimes);

                int threadsCount = _mainDXViewportView.DXScene.MaxBackgroundThreadsCount;
                
                string resultsText = _performanceAnalyzer.GetResultsText(addSystemInfo: false,
                                                                         addDXEngineInfo: false,
                                                                         addGarbageCollectionsInfo: false,
                                                                         addTotals: true,
                                                                         addStateChangesStatistics: false,
                                                                         addFirstFrameStatisticsWhenAvailable: false,
                                                                         addRenderingStatistics: true,
                                                                         addMultiThreadingStatistics: true);

                AddInfoText(resultsText);

                _benchmarkStepFramesCount = 0;

                if (ThreadsCountSlider.Value < ThreadsCountSlider.Maximum)
                {
                    int maxThreadsCount = (int)ThreadsCountSlider.Maximum;

                    AddInfoText(string.Format("====================\r\nStarted benchmark {0}/{1} with MaxBackgroundThreadsCount = {2} ...\r\n", threadsCount + 2, maxThreadsCount + 1, threadsCount + 1));
                    ThreadsCountSlider.Value += 1; // Increase threads count
                }
                else
                {
                    StopBenchmark();
                }
            }

            _benchmarkStepFramesCount++;
        }

        private void AddInfoText(string text)
        {
            InfoTextBlock.Text += text + Environment.NewLine;
            InfoTextBlock.ScrollToEnd();
        }

        private void StartStopCameraRotationButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_camera1.IsRotating)
            {
                _camera1.StopRotation();
                StartStopCameraRotationButton.Content = "Start camera rotation";
            }
            else
            {
                _camera1.StartRotation(45, 0);
                StartStopCameraRotationButton.Content = "Stop camera rotation";
            }
        }
    }
}
