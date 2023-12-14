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
using System.Windows.Navigation;
using System.Windows.Shapes;
using Ab3d.Assimp;
using Ab3d.Cameras;
using Ab3d.Common.Cameras;
using Ab3d.Controls;
using Ab3d.DirectX;
using Ab3d.DirectX.Controls;
using Ab3d.DirectX.Effects;
using Ab3d.Visuals;

// This sample shows how to define a master DXViewportView that contains the 3D scene and
// then add child DXViewportView objects that show the same 3D scene but provide its own cameras
// and can also override or change the rendering of the 3D scene.
//
// To create a child DXViewportView use the constructor that takes DXViewportView as parameter.
// If the child DXViewportView will customize rendering of the 3D scene the constructor must be called
// with setting useMasterRenderingSteps to false. This way the child DXViewportView.DXScene will
// define its own RenderingSteps that can be customized
// (in this sample the OverrideEffect property is set on the DefaultRenderObjectsRenderingStep)

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineOther
{
    /// <summary>
    /// Interaction logic for MultiDXViewportViewsSample.xaml
    /// </summary>
    public partial class MultiDXViewportViewsSample : Page
    {
        private const int ColumnsCount = 2;
        private const int RowsCount = 2;

        private List<DXView> _dxViewportViews;
        private DXViewportView _masterDXViewportView;

        private GridSplitter _horizontalGridSplitter;
        
        private ModelColorLineEffect _modelColorLineEffect;

        private Random _rnd = new Random();
        private Model3DGroup _animatedPersonModel;

        private readonly string[] RenderingTypeStrings = new string[] {"Standard", "Wirefame", "Filer by RenderingQueue", "Filter by object name"};

        private enum RenderingTypes
        {
            Standard,
            Wireframe,
            FilerByRenderingQueue,
            FilterByObjects
        }

        public MultiDXViewportViewsSample()
        {
            InitializeComponent();

            CreateInitialScene();

            this.Unloaded += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_modelColorLineEffect != null)
            {
                _modelColorLineEffect.Dispose();
                _modelColorLineEffect = null;
            }

            // Dispose DXViewportViews in the opposite order (so first child DXViewportView and lastly _masterDXViewportView)
            for (int i = _dxViewportViews.Count - 1; i >= 0; i--)
                _dxViewportViews[i].Dispose();

            _dxViewportViews.Clear();

            _masterDXViewportView = null;
        }

        private void CreateInitialScene()
        {
            for (int i = 0; i < ColumnsCount; i++)
                ViewsGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });

            for (int i = 0; i < RowsCount; i++)
                ViewsGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });

            if (ColumnsCount > 1)
            {
                for (int i = 0; i < ColumnsCount - 1; i++)
                {
                    var gridSplitter = new GridSplitter()
                    {
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        Width = 2,
                        Background = Brushes.Gray,
                    };

                    Grid.SetColumn(gridSplitter, i);

                    if (RowsCount > 1)
                    {
                        Grid.SetRow(gridSplitter, 0);
                        Grid.SetRowSpan(gridSplitter, RowsCount);
                    }

                    ViewsGrid.Children.Add(gridSplitter);
                }
            }

            if (RowsCount > 1)
            {
                for (int i = 0; i < RowsCount - 1; i++)
                {
                    _horizontalGridSplitter = new GridSplitter()
                    {
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Height = 2,
                        Background = Brushes.Gray,
                    };

                    Grid.SetRow(_horizontalGridSplitter, i);

                    if (ColumnsCount > 1)
                    {
                        Grid.SetColumn(_horizontalGridSplitter, 0);
                        Grid.SetColumnSpan(_horizontalGridSplitter, ColumnsCount);
                    }

                    ViewsGrid.Children.Add(_horizontalGridSplitter);
                }
            }


            _dxViewportViews = new List<DXView>();

            // Create master DXViewportView that will define the 3D scene
            _masterDXViewportView                      =  AddNewDXViewportView(null, 0, 0, addToRootGrid: true, renderingType: RenderingTypes.Standard, addDrawSelectionComboBox: false);
            _masterDXViewportView.Name                 =  "MainDXViewportView";
            _masterDXViewportView.BackgroundColor      =  Colors.White;
            _masterDXViewportView.DXSceneDeviceCreated += OnMasterDXSceneCreated;


            // Create 3D scene
            AddHouseWithTreesModel();

            var lightingRigVisual3D = new Ab3d.Visuals.LightingRigVisual3D();
            _masterDXViewportView.Viewport3D.Children.Add(lightingRigVisual3D);


            // Add child DXViewportViews
            int usedColumnsCount = ColumnsCount > 1 ? ColumnsCount : 1;
            int usedRowsCount    = RowsCount > 1 ? RowsCount : 1;


            int index = 1; // This is used to create different RenderingTypes; start with wireframe

            for (int i = 0; i < usedRowsCount; i++)
            {
                for (int j = 0; j < usedColumnsCount; j++)
                {
                    if (i == 0 && j == 0)
                        continue; // We have already created the main DXViewportView

                    // Force wireframe rendering for DXViewportView defined in the first row (except the masterDXViewportView) 
                    var renderingType = (RenderingTypes)(index % 4);

                    var dxViewportView = AddNewDXViewportView(_masterDXViewportView, i, j, renderingType);
                    dxViewportView.Name = $"DXViewportView_{i + 1}_{j + 1}";

                    index++;
                }
            }
        }

        private void OnMasterDXSceneCreated(object sender, EventArgs e)
        {
            // Create a custom RenderingQueue.
            // This way we can add some of the models to this queue and then when rendering we can filter by rendering queue instead of by object (filtering by object is much slower).
            // Note that we create an instance of MaterialSortedRenderingQueue - this enables grouping objects by their material (for fewer DirectX state changes) and also allows multi-threading and DirectX commands caching.
            var customRenderingQueue = new MaterialSortedRenderingQueue("CustomRenderingQueue", useMultiThreading: false, containsTransparentObjects: false);

            // If we want to enable multi-threading, then we need to fix allowed effect to StandardEffect or ThickLineEffect (only those two effects support multi-threading) - for example:
            //var customRenderingQueue = new MaterialSortedRenderingQueue("CustomRenderingQueue", useMultiThreading: true, containsTransparentObjects: false, allowedEffectType: typeof(StandardEffect));


            _masterDXViewportView.DXScene.AddRenderingQueueBefore(customRenderingQueue, _masterDXViewportView.DXScene.StandardGeometryRenderingQueue);
        }

        private DXViewportView AddNewDXViewportView(DXViewportView masterDXViewportView, int rowIndex, int columnIndex, RenderingTypes renderingType, bool addToRootGrid = true, bool addDrawSelectionComboBox = true)
        {
            TargetPositionCamera  targetPositionCamera;
            MouseCameraController mouseCameraController;
            DXViewportView        dxViewportView = CreateSimpleDXSceneViewFromCode(masterDXViewportView, renderingType, out targetPositionCamera, out mouseCameraController);

            // Set random BackgroundColor - but do not make it to dark so start with 128
            dxViewportView.BackgroundColor = Color.FromRgb((byte)(_rnd.Next(128) + 128), (byte)(_rnd.Next(128) + 128), (byte)(_rnd.Next(128) + 128));

            _dxViewportViews.Add(dxViewportView);


            if (addToRootGrid)
            {
                var viewRootGrid = new Grid();

                var border = new Border()
                {
                    Background = Brushes.Transparent,
                    Margin = new Thickness(1, 1, 3, 3)
                };

                border.Child = dxViewportView;
                mouseCameraController.EventsSourceElement = border;

                viewRootGrid.Children.Add(border);
                viewRootGrid.Children.Add(targetPositionCamera);
                viewRootGrid.Children.Add(mouseCameraController);

                Grid.SetColumn(viewRootGrid, columnIndex);
                Grid.SetRow(viewRootGrid, rowIndex);

                ViewsGrid.Children.Add(viewRootGrid);

                
                var textBlock = new TextBlock()
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment   = VerticalAlignment.Top,
                    Margin              = new Thickness(5, 5, 0, 0)
                };

                textBlock.Text = masterDXViewportView == null ? "Master DXViewportView:" : "Child DXViewportView:";
                viewRootGrid.Children.Add(textBlock);


                if (addDrawSelectionComboBox)
                {
                    var comboBox = new ComboBox()
                    {
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment   = VerticalAlignment.Top,
                        Margin              = new Thickness(0, 3, 5, 0)
                    };

                    comboBox.ItemsSource   = RenderingTypeStrings;
                    comboBox.SelectedIndex = (int)renderingType;

                    if (masterDXViewportView == null)
                        comboBox.IsEditable = false; // Lock Standard for master view

                    comboBox.SelectionChanged += delegate(object sender, SelectionChangedEventArgs args)
                    {
                        var newRenderingType = (RenderingTypes)comboBox.SelectedIndex;
                        SetSpecialRenderingType(dxViewportView, newRenderingType);

                        dxViewportView.Refresh(); // Render the scene again
                    };

                    viewRootGrid.Children.Add(comboBox);
                }
            }

            return dxViewportView;
        }

        private DXViewportView CreateSimpleDXSceneViewFromCode(DXViewportView masterDXViewportView, RenderingTypes renderingType, out TargetPositionCamera targetPositionCamera, out MouseCameraController mouseCameraController)
        {
            Viewport3D viewport3D = new Viewport3D();

            DXViewportView createdDXViewportView;
            if (masterDXViewportView != null)
            {
                // Create a child DXViewportView with using a constructor that takes a masterDXViewportView.
                // When useMasterRenderingSteps parameter is true (by default), then the RenderingSteps in the master DXViewportView.DXScene are used.
                // To customize rendering of a child DXViewportView, set useMasterRenderingSteps to false. This will create new RenderingSteps that can be customized (see below).
                //
                // When creating child DXViewportView we still need to pass a new Viewport3D object to the constructor,
                // but still the 3D scene will be used from the master's DXViewportView's Viewport3D.
                // The new viewport3D is used so that a new camera can be associated with that (see below when new TargetPositionCamera is created).

                bool useMasterRenderingSteps = renderingType == RenderingTypes.Standard;

                createdDXViewportView = new DXViewportView(masterDXViewportView, viewport3D, useMasterRenderingSteps);
            }
            else
            {
                // Create master DXViewportView with using constructor that takes only Viewport3D.
                createdDXViewportView = new DXViewportView(viewport3D);
            }

            // Enable transparency sorting for each View (this way transparent objects are correctly rendered for each child view camera).
            createdDXViewportView.IsTransparencySortingEnabled = true;

            createdDXViewportView.DXSceneDeviceCreated += delegate (object sender, EventArgs e)
            {
                SetSpecialRenderingType(createdDXViewportView, renderingType);
            };


            // Because each view supports its own camera, we need to create a new TargetPositionCamera and MouseCameraController for each view.

            double cameraHeading = masterDXViewportView == null ? 30 : _rnd.Next(360);

            targetPositionCamera = new TargetPositionCamera()
            {
                TargetPosition   = new Point3D(0, 0, 0),
                Heading          = cameraHeading,
                Attitude         = -20,
                ShowCameraLight  = ShowCameraLightType.Never, // If we have a camera light then the light position will be defined (and changed) with changing the camera position in one view
                Distance         = 1000,
                TargetViewport3D = viewport3D
            };

            mouseCameraController = new MouseCameraController()
            {
                TargetCamera = targetPositionCamera,
                RotateCameraConditions = MouseCameraController.MouseAndKeyboardConditions.LeftMouseButtonPressed,
                MoveCameraConditions = MouseCameraController.MouseAndKeyboardConditions.ControlKey | MouseCameraController.MouseAndKeyboardConditions.LeftMouseButtonPressed
            };

            return createdDXViewportView;
        }

        private void SetSpecialRenderingType(DXViewportView dxViewportView, RenderingTypes renderingType)
        {
            if (dxViewportView.DXScene.DefaultRenderObjectsRenderingStep == null)
                return; // Not yet initialized

            // First reset any previous customizations
            dxViewportView.DXScene.DefaultRenderObjectsRenderingStep.OverrideEffect = null;
            dxViewportView.DXScene.DefaultRenderObjectsRenderingStep.FilterObjectsFunction = null;
            dxViewportView.DXScene.DefaultRenderObjectsRenderingStep.FilterRenderingQueuesFunction = null;
            dxViewportView.DXScene.DefaultRenderObjectsRenderingStep.OverrideRasterizerState = null;


            if (renderingType == RenderingTypes.Wireframe)
            {
                // Use ModelColorLineEffect to render the 3D objects as wireframe (model colors define the color of the lines).
                // Note that instanced objects and some other advanced objects are not rendered.
                if (_modelColorLineEffect == null)
                {
                    _modelColorLineEffect = _masterDXViewportView.DXScene.DXDevice.EffectsManager.GetEffect<ModelColorLineEffect>();
                    _modelColorLineEffect.LineThickness = 1;
                }

                dxViewportView.DXScene.DefaultRenderObjectsRenderingStep.OverrideEffect = _modelColorLineEffect;

                // We could also force wireframe rendering overriding rasterizer state and force DirectX to render objects are wireframe
                // This would be faster but you would not be able to specify line thickness (it is always 1 pixel): 
                //dxViewportView.DXScene.DefaultRenderObjectsRenderingStep.OverrideRasterizerState = dxViewportView.DXScene.DXDevice.CommonStates.WireframeCullNone;
            }
            else if (renderingType == RenderingTypes.FilterByObjects)
            {
                dxViewportView.DXScene.DefaultRenderObjectsRenderingStep.FilterObjectsFunction =
                    delegate (RenderablePrimitiveBase renderablePrimitive)
                    {
                        var originalSceneNode = renderablePrimitive.OriginalObject as SceneNode;
                        if (originalSceneNode != null)
                            return originalSceneNode.Name.StartsWith("Sphere") || originalSceneNode.Name.StartsWith("Cylinder");

                        return false;
                    };
            }
            else if (renderingType == RenderingTypes.FilerByRenderingQueue)
            {
                dxViewportView.DXScene.DefaultRenderObjectsRenderingStep.FilterRenderingQueuesFunction = renderingQueue => renderingQueue.Name == "CustomRenderingQueue";
            }

            dxViewportView.DXScene.NotifyChange(DXScene.ChangeNotifications.RenderingStepsChanged);
        }

        private void AddHouseWithTreesModel()
        {
            // Use assimp importer to load house with trees.3DS
            AssimpLoader.LoadAssimpNativeLibrary();

            var assimpWpfImporter = new AssimpWpfImporter();
            var houseWithTreesModel = (Model3DGroup)assimpWpfImporter.ReadModel3D(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\Models\house with trees.3DS"));

            // Add AmbientLight
            var ambientLight = new AmbientLight(Color.FromRgb(80, 80, 80));
            houseWithTreesModel.Children.Add(ambientLight);

            // Show the loaded 3d scene
            var modelVisual3D = houseWithTreesModel.CreateModelVisual3D();
            _masterDXViewportView.Viewport3D.Children.Add(modelVisual3D);

            // Save the man01 model for animation (when clicked on "Change scene" button)
            _animatedPersonModel = assimpWpfImporter.NamedObjects["man01"] as Model3DGroup;


            // Add base green plate and the house models to a CustomRenderingQueue.
            // This is done with setting value of CustomRenderingQueue (custom DXAttribute) on some parts of the 3D scene.
            // This will add the specified models to the custom rendering queue (created in the OnMasterDXSceneCreated method).
            var modelNames = new string[] { "Box01", "Box02", "roof01" };
            foreach (var modelName in modelNames)
            {
                var model3D = assimpWpfImporter.NamedObjects[modelName] as Model3D;

                // Note that CustomRenderingQueue can be set to an instance of RenderingQueue or to a name (as string) of the RenderingQueue
                model3D.SetDXAttribute(DXAttributeType.CustomRenderingQueue, "CustomRenderingQueue");
            }
        }


        private System.Windows.Media.Color GetRandomWpfColor(bool isRandomAlpha = false)
        {
            byte a;

            if (isRandomAlpha)
                a = (byte)_rnd.Next(255);
            else
                a = 255;

            return Color.FromArgb(a, (byte)_rnd.Next(255), (byte)_rnd.Next(255), (byte)_rnd.Next(255));
        }

        private System.Windows.Media.Media3D.Material GetRandomWpfMaterial(bool isRandomAlpha = false)
        {
            return new DiffuseMaterial(GetRandomWpfBrush(isRandomAlpha));
        }

        private System.Windows.Media.Brush GetRandomWpfBrush(bool isRandomAlpha = false)
        {
            return new SolidColorBrush(GetRandomWpfColor(isRandomAlpha));
        }

        private void ChangeSceneButton_OnClick(object sender, RoutedEventArgs e)
        {
            Ab3d.Utilities.TransformationsHelper.AddTransformation(_animatedPersonModel, new TranslateTransform3D(20, 0, 0));
        }
    }
}
