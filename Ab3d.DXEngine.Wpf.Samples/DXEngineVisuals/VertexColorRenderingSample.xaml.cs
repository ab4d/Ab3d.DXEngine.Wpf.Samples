using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Ab3d.Common.Cameras;
using Ab3d.Controls;
using Ab3d.DirectX;
using Ab3d.DirectX.Effects;
using Ab3d.DirectX.Materials;
using Ab3d.DirectX.Models;
using Ab3d.Visuals;
using SharpDX;
using SharpDX.Direct3D;
using Point = System.Windows.Point;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    /// <summary>
    /// Interaction logic for VertexColorRenderingSample.xaml
    /// </summary>
    public partial class VertexColorRenderingSample : Page
    {
        private MeshGeometry3D _objectGeometry3D;
        private GeometryModel3D _vertexColorGeometryModel3D;

        private Color4[] _positionColorsArray;

        private VertexColorMaterial _vertexColorMaterial;

        private Color4[] _gradientColor4Array;

        private bool _isLastMousePositionHit;
        private bool _isUserBeamControl;
        private DateTime _animationStartTime;
        private LineMaterial _lineMaterial;

        public VertexColorRenderingSample()
        {
            InitializeComponent();

            CreateGradientColorsArray();

            // Use CameraControllerInfo to show that we can use left mouse button to set custom beam destination on the 3D model
            CameraControllerInfo.AddCustomInfoLine(0, MouseCameraController.MouseAndKeyboardConditions.LeftMouseButtonPressed, "SET BEAM DESTINATION");

            // When the ViewportBorder size is change the size of the overlay Canvas (drawn over the 3D scene)
            ViewportBorder.SizeChanged += delegate(object sender, SizeChangedEventArgs args)
            {
                UpdateOverlayCanvasSize();
            };


            // Process mouse events
            ViewportBorder.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                // Start user beam control
                _isUserBeamControl = true;

                ViewportBorder.CaptureMouse();

                var position = e.GetPosition(ViewportBorder);
                ProcessMouseHit(position);
            };

            ViewportBorder.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                // Stop user beam control
                _isUserBeamControl = false;
                ViewportBorder.ReleaseMouseCapture();

                ProcessMouseOutOfModel();
            };

            // Subscribe to MouseMove to allow user to specify the beam target
            ViewportBorder.MouseMove += delegate (object sender, MouseEventArgs e)
            {
                if (_isUserBeamControl)
                    ProcessMouseHit(e.GetPosition(ViewportBorder));
                else
                    ProcessMouseOutOfModel();
            };


            // Start animating the beam position
            CompositionTarget.Rendering += CompositionTargetOnRendering;


            // We add test models after the DXScene is initialized (this is required because specifal effects require DirectX device)
            MainDXViewportView.DXSceneInitialized += delegate(object sender, EventArgs e)
            {
                if (MainDXViewportView.DXScene == null) 
                    return; // Probably WPF 3D rendering

                // Get _vertexColorEffect that will be used to render model with vertex colors (note that this field must be disposed when it is not used any more - here in Unloaded event handler) 

                AddTestModels();
            };

            // Cleanup
            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                CompositionTarget.Rendering -= CompositionTargetOnRendering;

                if (_vertexColorMaterial != null)
                {
                    _vertexColorMaterial.Dispose();
                    _vertexColorMaterial = null;
                }

                if (_lineMaterial != null)
                {
                    _lineMaterial.Dispose();
                    _lineMaterial = null;
                }

                MainDXViewportView.Dispose();
            };
        }

        private void ProcessMouseHit(System.Windows.Point mousePosition)
        {
            bool isVertexColorDataChanged;

            RayMeshGeometry3DHitTestResult hitTestResult;

            if (double.IsNaN(mousePosition.X)) // if mousePosition.X is NaN, then we consider this as mouse did not hit the model
            {
                hitTestResult = null;
            }
            else
            {
                hitTestResult = VisualTreeHelper.HitTest(MainViewport, mousePosition) as RayMeshGeometry3DHitTestResult;

                BeamLine1.X2 = mousePosition.X;
                BeamLine1.Y2 = mousePosition.Y;
                BeamLine1.Visibility = Visibility.Visible;

                BeamLine2.X2 = mousePosition.X;
                BeamLine2.Y2 = mousePosition.Y;
                BeamLine2.Visibility = Visibility.Visible;
            }

            if (hitTestResult != null)
            {
                var hitPoint3D = hitTestResult.PointHit;

                // Update the _positionColorsArray (array of colors for each vertex) so that each color is calculated as distance from the hitPoint3D.
                // Colors are set to the distances between 0 and 50; after 50 the lase color (light blue) is assigned.
                CalculatePositionColorsFromDistance(_objectGeometry3D.Positions, hitPoint3D, 50, _positionColorsArray);

                isVertexColorDataChanged = true;
                _isLastMousePositionHit = true;

                BeamLine1.Stroke = Brushes.Red;
                BeamLine2.Stroke = Brushes.Red;
            }
            else
            {
                // Show Gray line for missed position

                BeamLine1.Stroke = Brushes.Gray;
                BeamLine2.Stroke = Brushes.Gray;

                if (_isLastMousePositionHit)
                {
                    // If before the mouse position hit the 3D mode, then the 3D model was colored
                    // But now the position do not hit the 3D mode any more - so we need to color the 3D mode with the last color - the one that is used for the biggest beam distance (light blue)
                    var lastColor = _gradientColor4Array[_gradientColor4Array.Length - 1];
                    FillPositionColorsArray(lastColor);

                    isVertexColorDataChanged = true;
                }
                else
                {
                    isVertexColorDataChanged = false;
                }

                _isLastMousePositionHit = false;
            }

            if (isVertexColorDataChanged)
            {
                //_vertexColorMaterial.PositionColors = _positionColorsArray; // PositionColors property was already set before

                // Update method on the VertexColorMaterial will update the underlying DirectX vertex buffer that
                // is created from PositionColors array and is sent to graphics card to render the 3D model.
                _vertexColorMaterial.Update();

                // Because we have manually updated the DXEngine material we need to notify the DXEngine's SceneNode that is using this material about the change.
                var sceneNode = MainDXViewportView.GetSceneNodeForWpfObject(_vertexColorGeometryModel3D);
                if (sceneNode != null)
                    sceneNode.NotifySceneNodeChange(SceneNode.SceneNodeDirtyFlags.MaterialChanged);
            }
        }

        private void ProcessMouseOutOfModel()
        {
            ProcessMouseHit(new System.Windows.Point(double.NaN, double.NaN)); // This will act as we missed the 3D model
        }


        // Animate the beam position
        private void CompositionTargetOnRendering(object sender, EventArgs eventArgs)
        {
            if (_isUserBeamControl) // If user is controlling the beam position (when left mouse button is pressed), then we do not animate it
                return;

            if (_animationStartTime == DateTime.MinValue)
            {
                _animationStartTime = DateTime.Now;
                return;
            }

            double elapsedSeconds = (DateTime.Now - _animationStartTime).TotalSeconds;


            // Position around center of the OverlayCanvas and with radius = 100 and in clockwise direction (negate the elapsedSeconds)
            double xPos = Math.Sin(-elapsedSeconds) * 100 + OverlayCanvas.Width / 2;
            double yPos = Math.Cos(-elapsedSeconds) * 100 + OverlayCanvas.Height / 2;

            // Simulate mouse hit at the specified positions
            ProcessMouseHit(new System.Windows.Point(xPos, yPos));
        }

        private void UpdateOverlayCanvasSize()
        {
            OverlayCanvas.Width = ViewportBorder.ActualWidth;
            OverlayCanvas.Height = ViewportBorder.ActualHeight;

            BeamLine1.X1 = OverlayCanvas.Width * 2.0 / 5.0;
            BeamLine1.Y1 = OverlayCanvas.Height;

            BeamLine2.X1 = OverlayCanvas.Width * 3.0 / 5.0;
            BeamLine2.Y1 = OverlayCanvas.Height;

            BeamLine1.Visibility = Visibility.Collapsed;
            BeamLine2.Visibility = Visibility.Collapsed;
        }



        private void AddTestModels()
        {
            // Load teapot model from obj file
            var readerObj = new ReaderObj();
            var geometryModel3D = readerObj.ReadModel3D(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\Models\teapot.obj")) as GeometryModel3D;

            if (geometryModel3D == null)
                return;

            _objectGeometry3D = (MeshGeometry3D) geometryModel3D.Geometry;

            

            int positionsCount = _objectGeometry3D.Positions.Count;

            // Create and fill the _positionColorsArray with the last color (light blue)
            _positionColorsArray = new Color4[positionsCount];

            var lastColor = _gradientColor4Array[_gradientColor4Array.Length - 1];
            FillPositionColorsArray(lastColor);

            // Now create the VertexColorMaterial that will be used instead of standard material
            // and will make the model render with special effect where each vertex can have its own color.
            _vertexColorMaterial = new VertexColorMaterial()
            {
                PositionColors = _positionColorsArray, // The PositionColors property is used to specify colors for each vertex
                CreateDynamicBuffer = true,            // Because we will update the _positionColorsArray on each frame, it is better to create a dynamic DirectX buffer
                
                // To show specular effect set the specular data here:
                //SpecularPower = 16,
                //SpecularColor = Color3.White,
                //HasSpecularColor = true
            };

            // Create standard WPF material and set the _vertexColorMaterial to be used when the model is rendered in DXEngine.
            var vertexColorDiffuseMaterial = new DiffuseMaterial();
            vertexColorDiffuseMaterial.SetUsedDXMaterial(_vertexColorMaterial);


            // Create a GeometryModel3D that will be rendered with _vertexColorMaterial
            _vertexColorGeometryModel3D = new GeometryModel3D(_objectGeometry3D, vertexColorDiffuseMaterial);

            var vertexColorModelVisual3D = new ModelVisual3D()
            {
                Content = _vertexColorGeometryModel3D
            };

            MainViewport.Children.Add(vertexColorModelVisual3D);



            // Show the same MeshGeometry3D but this time with wireframe material
            var wireframeWpfMaterial = new DiffuseMaterial();

            _lineMaterial = new LineMaterial()
            {
                LineThickness = 1,
                LineColor = Color4.Black,
                DepthBias = 0.1f,
            };

            wireframeWpfMaterial.SetUsedDXMaterial(_lineMaterial);


            var wireframeGeometryModel3D = new GeometryModel3D(_objectGeometry3D, wireframeWpfMaterial);

            var wireframeModelVisual3D = new ModelVisual3D()
            {
                Content = wireframeGeometryModel3D
            };

            MainViewport.Children.Add(wireframeModelVisual3D);
        }

        private void CalculatePositionColorsFromDistance(Point3DCollection positions, Point3D targetPosition, double maxLength, Color4[] positionColors)
        {
            var positionsCount = positions.Count;

            int maxColorsIndex = _gradientColor4Array.Length - 1;

            for (int i = 0; i < positionsCount; i++)
            {
                var onePosition = positions[i];

                // Get distance of this position from the targetPosition
                double length = (onePosition - targetPosition).Length;
                if (length > maxLength)
                    length = maxLength;

                // Get index of this color inside the _gradientColor4Array
                int colorIndex = (int) ((length / maxLength) * maxColorsIndex);

                // Set color
                positionColors[i] = _gradientColor4Array[colorIndex];
            }
        }

        // Fill _positionColorsArray with the specified color
        private void FillPositionColorsArray(Color4 color)
        {
            for (var i = 0; i < _positionColorsArray.Length; i++)
                _positionColorsArray[i] = color;
        }

        private void CreateGradientColorsArray()
        {
            // We use HeightMapMesh3D.GetGradientColorsArray to create an array with color values created from the gradient. The array size is 30.
            var gradientStopCollection = new GradientStopCollection();

            //gradientStopCollection.Add(new GradientStop(Colors.Red, 1));
            //gradientStopCollection.Add(new GradientStop(Colors.Yellow, 0.75));
            //gradientStopCollection.Add(new GradientStop(Colors.Lime, 0.5));
            //gradientStopCollection.Add(new GradientStop(Colors.Aqua, 0.25));
            //gradientStopCollection.Add(new GradientStop(Colors.Blue, 0));

            // We adjust the colors to create better heat effect
            gradientStopCollection.Add(new GradientStop(Colors.Red, 0));
            gradientStopCollection.Add(new GradientStop(Colors.Red, 0.10));
            gradientStopCollection.Add(new GradientStop(Colors.Yellow, 0.4));
            gradientStopCollection.Add(new GradientStop(Colors.Aqua, 0.7));
            gradientStopCollection.Add(new GradientStop(Colors.Aqua, 1));

            var linearGradientBrush = new LinearGradientBrush(gradientStopCollection, new System.Windows.Point(0, 0), new System.Windows.Point(0, 1));

            // We can use the GetGradientColorsArray from HeightMapMesh3D (Ab3d.PowerToys libraty) to convert the LinearGradientBrush to array of 30 colors
            var gradientColorsArray = Ab3d.Meshes.HeightMapMesh3D.GetGradientColorsArray(linearGradientBrush, 30);

            // Convert WPF colors to SharpDX Color4
            _gradientColor4Array = new Color4[gradientColorsArray.Length];
            for (var i = 0; i < gradientColorsArray.Length; i++)
                _gradientColor4Array[i] = gradientColorsArray[i].ToColor4();
        }
    }
}
