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
using Ab3d.Common;
using Ab3d.DirectX;
using Ab3d.DirectX.Materials;
using Ab3d.Visuals;
using SharpDX;
using Color = System.Windows.Media.Color;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineAdvanced
{
    /// <summary>
    /// Interaction logic for ScreenSpaceLineNodeSample.xaml
    /// </summary>
    public partial class ScreenSpaceLineNodeSample : Page
    {
        // Usually, 3D lines in DXEngine are shown with using 3D lines from Ab3d.PowerToys library (LineVisual3D, PolyLineVisual3D, etc. or Line3DFactory class).
        // This way the lines can be easily added to the scene and also the lines can be easily changed (positions, colors, thickness).
        // 
        // But in this case the line data needs to be first created in WPF collections,
        // then this needs to be passed to DXEngine and there the Point3D structs need to be changed to Vector3 structs (using float values instead of double)
        // and then DirectX buffers can be created from line data.
        //
        // This works well for most of the cases, but when you are showing many 3D lines and want the ultimate performance,
        // you can use the low-level DXEngine objects to define the 3D lines without any WPF objects.
        //
        // To do this you need to manually create ScreenSpaceLineNode.
        // You can define the line positions with passing that to the ScreenSpaceLineNode constructor
        // or you can create a ScreenSpaceLineMesh and pass that to the ScreenSpaceLineNode.
        // When using ScreenSpaceLineMesh, you can specify CreateDynamicVertexBuffer to true to create a dynamic vertex buffer.
        // This is recommended when line positions are changed often.
        //
        // This sample shows both options.
        //
        // It also shows how to update the line positions, color and thickness.


        private DisposeList _disposables;

        private ScreenSpaceLineNode _screenSpaceLineNode1;
        private ScreenSpaceLineNode _screenSpaceLineNode2;
        private ScreenSpaceLineNode _screenSpaceLineNode3;
        private ScreenSpaceLineNode _screenSpaceLineNode4;
        private ScreenSpaceLineMesh _screenSpaceLineMesh;

        public ScreenSpaceLineNodeSample()
        {
            InitializeComponent();

            _disposables = new DisposeList();

            var linePositions = CreateLinePositions(xOffset: 0, zOffset: -50);

            _screenSpaceLineNode1 = CreateLinesWithPositions(linePositions, isLineStrip: false, isPolyLine: false, isLineClosed: false, lineColor: Colors.Blue, xOffset: -90);
            AddTextBlockVisual3D(xOffset: -90, isLineStrip: false, isPolyLine: false);

            _screenSpaceLineNode2 = CreateLinesWithPositions(linePositions, isLineStrip: true, isPolyLine: false, isLineClosed: false, lineColor: Colors.Green, xOffset: -30);
            AddTextBlockVisual3D(xOffset: -30, isLineStrip: true, isPolyLine: false);

            _screenSpaceLineNode3 = CreateLinesWithPositions(linePositions, isLineStrip: true, isPolyLine: true, isLineClosed: false, lineColor: Colors.Red, xOffset: 30);
            AddTextBlockVisual3D(xOffset: 30, isLineStrip: true, isPolyLine: true);

            _screenSpaceLineNode4 = CreateLinesWithLineMesh(linePositions, isLineStrip: false, isLineClosed: false, isPolyLine: false, lineColor: Colors.Orange, xOffset: 90, screenSpaceLineMesh: out _screenSpaceLineMesh);
            AddTextBlockVisual3D(xOffset: 90, "using\r\nScreenSpaceLineMesh");

            Unloaded += delegate
            {
                Dispose();
            };
        }

        private void AddTextBlockVisual3D(double xOffset, bool isLineStrip, bool isPolyLine)
        {
            var text = string.Format("isLineStrip: {0}\r\nisPolyLine: {1}", isLineStrip, isPolyLine);
            AddTextBlockVisual3D(xOffset, text);
        }
        
        private void AddTextBlockVisual3D(double xOffset, string text)
        {
            var textBlockVisual3D = new TextBlockVisual3D()
            {
                Position = new Point3D(xOffset, -5, 100),
                PositionType = PositionTypes.Center,
                UpDirection = new Vector3D(0, 0.5, -0.5), // text is shown at slight angle
                Size = new Size(45, 20),                 // set width to 60; height is automatically calculated
                //BorderSize = new Size(45, 20),
                TextPadding = new Thickness(5, 3, 5, 3),
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1, 1, 1, 1),
                Text = text,
            };

            MainViewport.Children.Add(textBlockVisual3D);
        }

        private ScreenSpaceLineNode CreateLinesWithPositions(Vector3[] linePositions, bool isLineStrip, bool isPolyLine, bool isLineClosed, Color lineColor, float xOffset)
        {
            var lineMaterial = CreateLineMaterial(isPolyLine, lineColor);

            var screenSpaceLineNode = new ScreenSpaceLineNode(linePositions, isLineStrip, isLineClosed, lineMaterial);
            screenSpaceLineNode.Transform = new Transformation(SharpDX.Matrix.Translation(xOffset, 0, 0));

            // To show ScreenSpaceLineNode in DXViewportView we need to put it inside a SceneNodeVisual3D
            var sceneNodeVisual3D = new SceneNodeVisual3D(screenSpaceLineNode);
            MainViewport.Children.Add(sceneNodeVisual3D);

            _disposables.Add(screenSpaceLineNode);
            _disposables.Add(lineMaterial);

            return screenSpaceLineNode;
        }

        private ScreenSpaceLineNode CreateLinesWithLineMesh(Vector3[] linePositions, bool isLineStrip, bool isLineClosed, bool isPolyLine, Color lineColor, float xOffset, out ScreenSpaceLineMesh screenSpaceLineMesh)
        {
            if (linePositions == null || linePositions.Length < 2)
            {
                screenSpaceLineMesh = null;
                return null;
            }

            // If line is closed but the first position is not the same as the last position, then add the first position as the last one
            if (isLineClosed && linePositions[0] != linePositions[linePositions.Length - 1])
            {
                Array.Resize(ref linePositions, linePositions.Length + 1);
                linePositions[linePositions.Length - 1] = linePositions[0];
            }


            // If we can easily calculate the bounding box from line positions
            // it is recommended to specify it in the ScreenSpaceLineMesh constructor.
            // If boundingBox is not specified, it will be calculated in the ScreenSpaceLineMesh constructor with checking all the positions.
            //
            // NOTE: If bounding box is not correct then camera's near and far planes can be invalid and this can cut some 3D objects at near or far plane (when DXScene.OptimizeNearAndFarCameraPlanes is true - by default)
            //var boundingBox = new BoundingBox(new Vector3(startX, 0, startZ), new Vector3(startX + linesCount * margin, 0, endZ));

            // Create ScreenSpaceLineMesh - it is used to create DirectX vertex buffer from positions
            screenSpaceLineMesh = new ScreenSpaceLineMesh(linePositions, isLineStrip);

            // When the line positions are changed many times, it is recommended to set CreateDynamicVertexBuffer to true.
            screenSpaceLineMesh.CreateDynamicVertexBuffer = true;

            var lineMaterial = CreateLineMaterial(isPolyLine, lineColor);

            var screenSpaceLineNode = new ScreenSpaceLineNode(screenSpaceLineMesh, lineMaterial);
            screenSpaceLineNode.Transform = new Transformation(SharpDX.Matrix.Translation(xOffset, 0, 0));

            // To show ScreenSpaceLineNode in DXViewportView we need to put it inside a SceneNodeVisual3D
            var sceneNodeVisual3D = new SceneNodeVisual3D(screenSpaceLineNode);
            MainViewport.Children.Add(sceneNodeVisual3D);

            _disposables.Add(screenSpaceLineMesh);
            _disposables.Add(screenSpaceLineNode);
            _disposables.Add(lineMaterial);

            return screenSpaceLineNode;
        }

        private LineMaterial CreateLineMaterial(bool isPolyLine, Color lineColor)
        {
            var lineMaterial = new LineMaterial()
            {
                LineColor = lineColor.ToColor4(),
                LineThickness = 10,
                IsPolyLine = isPolyLine
            };

            return lineMaterial;
        }


        private Vector3[] CreateLinePositions(float xOffset, float zOffset)
        {
            int   linesCount = 8;
            float margin     = 40;

            float startX = xOffset - 20;
            float endX   = startX + 40;

            float startZ = -0.5f * linesCount * margin + zOffset;


            var positions = new Vector3[linesCount * 2];

            int i = 0;
            for (int x = 0; x < linesCount; x++)
            {
                float z = startZ + x * margin;

                positions[i]     = new Vector3(startX, 0, z);
                positions[i + 1] = new Vector3(endX, 0, z);

                i += 2;
            }

            return positions;
        }

        private void ChangePositions(ScreenSpaceLineNode screenSpaceLineNode)
        {
            ChangePositions(screenSpaceLineNode.Positions);

            screenSpaceLineNode.UpdatePositions();
            screenSpaceLineNode.UpdateBounds();
        }

        private void ChangePositions(ScreenSpaceLineNode screenSpaceLineNode, ScreenSpaceLineMesh screenSpaceLineMesh)
        {
            ChangePositions(screenSpaceLineMesh.Positions);

            // Recreate vertex buffer that is defined with ScreenSpaceLineMesh
            screenSpaceLineMesh.RecreateMesh();


            // Update bounds of the mesh and SceneNode.
            BoundingBox boundingBox;
            BoundingBox.FromPoints(screenSpaceLineMesh.Positions, out boundingBox); // We could set boundingBox manually, but for demonstration purpose we calculate it from the positions

            screenSpaceLineMesh.Bounds.SetBoundingBox(ref boundingBox);

            screenSpaceLineNode.UpdatePositions();
            screenSpaceLineNode.UpdateBounds();
        }

        private void ChangePositions(Vector3[] positions)
        {
            int count = positions.Length;

            for (int i = 0; i < count; i += 2)
            {
                var currentPosition = positions[i];
                positions[i] = new Vector3(currentPosition.X, currentPosition.Y + 5, currentPosition.Z);
            }
        }

        private void ChangePositionsButton_OnClick(object sender, RoutedEventArgs e)
        {
            ChangePositions(_screenSpaceLineNode1);
            ChangePositions(_screenSpaceLineNode2);
            ChangePositions(_screenSpaceLineNode3);
            ChangePositions(_screenSpaceLineNode4, _screenSpaceLineMesh);
        }

        private void ChangeColorButton_OnClick(object sender, RoutedEventArgs e)
        {
            var rnd = new Random();
            var randomColor = new Color4((float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(), 1);

            // _screenSpaceLineNode1.LineMaterial is ILineMaterial interface and does not provide setter.
            // But in this sample we know that we have created the LineMaterial object, so we can cast to that and use the setter.

            ((LineMaterial)_screenSpaceLineNode1.LineMaterial).LineColor = randomColor;
            ((LineMaterial)_screenSpaceLineNode2.LineMaterial).LineColor = randomColor;
            ((LineMaterial)_screenSpaceLineNode3.LineMaterial).LineColor = randomColor;
            ((LineMaterial)_screenSpaceLineNode4.LineMaterial).LineColor = randomColor;

            _screenSpaceLineNode1.NotifySceneNodeChange(SceneNode.SceneNodeDirtyFlags.MaterialChanged);
        }

        private void ChangeThicknessButton_OnClick(object sender, RoutedEventArgs e)
        {
            // _screenSpaceLineNode1.LineMaterial is ILineMaterial interface and does not provide setter.
            // But in this sample we know that we have created the LineMaterial object, so we can cast to that and use the setter.

            ((LineMaterial) _screenSpaceLineNode1.LineMaterial).LineThickness += 1;
            ((LineMaterial) _screenSpaceLineNode2.LineMaterial).LineThickness += 1;
            ((LineMaterial) _screenSpaceLineNode3.LineMaterial).LineThickness += 1;
            ((LineMaterial) _screenSpaceLineNode4.LineMaterial).LineThickness += 1;

            _screenSpaceLineNode1.NotifySceneNodeChange(SceneNode.SceneNodeDirtyFlags.MaterialChanged);
        }


        private void Dispose()
        {
            _disposables.Dispose();
            _disposables = null;
        }
    }
}
