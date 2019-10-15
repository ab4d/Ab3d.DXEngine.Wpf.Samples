using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;
using Ab3d.DirectX;
using Ab3d.DirectX.Controls;
using Ab3d.DirectX.Models;
using Ab3d.Visuals;

namespace Ab3d.DXEngine.Wpf.Samples.PowerToys
{
    /// <summary>
    /// Interaction logic for WireframeSample.xaml
    /// </summary>
    public partial class WireframeSample : Page
    {
        private WpfWireframeVisual3DNode _wpfHouseWithThreesWireframeVisualNode;
        private WpfWireframeVisual3DNode _wpfPersonWireframeVisualNode;

        private double MOVEMENT_STEP = 10.0; // how much the person model is moved when an arrow key is pressed

        private TranslateTransform3D _personTranslate;

        private Model3DGroup _personModel;


        public WireframeSample()
        {
            InitializeComponent();

            DXDiagnostics.IsCollectingStatistics = true;

            // Subscribe to DXSceneInitialized event
            // In the handler we will get the SceneNodes that are used in DXEngine to show WireframeVisual3D objects
            // This way we will be able to set some additional properties to DXEngine objects
            MainDXViewportView.DXSceneInitialized += (sender, e) => OnDXSceneViewInitialized();

            // Instead of subscribing to DXSceneInitialized event we could also manually call InitializeScene (the call is commented below) to create the SceneNode.
            // But this solution is slightly less efficient because at the time of initialization the size of the DXSceneView is not known (this means that the back buffers will not be initialized yet)
            // If we no not manually call InitializeScene, than this method will be called in the Loaded event of the DXSceneView.
            // MainViewportView.InitializeScene();

            if (DirectX.Client.Settings.DXEngineSettings.Current.UseDirectXOverlay)
                Scene2Border.Margin = new Thickness(0, 0, RightSideBorder.Width, 0); // When using DirectX overlay add right margin so the WPF controls are visible (they cannot be added on top of overlayed DirectX host control)


            DepthBiasInfoImage.ToolTip =
@"DepthBias is a property that is available only when using DXEngine
(not available on WPF's WireframeVisual3D) and specify a bias (offset)
to the depth values used by DirectX.
This helps to show the wireframe on top of the solid model.
The values are specified in a local coordinate system.";


            FillWireframeTypesPanel();

            // PersonModel and HouseWithTreesModel are defined in App.xaml
            var houseWithTreesModel = this.FindResource("HouseWithTreesModel") as Model3D;

            //houseWithTreesModel = Ab3d.Reader3ds.Read(@"D:\Wpf\my 3d objects\from emails\Cadian errors\3ds files\err40.3ds");

            SceneModel1.Content = houseWithTreesModel;
            HouseWithThreesWireframeVisual.OriginalModel = houseWithTreesModel;


            var originalPersonModel = this.FindResource("PersonModel") as Model3D;

            // originalPersonModel is frozen so its Transform cannot be changed - therefore we create a new _personModel that could be changed
            _personModel = new Model3DGroup();
            _personModel.Children.Add(originalPersonModel);

            _personTranslate = new TranslateTransform3D();
            _personModel.Transform = _personTranslate;

            SceneModel2.Content = _personModel;
            PersonWireframeVisual.OriginalModel = _personModel;


            this.Focusable = true; // by default Page is not focusable and therefore does not recieve keyDown event
            this.PreviewKeyDown += WireframeSample_PreviewKeyDown; // Use PreviewKeyDown to get arrow keys also (KeyDown event does not get them)
            this.Focus();

            this.Loaded += WireframeSample_Loaded;

            // IMPORTANT:
            // It is very important to call Dispose method on DXSceneView after the control is not used any more (see help file for more info)
            this.Unloaded += (sender, args) => MainDXViewportView.Dispose();
        }

        private void OnDXSceneViewInitialized()
        {
            if (MainDXViewportView.UsedGraphicsProfile.DriverType == GraphicsProfile.DriverTypes.Wpf3D)
            {
                HouseWithThreesWireframeVisual.RemoveDuplicateLines = true; // This is recommended setting for WPF because it reduces the number of lines
                PersonWireframeVisual.RemoveDuplicateLines = true;
                return;
            }

            HouseWithThreesWireframeVisual.RemoveDuplicateLines = false; // Rendering lines in DXEngine is very cheap so we can skip the process of finding and removing duplicates
            PersonWireframeVisual.RemoveDuplicateLines = false;

            // Get the SceneNodes (WpfWireframeVisual3DNode) used by DXScene
            // This way we can access some properties that are specific to DirectX and are not used in WPF WireframeVisual3D (like depth bias properties)
            _wpfHouseWithThreesWireframeVisualNode = MainDXViewportView.GetSceneNodeForWpfObject(HouseWithThreesWireframeVisual) as WpfWireframeVisual3DNode;

            if (_wpfHouseWithThreesWireframeVisualNode != null)
                _wpfHouseWithThreesWireframeVisualNode.Name = "HouseWithThreesWireframeVisualNode";


            _wpfPersonWireframeVisualNode = MainDXViewportView.GetSceneNodeForWpfObject(PersonWireframeVisual) as WpfWireframeVisual3DNode;

            if (_wpfPersonWireframeVisualNode != null)
                _wpfPersonWireframeVisualNode.Name = "PersonWireframeVisualNode";
            
            UpdateDepthBias();
        }


        private void FillWireframeTypesPanel()
        {
            // Create RadioButtons for each WireframeTypes enum value:
            // None
            // Wireframe
            // OriginalSolidModel
            // WireframeWithOriginalSolidModel
            // SingleColorSolidModel
            // WireframeWithSingleColorSolidModel

            var wireframeTypesTexts = Enum.GetNames(typeof(WireframeVisual3D.WireframeTypes));
            foreach (var wireframeTypesText in wireframeTypesTexts)
            {
                var radioButton = new RadioButton()
                {
                    Content = wireframeTypesText,
                    GroupName = "WireframeTypes"
                };

                if (wireframeTypesText == WireframeVisual3D.WireframeTypes.WireframeWithSingleColorSolidModel.ToString())
                    radioButton.IsChecked = true;

                radioButton.Checked += WireframeTypesRadioButtonChanged;

                WireframeTypePanel.Children.Add(radioButton);
            }
        }

        private void WireframeTypesRadioButtonChanged(object sender, RoutedEventArgs routedEventArgs)
        {
            if (!this.IsLoaded)
                return;

            var radioButton = (RadioButton)sender;
            HouseWithThreesWireframeVisual.WireframeType = (WireframeVisual3D.WireframeTypes)Enum.Parse(typeof(WireframeVisual3D.WireframeTypes), (string)radioButton.Content);

            UpdatePersonWireframeVisual();

            SolidModelColorComboBox.IsEnabled = (HouseWithThreesWireframeVisual.WireframeType == WireframeVisual3D.WireframeTypes.SingleColorSolidModel ||
                                                 HouseWithThreesWireframeVisual.WireframeType == WireframeVisual3D.WireframeTypes.WireframeWithSingleColorSolidModel);
        }

        void WireframeSample_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateWireframe();
        }

        private void SceneCamera2_CameraChanged(object sender, Ab3d.Common.Cameras.CameraChangedRoutedEventArgs e)
        {
            // Enclose changes in BeginInit and EndInit to prevent multiple updates
            SceneCamera1.BeginInit();
            
            SceneCamera1.Heading = SceneCamera2.Heading;
            SceneCamera1.Attitude = SceneCamera2.Attitude;
            SceneCamera1.Distance = SceneCamera2.Distance;
            
            SceneCamera1.EndInit();
        }

        private Color GetColorFromComboBox(ComboBox checkbox)
        {
            Color color;

            switch (checkbox.SelectedIndex)
            {
                case 0:
                    color = Colors.White;
                    break;

                case 1:
                    color = Colors.Red;
                    break;

                default:
                case 2:
                    color = Colors.Black;
                    break;
            }

            return color;
        }

        private void UpdateWireframe()
        {
            double lineThickness = (double)LineThicknessComboBox.SelectedIndex;
            if (lineThickness == 0) // Line thickness is equal to selected index except for the first index that represent 0.5 thickness.
                lineThickness = 0.5;

            // IMPORTANT:
            // To prevent regeneration of wireframe (that can be long running task) we use BeginInit and EndInit to create the wireframe only once after all the properties are set
            // Otherwise every change can lead to wireframe creation
            HouseWithThreesWireframeVisual.BeginInit();

                HouseWithThreesWireframeVisual.LineThickness = lineThickness;

                // If UseModelColor is true, than the line color is get from the color of the model's material (in case of DiffuseMaterial with SolidColorBrush)
                HouseWithThreesWireframeVisual.UseModelColor = PreserveColorRadioButton.IsChecked ?? false;
                HouseWithThreesWireframeVisual.LineColor = GetColorFromComboBox(LineColorComboBox);

                // SolidModelColor is used for SingleColorSolidModel or WireframeWithSingleColorSolidModel
                // In those modes the solid model will be shown with this color
                HouseWithThreesWireframeVisual.SolidModelColor = GetColorFromComboBox(SolidModelColorComboBox);

                // Finally set the Model3D that will be used to create wireframe
                HouseWithThreesWireframeVisual.OriginalModel = SceneModel1.Content;

            HouseWithThreesWireframeVisual.EndInit();

            UpdatePersonWireframeVisual();

            // OLD CODE
            // Create wireframe Model3D without WireframeVisual3D - with WireframeFactory:

            //Model3D wireframe = Ab3d.Models.WireframeFactory.CreateWireframe(SceneModel1, 1, preserveLineColor, customColor, SceneCameraViewport2);

            //WireframeModelGroup1.Children.Clear();
            //WireframeModelGroup1.Children.Add(wireframe);
        }

        private void UpdatePersonWireframeVisual()
        {
            // It looks like binding is not working when model is inside DXSceneView
            // Therefore we need to update the properties manually
            PersonWireframeVisual.BeginInit();

                PersonWireframeVisual.WireframeType = HouseWithThreesWireframeVisual.WireframeType;
                PersonWireframeVisual.LineThickness = HouseWithThreesWireframeVisual.LineThickness;
                PersonWireframeVisual.UseModelColor = HouseWithThreesWireframeVisual.UseModelColor;
                PersonWireframeVisual.LineColor = HouseWithThreesWireframeVisual.LineColor;
                PersonWireframeVisual.SolidModelColor = HouseWithThreesWireframeVisual.SolidModelColor;

            PersonWireframeVisual.EndInit();
        }

        private void OnWireframeSettingsChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            UpdateWireframe();
        }

        private void OnDepthBiasTextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateDepthBias();
        }

        private void UpdateDepthBias()
        {
            if (_wpfHouseWithThreesWireframeVisualNode == null)
                return;

            float depthBias;

            if (!float.TryParse(DepthBiasTextBox.Text, NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out depthBias))
                depthBias = 0.0f;

            if (_wpfHouseWithThreesWireframeVisualNode.DepthBias != depthBias)
            {
                _wpfHouseWithThreesWireframeVisualNode.DepthBias = depthBias;
                _wpfHouseWithThreesWireframeVisualNode.RecreateChildNodes();

                _wpfPersonWireframeVisualNode.DepthBias = depthBias;
                _wpfPersonWireframeVisualNode.RecreateChildNodes();
            }
        }



        void WireframeSample_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.A:
                case Key.Left:
                    // left
                    MovePerson(0, -MOVEMENT_STEP);
                    e.Handled = true;
                    break;

                case Key.D:
                case Key.Right:
                    // right
                    MovePerson(0, MOVEMENT_STEP);
                    e.Handled = true;
                    break;

                case Key.W:
                case Key.Up:
                    // forward
                    MovePerson(-MOVEMENT_STEP, 0);
                    e.Handled = true;
                    break;

                case Key.S:
                case Key.Down:
                    // backward
                    MovePerson(MOVEMENT_STEP, 0);
                    e.Handled = true;
                    break;
                
                //case Key.F1:
                //    string dumpText = MyScene3D.DXScene.GetSceneNodesDumpString(showBounds: true, showDirtyFlags: true);
                //    System.Diagnostics.Debug.WriteLine(dumpText);

                //    e.Handled = true;
                //    break;
            }

        }

        private void MovePerson(double dx, double dy)
        {
            _personTranslate.OffsetZ += dx;
            _personTranslate.OffsetX += dy; // y axis in 3d is pointing up

            // No need to manually call RecreateWireframeModel because WireframeVisual3D is automatically updated when the child property of OriginalObject is changed
            // PersonWireframeVisual.RecreateWireframeModel();
        }
    }
}
