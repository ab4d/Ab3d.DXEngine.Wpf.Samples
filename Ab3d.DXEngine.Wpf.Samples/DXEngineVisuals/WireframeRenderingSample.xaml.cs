using System;
using System.Collections.Generic;
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
using Ab3d.DirectX.Client.Settings;
using Ab3d.DirectX.Controls;
using Ab3d.DirectX.Effects;
using Ab3d.Models;
using Ab3d.Visuals;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    /// <summary>
    /// Interaction logic for WireframeRenderingSample.xaml
    /// </summary>
    public partial class WireframeRenderingSample : Page
    {
        private enum WireframeRenderingTypes
        {
            WireframeVisual3D,
            WireframeLines,
            CustomRenderObjectsRenderingStep
        }

        private TestScene[] _testScenes;

        public WireframeRenderingSample()
        {
            InitializeComponent();

            LineThicknessComboBox.ItemsSource = new double[] { 0.1, 0.2, 0.5, 1.0, 2.0 };
            LineThicknessComboBox.SelectedItem = 0.5;


            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                var assimpWpfImporter = new Ab3d.Assimp.AssimpWpfImporter();

                string fileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\Models\house with trees.3DS");
                var readModel3D = assimpWpfImporter.ReadModel3D(fileName);

                CreateScenes(readModel3D);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                if (_testScenes != null)
                {
                    foreach (var testScene in _testScenes)
                        testScene.Dispose();
                }
            };
        }

        private void CreateScenes(Model3D readModel3D)
        {
            _testScenes = new TestScene[3];

            _testScenes[0] = new TestScene("Manually created wireframe lines", WireframeRenderingTypes.WireframeLines, readModel3D, subtitle: 
@"Create a single line positions collection and render all lines with one draw call per line color.
Pros and cons:
+ best performance (only one draw call per line color)
+ exact control which objects / line positions are rendered
- require additional memory for line positions
- more work for user");

            _testScenes[1] = new TestScene("Custom RenderObjectsRenderingStep", WireframeRenderingTypes.CustomRenderObjectsRenderingStep, readModel3D, subtitle:
@"Create custom RenderObjectsRenderingStep that will render the same scene again but with rendering all objects as wireframe.
Pros and cons:
+ no additional memory required
- worse performance because of more draw calls (all objects are rendered again)
- harder to filter which objects to render");

            _testScenes[2] = new TestScene("WireframeVisual3D", WireframeRenderingTypes.WireframeVisual3D, readModel3D, subtitle:
@"Using WireframeVisual3D from Ab3d.PowerToys.
Pros and cons:
+ the easiest to use (using different WireframeType)
+ also works without Ab3d.DXEngine
- worse performance because of more draw calls (all objects are rendered again)
- very hard to filter which object to render");

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

            if (testScene == null)
                return;

            var changedCamera = testScene.MainCamera;

            for (var i = 0; i < _testScenes.Length; i++)
            {
                if (_testScenes[i] == testScene)
                    continue;

                _testScenes[i].SyncCamera(changedCamera);
            }
        }

        private void OnWireframeSettingsChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            bool showSoldObject = ShowSolidObjectCheckBox.IsChecked ?? false;
            bool showWireframe = ShowWireframeCheckBox.IsChecked ?? false;
            bool useModelColor = ModelLineColorCheckBox.IsChecked ?? false;

            var lineThickness = (double)LineThicknessComboBox.SelectedItem;

            foreach (var testScene in _testScenes)
                testScene.UpdateWireframeSettings(showSoldObject, showWireframe, useModelColor, lineThickness);
        }


        class TestScene
        {
            private readonly string _title;
            private readonly WireframeRenderingTypes _wireframeRenderingType;
            private readonly Model3D _model3D;
            private readonly string _subtitle;

            private bool _isSyncingCamera;
            private DXViewportView _dxViewportView;
            private MouseCameraController _mouseCameraController;
            private ModelColorLineEffect _modelColorLineEffect;
            private ContentVisual3D _wireframeLinesContentVisual3D;
            private WireframeVisual3D _wireframeVisual3D;

            private MultiLineVisual3D _multiLineVisual3D;
            private ContentVisual3D _solidModelVisual3D;

            private RenderObjectsRenderingStep _renderWireframeObjectsRenderingStep;

            private Dictionary<Color, Point3DCollection> _wireframePositionsByColor;
            

            private bool _showSoldObject;
            private bool _showWireframe;
            private bool _useModelColor;
            private double _lineThickness;
            private bool _isInitialized;

            public Viewport3D MainViewport3D { get; private set; }

            public TargetPositionCamera MainCamera { get; private set; }
            
            public event BaseCamera.CameraChangedRoutedEventHandler CameraChanged;

            public TestScene(string title, WireframeRenderingTypes wireframeRenderingType, Model3D model3D, string subtitle = null)
            {
                _title                  = title;
                _wireframeRenderingType = wireframeRenderingType;
                _model3D                = model3D;
                _subtitle               = subtitle;

                _showSoldObject = false;
                _showWireframe  = true;
                _useModelColor  = false;
                _lineThickness  = 0.5;
            }

            public Grid CreateScene()
            {
                var rootGrid = new Grid();

                var textBlock = new TextBlock()
                {
                    Text = _title,
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(10, 5, 10, 10),
                    VerticalAlignment = VerticalAlignment.Top,
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                var rootBorder = new Border()
                {
                    Background = Brushes.Transparent,
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(2, 2, 2, 2),
                    Margin = new Thickness(2, 0, 2, 0),
                    SnapsToDevicePixels = true,
                    UseLayoutRounding = true
                };

                MainViewport3D = new Viewport3D();

                MainCamera = new TargetPositionCamera()
                {
                    TargetViewport3D = MainViewport3D,
                    TargetPosition = new Point3D(0, 50, 0),
                    Heading = 30,
                    Attitude = -20,
                    Distance = 500,
                    ShowCameraLight = ShowCameraLightType.Always
                };

                MainCamera.CameraChanged += OnMainCameraChanged;


                _mouseCameraController = new MouseCameraController()
                {
                    TargetCamera           = MainCamera,
                    EventsSourceElement    = rootBorder,
                    RotateCameraConditions = MouseCameraController.MouseAndKeyboardConditions.LeftMouseButtonPressed,
                    MoveCameraConditions   = MouseCameraController.MouseAndKeyboardConditions.Disabled // disable mouse move
                };

                _dxViewportView = new DXViewportView(MainViewport3D)
                {
                    BackgroundColor = Colors.LightGray,
                    GraphicsProfiles = DXEngineSettings.Current.GraphicsProfiles // Use graphic profile that is defined in the user settings dialog
                };

                _dxViewportView.DXSceneInitialized += delegate(object sender, EventArgs args)
                {
                    Create3DScene();
                };

                rootBorder.Child = _dxViewportView;

                MainCamera.Refresh();

                rootGrid.Children.Add(rootBorder);
                rootGrid.Children.Add(textBlock);


                if (_subtitle != null)
                {
                    var subtitleTextBlock = new TextBlock()
                    {
                        Text = _subtitle,
                        FontSize = 12,
                        Margin = new Thickness(10, 30, 10, 10),
                        VerticalAlignment = VerticalAlignment.Top,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        TextWrapping = TextWrapping.Wrap
                    };

                    rootGrid.Children.Add(subtitleTextBlock);
                }

                return rootGrid;
            }

            public void SyncCamera(TargetPositionCamera sourceCamera)
            {
                _isSyncingCamera = true; // Do not trigger CameraChanged event

                MainCamera.BeginInit();

                MainCamera.Heading = sourceCamera.Heading;
                MainCamera.Attitude = sourceCamera.Attitude;
                MainCamera.Distance = sourceCamera.Distance;
                MainCamera.Offset = sourceCamera.Offset;

                MainCamera.EndInit();

                _isSyncingCamera = false;
            }

            private void Create3DScene()
            {
                var model3D = _model3D.Clone(); // Clone the model so each test scene has its own instance of the test model

                MainViewport3D.Children.Clear();
                
                if (_wireframeRenderingType == WireframeRenderingTypes.WireframeLines)
                {
                    // Show 3D lines with generating line positions for wireframe lines and then using MultiLineVisual3D to show the lines.
                    //
                    // It is also possible to show 3D lines with using DXEngine's ScreenSpaceLineNode.
                    // See DXEngineAdvanced / ScreenSpaceLineNodeSample sample to see how to do that.

                    var wireframeLinePositions = WireframeFactory.CreateWireframeLinePositions(model3D, null, usePolygonIndices: false, removedDuplicates: true);

                    _wireframeLinesContentVisual3D = new ContentVisual3D()
                    {
                        IsVisible = _showWireframe
                    };

                    MainViewport3D.Children.Add(_wireframeLinesContentVisual3D);


                    _multiLineVisual3D = new Ab3d.Visuals.MultiLineVisual3D()
                    {
                        Positions     = wireframeLinePositions,
                        LineColor     = Colors.Black,
                        LineThickness = _lineThickness
                    };

                    // Set depth bias: see LineDepthBiasSample for more info
                    _multiLineVisual3D.SetDXAttribute(DXAttributeType.LineDepthBias, 0.1);
                    _multiLineVisual3D.SetDXAttribute(DXAttributeType.LineDynamicDepthBiasFactor, 0.02);


                    if (_useModelColor)
                        CreateWireframeLinesWithModelColor(model3D);
                    else
                        _wireframeLinesContentVisual3D.Children.Add(_multiLineVisual3D);


                    _solidModelVisual3D = new ContentVisual3D(model3D)
                    {
                        IsVisible = _showSoldObject
                    };

                    MainViewport3D.Children.Add(_solidModelVisual3D);
                }
                else if (_wireframeRenderingType == WireframeRenderingTypes.CustomRenderObjectsRenderingStep)
                {
                    var dxScene = _dxViewportView.DXScene;

                    _renderWireframeObjectsRenderingStep = new RenderObjectsRenderingStep("RenderWireframeObjects");

                    if (dxScene != null)
                    {
                        _modelColorLineEffect = dxScene.DXDevice.EffectsManager.GetEffect<ModelColorLineEffect>();
                        _modelColorLineEffect.LineThickness = (float)_lineThickness;
                        _modelColorLineEffect.UseIndividualModelColor = _useModelColor;

                        // Set depth bias: see LineDepthBiasSample for more info
                        _modelColorLineEffect.DepthBias = 0.1f;
                        _modelColorLineEffect.DynamicDepthBiasFactor = 0.02f;

                        _renderWireframeObjectsRenderingStep.OverrideEffect = _modelColorLineEffect;
                        _renderWireframeObjectsRenderingStep.IsEnabled = _showWireframe;

                        // It is possible to render only part of the scene by setting FilterRenderingQueuesFunction or FilterObjectsFunction
                        //
                        //renderWireframeObjectsRenderingStep.FilterRenderingQueuesFunction = delegate(RenderingQueue queue)
                        //{
                        //    return queue == dxScene.ComplexGeometryRenderingQueue ||
                        //           queue == dxScene.StandardGeometryRenderingQueue ||
                        //           queue == dxScene.TransparentRenderingQueue;
                        //};
                        //
                        //renderWireframeObjectsRenderingStep.FilterObjectsFunction += delegate (RenderablePrimitiveBase renderableItem)
                        //{
                        //    return renderableItem.OriginalObject == dxEngineSceneNodeToRender;
                        //};

                        dxScene.RenderingSteps.AddAfter(dxScene.DefaultRenderObjectsRenderingStep, _renderWireframeObjectsRenderingStep);

                        dxScene.DefaultRenderObjectsRenderingStep.IsEnabled = _showSoldObject;

                        MainViewport3D.Children.Add(model3D.CreateModelVisual3D());
                    }
                }
                else if (_wireframeRenderingType == WireframeRenderingTypes.WireframeVisual3D)
                {
                    _wireframeVisual3D = new WireframeVisual3D()
                    {
                        WireframeType = GetWireframeType(_showSoldObject, _showWireframe),
                        LineColor = Colors.Black,
                        LineThickness = _lineThickness,
                        UseModelColor = _useModelColor,
                        OriginalModel = model3D
                    };

                    // Set depth bias: see LineDepthBiasSample for more info
                    _wireframeVisual3D.SetDXAttribute(DXAttributeType.LineDepthBias, 0.1);
                    _wireframeVisual3D.SetDXAttribute(DXAttributeType.LineDynamicDepthBiasFactor, 0.02);

                    MainViewport3D.Children.Add(_wireframeVisual3D);
                }

                
                var ambientLight = new AmbientLight(Color.FromRgb(80, 80, 80));
                MainViewport3D.Children.Add(ambientLight.CreateModelVisual3D());

                _isInitialized = true;
            }

            public void StartAnimation()
            {
                MainCamera.StartRotation(15, 0);
            }

            public void StopAnimation()
            {
                MainCamera.StopRotation();
            }

            public void UpdateWireframeSettings(bool showSoldObject, bool showWireframe, bool useModelColor, double lineThickness)
            {
                if (!_isInitialized)
                    return;

                if (_wireframeVisual3D != null)
                {
                    _wireframeVisual3D.WireframeType = GetWireframeType(showSoldObject, showWireframe);
                    _wireframeVisual3D.UseModelColor = useModelColor;
                    _wireframeVisual3D.LineThickness = lineThickness;
                }
                else if (_wireframeLinesContentVisual3D != null)
                {
                    _wireframeLinesContentVisual3D.IsVisible = showWireframe;
                    _solidModelVisual3D.IsVisible = showSoldObject;

                    foreach (var lineVisual3D in _wireframeLinesContentVisual3D.Children.OfType<BaseLineVisual3D>())
                        lineVisual3D.LineThickness = lineThickness;

                    if (useModelColor != _useModelColor)
                    {
                        _wireframeLinesContentVisual3D.Children.Clear();

                        if (useModelColor)
                            CreateWireframeLinesWithModelColor(_model3D);
                        else
                            _wireframeLinesContentVisual3D.Children.Add(_multiLineVisual3D);
                    }
                }
                else if (_modelColorLineEffect != null)
                {
                    _renderWireframeObjectsRenderingStep.IsEnabled = showWireframe;
                    _dxViewportView.DXScene.DefaultRenderObjectsRenderingStep.IsEnabled = showSoldObject;

                    _modelColorLineEffect.LineThickness = (float)lineThickness;
                    _modelColorLineEffect.UseIndividualModelColor = useModelColor;
                }

                _showSoldObject = showSoldObject;
                _showWireframe = showWireframe;
                _useModelColor = useModelColor;
                _lineThickness = lineThickness;
            }

            private WireframeVisual3D.WireframeTypes GetWireframeType(bool showSoldObject, bool showWireframe)
            {
                WireframeVisual3D.WireframeTypes wireframeType;

                if (showSoldObject && showWireframe)
                    wireframeType = WireframeVisual3D.WireframeTypes.WireframeWithOriginalSolidModel;
                else if (showSoldObject)
                    wireframeType = WireframeVisual3D.WireframeTypes.OriginalSolidModel;
                else if (showWireframe)
                    wireframeType = WireframeVisual3D.WireframeTypes.Wireframe;
                else
                    wireframeType = WireframeVisual3D.WireframeTypes.None;

                return wireframeType;
            }
            
            private void CreateWireframeLinesWithModelColor(Model3D model3D)
            {
                if (_wireframePositionsByColor == null)
                    _wireframePositionsByColor = new Dictionary<Color, Point3DCollection>();
                else
                    _wireframePositionsByColor.Clear();

                Ab3d.Utilities.ModelIterator.IterateGeometryModel3DObjects(model3D, null, (geometryModel3D, transform3D) =>
                {
                    var modelColor = Ab3d.Utilities.ModelUtils.GetMaterialDiffuseColor(geometryModel3D.Material, Colors.Black);

                    Point3DCollection wireframePositions;
                    if (!_wireframePositionsByColor.TryGetValue(modelColor, out wireframePositions))
                    {
                        wireframePositions = new Point3DCollection();
                        _wireframePositionsByColor.Add(modelColor, wireframePositions);
                    }

                    // Add wireframe positions to wireframePositions
                    WireframeFactory.AddWireframeLinePositions(geometryModel3D, transform3D, usePolygonIndices: false, removedDuplicates: true, wireframeLinePositions: wireframePositions);

                    // You can also use CreateWireframeLinePositions (but in our case we want to add positions to an existing Point3DCollection)
                    // var wireframePositions = WireframeFactory.CreateWireframeLinePositions(geometryModel3D, transform3D, usePolygonIndices: false, removedDuplicates: true);
                });


                _wireframeLinesContentVisual3D.Children.Clear();

                // Here we create a new MultiLineVisual3D instance for each line color.
                // If the model use many different colors then this creates many MultiLineVisual3D objects and require many DirectX draw calls.
                // To reduce the draw calls count, it is possible to render many lines with different line color by using
                // ScreenSpaceLineNode and PositionColoredLineMaterial where multiple lines with different colors are render with one draw call.
                //
                // See DXEngineAdvanced / ScreenSpaceLineNodeSample sample to see how to do that.

                foreach (var keyValuePair in _wireframePositionsByColor)
                {
                    var color = keyValuePair.Key;
                    var wireframePositions = keyValuePair.Value;

                    var multiLineVisual3D = new Ab3d.Visuals.MultiLineVisual3D()
                    {
                        Positions = wireframePositions,
                        LineColor = color,
                        LineThickness = _lineThickness,
                        IsVisible = _showWireframe
                    };

                    // Set depth bias: see LineDepthBiasSample for more info
                    multiLineVisual3D.SetDXAttribute(DXAttributeType.LineDepthBias, 0.1);
                    multiLineVisual3D.SetDXAttribute(DXAttributeType.LineDynamicDepthBiasFactor, 0.02);

                    _wireframeLinesContentVisual3D.Children.Add(multiLineVisual3D);
                }
            }

            private void OnMainCameraChanged(object sender, CameraChangedRoutedEventArgs e)
            {
                if (_isSyncingCamera)
                    return;

                OnCameraChanged(e);
            }

            protected void OnCameraChanged(CameraChangedRoutedEventArgs e)
            {
                if (CameraChanged != null)
                    CameraChanged(this, e);
            }

            public void Dispose()
            {
                if (_renderWireframeObjectsRenderingStep != null)
                {
                    _renderWireframeObjectsRenderingStep.Dispose();
                    _renderWireframeObjectsRenderingStep = null;
                }

                if (_modelColorLineEffect != null)
                {
                    _modelColorLineEffect.Dispose();
                    _modelColorLineEffect = null;
                }

                if (_dxViewportView != null)
                {
                    _dxViewportView.Dispose();
                    _dxViewportView = null;
                }
            }
        }
    }
}
