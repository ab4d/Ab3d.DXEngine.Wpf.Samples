using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Ab3d.DirectX;
using Ab3d.DirectX.Materials;
using Ab3d.Visuals;

#if SHARPDX
using SharpDX;
using SharpDX.Direct3D;
using Matrix = SharpDX.Matrix;
#endif

namespace Ab3d.DXEngine.Wpf.Samples.DXEnginePerformance
{
    /// <summary>
    /// Interaction logic for ColoredPointCloud.xaml
    /// </summary>
    public partial class ColoredPointCloud : Page
    {
        private const bool UseOptimizedPointMesh = true;


        // By default graphics card renders objects that are closer to the camera over the objects that are farther away from the camera.
        // This means that positions that are closer to the camera will be rendered over the positions that are farther away.
        // In this sample this may distort the shown colors because the when multiple positions are rendered to the same pixel,
        // only the color of the position that is closest to the camera will be shown.
        // To fix this problem we disable depth buffer.
        // But when other 3D objects are also rendered, we need to at least enable reading depth buffer, so that 
        // point cloud is not rendered on top of existing 3D objects but is correctly "put" into the 3D world with other objects.
        private bool DisableDepthRead = false;
        private bool DisableDepthWrite = false;



        private DisposeList _disposables;
        private PixelMaterial _pixelMaterial;
        private MeshObjectNode _meshObjectNode;

        private OptimizedPointMesh<Vector3> _optimizedPointMesh;

        private Vector3[] _positions;
        private Color4[] _positionColors;
        private Color4[] _savedPositionColors;
        private BoundingBox _positionsBounds;
        private CustomRenderableNode _customRenderableNode;
        private SceneNodeVisual3D _sceneNodeVisual3D;

        public ColoredPointCloud()
        {
            InitializeComponent();

            _disposables = new DisposeList();

            // Wait until DXScene is initialized and then create the data
            MainDXViewportView.DXSceneInitialized += delegate (object sender, EventArgs args)
            {
                Mouse.OverrideCursor = Cursors.Wait;

                try
                {
                    _positionsBounds = new BoundingBox(new Vector3(-100, 0, -100), new Vector3(100, 20, 100));

                    GenerateSinusPointCloudData(xCount: 300, yCount: 300, 
                                                bounds: _positionsBounds,
                                                positions: out _positions);


                    var linearGradientBrush = CreateDataGradientBrush();
                    var gradientColorsArray = CreateGradientColorsArray(linearGradientBrush);

                    // Setup offsets and factors so that each position will be converted
                    // into a value from 0 to 1 based on the y position.
                    // This value will be used to define the color from the gradientColorsArray.

                    float minValue = _positionsBounds.Minimum.Y;
                    float maxValue = _positionsBounds.Maximum.Y;

                    var offsets = new Vector3(0, -minValue, 0);
                    var factors = new Vector3(0, 1.0f / (maxValue - minValue), 0);
                    _positionColors = CreatePositionColorsArray(_positions, offsets, factors, gradientColorsArray);



                    InitializePointCloud(_positions, _positionsBounds, _positionColors, UseOptimizedPointMesh, DisableDepthRead, DisableDepthWrite);
                }
                finally
                {
                    Mouse.OverrideCursor = null;
                }
            };

            this.Unloaded += delegate (object sender, RoutedEventArgs args)
            {
                _disposables.Dispose();
                MainDXViewportView.Dispose();
            };
        }



        private void InitializePointCloud(Vector3[] positions, BoundingBox positionsBounds, Color4[] positionColors, bool useOptimizedPointMesh, bool disableDepthRead, bool disableDepthWrite)
        {
            if (MainDXViewportView.DXScene == null)
                return; // If this happens, then this method is called too soon (before DXEngine is initialized) or we are using WPF 3D


            // First, set up the material:

            // Create a new PixelMaterial
            _pixelMaterial = new PixelMaterial()
            {
                PixelColor = Color4.White, // When using PixelColors, PixelColor is used as a mask (multiplied with each color)
                PixelSize = 2,
                PixelColors = positionColors,

                // By default graphics card renders objects that are closer to the camera over the objects that are farther away from the camera.
                // This means that positions that are closer to the camera will be rendered over the positions that are farther away.
                // This may distort the shown colors.
                // Therefore when using pixel colors it is better to disable depth buffer checking and render all the pixels.
                // This is done with setting ReadZBuffer and WriteZBuffer to false.
                ReadZBuffer = !disableDepthRead,
                WriteZBuffer = !disableDepthWrite
            };

            _pixelMaterial.InitializeResources(MainDXViewportView.DXScene.DXDevice);

            _disposables.Add(_pixelMaterial);


            // Now set up the mesh and create SceneNode to show it

            if (useOptimizedPointMesh)
            {
                _optimizedPointMesh = new OptimizedPointMesh<Vector3>(positions,
                                                                      positionsBounds,
                                                                      segmentsCount: 100);

                // NOTE that you can also use OptimizedPointMesh that takes more complex vertex struct for example PositionColor or PositionNormal. In this case use the other constructor.

                _optimizedPointMesh.OptimizationIndicesNumberThreshold = 100000; // We are satisfied with reducing the number of shown positions to 100000 (no need to optimize further - higher number reduced the initialization time)
                _optimizedPointMesh.MaxOptimizationViewsCount = 10;     // Maximum number of created data sub-sets. The actual number can be lower when we hit the OptimizationIndicesNumberThreshold or when all vertices needs to be shown.

                _optimizedPointMesh.Optimize(new Size2(MainDXViewportView.DXScene.Width, MainDXViewportView.DXScene.Height), standardPointSize: 1);

                _optimizedPointMesh.InitializeResources(MainDXViewportView.DXScene.DXDevice);

                _disposables.Add(_optimizedPointMesh);


                // To render OptimizedPointMesh we need to use CustomRenderableNode that provides custom rendering callback action.
                _customRenderableNode = new CustomRenderableNode(RenderAction, _optimizedPointMesh.Bounds, _optimizedPointMesh, _pixelMaterial);
                _customRenderableNode.Name = "CustomRenderableNode";
                //customRenderableNode.CustomRenderingQueue = MainDXViewportView.DXScene.BackgroundRenderingQueue;

                _disposables.Add(_customRenderableNode);

                _sceneNodeVisual3D = new SceneNodeVisual3D(_customRenderableNode);
                //sceneNodeVisual3D.Transform = transform;

                MainViewport.Children.Add(_sceneNodeVisual3D);
            }
            else
            {
                // Use SimpleMesh - all positions will be always rendered:

                var simpleMesh = new SimpleMesh<Vector3>(vertexBufferArray: positions,
                                                         indexBufferArray: null,
                                                         inputLayoutType: InputLayoutType.Position);

                simpleMesh.PrimitiveTopology = PrimitiveTopology.PointList; // We need to change the default PrimitiveTopology.TriangleList to PointList

                // To correctly set the Camera's Near and Far distance, we need to provide the correct bounds of each shown 3D model.

                // It is highly recommended to manually set the Bounds.
                simpleMesh.Bounds = new Bounds(positionsBounds);

                // if we do not manually set the Bounds, then we need to call CalculateBounds to calculate the bounds
                //simpleMesh.CalculateBounds();

                // We will need to dispose the SimpleMesh
                _disposables.Add(simpleMesh);


                // Now create a new MeshObjectNode
                _meshObjectNode = new Ab3d.DirectX.MeshObjectNode(simpleMesh, _pixelMaterial);

                _disposables.Add(_meshObjectNode);

                // To be able to add the MeshObjectNode (or any other SceneNode) to WPF's Viewport3D,
                // we need to create a SceneNodeVisual3D
                var sceneNodeVisual3D = new SceneNodeVisual3D(_meshObjectNode);

                MainViewport.Children.Add(sceneNodeVisual3D);
            }


            Camera1.TargetPosition = positionsBounds.Center.ToWpfPoint3D();
            Camera1.Distance = positionsBounds.ToRect3D().GetDiagonalLength();
        }

        private void RenderAction(RenderingContext renderingContext, CustomRenderableNode customRenderableNode, object objectToRender)
        {
            Matrix worldViewProjectionMatrix = renderingContext.UsedCamera.GetViewProjection();

            if (customRenderableNode.Transform != null && !customRenderableNode.Transform.IsIdentity)
                worldViewProjectionMatrix = customRenderableNode.Transform.Value * worldViewProjectionMatrix;

            var optimizedPointMesh = (OptimizedPointMesh<Vector3>)objectToRender;

            optimizedPointMesh.UpdateVisibleSegments(worldViewProjectionMatrix);
            optimizedPointMesh.RenderGeometry(renderingContext);
        }


        private void GenerateSinusPointCloudData(int xCount, int yCount, BoundingBox bounds, out Vector3[] positions)
        {
            int positionsCount = xCount * yCount;

            positions = new Vector3[positionsCount]; 

            int positionIndex = 0;


            float xStep = (bounds.Maximum.X - bounds.Minimum.X) / (float)xCount;
            float zStep = (bounds.Maximum.Z - bounds.Minimum.Z) / (float)yCount;

            float yRange = bounds.Maximum.Y - bounds.Minimum.Y;

            float xPos = bounds.Minimum.X;

            for (int x = 0; x < xCount; x++)
            {
                float zPos = bounds.Minimum.Z;

                for (int y = 0; y < yCount; y++)
                {
                    float height = (float)Math.Sin(x * 0.01 * 2 * Math.PI) * (float)Math.Sin(y * 0.05 * 2 * Math.PI);
                    height = height * 0.5f + 0.5f; // put in range from 0 to 1

                    positions[positionIndex] = new Vector3(xPos, height * yRange, zPos);

                    positionIndex++;
                    zPos += zStep;
                }

                xPos += xStep;
            }
        }


        private Color4[] CreatePositionColorsArray(Vector3[] positions, Vector3 offsets, Vector3 factors, Color4[] gradientColorsArray)
        {
            var colors = new Color4[positions.Length];

            float gradientsCountFloat = gradientColorsArray.Length;
            int maxGradientsIndex = gradientColorsArray.Length - 1;

            for (var i = 0; i < positions.Length; i++)
            {
                var onePosition = positions[i];
                float colorValue = (onePosition.X + offsets.X) * factors.X +
                                   (onePosition.Y + offsets.Y) * factors.Y +
                                   (onePosition.Z + offsets.Z) * factors.Z;

                int gradientIndex = Convert.ToInt32(colorValue * gradientsCountFloat + 0.5f);
                gradientIndex = gradientIndex > 0
                    ? (gradientIndex < maxGradientsIndex ? gradientIndex : maxGradientsIndex)
                    : 0;

                colors[i] = gradientColorsArray[gradientIndex];
            }

            return colors;
        }

        private Color4[] CreateGradientColorsArray(LinearGradientBrush linearGradientBrush)
        {
            // We use HeightMapMesh3D.GetGradientColorsArray to create an array with color values created from the gradient. The array size is 50.
            var gradientColorsArray = Ab3d.Meshes.HeightMapMesh3D.GetGradientColorsArray(linearGradientBrush, 50);

            // Convert WPF colors to SharpDX Color4 used by DXEngine (and DirectX)
            var gradientColor4Array = new Color4[gradientColorsArray.Length];
            for (var i = 0; i < gradientColorsArray.Length; i++)
                gradientColor4Array[i] = gradientColorsArray[i].ToColor4();

            return gradientColor4Array;
        }

        private LinearGradientBrush CreateDataGradientBrush()
        {
            var gradientStopCollection = new GradientStopCollection();

            gradientStopCollection.Add(new GradientStop(Colors.Red, 1));
            gradientStopCollection.Add(new GradientStop(Colors.Yellow, 0.75));
            gradientStopCollection.Add(new GradientStop(Colors.Lime, 0.5));
            gradientStopCollection.Add(new GradientStop(Colors.Aqua, 0.25));
            gradientStopCollection.Add(new GradientStop(Colors.Blue, 0));

            var linearGradientBrush = new LinearGradientBrush(gradientStopCollection,
                new System.Windows.Point(0, 1),  // startPoint (offset == 0) - note that y axis is down (so 1 is bottom)
                new System.Windows.Point(0, 0)); // endPoint (offset == 1)

            return linearGradientBrush;
        }


        private void TransformButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_positions == null || MainDXViewportView.DXScene == null)
                return;

            var transform = _customRenderableNode.Transform;
            if (transform == null)
            {
                transform = new Transformation(Matrix.Identity);
                _customRenderableNode.Transform = transform;
            }

            transform.Value *= Matrix.Translation(0, 10, 0); // move up for 10 units

            _customRenderableNode.NotifySceneNodeChange(SceneNode.SceneNodeDirtyFlags.TransformChanged);
            MainDXViewportView.Refresh();
        }
        
        private void ChangeColorButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_positionColors == null || MainDXViewportView.DXScene == null)
                return;

            var rnd = new Random();
            var randomPixelColor = System.Windows.Media.Color.FromRgb((byte)rnd.Next(255), (byte)rnd.Next(255), (byte)rnd.Next(255)).ToColor4();

            // Change colors of 1/5 of positions to green
            int oneFifth = _positionColors.Length / 5;
            for (var i = 0; i < oneFifth; i++)
                _positionColors[i] = randomPixelColor;

            _pixelMaterial.UpdatePixelColors();
            
            if (_savedPositionColors != null)
            {
                // Also update _savedPositionColors
                for (var i = 0; i < oneFifth; i++)
                    _savedPositionColors[i] = randomPixelColor;
            }

            MainDXViewportView.DXScene.NotifyChange(DXScene.ChangeNotifications.RenderingPrimitiveDirty);
            MainDXViewportView.Refresh();
        }
        
        

        private void ShowHideButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_positionColors == null || MainDXViewportView.DXScene == null)
                return;

            int oneFifth = _positionColors.Length / 5;

            if (_positionColors[oneFifth].Alpha == 0)
            {
                ShowHideButton.Content = "Hide";

                // Restore position colors
                Array.Copy(_savedPositionColors, _positionColors, _positionColors.Length);
            }
            else
            {
                // Save position colors
                if (_savedPositionColors == null)
                    _savedPositionColors = new Color4[_positionColors.Length];

                // NOTE:
                // We could save only 1/5 of the colors (those that are changes below), but for simplicity we save all colors
                Array.Copy(_positionColors, _savedPositionColors, _positionColors.Length); 


                // set color's alpha value to 0, to hide the pixel
                for (var i = oneFifth; i < oneFifth * 2; i++)
                    _positionColors[i] = new Color4(0, 0, 0, 0); 

                ShowHideButton.Content = "Show";
            }

            _pixelMaterial.UpdatePixelColors();

            MainDXViewportView.DXScene.NotifyChange(DXScene.ChangeNotifications.RenderingPrimitiveDirty);
            MainDXViewportView.Refresh();
        }
    }
}