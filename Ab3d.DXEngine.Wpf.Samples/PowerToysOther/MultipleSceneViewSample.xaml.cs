using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Resources;
using System.Windows.Shapes;
using Ab3d.Cameras;
using Ab3d.DirectX;
using Ab3d.DXEngine.Wpf.Samples.Common;
using Ab3d.ObjFile;
using Ab3d.Visuals;
using SharpDX.Direct3D;

namespace Ab3d.DXEngine.Wpf.Samples.PowerToysOther
{
    // This sample is based on the similar sample for Ab3d.PowerToys but is updates to work well with DXEngine.
    //
    // The sample shows how to render the same 3D objects with multiple DXViewportViews.
    // To share the objects resources (vertex and index buffers and textures), all the DXViewportViews need to be created with the same DXDevice.
    // This sample shows how to do that.

    /// <summary>
    /// Interaction logic for MultipleSceneViewSample.xaml
    /// </summary>
    public partial class MultipleSceneViewSample : Page
    {
        private SceneLayout[] _allSceneLayouts;

        private SceneLayout _selectedLayout;

        private Model3D _loadedModel3D;
        private Model3D _animatedModel3D;

        private bool _isAnimationStarted;
        private AxisAngleRotation3D _animationAxisAngleRotation3D;

        private DXDevice _dxDevice;

        public MultipleSceneViewSample()
        {
            InitializeComponent();

            _dxDevice = CreateDXDevice();
            SetupViews(_dxDevice);

            UpdateLayoutSchemas();

            this.Loaded += delegate(object sender, RoutedEventArgs args)
            {
                LoadObjFile();
            };

            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                // Dispose all DXViewportViews
                _selectedLayout.Dispose();

                // Because we created the DXDevice here, we also need to dispose it
                _dxDevice.Dispose();
                _dxDevice = null;
            };
        }

        private DXDevice CreateDXDevice()
        {
            DXDevice dxDevice;

            // To use the same resources (Vertex, Index buffers and Textures) on multiple DXViewportViews,
            // we need to first creat a DXDevice and then initialize all DXViewportViews with that DXDevice instance.
            
            var dxDeviceConfiguration = new DXDeviceConfiguration();
            dxDeviceConfiguration.DriverType = DriverType.Hardware; // We could also specify Software rendering here

            try
            {
                dxDevice = new DXDevice(dxDeviceConfiguration);
                dxDevice.InitializeDevice();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Cannot create required DirectX device.\r\n" + ex.Message);
                return null;
            }

            if (dxDevice.Device == null)
            {
                MessageBox.Show("Cannot create required DirectX device.");
                return null;
            }

            return dxDevice;
        }

        private void SetupViews(DXDevice dxDevice)
        {
            // Define all possible layouts
            _allSceneLayouts = new SceneLayout[]
            {
                //  * | **
                // ---| **
                //  * | **
                new PerspectiveTopFrontViewLayout(dxDevice),

                //  * | *
                // ---|---
                //  * | *
                new TopLeftFrontPerspectiveViewLayout(dxDevice),

                // *******
                // *******
                // *******
                new PerspectiveViewLayout(dxDevice)
            };

            SelectLayout(_allSceneLayouts[0]);
        }

        private void UpdateLayoutSchemas()
        {
            // Create layout schemas for all layouts and show them as ToggleButtons on top of the sample
            foreach (var oneSceneLayout in _allSceneLayouts)
            {
                var layoutSchema = oneSceneLayout.CreateLayoutSchema();
                layoutSchema.SnapsToDevicePixels = true;
                layoutSchema.Width = 70;
                layoutSchema.Height = 36;
                layoutSchema.Margin = new Thickness(5, 5, 5, 5);

                var toggleButton = new ToggleButton();
                toggleButton.VerticalAlignment = VerticalAlignment.Center;
                toggleButton.Margin = new Thickness(0, 0, 10, 0);

                toggleButton.Tag = oneSceneLayout;
                toggleButton.Content = layoutSchema;

                if (oneSceneLayout == _selectedLayout)
                    toggleButton.IsChecked = true;

                toggleButton.Checked += delegate(object sender, RoutedEventArgs args)
                {
                    var checkedToggleButton = (ToggleButton) sender;
                    var layout = (SceneLayout) checkedToggleButton.Tag;

                    SelectLayout(layout);
                };

                LayoutsPanel.Children.Add(toggleButton);
            }
        }

        private void SelectLayout(SceneLayout layout)
        {
            if (_selectedLayout == layout)
                return;

            if (_selectedLayout != null)
                _selectedLayout.Dispose();

            layout.ActivateLayout(SceneViewsGrid);
            _selectedLayout = layout;

            foreach (var toggleButton in LayoutsPanel.Children.OfType<ToggleButton>())
            {
                if (toggleButton.Tag != _selectedLayout)
                    toggleButton.IsChecked = false;
            }

            ShowModel(_loadedModel3D);
        }

        private void LoadObjFile()
        {
            // We do not use robotarm.obj for this sample because obj files do not support hierarhies and therefore the robotarm model from obj file is not very good to be animated.
            //// Use ObjModelVisual3D to load robotarm.obj and then set objModelVisual3D.Content (as Model3D) to WireframeVisual
            //_objModelVisual3D = new ObjModelVisual3D()
            //{
            //    //Source = new Uri("pack://application:,,,/Ab3d.PowerToys.Samples;component/Resources/ObjFiles/robotarm.obj", UriKind.Absolute),
            //    SizeX = 50,
            //    Position = new Point3D(0, 0, 0),
            //    PositionType = ObjModelVisual3D.VisualPositionType.BottomCenter
            //};

            //_loadedModel3D = _objModelVisual3D.Content;
            //_animatedModel3D = _objModelVisual3D.UsedReaderObj.NamedObjects["Teapot"] as Model3D;

            var houseWithTreesModel = this.FindResource("HouseWithTreesModel") as Model3D;

            var personModel = this.FindResource("PersonModel") as Model3D;
            var animatedPersonModel = new Model3DGroup(); // Because personModel Frozen, it cannot be animated. Therefore we create a new Model3DGroup that can be animated
            animatedPersonModel.Children.Add(personModel);

            var model3DGroup = new Model3DGroup();
            model3DGroup.Children.Add(houseWithTreesModel);
            model3DGroup.Children.Add(animatedPersonModel);

            _loadedModel3D = model3DGroup;
            _animatedModel3D = animatedPersonModel;

            ShowModel(_loadedModel3D);
        }

        private void ShowModel(Model3D model)
        {
            // We need to set the model to all SceneView3D objects
            foreach (var sceneView3D in SceneViewsGrid.Children.OfType<SceneView3D>())
                sceneView3D.Model3D = model;
        }

        private void StartAnimationButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_isAnimationStarted)
            {
                CompositionTarget.Rendering -= CompositionTargetOnRendering;

                StartAnimationButton.Content = "Start animation";
                _isAnimationStarted = false;

                return;
            }

            if (_animatedModel3D == null)
                return;

            // Rotate around y axis
            _animationAxisAngleRotation3D = new AxisAngleRotation3D(new Vector3D(0, 1, 0), 0);
            _animatedModel3D.Transform = new RotateTransform3D(_animationAxisAngleRotation3D, new Point3D(0, 0, 130)); // Rotate around 0,0,130 to prevent moving through the house

            CompositionTarget.Rendering += CompositionTargetOnRendering;
            _isAnimationStarted = true;

            StartAnimationButton.Content = "Stop animation";
        }

        private void CompositionTargetOnRendering(object sender, EventArgs eventArgs)
        {
            if (_animationAxisAngleRotation3D != null)
                _animationAxisAngleRotation3D.Angle += 1; // each frame increase angle by 1 degree
        }
    }
}
