using System;
using System.Collections.Generic;
using System.Linq;
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
using System.Windows.Threading;
using Ab3d.DirectX;
using Ab3d.DirectX.Controls;

namespace Ab3d.DXEngine.Wpf.Samples.DXEnginePerformance
{
    /// <summary>
    /// Interaction logic for PolyLinesSample.xaml
    /// </summary>
    public partial class PolyLinesSample : Page
    {
        private DispatcherTimer _sliderTimer;

        private bool _isWarningIconShown;

        public PolyLinesSample()
        {
            InitializeComponent();


            // Slider changes are delayed (they are applied half second after the last slider change)
            _sliderTimer = new DispatcherTimer(DispatcherPriority.Normal);
            _sliderTimer.Interval = TimeSpan.FromSeconds(0.5);
            _sliderTimer.Tick += new EventHandler(_sliderTimer_Tick);

            PresentationTypeTextBlock.Text = MainViewportView.PresentationType.ToString();


            // To force rendering with WPF 3D uncomment the following line (be sure to reduce the number of spirals before strating the sample):
            //MainViewportView.GraphicsProfiles = new GraphicsProfile[] { GraphicsProfile.Wpf3D };


            PresentationTypeInfoControl.InfoText =
                @"PresentationType property on DXViewportView defines how the rendered image is presented to WPF.

By default DXViewportView is using DirectXImage- this sends the rendered image to the WPF composition system.
An advantage of this mode is that the 3D image can be combined with other WPF elements - WPF elements are seen through 3D scene or can be placed on top to 3D scene.
A disadvantage is that DXEngine needs that DXEngine needs to wait until the graphics card fully finishes rendering the 3D scene before the image can be sent to WPF. When rendering a very complex scene (and with lots of 3D lines that require geometry shader) this can be significantly slower then using DirectXOverlay that does not wait for GPU to complete the rendering.

When DXViewportView is using DirectXOverlay mode, the DirectX gets its own part of the screen with its own Hwnd.
This means that when DXEngine sends all the data to the graphics card it can simply call Present method and continue execution of .net code. Because graphics card has its own region of screen, it can continue rendering in the background and when ready show the image.
A disadvantage of DirectXOverlay is that it does not allow WPF objects to be drawn on top or below of DXView (the 3D image cannot be composed with other WPF objects).

When rendering many 3D lines, the DirectXOverlay mode can add a significant performance improvement over the DirectXImage mode.

To change the PresentationType in this sample, open the sample's XAML and change the value of the PresentationType in the DXViewportView declaration.";


            UseGeometryShaderInfoControl.InfoText =
@"When 'Use geometry shader' is checked, the DXEngine is using a geometry shader to create 3D lines. In this case each 3D line is created from 2 triangles. This way the DXEngine can render thick lines (lines with thickness greater than 1).

When the 'Use geometry shader' is unchecked, then the 3D lines are rendered directly by graphics card without geometry shader. This can render only lines with thickness set to 1. But because graphic card does not need to use geometry shader, it can render the lines faster.";


            Antialias3DLinesInfoControl.InfoText = 
@"The 'Antialiased 3D lines' is enabled only when the 'Use geometry shader' is unchecked.

It specifies how the 3D lines that are rendered directly by graphic card are rendered:
- when checked the 3D lines are rendered with antialiasing,
- when unchecked the 3D lines are not antialiased (allowing to render the lines even faster).";


            DrawCallsCountInfoControl.InfoText =
@"Modern graphics cards can easily render millions of 3D lines.
But such performance can be achieved only when the CPU is not limiting the performance with spending too much time in the DirectX and driver code.

Usually the time spent on the CPU can be estimated with checking the number of DirectX draw calls that are issued by the application.
It is very good to have less then 1000 draw calls per frame. Usually the number of draw calls starts affecting the 60 FPS frame rate when the number of draw calls exeeds a few thousand.

This can be easily tested in this sample:
- set the 'No.lines in one spiral:' to 100
- set both 'X spirals count' and 'Y spirals count' to 100

This will render 1 million 3D lines. Rotate the scene and you will see that the engine will be able to render only a few frames per second (having 10.000 draw calls).

Now reduce the number of 'X spirals count' and 'Y spirals count' to 10. Then increase the 'No. lines in one spiral:' to 10.000. This will again generate 1 million 3D lines.
But this time the rendering will be done with only 100 draw calls and this will make the performance much much better. If you have good graphic card you can easily increase the number of lines in one spiral to 50.000.

The point to remember is when trying to improve the performance it is usually good to try to reduce the number of draw calls.
When rendering 3D lines this can be achieved with using MultiLineVisual3D or PolyLineVisual3D or even MultiPolyLineVisual3D instead of many LineVisual3D objects.

NOTE:
You can get rendering statistics (including number of draw calls) with setting Ab3d.DirectX.DXDiagnostics.IsCollectingStatistics to true. Then you can subscribe to MainViewportView.DXScene.AfterFrameRendered event and there read date from the MainViewportView.DXScene.Statistics object.";

            this.Loaded += (sender, args) => RecreatePolyLines();


            // IMPORTANT:
            // It is very important to call Dispose method on DXSceneView after the control is not used any more (see help file for more info)
            this.Unloaded += (sender, args) => MainViewportView.Dispose();
        }

        private int GetSelectedSpiralLength()
        {
            int spiralLength = (int)SpiralLengthSlider.Value * 1000;

            if (spiralLength <= 0)
                spiralLength = 100; // use 100 as min value

            return spiralLength;
        }


        private void RecreatePolyLines()
        {
            int xCount = (int)Math.Round(XCountSlider.Value);
            int yCount = (int)Math.Round(YCountSlider.Value);
            int spiralLength = GetSelectedSpiralLength();

            

            LinesCountTextBlock.Text = string.Format("{0} x {1} x {2:#,##0} = {3:#,##0}", xCount, yCount, spiralLength, xCount * yCount * spiralLength);

            int drawCallsCount = xCount * yCount;
            DrawCallsCountTextBlock.Text = string.Format("{0} x {1} = {2:#,##0}", xCount, yCount, drawCallsCount);

            // Show waring icon when number of draw calls is over 1000
            if (drawCallsCount > 1000)
            {
                if (!_isWarningIconShown)
                {
                    DrawCallsCountInfoControl.ChangeIcon(new BitmapImage(new Uri("pack://application:,,,/Resources/warningIcon.png", UriKind.Absolute)));
                    _isWarningIconShown = true;
                }
            }
            else
            {
                if (_isWarningIconShown)
                {
                    // Remove waring icon and show standard info icon instead
                    DrawCallsCountInfoControl.ChangeIcon(null);
                    _isWarningIconShown = false;
                }
            }



            if (xCount == 0 || yCount == 0)
            {
                MainViewport.Children.Clear();
                return;
            }

            // NOTE: If this is called in Constructor it takes much longer
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                AddSpirals(xCount, yCount, spiralLength);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void AddSpirals(int xCount, int yCount, int spiralLength)
        {
            double circleRadius = 10;
            int spiralCircles = spiralLength / 20; // One circle in the spiral is created from 20 lines

            var spiralPositions = CreateSpiralPositions(startPosition: new Point3D(0, 0, 0),
                                                        circleXDirection: new Vector3D(1, 0, 0),
                                                        circleYDirection: new Vector3D(0, 1, 0),
                                                        oneSpiralCircleDirection: new Vector3D(0, 0, -10),
                                                        circleRadius: circleRadius,
                                                        segmentsPerCircle: 20,
                                                        circles: spiralCircles);

            var spiralPositionCollection = new Point3DCollection(spiralPositions);

            double xStart = -xCount * circleRadius * 3 / 2;
            double yStart = -yCount * circleRadius * 3 / 2;

            MainViewport.BeginInit();

            MainViewport.Children.Clear();

            for (int x = 0; x < xCount; x++)
            {
                for (int y = 0; y < yCount; y++)
                {
                    // PERFORMANCE NOTICE:
                    // The AddSpiralVisual3D creates one PolyLineVisual3D for each sphere.
                    // When creating many PolyLineVisual3D objects, the performance would be significantly improved if instead of many PolyLineVisual3D objects,
                    // all the spheres would be rendered with only one MultiPolyLineVisual3D. 
                    // This would allow rendering all the 3D lines with only one draw call.
                    AddSpiralVisual3D(spiralPositionCollection, new TranslateTransform3D(x * circleRadius * 3 + xStart, y * circleRadius * 3 + yStart, 0));
                }
            }

            MainViewport.EndInit();
        }

        private void AddSpiralVisual3D(Point3DCollection spiralPositionCollection, Transform3D transform)
        {
            var polyLineVisual3D = new Ab3d.Visuals.PolyLineVisual3D()
            {
                Positions = spiralPositionCollection,
                LineColor = Colors.DeepSkyBlue,
                LineThickness = 2,
                Transform = transform
            };

            MainViewport.Children.Add(polyLineVisual3D);
        }

        private List<Point3D> CreateSpiralPositions(Point3D startPosition, Vector3D circleXDirection, Vector3D circleYDirection, Vector3D oneSpiralCircleDirection, double circleRadius, int segmentsPerCircle, int circles)
        {
            List<Point> oneCirclePositions = new List<Point>(segmentsPerCircle);

            double angleStep = 2 * Math.PI / segmentsPerCircle;
            double angle = 0;

            for (int i = 0; i < segmentsPerCircle; i++)
            {
                // Get x any y position on a flat plane
                double xPos = Math.Sin(angle);
                double yPos = Math.Cos(angle);

                angle += angleStep;

                var newPoint = new Point(xPos * circleRadius, yPos * circleRadius);
                oneCirclePositions.Add(newPoint);
            }


            var allPositions = new List<Point3D>(segmentsPerCircle * circles);

            Vector3D onePositionDirection = oneSpiralCircleDirection / segmentsPerCircle;
            Point3D currentCenterPoint = startPosition;

            for (int i = 0; i < circles; i++)
            {
                for (int j = 0; j < segmentsPerCircle; j++)
                {
                    double xCircle = oneCirclePositions[j].X;
                    double yCircle = oneCirclePositions[j].Y;

                    var point3D = new Point3D(currentCenterPoint.X + (xCircle * circleXDirection.X) + (yCircle * circleYDirection.X),
                                              currentCenterPoint.Y + (xCircle * circleXDirection.Y) + (yCircle * circleYDirection.Y),
                                              currentCenterPoint.Z + (xCircle * circleXDirection.Z) + (yCircle * circleYDirection.Z));

                    allPositions.Add(point3D);

                    currentCenterPoint += onePositionDirection;
                }
            }

            return allPositions;
        }

        private void OnSpiralsCountSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!this.IsLoaded)
                return;

            // timer is used to dalay the creation of polylines
            // Restart the timer
            _sliderTimer.Stop();
            _sliderTimer.Start();
        }

        private void OnSpiralLengthSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!this.IsLoaded)
                return;

            var selectedSpiralLength = GetSelectedSpiralLength();
            SpiralLengthTextBlock.Text = string.Format("{0:#,##0}", selectedSpiralLength);

            // timer is used to dalay the creation of polylines
            // Restart the timer
            _sliderTimer.Stop();
            _sliderTimer.Start();
        }

        void _sliderTimer_Tick(object sender, EventArgs e)
        {
            _sliderTimer.Stop();

            RecreatePolyLines();
        }


        private void AnimationButton_Click(object sender, RoutedEventArgs e)
        {
            if (Camera1.IsRotating)
                StopAnimation();
            else
                StartAnimation();
        }

        private void StopAnimation()
        {
            AnimationButton.Content = "Start camera animation";
            Camera1.StopRotation();
        }

        private void StartAnimation()
        {
            Camera1.StartRotation(20, 0);
            AnimationButton.Content = "Stop camera animation";
        }

        private void OnSettingsCheckboxChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            // Antialiased CheckBox is enabled only when we do not use geometry shader
            Antialias3DLinesCheckBox.IsEnabled = !(UseGeometryShaderCheckBox.IsChecked ?? false);

            // DXSceneDeviceCreated is called after DXScene and DXDevice are created and before the 3D objects are initialized
            if (MainViewportView.DXScene != null) // In case of WPF rendering the DXScene is null
            {
                MainViewportView.DXScene.UseGeometryShaderFor3DLines = UseGeometryShaderCheckBox.IsChecked ?? false;
                MainViewportView.DXScene.RenderAntialiased3DLines = Antialias3DLinesCheckBox.IsChecked ?? false;

                MainViewportView.Refresh(); // Force rendering again
            }
        }
    }
}
