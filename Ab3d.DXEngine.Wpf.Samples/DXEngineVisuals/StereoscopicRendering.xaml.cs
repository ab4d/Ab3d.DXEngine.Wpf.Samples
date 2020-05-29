using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
using Ab3d.DirectX;
using Ab3d.DirectX.Controls;
using Ab3d.DXEngine.Wpf.Samples.Common;
using Ab3d.Utilities;
using Ab3d.Visuals;
using SharpDX;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    // Recommendations when shown Anaglyph images:
    // - use black background
    // - grayscale objects are better

    /// <summary>
    /// Interaction logic for StereoscopicRendering.xaml
    /// </summary>
    public partial class StereoscopicRendering : Page
    {
        private StereoscopicVirtualRealityProvider _currentVirtualRealityProvider;

        private AnaglyphVirtualRealityProvider _anaglyphVirtualRealityProvider;
        private SplitScreenVirtualRealityProvider _splitScreenVirtualRealityProvider;

        private string _lastDroppedFileName;

        private bool _isInternalChange;

        private Action<ComboBoxItem> _providerSettingsChangedAction;

        private bool _isTogglingFullScreen;

        public StereoscopicRendering()
        {
            InitializeComponent();

            CreateSceneObjects();
            GeneratedSceneComboBoxItem.IsSelected = true;

            // Start with AnaglyphVirtualRealityProvider - for colored glasses
            InitializeAnaglyphVirtualRealityProvider();
            

            // Wait until DXScene is initialized and then we can initialize virtual reality rendering mode with AnaglyphVirtualRealityProvider
            MainDXViewportView.DXSceneInitialized += delegate (object sender, EventArgs args)
            {
                if (MainDXViewportView.UsedGraphicsProfile.DriverType != GraphicsProfile.DriverTypes.Wpf3D && MainDXViewportView.DXScene != null)
                {
                    MainDXViewportView.DXScene.InitializeVirtualRealityRendering(_currentVirtualRealityProvider);
                }
                else
                {
                    SettingsGrid.IsEnabled = false;
                    Wpf3DRenderingWarningTextBlock.Visibility = Visibility.Visible;
                }
            };


            // Set initial values
            EyeSeparationSlider.Value = _currentVirtualRealityProvider.EyeSeparation * 1000; // Convert from m to mm
            ParallaxSlider.Value = _currentVirtualRealityProvider.Parallax;

            VRProviderInfoImage.ToolTip =
@"VR Provider ComboBox specifies type of stereoscopic rendering. The following types are supported:
AnaglyphProvider - rendered 3D scene for colored glasses (for example for red - cyan glasses).
SplitScreenProvider - rendered 3D scene for 3D TV
None - removes all virtual reality providers";

            // Set ToolTip to code documentation text for StereoscopicVirtualRealityProvider.EyeSeparation
            EyeSeparationInfoImage.ToolTip =
@"Gets or sets a distance between left and right eye. The best value for EyeSeparation is based on the size of objects
in the scene, the size of the Viewport3D, monitor DPI settings and the actual distance between eyes of the user.";


            // Set ToolTip to code documentation text for StereoscopicVirtualRealityProvider.Parallax
            ParallaxInfoImage.ToolTip =
@"Parallax defines a value in degrees that specifies an angle of the left and right eye look direction.
If the parallax is zero, then the look directions of left and right cameras are parallel.
If parallax is bigger then zero, then the left and right look directions are pointed to each other and
they cross at some position in front of the camera (the bigger the angle the closer the crossing point).

Usually the best 3D effect is producted when the parallax is set so that the look directions cross 
at the center of the scene - look directions of human eyes cross at the point of focus.";


            // Support dragging .obj files to load the 3D models from obj file
            var dragAndDropHelper = new DragAndDropHelper(MainDXViewportView, ".obj");
            dragAndDropHelper.FileDroped += OnObjFileDropped;


            // IMPORTANT:
            // It is very important to call Dispose method on DXSceneView after the control is not used any more (see help file for more info)
            this.Unloaded += delegate (object sender, RoutedEventArgs args)
            {
                if (!_isTogglingFullScreen) // Do not dispose the DXEngine when we are toggling to show or hide fullscreen
                    MainDXViewportView.Dispose();
            };
        }

        #region InitializeSplitScreenVirtualRealityProvider
        private void InitializeSplitScreenVirtualRealityProvider()
        {
            DisposeCurrentVirtualRealityProvider();


            _splitScreenVirtualRealityProvider = new SplitScreenVirtualRealityProvider(eyeSeparation: 0.07f, // 70 mm
                                                                                       parallax: 0.6f,
                                                                                       splitScreen: SplitScreenVirtualRealityProvider.SplitScreenType.SideBySide);

            _splitScreenVirtualRealityProvider.ImagesSeparationDistance = 42; // Based on observations, the 42 works best for 3D TV (this value might be different for your 3D TV; see comments in the (i) info icon)
            UpdateSeparationDistanceText();

            TitleTextBlock.Text = "Split screen stereoscopic rendering for 3D TV";

            ProviderSettingsTextBlock.Text = "Split type:";


            ProviderSettingsComboBox.Items.Clear();
            ProviderSettingsComboBox.Items.Add(new ComboBoxItem()
            {
                Content = "Side by side (vertical)",
                Tag = SplitScreenVirtualRealityProvider.SplitScreenType.SideBySide
            });
            ProviderSettingsComboBox.Items.Add(new ComboBoxItem()
            {
                Content = "Top and bottom (horizontal)",
                Tag = SplitScreenVirtualRealityProvider.SplitScreenType.TopAndBottom
            });

            ProviderSettingsComboBox.SelectedIndex = 0;

            _currentVirtualRealityProvider = _splitScreenVirtualRealityProvider;

            if (MainDXViewportView.DXScene != null && MainDXViewportView.UsedGraphicsProfile.DriverType != GraphicsProfile.DriverTypes.Wpf3D)
                MainDXViewportView.DXScene.InitializeVirtualRealityRendering(_currentVirtualRealityProvider);

            _providerSettingsChangedAction = delegate(ComboBoxItem selectedItem)
            {
                if (selectedItem.Tag is SplitScreenVirtualRealityProvider.SplitScreenType)
                {
                    var splitScreenType = (SplitScreenVirtualRealityProvider.SplitScreenType) selectedItem.Tag;
                    _splitScreenVirtualRealityProvider.SplitScreen = splitScreenType;

                    // When the split screen type is changed from SideBySide to TopAndBottom,
                    // we should divide the current ImagesSeparationDistance by two.
                    // Similarity, we need to duplicate the ImagesSeparationDistance when changed from TopAndBottom to SideBySide
                    if (splitScreenType == SplitScreenVirtualRealityProvider.SplitScreenType.TopAndBottom)
                        _splitScreenVirtualRealityProvider.ImagesSeparationDistance = (int)(_splitScreenVirtualRealityProvider.ImagesSeparationDistance / 2); 
                    else
                        _splitScreenVirtualRealityProvider.ImagesSeparationDistance = 2 * _splitScreenVirtualRealityProvider.ImagesSeparationDistance;

                    UpdateSeparationDistanceText();
                }
            };

            SeparationDistancePanel.Visibility = Visibility.Visible;
        }
        #endregion

        #region InitializeAnaglyphVirtualRealityProvider
        private void InitializeAnaglyphVirtualRealityProvider()
        {
            DisposeCurrentVirtualRealityProvider();


            _anaglyphVirtualRealityProvider = new AnaglyphVirtualRealityProvider(eyeSeparation: 0.07f, // 70 mm
                                                                                 parallax: 0.6f,
                                                                                 anaglyphColorTransformation: AnaglyphVirtualRealityProvider.OptimizedAnaglyph);

            TitleTextBlock.Text = "Anaglyph stereoscopic rendering for red and cyan glasses";

            ProviderSettingsTextBlock.Text = "Anaglyph type:";

            // AnaglyphVirtualRealityProvider defines some predefined types of anaglyph rendering - so called ColorTransformations
            // Use reflection to read all possible types and fill the ColorTransformTypeComboBox with them
            FillAvailableColorTransformTypes(ProviderSettingsComboBox);


            _currentVirtualRealityProvider = _anaglyphVirtualRealityProvider;

            if (MainDXViewportView.DXScene != null && MainDXViewportView.UsedGraphicsProfile.DriverType != GraphicsProfile.DriverTypes.Wpf3D)
                MainDXViewportView.DXScene.InitializeVirtualRealityRendering(_currentVirtualRealityProvider);


            _providerSettingsChangedAction = delegate(ComboBoxItem selectedItem)
            {
                var anaglyphColorTransformation = selectedItem.Tag as AnaglyphVirtualRealityProvider.AnaglyphColorTransformation;

                if (anaglyphColorTransformation != null)
                    _anaglyphVirtualRealityProvider.ColorTransformation = anaglyphColorTransformation;
            };

            SeparationDistancePanel.Visibility = Visibility.Collapsed;
        }

        // Fills comboBox with all color transformation types defined in AnaglyphVirtualRealityProvider (types of anaglyph)
        private void FillAvailableColorTransformTypes(ComboBox comboBox)
        {
            comboBox.Items.Clear();

            var colorTransformationTypes = typeof(AnaglyphVirtualRealityProvider).GetFields(BindingFlags.Public | BindingFlags.Static)
                                                                                 .Where(f => f.FieldType == typeof(AnaglyphVirtualRealityProvider.AnaglyphColorTransformation));

            // Showing collection of FieldInfo objects does not show the correct names
            //comboBox.ItemsSource = colorTransformationTypes;

            foreach (var colorTransformationType in colorTransformationTypes)
            {
                var colorTransformationInstance = colorTransformationType.GetValue(null); // Get instance of AnaglyphColorTransformation

                var comboBoxItem = new ComboBoxItem()
                {
                    Content = colorTransformationType.Name,
                    Tag = colorTransformationInstance
                };

                if (colorTransformationInstance == _anaglyphVirtualRealityProvider.ColorTransformation)
                    comboBoxItem.IsSelected = true;

                comboBox.Items.Add(comboBoxItem);
            }

            AnaglyphVirtualRealityProvider.AnaglyphColorTransformation CustomAnaglyph = new AnaglyphVirtualRealityProvider.AnaglyphColorTransformation(
                                                                    leftEyeMatrix: new Matrix3x3(0.299f, 0.587f, 0.114f,
                                                                                                 0, 0, 0,
                                                                                                 0, 0, 0),
                                                                    rightEyeMatrix: new Matrix3x3(0, 0, 0,
                                                                                                  0.299f, 0.587f, 0.114f,
                                                                                                  0.299f, 0.587f, 0.114f),
                                                                    gamma: new Vector3(2.0f, 1.0f, 1.0f)); // gamma 2.0 only for red

            // The meaning behind those numbers is the following:
            //
            // The rows in the matrix represent the destination color. For example the left eye matrix defined above will output only red color because values are defined
            // only in the first row (RGB means - 1st row = red, 2nd row = green, 3rd row = blue). The second and third row there are 0, 0, 0 - so left eye will not have any green or blue color.
            // The right eye matrix defines values for green and blue colors (green + blue = cyan). So the right eye is best seen with cyan glasses.
            //
            // The columns in the matrix represent how the source color is used.
            // For example the "0.299f, 0.587f, 0.114f" means that the output color will be get with using 29.9% red, 58.7% green and 11.4% blue
            //
            // 
            // The gamma values can be used to apply a gamma correction before the matrix calculations are applied.
            // This "boost" specific colors.
            // When value 1.0f is used for gamma, then this color will have any gamma correction.
            // When gamma vector is 1, 1, 1 then a simplified and faster pixel shader without gamma correction is used.

            var customComboBoxItem = new ComboBoxItem()
            {
                Content = "CustomAnaglyph",
                Tag = CustomAnaglyph
            };

            comboBox.Items.Add(customComboBoxItem);

            /*
             *
             * The following are all the AnaglyphColorTransformation that are defined in AnaglyphVirtualRealityProvider:
             * 
                // From: http://www.3dtv.at/knowhow/AnaglyphComparison_en.aspx

                /// <summary>
                /// TrueAnaglyph
                /// </summary>
                public static readonly AnaglyphColorTransformation TrueAnaglyph = new AnaglyphColorTransformation(
                                                                new Matrix3x3(0.299f, 0.587f, 0.114f,
                                                                              0, 0, 0,
                                                                              0, 0, 0),
                                                                new Matrix3x3(0, 0, 0,
                                                                              0, 0, 0,
                                                                              0.299f, 0.587f, 0.114f),
                                                                new Vector3(1, 1, 1)); // No gamma

                /// <summary>
                /// GrayAnaglyph 
                /// </summary>
                public static readonly AnaglyphColorTransformation GrayAnaglyph = new AnaglyphColorTransformation(
                                                                new Matrix3x3(0.299f, 0.587f, 0.114f,
                                                                              0, 0, 0,
                                                                              0, 0, 0),
                                                                new Matrix3x3(0, 0, 0,
                                                                              0.299f, 0.587f, 0.114f,
                                                                              0.299f, 0.587f, 0.114f),
                                                                new Vector3(1, 1, 1)); // No gamma

                /// <summary>
                /// ColorAnaglyph
                /// </summary>
                public static readonly AnaglyphColorTransformation ColorAnaglyph = new AnaglyphColorTransformation(
                                                                new Matrix3x3(1, 0, 0,
                                                                              0, 0, 0,
                                                                              0, 0, 0),
                                                                new Matrix3x3(0, 0, 0,
                                                                              0, 1, 0,
                                                                              0, 0, 1),
                                                                new Vector3(1, 1, 1)); // No gamma

                /// <summary>
                /// HalfColorAnaglyph
                /// </summary>
                public static readonly AnaglyphColorTransformation HalfColorAnaglyph = new AnaglyphColorTransformation(
                                                                new Matrix3x3(0.299f, 0.587f, 0.114f,
                                                                              0, 0, 0,
                                                                              0, 0, 0),
                                                                new Matrix3x3(0, 0, 0,
                                                                              0, 1, 0,
                                                                              0, 0, 1),
                                                                new Vector3(1, 1, 1)); // No gamma
        
                /// <summary>
                /// LeftEye
                /// </summary>
                public static readonly AnaglyphColorTransformation LeftEye = new AnaglyphColorTransformation(
                                                                new Matrix3x3(1, 0, 0,
                                                                              0, 1, 0,
                                                                              0, 0, 1),
                                                                new Matrix3x3(0, 0, 0,
                                                                              0, 0, 0,
                                                                              0, 0, 0),
                                                                new Vector3(1, 1, 1)); // No gamma
        
                
                /// <summary>
                /// RightEye
                /// </summary>
                public static readonly AnaglyphColorTransformation RightEye = new AnaglyphColorTransformation(
                                                                new Matrix3x3(0, 0, 0,
                                                                              0, 0, 0,
                                                                              0, 0, 0),
                                                                new Matrix3x3(1, 0, 0,
                                                                              0, 1, 0,
                                                                              0, 0, 1),
                                                                new Vector3(1, 1, 1)); // No gamma

                /// <summary>
                /// OptimizedAnaglyph
                /// </summary>
                public static readonly AnaglyphColorTransformation OptimizedAnaglyph = new AnaglyphColorTransformation(
                                                                new Matrix3x3(0, 0.7f, 0.3f,
                                                                              0, 0, 0,
                                                                              0, 0, 0),
                                                                new Matrix3x3(0, 0, 0,
                                                                              0, 1, 0,
                                                                              0, 0, 1),
                                                                new Vector3(1.5f, 1.5f, 1.5f));


                // http://www.site.uottawa.ca/~edubois/anaglyph/LeastSquaresHowToPhotoshop.pdf

                /// <summary>
                /// DuboisAnaglyph
                /// </summary>
                public static readonly AnaglyphColorTransformation DuboisAnaglyph = new AnaglyphColorTransformation(
                                                                new Matrix3x3(0.437f, 0.449f, 0.164f,
                                                                              -0.062f, -0.062f, -0.024f,
                                                                              -0.048f, -0.050f, -0.017f),
                                                                new Matrix3x3(-0.011f, -0.032f, -0.007f,
                                                                               0.377f,  0.761f,  0.009f,
                                                                              -0.026f, -0.093f,  1.234f),
                                                                new Vector3(2.4f, 2.4f, 2.4f));
            */
        }
        #endregion
        
        private void DisposeCurrentVirtualRealityProvider()
        {
            if (_splitScreenVirtualRealityProvider != null)
            {
                _splitScreenVirtualRealityProvider.Dispose();
                _splitScreenVirtualRealityProvider = null;
            }

            if (_anaglyphVirtualRealityProvider != null)
            {
                _anaglyphVirtualRealityProvider.Dispose();
                _anaglyphVirtualRealityProvider = null;
            }
        }

        private void CreateSceneObjects()
        {
            // NOTE:
            // It is recommended to use meter as basic unit for all 3D objects used by VR.

            var rootVisual3D = new ModelVisual3D();

            var wireGridVisual3D = new WireGridVisual3D()
            {
                CenterPosition = new Point3D(0, 0, 0),
                Size = new Size(3, 3),  // 3 x 3 meters
                WidthCellsCount = 10,
                HeightCellsCount = 10,
                LineColor = Colors.Gray,
                LineThickness = 2
            };

            rootVisual3D.Children.Add(wireGridVisual3D);


            double centerX = 0;
            double centerZ = 0;
            double circleRadius = 1;

            var boxMaterial = new DiffuseMaterial(Brushes.Gray);
            var sphereMaterial = new MaterialGroup();
            sphereMaterial.Children.Add(new DiffuseMaterial(Brushes.Silver));
            sphereMaterial.Children.Add(new SpecularMaterial(Brushes.White, 16));

            for (int a = 0; a < 360; a += 36)
            {
                double rad = SharpDX.MathUtil.DegreesToRadians(a);
                double x = Math.Sin(rad) * circleRadius + centerX;
                double z = Math.Cos(rad) * circleRadius + centerZ;

                var boxVisual3D = new BoxVisual3D()
                {
                    CenterPosition = new Point3D(x, 0.3, z),
                    Size = new Size3D(0.2, 0.6, 0.2),
                    Material = boxMaterial
                };

                var sphereVisual3D = new SphereVisual3D()
                {
                    CenterPosition = new Point3D(x, 0.75, z),
                    Radius = 0.15,
                    Material = sphereMaterial
                };

                rootVisual3D.Children.Add(boxVisual3D);
                rootVisual3D.Children.Add(sphereVisual3D);
            }

            MainViewport.Children.Clear();
            MainViewport.Children.Add(rootVisual3D);

            Camera1.Heading = 25;
            Camera1.Attitude = -20;
            Camera1.Distance = 4; // 3 meters

            Camera1.Refresh();
        }

        private void OnObjFileDropped(object sender, FileDropedEventArgs args)
        {
            LoadObjFile(args.FileName);

            _lastDroppedFileName = args.FileName;

            _isInternalChange = true;
            CustomSceneComboBoxItem.IsSelected = true;
            _isInternalChange = false;
        }

        private void LoadObjFile(string fileName)
        { 
            var readerObj = new Ab3d.ReaderObj();
            var wpf3DModel = readerObj.ReadModel3D(fileName);

            // To change all material to Gray use the following line:
            //Ab3d.Utilities.ModelUtils.ChangeMaterial(wpf3DModel, new DiffuseMaterial(Brushes.Gray), null);

            double readObjectSize = Math.Sqrt(wpf3DModel.Bounds.SizeX * wpf3DModel.Bounds.SizeX + wpf3DModel.Bounds.SizeY * wpf3DModel.Bounds.SizeY + wpf3DModel.Bounds.SizeZ + wpf3DModel.Bounds.SizeZ);
            var objectsCenter = new Point3D(wpf3DModel.Bounds.X + wpf3DModel.Bounds.SizeX / 2, wpf3DModel.Bounds.Y + wpf3DModel.Bounds.SizeY / 2, wpf3DModel.Bounds.Z + wpf3DModel.Bounds.SizeZ / 2);
            double scaleFactor = 3.0 / readObjectSize;  // Scale object to 3 meters


            var modelVisual3D = new ModelVisual3D();
            modelVisual3D.Content = wpf3DModel;

            var transform3DGroup = new Transform3DGroup();
            transform3DGroup.Children.Add(new TranslateTransform3D(-objectsCenter.X, -objectsCenter.Y, -objectsCenter.Z));
            transform3DGroup.Children.Add(new ScaleTransform3D(scaleFactor, scaleFactor, scaleFactor));
            modelVisual3D.Transform = transform3DGroup;
            

            MainViewport.Children.Clear();
            MainViewport.Children.Add(modelVisual3D);

            // Refresh the camea because with cleating all the objects, we have also removed the camera light.
            // Calling Refresh will recreate the light
            Camera1.Refresh();
        }

        private void ProviderSettingsComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            var comboBoxItem = ProviderSettingsComboBox.SelectedItem as ComboBoxItem;

            if (comboBoxItem != null && _providerSettingsChangedAction != null)
                _providerSettingsChangedAction(comboBoxItem);
        }

        private void EyeSeparationSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_currentVirtualRealityProvider == null)
                return;

            float newEyeSeparationValue = (float)EyeSeparationSlider.Value * 0.001f; // convert mm to meters

            if (!MathUtils.IsSame(newEyeSeparationValue, _currentVirtualRealityProvider.EyeSeparation))
                _currentVirtualRealityProvider.EyeSeparation = newEyeSeparationValue;
        }

        private void ParallaxSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_currentVirtualRealityProvider == null)
                return;

            float newParallaxValue = (float)ParallaxSlider.Value;

            if (!MathUtils.IsSame(newParallaxValue, _currentVirtualRealityProvider.Parallax))
                _currentVirtualRealityProvider.Parallax = newParallaxValue;
        }

        private void InvertViewsCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_currentVirtualRealityProvider == null)
                return;

            var newInvertLeftRightView = InvertViewsCheckBox.IsChecked ?? false;

            if (newInvertLeftRightView != _currentVirtualRealityProvider.InvertLeftRightView)
                _currentVirtualRealityProvider.InvertLeftRightView = newInvertLeftRightView;
        }

        private void AnaglyphEnabledCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_currentVirtualRealityProvider == null)
                return;

            var newIsEnabled = AnaglyphEnabledCheckBox.IsChecked ?? false;

            if (newIsEnabled != _currentVirtualRealityProvider.IsEnabled)
            {
                _currentVirtualRealityProvider.IsEnabled = newIsEnabled;

                // Instead of changing IsEnabled we can also initialize or dispose the AnaglyphVirtualRealityProvider in the DXScene
                // This is done by commenting the line above (setting IsEnabled) and uncommenting the following lines
                //if (newIsEnabled)
                //{
                //    // Initialize anaglyph rendering again
                //    MainDXViewportView.DXScene.InitializeVirtualRealityRendering(virtualRealityProvider: _anaglyphVirtualRealityProvider);
                //}
                //else
                //{
                //    // Dispose current anaglyph rendering
                //    // We do that with setting null for virtualRealityProvider
                //    // This will remove rendering steps that were added by AnaglyphVirtualRealityProvider
                //    // This will also call dipose method on AnaglyphVirtualRealityProvider and will dispose all resources that were used by AnaglyphVirtualRealityProvider
                //    MainDXViewportView.DXScene.InitializeVirtualRealityRendering(virtualRealityProvider: null);
                //}
            }
        }

        private void FullScreenButton_OnClick(object sender, RoutedEventArgs e)
        {
            var parentWindow = Window.GetWindow(this) as MainWindow;

            _isTogglingFullScreen = true;

            if (parentWindow.WindowState == WindowState.Maximized)
            {
                parentWindow.ExitFullScreen();
                TitleTextBlock.Visibility = Visibility.Visible;
            }
            else
            {
                TitleTextBlock.Visibility = Visibility.Collapsed;
                parentWindow.ShowFullScreen();
            }

            _isTogglingFullScreen = false;
        }

        private void SceneComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            if (ReferenceEquals(SceneComboBox.SelectedItem, GeneratedSceneComboBoxItem))
            {
                CreateSceneObjects();
            }
            else if (ReferenceEquals(SceneComboBox.SelectedItem, DragonSceneComboBoxItem))
            {
                string fileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources\\Models\\dragon_vrip_res3.obj");
                LoadObjFile(fileName);

                Camera1.Heading = 50;
                Camera1.Attitude = -15;
                Camera1.Distance = 0.7;
            }
            else if (ReferenceEquals(SceneComboBox.SelectedItem, CustomSceneComboBoxItem))
            {
                if (!this.IsLoaded || _isInternalChange)
                    return;

                if (_lastDroppedFileName == null)
                {
                    MessageBox.Show("Drag and drop .obj file to show the 3D models from the file");
                    return;
                }

                LoadObjFile(_lastDroppedFileName);
            }
        }

        private void ProviderComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            
            if (ReferenceEquals(ProviderComboBox.SelectedItem, AnaglyphProvider))
            {
                InitializeAnaglyphVirtualRealityProvider();
            }
            else if (ReferenceEquals(ProviderComboBox.SelectedItem, SplitScreenProvider))
            {
                InitializeSplitScreenVirtualRealityProvider();
            }
            else // (ReferenceEquals(ProviderComboBox.SelectedItem, NoProvider))
            {
                DisposeCurrentVirtualRealityProvider();
            }
        }

        private void HideHyperlink_OnClick(object sender, RoutedEventArgs e)
        {
            OptionsBorder.Visibility = Visibility.Collapsed;
            ShowLinkTextBlock.Visibility = Visibility.Visible;
        }

        private void ShowHyperlink_OnClick(object sender, RoutedEventArgs e)
        {
            OptionsBorder.Visibility = Visibility.Visible;
            ShowLinkTextBlock.Visibility = Visibility.Collapsed;
        }

        private void SeparationIncreaseButton_OnClick(object sender, RoutedEventArgs e)
        {
            // We increase separation by 2 - this means that for example left image goes for one pixel to the left and right image goes for one pixel to the right.
            ChangeSeparationDistance(2);
        }

        private void SeparationDecreaseButton_OnClick(object sender, RoutedEventArgs e)
        {
            ChangeSeparationDistance(-2);
        }

        private void ChangeSeparationDistance(double change)
        {
            if (_splitScreenVirtualRealityProvider == null)
                return;

            _splitScreenVirtualRealityProvider.ImagesSeparationDistance += (float)change;
            UpdateSeparationDistanceText();
        }

        private void UpdateSeparationDistanceText()
        {
            SeparationDistanceTextBlock.Text = string.Format("distance: {0:0}", _splitScreenVirtualRealityProvider.ImagesSeparationDistance);
        }
    }
}
