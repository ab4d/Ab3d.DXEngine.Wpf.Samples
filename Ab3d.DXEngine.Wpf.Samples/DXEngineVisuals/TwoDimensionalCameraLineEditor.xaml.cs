using System;
using System.Collections.Generic;
using System.IO;
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
using Ab3d.Common;
using Ab3d.Common.Cameras;
using Ab3d.Common.Models;
using Ab3d.Controls;
using Ab3d.DirectX;
using Ab3d.DirectX.Common;
using Ab3d.DirectX.Controls;
using Ab3d.DirectX.Effects;
using Ab3d.Utilities;
using Ab3d.Visuals;
using SharpDX.DXGI;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    /// <summary>
    /// Interaction logic for TwoDimensionalCameraLineEditor.xaml
    /// </summary>
    public partial class TwoDimensionalCameraLineEditor : Page
    {
        private TwoDimensionalCamera _twoDimensionalCamera;
        
        private bool _isMouseButtonPressed;

        private double _lastZoomFactor;

        private Point _snappedViewPosition;

        private double _positionMarkerWpfSize = 10;
        private double _positionMarkerHalfSize;
        
        private List<Point> _allPositions;
        private List<PolyLineVisual3D> _positionMarkers;

        private PolyLineVisual3D _snappedPositionMarker;

        private LineVisual3D _creatingLine;
        
        private MultiLineVisual3D _multiLineVisual3D;
        
        
        public TwoDimensionalCameraLineEditor()
        {
            InitializeComponent();

            MouseCameraControllerInfo1.AddCustomInfoLine(0, MouseCameraController.MouseAndKeyboardConditions.LeftMouseButtonPressed, "Create new line");


            // NOTE: TwoDimensionalCamera class is available with full source in this samples project in the Common folder.

            // Create an instance of TwoDimensionalCamera.
            // TwoDimensionalCamera will internally create a TargetPositionCamera and MouseCameraController (when mouseEventsSourceElement is not null).
            // They will be used to show the 2D scene.
            _twoDimensionalCamera = new TwoDimensionalCamera(MainDXViewportView,
                                                             mouseEventsSourceElement: ViewportBorder,   // if mouseEventsSourceElement is null, then MouseCameraController will not be created by TwoDimensionalCamera
                                                             useScreenPixelUnits: false,                 // when false, then the size in device independent units is used (as size of DXViewportView); when true size in screen pixels is used (see SharpHorizontalAndVerticalLines sample)
                                                             coordinateSystemType: TwoDimensionalCamera.CoordinateSystemTypes.CenterOfViewOrigin)
            {
                MoveCameraConditions = MouseCameraController.MouseAndKeyboardConditions.RightMouseButtonPressed,
                QuickZoomConditions  = MouseCameraController.MouseAndKeyboardConditions.LeftMouseButtonPressed | MouseCameraController.MouseAndKeyboardConditions.RightMouseButtonPressed,
                ShowCameraLight      = ShowCameraLightType.Always,
                IsMouseAndTouchZoomEnabled = true,
            };

            // You can access the create TargetPositionCamera with UsedCamera property 
            // and MouseCameraController with UsedMouseCameraController property.
            MouseCameraControllerInfo1.MouseCameraController = _twoDimensionalCamera.UsedMouseCameraController;

            // If we want to use LeftMouseButton for mouse movement, then we can also start new line with the same button with setting MouseMoveThreshold:
            //// Set MouseMoveThreshold so that mouse move starts after the mouse is moved 
            //// for at least 3 pixels. This way we can use mouse click to start / end line drawing.
            //_twoDimensionalCamera.UsedMouseCameraController.MouseMoveThreshold = 3;

            
            _twoDimensionalCamera.CameraChanged += delegate(object sender, EventArgs args)
            {
                // When zoom is changed, we need to update all line markers
                if (!MathUtils.IsSame(_lastZoomFactor, _twoDimensionalCamera.ZoomFactor))
                {
                    // Update line markers size (we want that at each zoom level their WPF size is _positionMarkerWpfSize
                    _positionMarkerHalfSize = _twoDimensionalCamera.GetViewSizeFromWpfSize(_positionMarkerWpfSize) * 0.5;

                    if (double.IsNaN(_snappedViewPosition.X)) // Check if _snappedViewPosition is actually set
                        return; 
                    
                    // Reuse last position
                    var mousePosition = _twoDimensionalCamera.ToWpfPosition(_snappedViewPosition);
                    CheckPositionMarkers(mousePosition, updateExistingPositionMarkers: true); // _positionMarkerHalfSize is changed so we need to update all shown position markers

                    _lastZoomFactor = _twoDimensionalCamera.ZoomFactor;
                }
            };


            _twoDimensionalCamera.UsedMouseCameraController.CameraMoveStarted += delegate (object sender, EventArgs args)
            {
                _isMouseButtonPressed = false; // when we started mouse camera movement, we need to prevent creating the line when the mouse button is released
            };

            _twoDimensionalCamera.UsedMouseCameraController.CameraQuickZoomStarted += delegate (object sender, EventArgs args)
            {
                _isMouseButtonPressed = false; // when we started quick zoom, we need to prevent creating the line when the mouse button is released
            };
            

            // We store lines in our List of Points.
            // The main purpose of this is that we can have a very fast access to line positions.
            // (if we would read the Positions from MultiLineVisual3D this would be much slower because accessing DependencyProperties and Point3DCollection is very slow).
            _allPositions = new List<Point>();

            // _positionMarkers is a list that stores all created line markers. The size of array is the same as the size of _allPositions
            _positionMarkers = new List<PolyLineVisual3D>();


            // One MultiLineVisual3D will show fully created 3D lines (this is the much faster then creating individual LineVisual3D objects)
            _multiLineVisual3D = new MultiLineVisual3D()
            {
                Positions     = new Point3DCollection(),
                LineColor     = Colors.Black,
                LineThickness = 1
            };

            MainViewport.Children.Add(_multiLineVisual3D);


            // _creatingLine will show the line that is currently being created - user has not placed the end position yet.
            _creatingLine = new LineVisual3D()
            {
                LineColor     = Colors.Gray,
                LineThickness = 1,
                IsVisible     = false
            };

            MainViewport.Children.Add(_creatingLine);


            // _snappedPositionMarker will be shown over standard gray position marker and will indicate with its black color that mouse position is snapped to the marked position.
            _snappedPositionMarker = new PolyLineVisual3D()
            {
                Positions     = new Point3DCollection(5),
                LineColor     = Colors.Black,
                LineThickness = 1,
                IsVisible     = false
            };

            MainViewport.Children.Add(_snappedPositionMarker);


            _positionMarkerHalfSize = _twoDimensionalCamera.GetViewSizeFromWpfSize(_positionMarkerWpfSize) * 0.5;
            
            AddAxisLines();

            AddRandomLines(10);

            _snappedViewPosition = new Point(double.NaN, double.NaN); // Mark as invalid


            // We use ViewportBorder to get our mouse events
            ViewportBorder.MouseLeftButtonDown += delegate(object o, MouseButtonEventArgs args)
            {
                _isMouseButtonPressed = true; // we will start the line on mouse up
            };
            
            ViewportBorder.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs args)
            {
                if (!_isMouseButtonPressed)
                    return;

                if (_creatingLine.IsVisible)
                    CompleteNewLine();
                else
                    StartNewLine(_snappedViewPosition);

                _isMouseButtonPressed = false;
            };
            
            ViewportBorder.MouseMove += delegate(object o, MouseEventArgs args)
            {
                var mousePosition = args.GetPosition(ViewportBorder);
                CheckPositionMarkers(mousePosition, updateExistingPositionMarkers: false);

                if (_creatingLine.IsVisible)
                    UpdateLastLinePosition(_snappedViewPosition);
            };

            
            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                MainDXViewportView.Dispose();
            };
        }


        // updateExistingPositionMarkers need to be true when the _positionMarkerHalfSize is changed (when zooming)
        private void CheckPositionMarkers(Point mousePosition, bool updateExistingPositionMarkers)
        {
            var viewPosition = _twoDimensionalCamera.ToViewPosition(mousePosition);

            if (_allPositions.Count == 0)
            {
                _snappedViewPosition = viewPosition;
                return;
            }


            double minLengthSquared = double.MaxValue;
            int    minLengthIndex   = -1;

            double x = viewPosition.X;
            double y = viewPosition.Y;

            double positionMarkerDistanceSquared = (_positionMarkerHalfSize * 20) * (_positionMarkerHalfSize * 20);

            for (var i = 0; i < _allPositions.Count; i++)
            {
                var onePosition = _allPositions[i];

                double dx = onePosition.X - x;
                double dy = onePosition.Y - y;

                double lengthSquared = dx * dx + dy * dy; // to find the closest positions, we do not need to get exact length (this would require Math.Sqrt that is slow)

                var showMarker = lengthSquared < positionMarkerDistanceSquared;

                if (showMarker)
                {
                    if (_positionMarkers[i] == null)
                        _positionMarkers[i] = CreatePositionMarker(onePosition, Colors.Gray);
                    else if (updateExistingPositionMarkers)
                        UpdatePositionMarker(_positionMarkers[i], onePosition);
                }
                else if (_positionMarkers[i] != null)
                {
                    // Hide shown marker
                    MarkerLinesVisual3D.Children.Remove(_positionMarkers[i]);
                    _positionMarkers[i] = null;
                }

                if (lengthSquared < minLengthSquared)
                {
                    minLengthSquared = lengthSquared;
                    minLengthIndex   = i;
                }
            }

            double actualLength = Math.Sqrt(minLengthSquared); // Now we can do a sqrt for one value

            if (actualLength <= _positionMarkerHalfSize)
            {
                _snappedViewPosition = _allPositions[minLengthIndex];
                
                // Show black position marker when mouse is inside it and we snap the position to that marker
                UpdatePositionMarker(_snappedPositionMarker, _snappedViewPosition, zPos: 1.0); // set zPos to 1 to show that position marker on top of other position markers
                _snappedPositionMarker.IsVisible = true;
            }
            else
            {
                _snappedViewPosition = viewPosition;
                _snappedPositionMarker.IsVisible = false; // position is not snapped to any actual position => hide _snappedPositionMarker
            }
        }

        private void StartNewLine(Point viewPosition)
        {
            _creatingLine.StartPosition = new Point3D(viewPosition.X, viewPosition.Y, 0.5);
            _creatingLine.EndPosition   = _creatingLine.StartPosition;

            _creatingLine.IsVisible = true;
        }

        private void CompleteNewLine()
        {
            // Creating line is already shown -> complete the line
            var startPosition3D = _creatingLine.StartPosition;
            var endPosition3D   = _creatingLine.EndPosition;

            CompleteNewLine(startPosition3D, endPosition3D, showEndPositionMarker: true);

            _creatingLine.IsVisible = false;
        }
        
        private void CompleteNewLine(Point3D startPosition3D, Point3D endPosition3D, bool showEndPositionMarker)
        {
            var distance = (endPosition3D - startPosition3D).Length;

            if (distance < 0.001) // Do not add line with the same start and end position
                return;
            
            
            _multiLineVisual3D.Positions.Add(startPosition3D);
            _multiLineVisual3D.Positions.Add(endPosition3D);

            
            var startPosition = new Point(startPosition3D.X, startPosition3D.Y);
            var endPosition   = new Point(endPosition3D.X,   endPosition3D.Y);
            
            _allPositions.Add(startPosition);
            _allPositions.Add(endPosition);
            
            
            _positionMarkers.Add(null);

            if (showEndPositionMarker)
            {
                var endPositionLineMarker = CreatePositionMarker(endPosition, Colors.Gray);
                _positionMarkers.Add(endPositionLineMarker);
            }
            else
            {
                _positionMarkers.Add(null);
            }
        }

        private void UpdateLastLinePosition(Point viewPosition)
        {
            _creatingLine.EndPosition = new Point3D(viewPosition.X, viewPosition.Y, 0.5);
        }

        private void UpdatePositionMarker(PolyLineVisual3D polyLineVisual3D, Point position, double zPos = 0.5)
        {
            var markerPositions = polyLineVisual3D.Positions;

            markerPositions.Clear(); // Reuse existing Point3DCollection

            double halfSize = _positionMarkerHalfSize;

            double x  = position.X;
            double y  = position.Y;
            double x1 = x - halfSize;
            double y1 = y - halfSize;
            double x2 = x + halfSize;
            double y2 = y + halfSize;

            markerPositions.Add(new Point3D(x1, y1, zPos)); // Set z to 0.5 to show marker lines above other lines
            markerPositions.Add(new Point3D(x2, y1, zPos));
            markerPositions.Add(new Point3D(x2, y2, zPos));
            markerPositions.Add(new Point3D(x1, y2, zPos));
            markerPositions.Add(new Point3D(x1, y1, zPos)); // Close the rectangle with duplicating the first position
        }
        
        private PolyLineVisual3D CreatePositionMarker(Point position, Color color)
        {
            var polyLineVisual3D = new PolyLineVisual3D()
            {
                Positions     = new Point3DCollection(5),
                LineColor     = color,
                LineThickness = 1,
            };

            UpdatePositionMarker(polyLineVisual3D, position);

            MarkerLinesVisual3D.Children.Add(polyLineVisual3D);

            return polyLineVisual3D;
        }



        private void ShowAllLinePositionsMarkersToOverlayCanvas()
        {
            // NOTE:
            // When using line markers on an overlay Canvas,
            // we need to update the positions of the markers on each camera change!

            OverlayCanvas.Children.Clear();

            foreach (var lineVisual3D in RootLinesVisual3D.Children.OfType<LineVisual3D>())
            {
                // Convert view position to WPF's position
                var pos1 = _twoDimensionalCamera.ToWpfPosition(lineVisual3D.StartPosition);
                var pos2 = _twoDimensionalCamera.ToWpfPosition(lineVisual3D.EndPosition);

                AddMarkerToOverlayCanvas(pos1, 8, Brushes.Gray);
                AddMarkerToOverlayCanvas(pos2, 8, Brushes.Gray);
            }
        }

        private void AddMarkerToOverlayCanvas(Point position, double size, Brush stroke)
        {
            var rectangle = new Rectangle()
            {
                Width            = size,
                Height           = size,
                Stroke           = stroke,
                StrokeThickness  = 1,
                IsHitTestVisible = false
            };

            Canvas.SetLeft(rectangle, position.X - rectangle.Width * 0.5);
            Canvas.SetTop(rectangle, position.Y - rectangle.Height * 0.5);

            OverlayCanvas.Children.Add(rectangle);
        }

        
        private void AddRandomLines(int linesCount)
        {
            var rnd = new Random();

            var initialRect = new Rect(-400, -300, 800, 600);

            for (int i = 0; i < linesCount; i++)
            {
                var p1 = new Point3D(rnd.NextDouble() * initialRect.Width + initialRect.Left, rnd.NextDouble() * initialRect.Height + initialRect.Top, 0);
                var p2 = new Point3D(rnd.NextDouble() * initialRect.Width + initialRect.Left, rnd.NextDouble() * initialRect.Height + initialRect.Top, 0);

                CompleteNewLine(p1, p2, showEndPositionMarker: false);
            }
        }

        private void AddAxisLines()
        {
            double zPos = -0.5; // Move axis slightly away from camera so other lines will be always on top of axis

            var axisRootVisual3D = new ContentVisual3D();

            var xAxisLineVisual3D = new LineVisual3D()
            {
                StartPosition = new Point3D(0, 0, zPos),
                EndPosition = new Point3D(100, 0, zPos),
                LineColor = Colors.Red,
                EndLineCap = LineCap.ArrowAnchor
            };

            axisRootVisual3D.Children.Add(xAxisLineVisual3D);


            var yAxisLineVisual3D = new LineVisual3D()
            {
                StartPosition = new Point3D(0, 0, zPos),
                EndPosition = new Point3D(0, 100, zPos),
                LineColor = Colors.Green,
                EndLineCap = LineCap.ArrowAnchor
            };

            axisRootVisual3D.Children.Add(yAxisLineVisual3D);

            var xTextBlockVisual3D = new TextBlockVisual3D()
            {
                Text = "X",
                Position = new Point3D(110, 0, zPos),
                PositionType = PositionTypes.Center,
                Size = new Size(10, 10),
                TextDirection = new Vector3D(1, 0, 0),
                UpDirection = new Vector3D(0, 1, 0),
                Foreground = Brushes.Red,
                FontWeight = FontWeights.Bold
            };

            axisRootVisual3D.Children.Add(xTextBlockVisual3D);

            var yTextBlockVisual3D = new TextBlockVisual3D()
            {
                Text = "Y",
                Position = new Point3D(0, 110, zPos),
                PositionType = PositionTypes.Center,
                Size = new Size(10, 10),
                TextDirection = new Vector3D(1, 0, 0),
                UpDirection = new Vector3D(0, 1, 0),
                Foreground = Brushes.Green,
                FontWeight = FontWeights.Bold
            };

            axisRootVisual3D.Children.Add(yTextBlockVisual3D);

            MainViewport.Children.Add(axisRootVisual3D);
        }
    }
}
