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
using Ab3d.Common.Cameras;
using Ab3d.Common.Models;
using Ab3d.Controls;
using Ab3d.DirectX;
using Ab3d.DirectX.Common;
using Ab3d.DirectX.Controls;
using Ab3d.DirectX.Effects;
using Ab3d.Utilities;
using Ab3d.Visuals;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    /// <summary>
    /// Interaction logic for TwoDimensionalCameraSample.xaml
    /// </summary>
    public partial class TwoDimensionalCameraSample : Page
    {
        private TwoDimensionalCamera _twoDimensionalCamera;

        public TwoDimensionalCameraSample()
        {
            InitializeComponent();

            // NOTE: TwoDimensionalCamera class is available with full source in this samples project in the Common folder.

            // Create an instance of TwoDimensionalCamera.
            // TwoDimensionalCamera will internally create a TargetPositionCamera and MouseCameraController (when mouseEventsSourceElement is not null).
            // They will be used to show the 2D scene.
            _twoDimensionalCamera = new TwoDimensionalCamera(MainDXViewportView,
                                                             mouseEventsSourceElement: ViewportBorder,   // if mouseEventsSourceElement is null, then MouseCameraController will not be created by TwoDimensionalCamera
                                                             useScreenPixelUnits: false,                 // when false, then the size in device independent units is used (as size of DXViewportView); when true size in screen pixels is used (see SharpHorizontalAndVerticalLines sample)
                                                             coordinateSystemType: TwoDimensionalCamera.CoordinateSystemTypes.CenterOfViewOrigin)
            {
                MoveCameraConditions       = MouseCameraController.MouseAndKeyboardConditions.RightMouseButtonPressed,
                QuickZoomConditions        = MouseCameraController.MouseAndKeyboardConditions.LeftMouseButtonPressed | MouseCameraController.MouseAndKeyboardConditions.RightMouseButtonPressed,
                IsMouseAndTouchZoomEnabled = true,
                ShowCameraLight            = ShowCameraLightType.Always
            };

            _twoDimensionalCamera.CameraChanged += delegate(object sender, EventArgs args)
            {
                UpdateSceneInfo();
            };


            // You can access the create TargetPositionCamera with UsedCamera property 
            // and MouseCameraController with UsedMouseCameraController property.
            CameraAxisPanel1.TargetCamera                    = _twoDimensionalCamera.UsedCamera;
            MouseCameraControllerInfo1.MouseCameraController = _twoDimensionalCamera.UsedMouseCameraController;
            


            CreateShapesSample();
            //LoadSampleLinesData(lineThickness: 0.8, new Rect(-400, -300, 800, 600));

            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                MainDXViewportView.Dispose();
            };
        }

        private void UpdateSceneInfo()
        {
            var visibleRect = _twoDimensionalCamera.VisibleRect;

            SceneInfoTextBlock.Text = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Zoom factor: {0:0.00}\r\nOffset: {1:0.0} {2:0.0}\r\nVisible Rect: {3:0} {4:0} {5:0} {6:0}\r\nView size: {7} x {8}",
                _twoDimensionalCamera.ZoomFactor, _twoDimensionalCamera.Offset.X, _twoDimensionalCamera.Offset.Y,
                visibleRect.X, visibleRect.Y, visibleRect.Width, visibleRect.Height,
                _twoDimensionalCamera.ViewSize.Width, _twoDimensionalCamera.ViewSize.Height);
        }

        private void CreateShapesSample()
        {
            //
            // First create a few lines
            //
            for (int i = 0; i < 5; i++)
            {
                // 2D coordinates of the line
                double x1 = -300;
                double y1 = -50 + i * 20;
                
                double x2 = -250;
                double y2 = i * 20;

                var lineVisual3D = new LineVisual3D()
                {
                    // Because we use 3D engine to show 2D lines, we need to convert 2D coordinates to 3D coordinates.
                    // This is done with setting Z to 0 (but we could use that to move some lines or shapes in front of the other lines).
                    StartPosition = new Point3D(x1, y1, 0),
                    EndPosition   = new Point3D(x2, y2, 0),
                    LineColor     = Colors.Black,
                    LineThickness = i + 1
                };

                RootLinesVisual3D.Children.Add(lineVisual3D);
            }


            //
            // Create a polyline
            //
            var polygonPositions = new Point3DCollection(new Point3D[]
            {
                new Point3D(-70,  -50, 0),
                new Point3D(-170, -50, 0),
                new Point3D(-170, 50,  0),
                new Point3D(-70,  50,  0),
            });

            var polyLineVisual3D = new PolyLineVisual3D()
            {
                Positions     = polygonPositions,
                LineColor     = Colors.Black,
                LineThickness = 1,
                IsClosed      = true
            };

            RootLinesVisual3D.Children.Add(polyLineVisual3D);


            //
            // Create 2 lines with pattern (see LinesWithPatternSample for more info)
            //
            var lineWithPattern1 = new Ab3d.Visuals.LineVisual3D()
            {
                StartPosition = new Point3D(-300, -100, 0),
                EndPosition   = new Point3D( 300, -100, 0),
                LineThickness = 2,
                LineColor     = Colors.Orange
            };

            lineWithPattern1.SetDXAttribute(DXAttributeType.LinePattern, 0x3333); // 0x3333 is 0011001100110011
            //lineWithPattern1.SetDXAttribute(DXAttributeType.LinePatternScale,  1); // We do not need to set default scale and offset values
            //lineWithPattern1.SetDXAttribute(DXAttributeType.LinePatternOffset, 0);

            RootLinesVisual3D.Children.Add(lineWithPattern1);

            
            var lineWithPattern2 = new Ab3d.Visuals.LineVisual3D()
            {
                StartPosition = new Point3D(-300, -110, 0),
                EndPosition   = new Point3D( 300, -110, 0),
                LineThickness = 2,
                LineColor     = Colors.Orange
            };

            lineWithPattern2.SetDXAttribute(DXAttributeType.LinePattern, 0x5555); // 0x3333 is 0101010101010101

            RootLinesVisual3D.Children.Add(lineWithPattern2);


            //
            // Create curve with BezierCurve class from Ab3d.PowerToys library (see Lines3D/CurvesSample in Ab3d.PowerToys samples project for more)
            //
            // NOTE:
            // Ab3d.DXEngine cannot show real curves but only straight lines.
            // But you can convert a curve to many lines to simulate a curve (but this may be seen when zooming in).
            //
            var curveControlPoints = new Point3D[]
            {
                new Point3D(0,   0,  0),
                new Point3D(30,  0,  0),
                new Point3D(60,  50, 0),
                new Point3D(90,  0,  0),
                new Point3D(120, 0,  0)
            };

            var bezierCurve    = Ab3d.Utilities.BezierCurve.CreateFromCurvePositions(curveControlPoints);
            var curvePositions = bezierCurve.CreateBezierCurve(positionsPerSegment: 20); // create 20 positions between each control point

            var curveLineVisual3D = new PolyLineVisual3D()
            {
                Positions     = curvePositions,
                LineColor     = Colors.Green,
                LineThickness = 2,
                Transform     = new TranslateTransform3D(0, -50, 0)
            };

            RootLinesVisual3D.Children.Add(curveLineVisual3D);


            //
            // Create lines from a WPF ellipse or any other WPF's shape
            //
            var ellipseGeometry = new EllipseGeometry(new Rect(0, 0, 120, 50));
            var flattenGeometry = ellipseGeometry.GetFlattenedPathGeometry(tolerance: 0.1, type: ToleranceType.Absolute);

            var geometryPoints = new List<Point>();
            geometryPoints.Add(flattenGeometry.Figures[0].StartPoint);

            // We need only the first Figure
            foreach (var oneSegment in flattenGeometry.Figures[0].Segments)
            {
                if (oneSegment is PolyLineSegment)
                    geometryPoints.AddRange(((PolyLineSegment)oneSegment).Points);
                else if (oneSegment is LineSegment)
                    geometryPoints.Add(((LineSegment)oneSegment).Point);
            }

            var ellipsePoints = ConvertToPoint3DCollection(geometryPoints);

            var ellipseLineVisual3D = new PolyLineVisual3D()
            {
                Positions     = ellipsePoints,
                LineColor     = Colors.Green,
                LineThickness = 2,
                Transform     = new TranslateTransform3D(0, 20, 0)
            };

            RootLinesVisual3D.Children.Add(ellipseLineVisual3D);


            //
            // Show 2D shapes with using triangulator from Ab3d.PowerToys to convert shape to a set of triangles.
            //
            // NOTE:
            // The current version (Ab3d.PowerToys v9.4) does not support triangulating shapes with holes.
            //
            var shapePoints = new Point[]
            {
                new Point(0,   0),
                new Point(50,  0),
                new Point(100, 50),
                new Point(100, 100),
                new Point(50,  80),
                new Point(50,  40),
                new Point(0,   40),
                new Point(0,   0),
            };

            var triangulator = new Ab3d.Utilities.Triangulator(shapePoints);
            var triangleIndices = triangulator.CreateTriangleIndices();

            var shapePoints3D = ConvertToPoint3DCollection(shapePoints);

            var meshGeometry3D = new MeshGeometry3D()
            {
                Positions       = shapePoints3D,
                TriangleIndices = new Int32Collection(triangleIndices)
            };

            var shapeGeometryModel3D = new GeometryModel3D(meshGeometry3D, new DiffuseMaterial(Brushes.LightBlue));

            // NOTE:
            // We set Z of the shape to -0.5 !!! 
            // This will move the solid shape slightly behind the 3D line so the line will be always on top of the shape
            shapeGeometryModel3D.Transform = new TranslateTransform3D(200, -50, -0.5);

            RootLinesVisual3D.Children.Add(shapeGeometryModel3D.CreateModelVisual3D());


            // Also add an outline to the shape
            var shapeOutlineVisual3D = new PolyLineVisual3D()
            {
                Positions     = shapePoints3D,
                LineColor     = Colors.Black,
                LineThickness = 1,
                Transform     = shapeGeometryModel3D.Transform
            };

            RootLinesVisual3D.Children.Add(shapeOutlineVisual3D);
        }

        private static Point3DCollection ConvertToPoint3DCollection(IList<Point> points)
        {
            var count   = points.Count;

            var points3D = new Point3DCollection(count);
            for (var i = 0; i < count; i++)
                points3D.Add(new Point3D(points[i].X, points[i].Y, 0));

            return points3D;
        }


        private void LoadSampleLinesData(double lineThickness, Rect targetRect)
        {
            // Read many lines from a custom bin file format.
            // The bin file was created from a metafile (wmf) file that was read by Ab2d.ReaderWmf,
            // then the lines were grouped by color and saved to a custom bin file.

            Rect bounds;
            var  lines = ReadLineDataFromBin(AppDomain.CurrentDomain.BaseDirectory + @"Resources\palazz_sport.bin", out bounds);


            Point targetCenter = new Point(targetRect.X + targetRect.Width * 0.5, targetRect.Y + targetRect.Height * 0.5);

            double xScale = targetRect.Width / bounds.Width;
            double yScale = targetRect.Height / bounds.Height;

            double scale = Math.Min(xScale, yScale); // Preserve aspect ratio - so use the minimal scale

            double xOffset = targetCenter.X - bounds.Width * scale * 0.5;
            double yOffset = targetCenter.Y + bounds.Height * scale * 0.5; // targetCenter.Y - bounds.Height * scale * 0.5 + bounds.Height * scale // because we flipped y we need to offset by height


            var matrixTransform = new MatrixTransform(scale, 0,
                0, -scale, // We also need to flip y axis because here y axis is pointing up
                xOffset, yOffset);

            for (var i = 0; i < lines.Count; i++)
            {
                var oneLineData = lines[i];
                var positions   = oneLineData.Positions;

                var point3DCollection = new Point3DCollection(positions.Count);
                for (var j = 0; j < positions.Count; j++)
                {
                    var p = new Point(positions[j].X, positions[j].Y);
                    p = matrixTransform.Transform(p);

                    point3DCollection.Add(new Point3D(p.X, p.Y, 0));
                }

                if (oneLineData.IsLineStrip)
                {
                    var polyLineVisual3D = new PolyLineVisual3D()
                    {
                        Positions     = point3DCollection,
                        LineColor     = oneLineData.LineColor,
                        LineThickness = lineThickness < 0 ? oneLineData.LineThickness : lineThickness
                    };

                    RootLinesVisual3D.Children.Add(polyLineVisual3D);
                }
                else
                {
                    var multiLineVisual3D = new MultiLineVisual3D()
                    {
                        Positions     = point3DCollection,
                        LineColor     = oneLineData.LineColor,
                        LineThickness = lineThickness < 0 ? oneLineData.LineThickness : lineThickness
                    };

                    RootLinesVisual3D.Children.Add(multiLineVisual3D);
                }
            }
        }


        private static List<LineData> ReadLineDataFromBin(string binFileName, out Rect bounds)
        {
            List<LineData> lines = null;

            using (var stream = System.IO.File.OpenRead(binFileName))
            {
                using (var reader = new BinaryReader(stream))
                {
                    int linesCount = reader.ReadInt32();

                    bounds = new Rect(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

                    lines = new List<LineData>(linesCount);

                    for (int i = 0; i < linesCount; i++)
                    {
                        var lineColor     = Color.FromArgb(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
                        var lineThickness = reader.ReadSingle();
                        var isLineStrip   = reader.ReadBoolean();

                        var pointsCount = reader.ReadInt32();

                        var lineData = new LineData(lineColor, lineThickness, isLineStrip, pointsCount);

                        var positions = lineData.Positions;
                        for (int j = 0; j < pointsCount; j++)
                            positions.Add(new Point(reader.ReadSingle(), reader.ReadSingle()));

                        lines.Add(lineData);
                    }
                }
            }

            return lines;
        }

        private class LineData
        {
            public Color LineColor;
            public double LineThickness;
            public bool IsLineStrip;
            public List<Point> Positions;

            public LineData(Color lineColor, double lineThickness, bool isLineStrip, int initialPointsCount = 0)
            {
                LineColor     = lineColor;
                LineThickness = lineThickness;
                IsLineStrip   = isLineStrip;

                if (initialPointsCount > 0)
                    Positions = new List<Point>(initialPointsCount);
                else
                    Positions = new List<Point>();
            }
        }

        private void OnSceneTypeRadioButtonChecked(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            RootLinesVisual3D.Children.Clear();

            if (StadiumPlanRadioButton.IsChecked ?? false)
            {
                LoadSampleLinesData(lineThickness: 0.8, new Rect(-400, -300, 800, 600));
            }
            else
            {
                CreateShapesSample();
            }
        }

        private void ResetCameraButton_OnClick(object sender, RoutedEventArgs e)
        {
            _twoDimensionalCamera.Reset();
        }
    }
}
