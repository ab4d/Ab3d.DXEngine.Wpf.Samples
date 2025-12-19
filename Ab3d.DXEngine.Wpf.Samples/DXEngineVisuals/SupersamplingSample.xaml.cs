using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Ab3d.Cameras;
using Ab3d.Common.Cameras;
using Ab3d.Controls;
using Ab3d.DirectX;
using Ab3d.DirectX.Controls;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    /// <summary>
    /// Interaction logic for SupersamplingSample.xaml
    /// </summary>
    public partial class SupersamplingSample : Page
    {
        public SupersamplingSample()
        {
            InitializeComponent();

            // NOTE: Standard GraphicsProfiles use the following MSAA and SSAA settings:
            // LowQualityHardwareRendering:    0xMSAA,  1xSSAA
            // NormalQualityHardwareRendering: 4xMSAA,  1xSSAA
            // HighQualityHardwareRendering:   4xMSAA,  4xSSAA
            // UltraQualityHardwareRendering:  2xMSAA, 16xSSAA

            AddDXViewport3DGrid("No MSAA, No SSAA", "No multisampling and no supersampling", 
                                multisamplingCount: 0, supersamplingCount: 1, rowIndex: 0, columnIndex: 0);

            AddDXViewport3DGrid("8x MSAA, No SSAA", "Multisampling  (MSAA) produces smooth antialiased edges, but because color is calculated only once per each pixel, the smaller details can be lost.", 
                                multisamplingCount: 8, supersamplingCount: 1, rowIndex: 0, columnIndex: 1);
            
            AddDXViewport3DGrid("No MSAA, 16x SSAA", "Supersampling (SSAA) renders the scene to a bigger texture (in this case 16 times bigger: width and height are 4 times bigger) and then down-samples the texture to the final size. This preserves tiny details.",
                                multisamplingCount: 2, supersamplingCount: 16, rowIndex: 0, columnIndex: 2);
        }

        private ModelVisual3D CreateScene()
        {
            var supersamplingModel = (GeometryModel3D) this.FindResource("SupersamplingModel");
            supersamplingModel = supersamplingModel.Clone(); // Clone the model so that each DXViewportView (and each DirectX device) have its own model

            var rootModelGroup = new Model3DGroup();

            for (int i = 0; i < 10; i++)
            {
                var model3DGroup = new Model3DGroup();
                model3DGroup.Children.Add(supersamplingModel);
                model3DGroup.Transform = new TranslateTransform3D(0, 0, i * -100); // distribute the models so that each of them is longer from the camera

                rootModelGroup.Children.Add(model3DGroup);
            }

            var modelVisual3D = new ModelVisual3D();
            modelVisual3D.Content = rootModelGroup;

            return modelVisual3D;
        }

        private void AddDXViewport3DGrid(string title, string subTitle, int multisamplingCount, int supersamplingCount, int rowIndex, int columnIndex)
        {
            var viewport3D = new Viewport3D();

            var sceneModelVisual3D = CreateScene();
            viewport3D.Children.Add(sceneModelVisual3D);


            var dxViewportView = new DXViewportView(viewport3D);

            // Set GraphicsProfile:
            var graphicsProfile = new GraphicsProfile("CustomGraphicsProfile", 
                                                      GraphicsProfile.DriverTypes.DirectXHardware, 
                                                      ShaderQuality.High, 
                                                      preferedMultisampleCount: multisamplingCount,
                                                      supersamplingCount: supersamplingCount, 
                                                      textureFiltering: TextureFilteringTypes.Anisotropic_x4);

            dxViewportView.GraphicsProfiles = new GraphicsProfile[] { graphicsProfile, GraphicsProfile.Wpf3D }; // Add WPF 3D as fallback


            var border = new Border()
            {
                Background          = Brushes.Transparent,
                BorderBrush         = Brushes.Gray,
                BorderThickness     = new Thickness(1, 1, 1, 1),
                Margin              = new Thickness(1, 1, 3, 3),
                UseLayoutRounding   = true,
                SnapsToDevicePixels = true
            };

            border.Child = dxViewportView;
            
            
            var targetPositionCamera = new TargetPositionCamera()
            {
                TargetPosition   = new Point3D(15, 50, 5),
                Heading          = -17,
                Attitude         = -25,
                ShowCameraLight  = ShowCameraLightType.Always,
                Distance         = 80,
                TargetViewport3D = viewport3D
            };

            var mouseCameraController = new MouseCameraController()
            {
                TargetCamera           = targetPositionCamera,
                EventsSourceElement    = border,
                RotateCameraConditions = MouseCameraController.MouseAndKeyboardConditions.LeftMouseButtonPressed,
                MoveCameraConditions   = MouseCameraController.MouseAndKeyboardConditions.ControlKey | MouseCameraController.MouseAndKeyboardConditions.LeftMouseButtonPressed,
            };


            var titlesPanel = new StackPanel()
            {
                Orientation         = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment   = VerticalAlignment.Top,
                Margin              = new Thickness(10, 10, 10, 0)
            };

            if (title != null)
            {
                var textBlock = new TextBlock()
                {
                    Text = title,
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32)),
                };

                titlesPanel.Children.Add(textBlock);
            }
            
            if (subTitle != null)
            {
                var textBlock = new TextBlock()
                {
                    Text         = subTitle,
                    FontSize     = 14,
                    TextWrapping = TextWrapping.Wrap,
                    Margin       = new Thickness(0, 3, 0, 0)
                };

                titlesPanel.Children.Add(textBlock);
            }


            var viewRootGrid = new Grid();
            viewRootGrid.Children.Add(border);
            viewRootGrid.Children.Add(titlesPanel);
            viewRootGrid.Children.Add(targetPositionCamera);
            viewRootGrid.Children.Add(mouseCameraController);


            Grid.SetColumn(viewRootGrid, columnIndex);
            Grid.SetRow(viewRootGrid, rowIndex);
            RootGrid.Children.Add(viewRootGrid);
        }
    }
}
