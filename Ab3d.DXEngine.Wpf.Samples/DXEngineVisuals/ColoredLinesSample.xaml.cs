using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Ab3d.DirectX;
using Ab3d.DirectX.Materials;
using Ab3d.Visuals;

#if SHARPDX
using SharpDX;
#endif

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    /// <summary>
    /// Interaction logic for ColoredLinesSample.xaml
    /// </summary>
    public partial class ColoredLinesSample : Page
    {
        public ColoredLinesSample()
        {
            InitializeComponent();

            var disposables = new DisposeList();

            // Both poly-line and multi-line use the same positions and positionColors
            var positions = new Vector3[]
            {
                new Vector3(-225, 50, -50),
                new Vector3(-150, 100, -50),
                new Vector3(-75,  50, -50),
                new Vector3(0,    100, -50),
                new Vector3(75,   50, -50),
                new Vector3(150,  100, -50),
            };

            var positionColors = new Color4[]
            {
                Colors.Blue.ToColor4(),
                Colors.Green.ToColor4(),
                Colors.Yellow.ToColor4(),
                Colors.Orange.ToColor4(),
                Colors.Red.ToColor4(),
                Colors.Transparent.ToColor4()
            };


            // Create a colored poly-line:
            var screenSpaceLineNode1 = CreateColoredLineNode(positions, positionColors, 10, disposables, isPolyLine: true);

            var sceneNodeVisual1 = new SceneNodeVisual3D(screenSpaceLineNode1);
            MainViewport.Children.Add(sceneNodeVisual1);


            // Create a colored multi-line:
            var screenSpaceLineNode2 = CreateColoredLineNode(positions, positionColors, 10, disposables, isPolyLine: false);

            var sceneNodeVisual2 = new SceneNodeVisual3D(screenSpaceLineNode2);
            sceneNodeVisual2.Transform = new TranslateTransform3D(0, 75, 0);
            MainViewport.Children.Add(sceneNodeVisual2);


            // Create a single line:
            // Note that if you want to render many colored lines,
            // it is better to use one multi-line that can render many lines with one draw call
            // then creating many individual lines with CreateColoredLineNode that take startPosition and endPosition.
            var screenSpaceLineNode3 = CreateColoredLineNode(startPosition:  new Vector3(-225, 0, -50),
                                                             endPosition:    new Vector3(150, 0, -50),
                                                             startLineColor: Colors.Blue.ToColor4(),
                                                             endLineColor:   Colors.Transparent.ToColor4(),
                                                             lineThickness:  10,
                                                             disposables:    disposables);

            var sceneNodeVisual3 = new SceneNodeVisual3D(screenSpaceLineNode3);
            sceneNodeVisual3.Transform = new TranslateTransform3D(0, 0, 0);
            MainViewport.Children.Add(sceneNodeVisual3);



            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                disposables.Dispose();
                MainDXViewportView.Dispose();
            };
        }

        
        // Creates a ScreenSpaceLineNode with multiple positions and with different colors for positions.
        public static ScreenSpaceLineNode CreateColoredLineNode(Vector3[] positions, 
                                                                Color4[] lineColors, 
                                                                float lineThickness, 
                                                                DisposeList disposables,
                                                                bool isPolyLine = true) // when false we create a multi-line line (each individual line is defined by 2 position)
        {
            var lineMaterial = new PositionColoredLineMaterial()
            {
                LineColor = Color4.White, // When PositionColors are used, then LineColor is used as a mask - each color is multiplied by LineColor - use White to preserve PositionColors
                LineThickness = lineThickness,
                PositionColors = lineColors,
                IsPolyLine = isPolyLine
            };

            // NOTE: When rendering multi-lines we need to set isLineStrip to false
            var screenSpaceLineNode = new ScreenSpaceLineNode(positions, isLineClosed: false, isLineStrip: isPolyLine, lineMaterial: lineMaterial);

            if (disposables != null)
            {
                disposables.Add(screenSpaceLineNode);
                disposables.Add(lineMaterial);
            }

            return screenSpaceLineNode;
        }

        // Creates a ScreenSpaceLineNode with different colors for start and end position
        public static ScreenSpaceLineNode CreateColoredLineNode(Vector3 startPosition,
                                                                Vector3 endPosition,
                                                                Color4 startLineColor,
                                                                Color4 endLineColor,
                                                                float lineThickness,
                                                                DisposeList disposables)
        {
            // Convert positions to array
            var linePositions = new Vector3[] { startPosition, endPosition };
            var lineColors    = new Color4[]  { startLineColor, endLineColor };
             
            return CreateColoredLineNode(linePositions, lineColors, lineThickness, disposables, isPolyLine: false);
        }
    }
}