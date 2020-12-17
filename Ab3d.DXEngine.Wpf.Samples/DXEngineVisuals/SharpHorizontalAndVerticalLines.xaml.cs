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
using RenderingEventArgs = Ab3d.DirectX.RenderingEventArgs;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    /// <summary>
    /// Interaction logic for SharpHorizontalAndVerticalLines.xaml
    /// </summary>
    public partial class SharpHorizontalAndVerticalLines : Page
    {
        private TwoDimensionalCamera _twoDimensionalCamera;

        public SharpHorizontalAndVerticalLines()
        {
            InitializeComponent();

            _twoDimensionalCamera = new TwoDimensionalCamera(MainDXViewportView,
                                                             ViewportBorder,
                                                             useScreenPixelUnits: true,
                                                             coordinateSystemType: TwoDimensionalCamera.CoordinateSystemTypes.CenterOfViewOrigin)
            {
                MoveCameraConditions       = MouseCameraController.MouseAndKeyboardConditions.RightMouseButtonPressed,
                QuickZoomConditions        = MouseCameraController.MouseAndKeyboardConditions.LeftMouseButtonPressed | MouseCameraController.MouseAndKeyboardConditions.RightMouseButtonPressed,
                IsMouseAndTouchZoomEnabled = true,
                ShowCameraLight            = ShowCameraLightType.Always
            };

            MouseCameraControllerInfo1.MouseCameraController = _twoDimensionalCamera.UsedMouseCameraController;


            // Use Loaded on TwoDimensionalCamera to delay creating the scene
            // until the TwoDimensionalCamera is loaded - then the view's size and dpi scale are known.
            _twoDimensionalCamera.Loaded += delegate(object sender, EventArgs args)
            {
                CreateTestScene();
            };


            MainDXViewportView.DXSceneInitialized += delegate(object sender, EventArgs args)
            {
                // Do we need to show warning - it is shown when using super-sampling
                if (MainDXViewportView.DXScene.SupersamplingCount > 1)
                    SupersamplingWarningTextBlock.Visibility = Visibility.Visible;

                // NOTE:
                // To render all lines without anti-aliasing and without geometry shader,
                // set UseGeometryShaderFor3DLines and RenderAntialiased3DLines to false:
                //
                //MainDXViewportView.DXScene.UseGeometryShaderFor3DLines = false;
                //MainDXViewportView.DXScene.RenderAntialiased3DLines    = false;
            };


            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                MainDXViewportView.Dispose();
            };
        }
        
        private void CreateTestScene()
        {
            // Line StartPosition and EndPosition define where the center of the line goes.
            // Half of line thickness is rendered on each side of the center line and the other half on the other side of the center line.
            // So if line thickness is 1 screen pixel and center of the line goes over the border between two pixels,
            // then line is rendered half pixels on each side of the center.
            // The doth (.) in the following image show where only half of the pixel is occupied by the line color.
            // If line color is black and background color is white, then such line will produce gray pixels
            // that will be shown as 2 pixels thick line.

            // Line with LineThickness = 1 pixel
            // StartPosition = (-1, 0)
            // EndPosition = (3, 0)

            //     -2  -1   0   1   2   3
            //  2   --------|------------
            //      |   |   |   |   |   |
            //  1   --------|------------
            //      |   | . | . | . | . |
            //  0 ----------|--------------
            //      |   | . | . | . | . |
            // -1   --------|------------
            //      |   |   |   |   |   |
            // -2   --------|------------


            // To render a 1 pixel thick line in such a way that it will fill the whole pixel,
            // we need to move the center of the line so that it will go through the center of the pixels.
            // This will render the line as black line.
            // In the image below this is done with setting y to 0.5:

            // Line with LineThickness = 1 pixel
            // StartPosition = (-1, 0.5)
            // EndPosition = (3, 0.5)

            //     -2  -1   0   1   2   3
            //  2   --------|------------
            //      |   |   |   |   |   |
            //  1   --------|------------
            //      |   | x | x | x | x |
            //  0 ----------|--------------
            //      |   |   |   |   |   |
            // -1   --------|------------
            //      |   |   |   |   |   |
            // -2   --------|------------       


            // For this to work the following conditions need to be meet:
            // - the line need to be correctly positioned (also taking into account DPI settings and ZoomFactor)
            // - the camera need to be correctly set so that the (0, 0) position lies between screen pixels.
            // - DXViewportView need to be correctly snapped to screen pixels by WPF.
            //
            // It is not easy to correctly account all those parameters.
            // The current version of TwoDimensionalCamera does not yet support that.
            //
            // But with Ab3d.DXEngine it is possible to render some (or all) the lines 
            // in such a way that they will be rendered exactly to screen pixels.
            //
            // This can be done with setting UseGeometryShaderFor3DLines and RenderAntialiased3DLines attributes to false.
            // This way the lines are rendered by DirectX. Such lines are always 1 screen pixel thick
            // and are always rendered to the screen pixels.
            //
            // The biggest disadvantage of this is that such lines are always 1 screen pixel thick.
            //
            // Such lines look good only when they are horizontal or vertical.
            // But when they are at an angle, the standard lines appear much nicer and smoother.
            //
            // Another disadvantage is that such lines are not rendered correctly when super-sampling is used.
            // The problem is that such line is rendered with 1 pixel thickness to a super-sized texture,
            // so when such texture is down-sampled the line will appear much dimmer because its color will 
            // combined with surrounding colors. For example with 4xSSAA they will be half dimmer, with 16xSSAA they will be 4 times dimmer.
            // (standard lines are rendered thicker to super-sampled texture so thickness is correct after down-sampling).

            
            // We will render two sets of lines:
            // 1) The lines on the left will be rendered normally.
            // 2) The lines on the right will be rendered without using geometry shader and without anti-aliasing.


            double screenPixelLineThickness = _twoDimensionalCamera.ScreenPixelSize;

            var triangleFan1 = AddTriangleFan(new Point3D(-300, 60, 0), linesLength: 200, lineThickness: screenPixelLineThickness);
            var triangleFan2 = AddTriangleFan(new Point3D(100,  60, 0), linesLength: 200, lineThickness: screenPixelLineThickness);

            triangleFan2.SetDXAttribute(DXAttributeType.UseGeometryShaderFor3DLines, false);
            triangleFan2.SetDXAttribute(DXAttributeType.RenderAntialiased3DLines,    false);


            var wireGridVisual1 = new WireGridVisual3D()
            {
                CenterPosition   = new Point3D(-200, -60, 0),
                Size             = new Size(200, 200),
                HeightDirection  = new Vector3D(0, 1, 0),
                WidthDirection   = new Vector3D(1, 0, 0),
                WidthCellsCount  = 10,
                HeightCellsCount = 10,
                LineThickness    = screenPixelLineThickness,
            };

            MainViewport.Children.Add(wireGridVisual1);


            var wireGridVisual2 = new WireGridVisual3D()
            {
                CenterPosition   = new Point3D(200, -60, 0),
                Size             = new Size(200, 200),
                HeightDirection  = new Vector3D(0, 1, 0),
                WidthDirection   = new Vector3D(1, 0, 0),
                WidthCellsCount  = 10,
                HeightCellsCount = 10,
                LineThickness    = screenPixelLineThickness

                // NOTE:
                // When IsClosed is set to true or when using MajorLinesFrequency,
                // then WireGridVisual3D creates a Model3DGroup to render multiple line types.
                // In this case setting DXAttribute does not work.
                // If you want to to use MajorLinesFrequency, then create two WireGridVisual3D.
                // For closed WireGridVisual3D, create a separate RectangleVisual3D
                // MajorLinesFrequency = 2,
                // IsClosed = true 
            };

            wireGridVisual2.SetDXAttribute(DXAttributeType.UseGeometryShaderFor3DLines, false);
            wireGridVisual2.SetDXAttribute(DXAttributeType.RenderAntialiased3DLines, false);

            MainViewport.Children.Add(wireGridVisual2);


            // Add list of lines each with slightly different sub-pixel position.
            AddLineList(startPosition: new Point3D(-300, -220, 0), lineVector: new Vector3D(0, 40, 0), offsetVector: new Vector3D(10 + screenPixelLineThickness / 10, 0, 0), startLineThickness: screenPixelLineThickness, endLineThickness: screenPixelLineThickness, count: 20, renderAs1PixelNonAntiAliasedLine: false);
            AddLineList(startPosition: new Point3D( 100, -220, 0), lineVector: new Vector3D(0, 40, 0), offsetVector: new Vector3D(10 + screenPixelLineThickness / 10, 0, 0), startLineThickness: screenPixelLineThickness, endLineThickness: screenPixelLineThickness, count: 20, renderAs1PixelNonAntiAliasedLine: true);

            // Add list of lines with different line thickness.
            // This shows that lines without geometry shader are always 1 screen pixel thick and do not support line thickness.
            AddLineList(startPosition: new Point3D(-300, -280, 0), lineVector: new Vector3D(0, 40, 0), offsetVector: new Vector3D(10 + screenPixelLineThickness / 10, 0, 0), startLineThickness: 0.5, endLineThickness: 5, count: 20, renderAs1PixelNonAntiAliasedLine: false);
            AddLineList(startPosition: new Point3D( 100, -280, 0), lineVector: new Vector3D(0, 40, 0), offsetVector: new Vector3D(10 + screenPixelLineThickness / 10, 0, 0), startLineThickness: 0.5, endLineThickness: 5, count: 20, renderAs1PixelNonAntiAliasedLine: true);
        }

        private MultiLineVisual3D AddTriangleFan(Point3D startPosition, double linesLength, double lineThickness)
        {
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

            MainViewport.Children.Add(multiLineVisual3D);

            return multiLineVisual3D;
        }

        private void AddLineList(Point3D startPosition, Vector3D lineVector, Vector3D offsetVector, double startLineThickness, double endLineThickness, int count, bool renderAs1PixelNonAntiAliasedLine)
        {
            for (int i = 0; i < count; i++)
            {
                var startLinePosition = startPosition + i * offsetVector;
                var endLinePosition   = startLinePosition + lineVector;

                var lineVisual3D = new LineVisual3D()
                {
                    StartPosition = startLinePosition,
                    EndPosition   = endLinePosition,
                    LineColor     = Colors.Black,
                    LineThickness = startLineThickness + (endLineThickness - startLineThickness) * (double) i / (double) count,
                };

                MainViewport.Children.Add(lineVisual3D);
                
                if (renderAs1PixelNonAntiAliasedLine)
                {
                    lineVisual3D.SetDXAttribute(DXAttributeType.UseGeometryShaderFor3DLines, false);
                    lineVisual3D.SetDXAttribute(DXAttributeType.RenderAntialiased3DLines,    false);
                }
            }
        }
    }
}
