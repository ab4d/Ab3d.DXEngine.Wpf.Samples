using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using System.Windows.Threading;
using Ab3d.DirectX;
using Ab3d.DirectX.Controls;
using Ab3d.DirectX.Materials;
using Ab3d.Visuals;
using SharpDX;
using Matrix = SharpDX.Matrix;
using Point = System.Windows.Point;

namespace Ab3d.DXEngine.Wpf.Samples.DXEnginePerformance
{
    /// <summary>
    /// Interaction logic for LinePerformanceSample.xaml
    /// </summary>
    public partial class LinePerformanceSample : Page
    {
        private DispatcherTimer _sliderTimer;

        private Stopwatch _initializationTimeStopwatch;

        private DisposeList _disposables;

        private enum LineTypes
        {
            None,
            MultiLines,
            MultiPolyLines,
            MultiMultiLines,
            MultiScreenSpaceLineNode,
            MultiScreenSpaceLineNodePolylines,
            SingleMultiPolyLines,
            SingleScreenSpaceLineNode,
            ScreenSpaceLineNodeReusedMesh,
            MultiThreadedScreenSpaceLineNode
        }

        public LinePerformanceSample()
        {
            InitializeComponent();


            _initializationTimeStopwatch = new Stopwatch();
            _disposables = new DisposeList();

            // Slider changes are delayed (they are applied half second after the last slider change)
            _sliderTimer = new DispatcherTimer(DispatcherPriority.Normal);
            _sliderTimer.Interval = TimeSpan.FromSeconds(0.5);
            _sliderTimer.Tick += new EventHandler(_sliderTimer_Tick);

            PresentationTypeTextBlock.Text = MainViewportView.PresentationType.ToString();


            // To force rendering with WPF 3D uncomment the following line (be sure to reduce the number of spirals before starting the sample):
            //MainViewportView.GraphicsProfiles = new GraphicsProfile[] { GraphicsProfile.Wpf3D };


            PresentationTypeInfoControl.InfoText =
                @"PresentationType property on DXViewportView defines how the rendered image is presented to WPF.

By default DXViewportView is using DirectXImage - this sends the rendered image to the WPF composition system.
An advantage of this mode is that the 3D image can be combined with other WPF elements - WPF elements are seen through 3D scene or can be placed on top to 3D scene.
A disadvantage is that DXEngine needs to wait until the graphics card fully finishes rendering the 3D scene before the image can be sent to WPF. When rendering a very complex scene (and with lots of 3D lines that require geometry shader) this can be significantly slower then using DirectXOverlay that does not wait for GPU to complete the rendering.

When DXViewportView is using DirectXOverlay mode, the DirectX gets its own part of the screen with its own Hwnd.
This means that when DXEngine sends all the data to the graphics card it can simply call Present method and continue execution of the .Net code. Because graphics card has its own region of screen, it can continue rendering in the background and when ready show the image.
A disadvantage of DirectXOverlay is that it does not allow WPF objects to be drawn on top or below of DXViewportView (the 3D image cannot be composed with other WPF objects).

When rendering many 3D lines, the DirectXOverlay mode can add a significant performance improvement over the DirectXImage mode.

To change the PresentationType in this sample, open the sample's XAML and change the value of the PresentationType in the DXViewportView declaration.";


            HardwareAccelerate3DLinesInfoControl.InfoText =
                @"When 'Hardware accelerate 3D lines' is checked, the DXEngine is hardware accelerating rendering of 3D lines. In this case the meshes of 3D lines are generated in graphics card's geometry shader (when 'Use geometry shader' is checked) or by DirectX line rendering technique (when 'Use geometry shader' is unchecked). Hardware accelerated line rendering allow rendering millions of line segments (on decent graphics card).

When the 'Hardware accelerate 3D lines' is unchecked, then the meshes for 3D lines are generated on the CPU by the Ab3d.PowerToys library. This means that each time camera is changed, new line meshes need to be generated on the CPU. This is significantly slower than when using hardware accelerated line rendering.";


            UseGeometryShaderInfoControl.InfoText =
@"When 'Use geometry shader' is checked, the DXEngine is using a geometry shader to create 3D lines. In this case each 3D line is created from 2 triangles. This way the DXEngine can render thick lines (lines with thickness greater than 1).

When the 'Use geometry shader' is unchecked, then the 3D lines are rendered directly by DirectX without using a geometry shader. This can render only lines with thickness set to 1. But because graphic card does not need to use geometry shader, it can render the lines faster.";


            Antialias3DLinesInfoControl.InfoText = 
@"The 'Antialiased 3D lines' is enabled only when the 'Use geometry shader' is unchecked.

It specifies how the 3D lines that are rendered directly by DirectX:
- when checked the 3D lines are rendered with antialiasing,
- when unchecked the 3D lines are not antialiased (rendering the lines even faster).";


            InitializationTimeInfoControl.InfoText =
@"Initialization time shows time in milliseconds that was elapsed from the start of creation of 3D lines to the end of update phase (after creating all DirectX resources).
Note that the first time the sample is started, it may take a little bit longer to initialize the lines. To test that, click on 'Recreate lines' button again.";

            DrawCallsCountInfoControl.InfoText =
@"Modern graphics cards can easily render millions of 3D lines.
But such performance can be achieved only when the CPU is not limiting the performance by spending too much time in the DirectX and driver code.

Usually the time spent on the CPU can be estimated by checking the number of DirectX draw calls that are issued by the application.
It is very good to have less then 1000 draw calls per frame. Usually the number of draw calls starts affecting the 60 FPS frame rate when the number of draw calls exceeds a few thousand.

This can be easily tested in this sample:
- set the 'No.lines in one spiral:' to 100
- set both 'X spirals count' and 'Y spirals count' to 100

This will render 1 million 3D lines. Rotate the scene and you will see that the engine will be able to render only a few frames per second (having 10.000 draw calls).

Now reduce the number of 'X spirals count' and 'Y spirals count' to 10. Then increase the 'No. lines in one spiral:' to 10.000. This will again generate 1 million 3D lines.
But this time the rendering will be done with only 100 draw calls and this will make the performance much much better. If you have good graphic card you can easily increase the number of lines in one spiral to 50.000.

The point to remember is when trying to improve the performance it is usually good to try to reduce the number of draw calls (usually correlates with number of created objects).

If you need to render multiple lines with different colors in one draw call, then you can use ScreenSpaceLineNode and PositionColoredLineMaterial (see DXEngineAdvanced / ScreenSpaceLineNodeSample sample to see how to do that).

NOTE:
You can get rendering statistics (including number of draw calls) by setting Ab3d.DirectX.DXDiagnostics.IsCollectingStatistics to true. Then you can subscribe to MainViewportView.DXScene.AfterFrameRendered event and there read date from the MainViewportView.DXScene.Statistics object. You can also use the DiagnosticsWindow or DXEngineSnoop application.";

            
            // Wait until the the DXScene is initialized
            MainViewportView.DXSceneInitialized += MainViewportViewOnDXSceneInitialized;


            // IMPORTANT:
            // It is very important to call Dispose method on DXSceneView after the control is not used any more (see help file for more info)
            this.Unloaded += (sender, args) =>
            {
                _disposables.Dispose();
                MainViewportView.Dispose();
            };
        }

        private void MainViewportViewOnDXSceneInitialized(object sender, EventArgs e)
        {
            if (MainViewportView.DXScene == null)
            {
                // WPF 3D rendering
                // Disable using ScreenSpaceLineNode
                MultiScreenSpaceLineNodeRadioButton.IsEnabled           = false;
                MultiScreenSpaceLineNodePolylinesRadioButton.IsEnabled  = false;
                SingleScreenSpaceLineNodePolylinesRadioButton.IsEnabled = false;
                ReusedMeshScreenSpaceLineNodeRadioButton.IsEnabled      = false;
                MultihreadedScreenSpaceLineNodeRadioButton.IsEnabled    = false;

                MultiPolyLinesRadioButton.IsChecked = true; // This will also call RecreateLines
            }
            else
            {
                // Call RecreateLines two times because the first time it takes significantly more time to initialize the lines
                RecreateLines();
                RecreateLines();
            }
        }

        private int GetSelectedSpiralLength()
        {
            int spiralLength = (int)SpiralLengthSlider.Value * 1000;

            if (spiralLength <= 0)
                spiralLength = 100; // use 100 as min value

            return spiralLength;
        }
        
        private void RecreateLines()
        {
            InitializationTimeTextBlock.Text = null;
            LinesCountTextBlock.Text = null;
            DrawCallsCountTextBlock.Text = null;

            // Wait until idle and then Create the lines
            Dispatcher.BeginInvoke(new Action(CreateLines), DispatcherPriority.Background);
        }

        private void CreateLines()
        {
            if (MainViewportView.DXScene == null) // This may happen if user already left the sample before BeginInvoke with Background is executed
                return;


            Mouse.OverrideCursor = Cursors.Wait;

            MainViewport.Children.Clear();

            // Manually call Update so that time to clean the old lines will not be measured with _initializationTimeStopwatch
            MainViewportView.Refresh();

            GC.Collect();
            GC.WaitForFullGCComplete();
            GC.Collect();

            _disposables.Dispose();
            _disposables = new DisposeList();


            int xCount = (int)Math.Round(XCountSlider.Value);
            int yCount = (int)Math.Round(YCountSlider.Value);
            int spiralLength = GetSelectedSpiralLength();


            var selectedLineTypes = GetSelectedLineTypes();
            var isScreenSpaceLineNode = IsScreenSpaceLineNode(selectedLineTypes);
            var isPolyLine = IsPolyLine(selectedLineTypes);

            int lineSegmentsCount = xCount * yCount * spiralLength;


            if (xCount >= 0 && yCount >= 0)
            {
                _initializationTimeStopwatch.Restart();

                try
                {
                    if (isScreenSpaceLineNode)
                        AddDXEngineSpirals(xCount, yCount, spiralLength, selectedLineTypes);
                    else
                        AddSpirals(xCount, yCount, spiralLength, selectedLineTypes);
                }
                finally
                {
                    // Recreate the DXEngine objects (if we would call Refresh, then the DXEngine may also wait for VSync and this would not provide correct times)
                    MainViewportView.Update();

                    _initializationTimeStopwatch.Stop();
                    InitializationTimeTextBlock.Text = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:#,##0.00} ms", _initializationTimeStopwatch.Elapsed.TotalMilliseconds);

                    Mouse.OverrideCursor = null;
                }
            }
            else
            {
                Mouse.OverrideCursor = null;
            }

            if (isPolyLine)
                LinesCountTextBlock.Text = string.Format("{0} x {1} x {2:#,##0} = {3:#,##0}", xCount, yCount, spiralLength, lineSegmentsCount);
            else
                LinesCountTextBlock.Text = string.Format("{0} x {1} x {2:#,##0} / 2 = {3:#,##0}", xCount, yCount, spiralLength, lineSegmentsCount / 2);

            int drawCallsCount;
            if (selectedLineTypes == LineTypes.SingleMultiPolyLines || selectedLineTypes == LineTypes.SingleScreenSpaceLineNode)
            {
                drawCallsCount = 1;
                DrawCallsCountTextBlock.Text = "1";
            }
            else if (selectedLineTypes == LineTypes.MultiLines)
            {
                drawCallsCount = xCount * yCount * lineSegmentsCount / 2;
                DrawCallsCountTextBlock.Text = string.Format("{0} x {1} x {2:#,##0} / 2 = {3:#,##0}", xCount, yCount, spiralLength, lineSegmentsCount / 2);
            }
            else
            {
                drawCallsCount = xCount * yCount;
                DrawCallsCountTextBlock.Text = string.Format("{0} x {1} = {2:#,##0}", xCount, yCount, drawCallsCount);
            }


            // Show waring icon when number of draw calls is over 1000
            WarningImage.Visibility = (drawCallsCount > 1000) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AddSpirals(int xCount, int yCount, int spiralLength, LineTypes selectedLineTypes)
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

            double xStart = -xCount * circleRadius * 3 / 2;
            double yStart = -yCount * circleRadius * 3 / 2;

            
            List<Point3DCollection> positionsList;
            if (selectedLineTypes == LineTypes.SingleMultiPolyLines)
                positionsList = new List<Point3DCollection>(xCount * yCount);
            else
                positionsList = null;

            for (int x = 0; x < xCount; x++)
            {
                for (int y = 0; y < yCount; y++)
                {
                    var lineTransform = new TranslateTransform3D(x * circleRadius * 3 + xStart, y * circleRadius * 3 + yStart, 0);

                    switch (selectedLineTypes)
                    {
                        case LineTypes.MultiPolyLines:
                            var polyLineVisual3D = new Ab3d.Visuals.PolyLineVisual3D()
                            {
                                Positions = spiralPositions,
                                LineColor = Colors.DeepSkyBlue,
                                LineThickness = 2,
                                Transform = lineTransform
                            };
                            MainViewport.Children.Add(polyLineVisual3D);
                            break;
                        
                        case LineTypes.MultiMultiLines:
                            var multiLineVisual3D = new Ab3d.Visuals.MultiLineVisual3D()
                            {
                                Positions = spiralPositions,
                                LineColor = Colors.DeepSkyBlue,
                                LineThickness = 2,
                                Transform = lineTransform
                            };
                            MainViewport.Children.Add(multiLineVisual3D);
                            break;
                        
                        case LineTypes.MultiLines:
                            for (var i = 0; i < spiralPositions.Count; i += 2)
                            {
                                var lineVisual3D = new Ab3d.Visuals.LineVisual3D()
                                {
                                    StartPosition = spiralPositions[i],
                                    EndPosition = spiralPositions[i + 1],
                                    LineColor = Colors.DeepSkyBlue,
                                    LineThickness = 2,
                                    Transform = lineTransform
                                };
                                MainViewport.Children.Add(lineVisual3D);
                            }
                            break;

                        case LineTypes.SingleMultiPolyLines:
                            // For this sample we need to manually transform the lines.
                            // Therefore initializing the positions list for MultiPolyLineVisual3D takes longer than it could take in common use case.
                            // The most significant performance improvement of MultiPolyLineVisual3D is that all the polylines can be rendered by one draw call.
                            var transformedPositions = new Point3DCollection(spiralPositions.Count);
                            for (int i = 0; i < spiralPositions.Count; i++)
                                transformedPositions.Add(lineTransform.Transform(spiralPositions[i]));

                            if (positionsList != null)
                                positionsList.Add(transformedPositions);
                            break;
                    }
                }
            }

            if (selectedLineTypes == LineTypes.SingleMultiPolyLines)
            {
                // Only single MultiPolyLineVisual3D is created for all xCount * yCount polylines
                var multiPolyLineVisual3D = new MultiPolyLineVisual3D()
                {
                    PositionsList = positionsList,
                    LineColor = Colors.DeepSkyBlue,
                    LineThickness = 2,
                };
                MainViewport.Children.Add(multiPolyLineVisual3D);
            }
        }

        // This method add DXEngine's ScreenSpaceLineNode instead of WPF's LineVisual3D objects
        private void AddDXEngineSpirals(int xCount, int yCount, int spiralLength, LineTypes selectedLineTypes)
        {
            float circleRadius = 10;
            int spiralCircles = spiralLength / 20; // One circle in the spiral is created from 20 lines

            var spiralPositions = CreateDXSpiralPositions(startPosition: new Vector3(0, 0, 0),
                                                          circleXDirection: new Vector3(1, 0, 0),
                                                          circleYDirection: new Vector3(0, 1, 0),
                                                          oneSpiralCircleDirection: new Vector3(0, 0, -10),
                                                          circleRadius: circleRadius,
                                                          segmentsPerCircle: 20,
                                                          circles: spiralCircles);

            float xStart = -xCount * circleRadius * 3 / 2;
            float yStart = -yCount * circleRadius * 3 / 2;


            List<Vector3[]> positionsList;
            if (selectedLineTypes == LineTypes.SingleScreenSpaceLineNode)
                positionsList = new List<Vector3[]>(xCount * yCount);
            else
                positionsList = null;

            var lineMaterial = new LineMaterial(Colors.DeepSkyBlue.ToColor4(), lineThickness: 2);
            lineMaterial.InitializeResources(MainViewportView.DXScene.DXDevice);
            _disposables.Add(lineMaterial);

            if (selectedLineTypes == LineTypes.MultiThreadedScreenSpaceLineNode)
            {
                // We will create ScreenSpaceLineNode and initialize DirectX resources in BG threads
                // After the ScreenSpaceLineNode objects are created, we need to add them to the scene in the main UI thread
                //
                // When this sample is run in debug mode in Visual Studio, then this option may be running much slower then when running in release mode without VS
                var lineNodes = new ScreenSpaceLineNode[xCount * yCount];
                Parallel.For(0, xCount * yCount, (i, state) =>
                {
                    int x = i % xCount;
                    int y = (int)(i / xCount);

                    var lineTransform = new Transformation(Matrix.Translation(x * circleRadius * 3 + xStart, y * circleRadius * 3 + yStart, 0));
                    var screenSpaceLineNode = new ScreenSpaceLineNode(spiralPositions, isLineStrip: false, isLineClosed: false, lineMaterial: lineMaterial, startLineCap: LineCap.Flat, endLineCap: LineCap.Flat)
                    {
                        Transform = lineTransform
                    };

                    screenSpaceLineNode.InitializeResources(MainViewportView.DXScene); // Create DirectX resources in BG thread

                    lineNodes[i] = screenSpaceLineNode;
                });

                // IMPORTANT:
                // We must add SceneNode to the scene on the main UI thread
                for (var i = 0; i < lineNodes.Length; i++)
                {
                    var sceneNodeVisual1 = new SceneNodeVisual3D(lineNodes[i]);
                    MainViewport.Children.Add(sceneNodeVisual1);
                }
            }
            else
            {
                // NO multi-threading

                ScreenSpaceLineMesh screenSpaceLineMesh = null;
                if (selectedLineTypes == LineTypes.ScreenSpaceLineNodeReusedMesh)
                {
                    // Create only a single ScreenSpaceLineMesh that is used for all ScreenSpaceLineNode
                    // This will significantly improve initialization performance
                    screenSpaceLineMesh = new ScreenSpaceLineMesh(spiralPositions, isLineStrip: true);
                }
                
                for (int x = 0; x < xCount; x++)
                {
                    for (int y = 0; y < yCount; y++)
                    {
                        var lineTransform = new Transformation(Matrix.Translation(x * circleRadius * 3 + xStart, y * circleRadius * 3 + yStart, 0));

                        switch (selectedLineTypes)
                        {
                            case LineTypes.MultiScreenSpaceLineNode:
                                var screenSpaceLineNode1 = new ScreenSpaceLineNode(spiralPositions, isLineStrip: false, isLineClosed: false, lineMaterial: lineMaterial, startLineCap: LineCap.Flat, endLineCap: LineCap.Flat)
                                {
                                    Transform = lineTransform
                                };

                                _disposables.Add(screenSpaceLineNode1);

                                var sceneNodeVisual1 = new SceneNodeVisual3D(screenSpaceLineNode1);

                                MainViewport.Children.Add(sceneNodeVisual1);
                                break;

                            case LineTypes.MultiScreenSpaceLineNodePolylines:
                                // Set isLineStrip to true to render poly-lines
                                var screenSpaceLineNode2 = new ScreenSpaceLineNode(spiralPositions, isLineStrip: true, isLineClosed: false, lineMaterial: lineMaterial, startLineCap: LineCap.Flat, endLineCap: LineCap.Flat)
                                {
                                    Transform = lineTransform
                                };

                                _disposables.Add(screenSpaceLineNode2);
                                var sceneNodeVisual2 = new SceneNodeVisual3D(screenSpaceLineNode2);

                                MainViewport.Children.Add(sceneNodeVisual2);
                                break;

                            case LineTypes.SingleScreenSpaceLineNode:
                                // For this sample we need to manually transform the lines.
                                // Therefore initializing the positions list for ScreenSpaceLineNode for multiple polylines takes longer than it could take in common use case.
                                var transformedPositions = new Vector3[spiralPositions.Length];
                                for (int i = 0; i < spiralPositions.Length; i++)
                                    lineTransform.Transform(ref spiralPositions[i], out transformedPositions[i]);

                                if (positionsList != null)
                                    positionsList.Add(transformedPositions);
                                break;
                            
                            case LineTypes.ScreenSpaceLineNodeReusedMesh:
                                var screenSpaceLineNode3 = new ScreenSpaceLineNode(screenSpaceLineMesh, lineMaterial)
                                {
                                    Transform = lineTransform
                                };

                                _disposables.Add(screenSpaceLineNode3);
                                var sceneNodeVisual3 = new SceneNodeVisual3D(screenSpaceLineNode3);

                                MainViewport.Children.Add(sceneNodeVisual3);
                                break;
                        }
                    }
                }

                if (selectedLineTypes == LineTypes.SingleScreenSpaceLineNode)
                {
                    // Only single ScreenSpaceLineNode is created for all xCount * yCount polylines
                    var screenSpaceLineNode4 = new ScreenSpaceLineNode(positionsList, isLineStrip: true, isEachLineClosed: false, lineMaterial: lineMaterial);
                    _disposables.Add(screenSpaceLineNode4);
                    var sceneNodeVisual4 = new SceneNodeVisual3D(screenSpaceLineNode4);
                    MainViewport.Children.Add(sceneNodeVisual4);
                }
            }
        }

        private Point3DCollection CreateSpiralPositions(Point3D startPosition, Vector3D circleXDirection, Vector3D circleYDirection, Vector3D oneSpiralCircleDirection, double circleRadius, int segmentsPerCircle, int circles)
        {
            var oneCirclePositions = new Point[segmentsPerCircle];

            double angleStep = 2 * Math.PI / segmentsPerCircle;
            double angle = 0;

            for (int i = 0; i < segmentsPerCircle; i++)
            {
                // Get x any y position on a flat plane
                double xPos = Math.Sin(angle);
                double yPos = Math.Cos(angle);

                angle += angleStep;

                var newPoint = new Point(xPos * circleRadius, yPos * circleRadius);
                oneCirclePositions[i] = newPoint;
            }


            // It is important to pre-initialize the size the size of Point3DCollection
            var allPositions = new Point3DCollection(segmentsPerCircle * circles);

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
        
        // This method uses Vector3 instead of WPF Point3D nad Vector3D
        private Vector3[] CreateDXSpiralPositions(Vector3 startPosition, Vector3 circleXDirection, Vector3 circleYDirection, Vector3 oneSpiralCircleDirection, float circleRadius, int segmentsPerCircle, int circles)
        {
            var oneCirclePositions = new Vector2[segmentsPerCircle];

            float angleStep = 2f * (float)Math.PI / segmentsPerCircle;
            float angle = 0;

            for (int i = 0; i < segmentsPerCircle; i++)
            {
                // Get x any y position on a flat plane
                float xPos = (float)Math.Sin(angle); // You can use MathF.Sin / Cos in .Net Core
                float yPos = (float)Math.Cos(angle);

                angle += angleStep;

                var newPoint = new Vector2(xPos * circleRadius, yPos * circleRadius);
                oneCirclePositions[i] = newPoint;
            }


            var allPositions = new Vector3[segmentsPerCircle * circles];

            Vector3 onePositionDirection = oneSpiralCircleDirection / segmentsPerCircle;
            Vector3 currentCenterPoint = startPosition;

            int index = 0;
            for (int i = 0; i < circles; i++)
            {
                for (int j = 0; j < segmentsPerCircle; j++)
                {
                    float xCircle = oneCirclePositions[j].X;
                    float yCircle = oneCirclePositions[j].Y;

                    var point3D = new Vector3(currentCenterPoint.X + (xCircle * circleXDirection.X) + (yCircle * circleYDirection.X),
                                              currentCenterPoint.Y + (xCircle * circleXDirection.Y) + (yCircle * circleYDirection.Y),
                                              currentCenterPoint.Z + (xCircle * circleXDirection.Z) + (yCircle * circleYDirection.Z));

                    allPositions[index] = point3D;
                    index++;

                    currentCenterPoint += onePositionDirection;
                }
            }

            return allPositions;
        }

        private LineTypes GetSelectedLineTypes()
        {
            if (MultiLinesRadioButton.IsChecked ?? false)
                return LineTypes.MultiLines;
            if (MultiPolyLinesRadioButton.IsChecked ?? false)
                return LineTypes.MultiPolyLines;
            if (MultiMultiLinesRadioButton.IsChecked ?? false)
                return LineTypes.MultiMultiLines;
            if (MultiScreenSpaceLineNodeRadioButton.IsChecked ?? false)
                return LineTypes.MultiScreenSpaceLineNode;
            if (MultiScreenSpaceLineNodePolylinesRadioButton.IsChecked ?? false)
                return LineTypes.MultiScreenSpaceLineNodePolylines;
            if (SinglePolyLinesRadioButton.IsChecked ?? false)
                return LineTypes.SingleMultiPolyLines;
            if (SingleScreenSpaceLineNodePolylinesRadioButton.IsChecked ?? false)
                return LineTypes.SingleScreenSpaceLineNode;
            if (ReusedMeshScreenSpaceLineNodeRadioButton.IsChecked ?? false)
                return LineTypes.ScreenSpaceLineNodeReusedMesh;
            if (MultihreadedScreenSpaceLineNodeRadioButton.IsChecked ?? false)
                return LineTypes.MultiThreadedScreenSpaceLineNode;

            return LineTypes.None;
        }

        private bool IsScreenSpaceLineNode(LineTypes linesType)
        {
            return linesType == LineTypes.MultiScreenSpaceLineNode ||
                   linesType == LineTypes.MultiScreenSpaceLineNodePolylines ||
                   linesType == LineTypes.SingleScreenSpaceLineNode ||
                   linesType == LineTypes.ScreenSpaceLineNodeReusedMesh ||
                   linesType == LineTypes.MultiThreadedScreenSpaceLineNode;
        }
        
        private bool IsPolyLine(LineTypes linesType)
        {
            return linesType == LineTypes.MultiPolyLines ||
                   linesType == LineTypes.MultiScreenSpaceLineNodePolylines ||
                   linesType == LineTypes.SingleMultiPolyLines ||
                   linesType == LineTypes.SingleScreenSpaceLineNode ||
                   linesType == LineTypes.ScreenSpaceLineNodeReusedMesh ||
                   linesType == LineTypes.MultiThreadedScreenSpaceLineNode;
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

            RecreateLines();
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

        private void OnRenderSettingsCheckboxChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            bool isHardwareAccelerationEnabled = HardwareAccelerate3DLinesCheckBox.IsChecked ?? false;

            // UseGeometryShaderCheckBox is enabled only when we hardware accelerate 3D lines
            UseGeometryShaderCheckBox.IsEnabled = isHardwareAccelerationEnabled;

            // Antialiased CheckBox is enabled only when we hardware accelerate 3D lines and do NOT use geometry shader
            Antialias3DLinesCheckBox.IsEnabled = isHardwareAccelerationEnabled && !(UseGeometryShaderCheckBox.IsChecked ?? false);

            // DXSceneDeviceCreated is called after DXScene and DXDevice are created and before the 3D objects are initialized
            if (MainViewportView.DXScene != null) // In case of WPF rendering the DXScene is null
            {
                MainViewportView.DXScene.HardwareAccelerate3DLines = HardwareAccelerate3DLinesCheckBox.IsChecked ?? false;

                MainViewportView.DXScene.UseGeometryShaderFor3DLines = UseGeometryShaderCheckBox.IsChecked ?? false;
                MainViewportView.DXScene.RenderAntialiased3DLines = Antialias3DLinesCheckBox.IsChecked ?? false;

                // We can also disable / enable hardware accelerated rendering of line caps (arrows, boxes, etc.)
                // This is not done in this sample
                //MainViewportView.DXScene.HardwareAccelerate3DLineCaps = false; // true by default

                if (ReferenceEquals(sender, HardwareAccelerate3DLinesCheckBox))
                {
                    // When hardware acceleration is not enabled, then disable creating ScreenSpaceLineNode
                    MultiScreenSpaceLineNodeRadioButton.IsEnabled           = isHardwareAccelerationEnabled;
                    MultiScreenSpaceLineNodePolylinesRadioButton.IsEnabled  = isHardwareAccelerationEnabled;
                    SingleScreenSpaceLineNodePolylinesRadioButton.IsEnabled = isHardwareAccelerationEnabled;
                    ReusedMeshScreenSpaceLineNodeRadioButton.IsEnabled      = isHardwareAccelerationEnabled;
                    MultihreadedScreenSpaceLineNodeRadioButton.IsEnabled    = isHardwareAccelerationEnabled;

                    if (IsScreenSpaceLineNode(GetSelectedLineTypes()))
                    {
                        // If currently we created, ScreenSpaceLineNode then switch to MultiPolyLinesRadioButton
                        // This will also call RecreateLines
                        MultiPolyLinesRadioButton.IsChecked = true;
                    }
                    else
                    {
                        // After changing the HardwareAccelerate3DLines, we need to recreate the lines (this setting is used only when 3D lines are created).
                        RecreateLines();
                    }
                }
                else
                {
                    // Otherwise just render the scene again
                    MainViewportView.Refresh();
                }
            }
        }

        private void RecreateLinesButton_OnClick(object sender, RoutedEventArgs e)
        {
            RecreateLines();
        }

        private void OnLineTypeChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            RecreateLines();
        }
    }
}
