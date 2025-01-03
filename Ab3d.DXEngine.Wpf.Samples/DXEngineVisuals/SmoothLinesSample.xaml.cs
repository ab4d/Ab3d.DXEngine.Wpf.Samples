﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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
using Ab3d.Cameras;
using Ab3d.Common;
using Ab3d.Common.Cameras;
using Ab3d.Controls;
using Ab3d.DirectX;
using Ab3d.DirectX.Common;
using Ab3d.DirectX.Controls;
using Ab3d.Visuals;
using SharpDX.Direct3D;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    /// <summary>
    /// Interaction logic for SmoothLinesSample.xaml
    /// </summary>
    public partial class SmoothLinesSample : Page
    {
        private double _dpiScale;

        private List<DXViewportView> _dxViewports = new List<DXViewportView>();

        public SmoothLinesSample()
        {
            InitializeComponent();

            DisableWpfResizingOfRenderedImageInfoControl.InfoText = 
@"When checked than NearestNeighbor image filtering is used by WPF and this can show
a sharper rendered image because the exact same image is shows as rendered by DXEngine.
This requires that the root Window element or parent element that sets the size for 
DXViewportView (for example Grid) has UseLayoutRounding set to true, otherwise image tearing can appear.

When unchecked then linear filtering is used exactly match (to subpixel value) the size 
of the rendered image and exactly align it to other WPF objects. 
This may produce more blurred image.";

            FilterTypeInfoControl.InfoText = 
@"SSAA-FilterType sets the ResolveFilter property on the DefaultResolveBackBufferRenderingStep.
It is used only when using super-sampling (SSAA; on lower two 3D scenes)
and defines how the super-sampled image is downsampled to its final size. 
Using a filter results in generally smoother at the cost of a blurrier final result.
Default value is RotatedFilterSize5.";


            // NOTE: Standard GraphicsProfiles use the following MSAA and SSAA settings:
            // LowQualityHardwareRendering:             0xMSAA,  1xSSAA
            // NormalQualityHardwareRendering:          4xMSAA,  1xSSAA
            // OptimizedHighQualityHardwareRendering:   4xMSAA,  2xSSAA
            // HighQualityHardwareRendering:            4xMSAA,  4xSSAA
            // UltraQualityHardwareRendering:           2xMSAA, 16xSSAA

            // Get dpi scale
            if (Application.Current == null || Application.Current.MainWindow == null)
                throw new Exception("Main application's window not yet created");

            double dpiScaleX, dpiScaleY;
            DXView.GetDpiScale(Application.Current.MainWindow, out dpiScaleX, out dpiScaleY);

            _dpiScale = (dpiScaleX + dpiScaleY) * 0.5; // Get one value for dpi scale


            AddDXViewport3DGrid("No MSAA, No SSAA", "LowQualityHardwareRendering",
                                multisamplingCount: 0, supersamplingCount: 1, rowIndex: 0, columnIndex: 0);

            AddDXViewport3DGrid("4x MSAA, No SSAA", "NormalQualityHardwareRendering",
                                multisamplingCount: 4, supersamplingCount: 1, rowIndex: 0, columnIndex: 1);

            AddDXViewport3DGrid("4x MSAA, 2x SSAA", "OptimizedHighQualityHardwareRendering",
                                multisamplingCount: 4, supersamplingCount: 2, rowIndex: 1, columnIndex: 0);

            AddDXViewport3DGrid("4x MSAA, 4x SSAA", "HighQualityHardwareRendering",
                                multisamplingCount: 4, supersamplingCount: 2, rowIndex: 1, columnIndex: 1);


            // To compare HighQualityHardwareRendering and UltraQualityHardwareRendering,
            // comment the two calls to AddDXViewport3DGrid and uncomment the following:

            //AddDXViewport3DGrid("4x MSAA, 4x SSAA", "HighQualityHardwareRendering",
            //    multisamplingCount: 4, supersamplingCount: 4, rowIndex: 1, columnIndex: 0);

            //AddDXViewport3DGrid("2x MSAA, 16x SSAA", "UltraQualityHardwareRendering",
            //                    multisamplingCount: 2, supersamplingCount: 16, rowIndex: 1, columnIndex: 1);


            // When there is no DPI scaling used, then do not show the DPI scale info
            if (_dpiScale == 1)
                ScreenPixelLineThicknessTextBlock.Visibility = Visibility.Collapsed;
            else
                ScreenPixelLineThicknessTextBlock.Text = string.Format(System.Globalization.CultureInfo.InvariantCulture, ScreenPixelLineThicknessTextBlock.Text, _dpiScale);
        }

        private ModelVisual3D CreateScene()
        {
            var rootModelVisual3D = new ModelVisual3D();

            double screenPixelLineThickness = 1.0 / _dpiScale; // set line thickness so that it will take 1 pixel
            
            string oneLineThickLineText;
            double oneLineThickness;

            if (_dpiScale == 1)
            {
                // No DPI scaling is used
                oneLineThickness = 0.8;
                oneLineThickLineText = null; // No special line title
            }
            else
            {
                // DPI scaling is used => show line with 1 screen pixel line thickness
                oneLineThickness = screenPixelLineThickness;
                oneLineThickLineText = string.Format(System.Globalization.CultureInfo.InvariantCulture, "LineThickness {0:0.##} (1px *):", screenPixelLineThickness);
            }


            AddLinesFan(rootModelVisual3D, startPosition: new Point3D(-210, -5, 0), lineThickness: 0.6, linesLength: 100);
            AddLinesFan(rootModelVisual3D, startPosition: new Point3D(-50, -5, 0),  lineThickness: oneLineThickness, linesLength: 100, oneLineThickLineText);

            AddLinesFan(rootModelVisual3D, startPosition: new Point3D(-210, -140, 0), lineThickness: 1, linesLength: 100);
            AddLinesFan(rootModelVisual3D, startPosition: new Point3D(-50, -140, 0),  lineThickness: 2, linesLength: 100);


            // Add lines that start from vertical lines and then continue with slightly angled lines
            var linePositions = new Point3DCollection();

            double xPos = 100;
            for (int i = 0; i <= 16; i++)
            {
                var startPosition = new Point3D(xPos, -5.5, 0);

                linePositions.Add(startPosition);
                linePositions.Add(startPosition + new Vector3D(i, 100, 0));
                
                xPos += 6;
            }

            var multiLineVisual3D = new MultiLineVisual3D()
            {
                Positions     = linePositions,
                LineColor     = Colors.Black,
                LineThickness = screenPixelLineThickness
            };

            rootModelVisual3D.Children.Add(multiLineVisual3D);



            // Add line circles
            double radius = 10;
            while (radius <= 40)
            {
                var lineArcVisual3D = new LineArcVisual3D()
                {
                    Radius = radius,
                    LineColor = Colors.Black,
                    LineThickness = screenPixelLineThickness,
                    StartAngle = 0,
                    EndAngle = 360,
                    Segments = 60,
                    CircleNormal = new Vector3D(0, 0, 1),
                };

                var transform3DGroup = new Transform3DGroup();
                transform3DGroup.Children.Add(new ScaleTransform3D(1.5, 1, 1));
                transform3DGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), 45)));
                transform3DGroup.Children.Add(new TranslateTransform3D(150, -90.5, 0));
                lineArcVisual3D.Transform = transform3DGroup;

                radius += radius <= 20 ? 2 : 3;

                rootModelVisual3D.Children.Add(lineArcVisual3D);
            }

            return rootModelVisual3D;
        }

        private void AddLinesFan(ModelVisual3D parentModelVisual3D, Point3D startPosition, double lineThickness, double linesLength, string titleText = null)
        {
            if (titleText == null)
                titleText = string.Format(System.Globalization.CultureInfo.InvariantCulture, "LineThickness {0:0.0}:", lineThickness);


            var textBlockVisual3D = new TextBlockVisual3D()
            {
                Position         = startPosition + new Vector3D(-2, linesLength + 5, 0),
                PositionType     = PositionTypes.Bottom | PositionTypes.Left,
                FontSize         = 11,
                RenderBitmapSize = new Size(1024, 256),
                Text             = titleText
            };

            parentModelVisual3D.Children.Add(textBlockVisual3D);


            var linePositions = new Point3DCollection();

            for (int a = 0; a <= 90; a += 5)
            {
                Point3D endPosition = startPosition + new Vector3D(linesLength * Math.Cos(a / 180.0 * Math.PI), linesLength * Math.Sin(a / 180.0 * Math.PI), 0);

                linePositions.Add(startPosition);
                linePositions.Add(endPosition);
            }

            var multiLineVisual3D = new MultiLineVisual3D()
            {
                Positions     = linePositions,
                LineColor     = Colors.Black,
                LineThickness = lineThickness
            };

            parentModelVisual3D.Children.Add(multiLineVisual3D);
        }

        private DXViewportView AddDXViewport3DGrid(string title, string graphicsProfileName, int multisamplingCount, int supersamplingCount, int rowIndex, int columnIndex)
        {
            var viewport3D = new Viewport3D();

            var sceneModelVisual3D = CreateScene();
            viewport3D.Children.Add(sceneModelVisual3D);


            var dxViewportView = new DXViewportView(viewport3D);
            dxViewportView.BackgroundColor     = Colors.White;
            dxViewportView.UseLayoutRounding   = true;
            dxViewportView.SnapsToDevicePixels = true;

            _dxViewports.Add(dxViewportView);

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
                Background          = Brushes.White,
                BorderBrush         = Brushes.Gray,
                BorderThickness     = new Thickness(1, 1, 1, 1),
                Margin              = new Thickness(1, 1, 2, 2),
                UseLayoutRounding   = true,
                SnapsToDevicePixels = true
            };

            border.Child = dxViewportView;
            

            var targetPositionCamera = new TargetPositionCamera()
            {
                TargetPosition  = new Point3D(0, 0, 0),
                Heading         = 0,
                Attitude        = 0,
                ShowCameraLight = ShowCameraLightType.Always,
                CameraType      = BaseCamera.CameraTypes.OrthographicCamera,
                CameraWidth     = 800,
                Distance        = 800,
                TargetViewport3D = viewport3D
            };

            dxViewportView.DXRenderSizeChanged += delegate(object sender, DXViewSizeChangedEventArgs args)
            {
                targetPositionCamera.CameraWidth = dxViewportView.DXRenderSize.Width;
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
                Orientation         = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment   = VerticalAlignment.Top,
                Margin              = new Thickness(10, 5, 10, 0)
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
            
            if (graphicsProfileName != null)
            {
                var textBlock = new TextBlock()
                {
                    Text              = "GraphicsProfile: " + graphicsProfileName,
                    FontSize          = 12,
                    TextWrapping      = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin            = new Thickness(graphicsProfileName.Length > 35 ? 3 : 15, 0, 0, 0) // Special margin for "OptimizedHighQualityHardwareRendering"
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

            return dxViewportView;
        }

        private void OnDisableWpfResizingOfRenderedImageCheckCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;
            
            foreach (var dxViewportView in _dxViewports)
                dxViewportView.DisableWpfResizingOfRenderedImage = DisableWpfResizingOfRenderedImageCheckBox.IsChecked ?? false;
        }

        private void FilterTypeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded || FilterTypeComboBox.SelectedIndex == -1)
                return;


            var filterType = (SupersamplingResolveFilterType)FilterTypeComboBox.SelectedIndex;

            foreach (var dxViewportView in _dxViewports)
            {
                dxViewportView.DXScene.DefaultResolveBackBufferRenderingStep.ResolveFilter = filterType;
                dxViewportView.Refresh(); // Render again
            }
        }
    }
}
