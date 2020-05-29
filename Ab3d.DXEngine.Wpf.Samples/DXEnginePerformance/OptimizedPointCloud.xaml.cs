using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
using Ab3d.DirectX;
using Ab3d.DirectX.Effects;
using Ab3d.DirectX.Materials;
using Ab3d.Visuals;
using SharpDX;
using SharpDX.Direct3D;

namespace Ab3d.DXEngine.Wpf.Samples.DXEnginePerformance
{
    // OptimizedPointMesh class is used to show point cloud and can dynamically reduce the number of rendered positions to improver rendering performance.
    // It creates one Vertex buffer and multiple Index buffers that are used to provide different level of optimized sub-sets of all the vertices.
    //
    // OptimizedPointMesh uses two techniques to do the optimization:<br/>
    // 1)
    // Divide all the positions into multiple segments and when rendering it checks bounding box of each segment to determine
    // if it is visible on the screen on not. Number of segments is defined in the OptimizedPointMesh constructor.
    // 
    // 2)
    // For each segment multiple views (Index Buffers) are created where the number of positions is defined by the pixel size 
    // on the screen and how close together the positions are (positions that are closer then size of one pixel are rendered as one pixel).
    // Number of views is defined by MaxOptimizationSubsetsCount and OptimizationIndicesNumberThreshold properties.
    // 
    // 
    // IMPORTANT:
    // It is very important that the positions are organized in such a way that the positions that are close together
    // in 3D space are also close together in positions array. This way the OptimizedPointMesh will be able to 
    // efficiently group positions into segments and render only one position instead of multiple positions that are close together.
    // 
    // Please contact support@ab4d.com if you need more information about the OptimizedPointMesh.


    /// <summary>
    /// Interaction logic for OptimizedPointCloud.xaml
    /// </summary>
    public partial class OptimizedPointCloud : Page
    {
        private const int PositionsBlockSize = 2;


        private DisposeList _modelDisposables;

        private PixelMaterial _pixelMaterial;

        private OptimizedPointMesh<Vector3> _optimizedPointMesh;
        private CustomRenderableNode _customRenderableNode;

        private int _lastRenderedPositionsCount;
        private int _lastTotalPositionsCount;
        private WireCrossVisual3D _wireCrossVisual3D;
        private SceneNodeVisual3D _sceneNodeVisual3D;

        private int _transformCounter;
        private AxisAngleRotation3D _axisAngleRotation3D;

        private bool _hasTransformationMatrix;
        private SharpDX.Matrix _transformationMatrix;


        public OptimizedPointCloud()
        {
            InitializeComponent();

            PixelSizeComboBox.ItemsSource = new float[] { 0.1f, 0.5f, 1, 2, 4, 8 };
            PixelSizeComboBox.SelectedIndex = 3;

            SceneTypeComboBox.ItemsSource = new string[]
            {
                "100,000 pixels (100 x 1,000)",
                "1 million pixels (100 x 10,000)",
                "10 million pixels (100 x 100,000)",
                "25 million pixels (1000 x 25,000)",
                "100 million pixels (1000 x 100,000)",
                "150 million pixels (1000 x 150,000)" 
                // Bigger values would exceed max .Net array size in CreatePositionsArray method
            };
            SceneTypeComboBox.SelectedIndex = 1;

            _modelDisposables = new DisposeList();


            DXDiagnostics.IsCollectingStatistics = true; // Collect rendering statistics

            MainDXViewportView.DXSceneInitialized += delegate (object sender, EventArgs args)
            {
                CreateScene();
            };

            MainDXViewportView.SceneRendered += delegate (object sender, EventArgs args)
            {
                UpdateStatistics();
            };

            // Setup keyboard support
            this.Focusable      =  true;             // by default Page is not focusable and therefore does not receive keyDown event
            this.PreviewKeyDown += OnPreviewKeyDown; // Use PreviewKeyDown to get arrow keys also (KeyDown event does not get them)
            this.Focus();

            this.Unloaded += delegate (object sender, RoutedEventArgs args)
            {
                _modelDisposables.Dispose();
                MainDXViewportView.Dispose();
            };
        }

        private void CreateScene()
        {
            if (MainDXViewportView.DXScene == null)
                return; // Not yet initialized or using WPF 3D

            Mouse.OverrideCursor = Cursors.Wait;

            MainViewport.Children.Clear();
            _modelDisposables.Dispose(); // Dispose previously used resources

            _modelDisposables = new DisposeList(); // Start with a fresh DisposeList


            // Parse number of positions to render
            var regex = new Regex(@".+\((?<width>\d+)\s?x\s?(?<length>[0-9,]+)\)");
            var sizeText = (string)SceneTypeComboBox.SelectedItem;

            var match = regex.Match(sizeText);

            int widthCount, lengthCount;

            if (match.Success)
            {
                string widthText = match.Groups["width"].Value;
                string lengthText = match.Groups["length"].Value;

                widthText = widthText.Replace(",", "");
                lengthText = lengthText.Replace(",", "");

                widthCount = Int32.Parse(widthText);
                lengthCount = Int32.Parse(lengthText);
            }
            else
            {
                widthCount = 100;
                lengthCount = 10000;
            }


            float widthStep = widthCount <= 100 ? 0.1f : 0.01f;
            float lengthStep = lengthCount <= 10000 ? 0.1f : 0.01f;


            float pixelSize = (float)PixelSizeComboBox.SelectedItem;


            var positionsArray = CreatePositionsArray(new Point3D(0, 0, 0), widthCount, lengthCount, widthStep, lengthStep);
            ShowPositionsArray(positionsArray, pixelSize, Colors.Red.ToColor4(), Bounds.Empty);

            Mouse.OverrideCursor = null;
        }
        
        private Vector3[] CreatePositionsArray(Point3D startPosition, int widthCount, int lengthCount, float widthStep, float lengthStep)
        {
            var positionsArray = new Vector3[widthCount * lengthCount];

            float yPos = (float)startPosition.Y;

            int i = 0;

            // We add positions to positionsArray with adding PositionsBlockSize x PositionsBlockSize blocks of pixels.
            // The reason for this is that 
            lengthCount /= PositionsBlockSize;
            widthCount /= PositionsBlockSize;

            double blockOuterSize = PositionsBlockSize + 0; // Add margin so we can see the individual blocks

            for (int lengthIndex = 0; lengthIndex < lengthCount; lengthIndex++)
            {
                float lengthPos = (float)(startPosition.Y + (lengthIndex * blockOuterSize * lengthStep));

                for (int widthIndex = 0; widthIndex < widthCount; widthIndex++)
                {
                    float widthPos = (float)(startPosition.X + (widthIndex * blockOuterSize * widthStep));


                    // Add one block of positions
                    for (int innerWidthIndex = 0; innerWidthIndex < PositionsBlockSize; innerWidthIndex++)
                    {
                        float innerWidthPos = widthPos + innerWidthIndex * widthStep;

                        for (int innerLengthIndex = 0; innerLengthIndex < PositionsBlockSize; innerLengthIndex++)
                        {
                            positionsArray[i] = new Vector3(lengthPos + innerLengthIndex * lengthStep, yPos, innerWidthPos);
                            i++;
                        }
                    }
                }
            }

            return positionsArray;
        }

        private void ShowPositionsArray(Vector3[] positionsArray, float pixelSize, Color4 pixelColor, Bounds positionBounds)
        {
            BoundingBox positionsBoundingBox;

            // To correctly set the Camera's Near and Far distance, we need to provide the correct bounds of each shown 3D model.
            if (positionBounds != null && !positionBounds.IsEmpty)
            {
                // It is highly recommended to manually set the Bounds.
                positionsBoundingBox = positionBounds.BoundingBox;
            }
            else
            {
                // if we do not manually set the Bounds, then we need to call CalculateBounds to calculate the bounds
                positionsBoundingBox = BoundingBox.FromPoints(positionsArray);
            }


            // Create OptimizedPointMesh that will optimize rendering or positions.
            // It uses two techniques to do that:

            _optimizedPointMesh = new OptimizedPointMesh<Vector3>(positionsArray, 
                                                                  positionsBoundingBox,
                                                                  segmentsCount: 100); // All the positions are divided into 100 segments - when rendering each segment is checked if it is visible in the current camera (if not, then it is not rendered)

            // NOTE that you can also use OptimizedPointMesh that takes more complex vertex struct for example PositionColor or PositionNormal. In this case use the other constructor.

            _optimizedPointMesh.OptimizationIndicesNumberThreshold = 100000; // We are satisfied with reducing the number of shown positions to 100000 (no need to optimize further - higher number reduced the initialization time)
            _optimizedPointMesh.MaxOptimizationViewsCount = 10; // Maximum number of created data sub-sets. The actual number can be lower when we hit the OptimizationIndicesNumberThreshold or when all vertices needs to be shown.
            
            _optimizedPointMesh.Optimize(new SharpDX.Size2(MainDXViewportView.DXScene.Width, MainDXViewportView.DXScene.Height), pixelSize);

            _optimizedPointMesh.InitializeResources(MainDXViewportView.DXScene.DXDevice);




            // We will need to dispose the SimpleMesh
            _modelDisposables.Add(_optimizedPointMesh);


            // Create a new PixelMaterial
            _pixelMaterial = new PixelMaterial()
            {
                PixelColor = pixelColor,
                PixelSize = pixelSize,

                // By default graphics card renders objects that are closer to the camera over the objects that are farther away from the camera.
                // This means that positions that are closer to the camera will be rendered over the positions that are farther away.
                // This may distort the shown colors.
                // Therefore when using pixel colors it is better to disable depth buffer checking and render all the pixels.
                // This is done with setting ReadZBuffer and WriteZBuffer to false.
                ReadZBuffer = false,
                WriteZBuffer = false
            };


            // It is also possible to set per-pixel colors (or per-pixel sizes with setting PixelSizes - not demonstrated here).
            // This comes with a performance drawback (see comment below).
            //
            // To test per-pixel colors, uncomment the following code:

            //var pixelColors = new Color4[positionsArray.Length];
            //for (int i = 0; i < positionsArray.Length; i++)
            //    pixelColors[i] = new Color4((i % 2 == 0) ? 1 : 0, 0, (i % 2 != 0) ? 1 : 0, 1);

            //_pixelMaterial.PixelColors = pixelColors;
            //_pixelMaterial.PixelColor = Color4.White; // When PixelColors array is used, then PixelColor is used as mask (each color in PixelColors is multiplied with PixelColor). To preserve the colors in PixelColors we need to set PixelColor to White.

            // By default the OptimizedPointCloud "combines" positions that are close together (closer the the size of one pixel on the screen).
            // and rendered only some of them. In this case it is possible that only each second point (or each tenth point) is rendered 
            // and this can removes the "color mixing" in our sample.
            // In such cases is is possible to disable this optimization with setting OptimizePositions to false:
            //_optimizedPointMesh.OptimizePositions = false;
            //
            // After this the OptimizedPointCloud will only provide optimization that works with grouping positions into 100 segments
            // and then checking which segments is visible in the camera (by checking segment bounding box). 
            // But when the camera is positioned in such a way that all positions are visible, 
            // then all positions will be sent to graphics card - in this case the OptimizePositions can provide good results with skipping some pixels.
            //
            // But if the actual colors from your data will not have such sharp color differences (will have more gradients),
            // then this problem should not be visible.


            _pixelMaterial.InitializeResources(MainDXViewportView.DXScene.DXDevice);

            _modelDisposables.Add(_pixelMaterial);


            // To render OptimizedPointMesh we need to use CustomRenderableNode that provides custom rendering callback action.
            _customRenderableNode = new CustomRenderableNode(RenderAction, _optimizedPointMesh.Bounds, _optimizedPointMesh, _pixelMaterial);
            _customRenderableNode.Name = "CustomRenderableNode";

            _modelDisposables.Add(_customRenderableNode);

            _sceneNodeVisual3D = new SceneNodeVisual3D(_customRenderableNode);

            MainViewport.Children.Add(_sceneNodeVisual3D);
        }

        private void RenderAction(RenderingContext renderingContext, CustomRenderableNode customRenderableNode, object objectToRender)
        {
            SharpDX.Matrix worldViewProjectionMatrix = renderingContext.UsedCamera.GetViewProjection();

            if (_hasTransformationMatrix)
                worldViewProjectionMatrix = _transformationMatrix * worldViewProjectionMatrix;

            // We could also read the transformation manually (as in the commented code below),
            // but it is faster to prepare the matrix when it is changed and not read WPF's DependencyProperties in rendering loop.

            //if (_sceneNodeVisual3D.Transform != null && !_sceneNodeVisual3D.Transform.Value.IsIdentity)
            //    worldViewProjectionMatrix = _sceneNodeVisual3D.Transform.Value.ToMatrix() * worldViewProjectionMatrix;

            //if (customRenderableNode.Transform != null && !customRenderableNode.Transform.IsIdentity)
            //    worldViewProjectionMatrix = customRenderableNode.Transform.Value * worldViewProjectionMatrix;


            var optimizedPointMesh = (OptimizedPointMesh<Vector3>)objectToRender;

            optimizedPointMesh.UpdateVisibleSegments(worldViewProjectionMatrix);
            optimizedPointMesh.RenderGeometry(renderingContext);
        }


        private void OnPixelSizeChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded || DesignerProperties.GetIsInDesignMode(this))
                return;

            var newPixelSize = (float)PixelSizeComboBox.SelectedItem;


            _optimizedPointMesh.Optimize(new SharpDX.Size2(MainDXViewportView.DXScene.Width, MainDXViewportView.DXScene.Height), newPixelSize);

            _pixelMaterial.PixelSize = newPixelSize;

            // After changing DirectX Material, we need to manually notify its object that the material was changed
            // This will also render the scene again.
            _customRenderableNode.NotifySceneNodeChange(SceneNode.SceneNodeDirtyFlags.MaterialChanged);
        }

        private void OnSceneTypeChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded || DesignerProperties.GetIsInDesignMode(this))
                return;

            CreateScene();
        }

        private void UpdateStatistics()
        {
            if (MainDXViewportView.DXScene == null || MainDXViewportView.DXScene.Statistics == null)
                return;


            // NOTE that if want to use RenderingStatistics we need to set DXDiagnostics.IsCollectingStatistics to true (here this is done in constructor)
            RenderingStatistics statistics = MainDXViewportView.DXScene.Statistics;

            int currentRenderedPositionsCount = statistics.DrawnIndicesCount;
            int currentTotalPositionsCount = _optimizedPointMesh.VertexBufferArray.Length;

            if (currentRenderedPositionsCount == _lastRenderedPositionsCount && currentTotalPositionsCount == _lastTotalPositionsCount)
                return; // Nn change


            RenderingStatisticsTextBlock.Text = string.Format("Rendered positions: {0:#,##0} / Total positions: {1:#,##0}",
                statistics.DrawnIndicesCount,
                currentTotalPositionsCount);

            PerformanceTextBlock.Text = string.Format("({0:0.0}% reduction)",
                100.0 - ((100.0 * (double)currentRenderedPositionsCount) / (double)currentTotalPositionsCount));

            _lastRenderedPositionsCount = currentRenderedPositionsCount;
            _lastTotalPositionsCount = currentTotalPositionsCount;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var mousePosition = Mouse.GetPosition(MainDXViewportView);
                var mouseRay = MainDXViewportView.DXScene.GetRayFromCamera((int)mousePosition.X, (int)mousePosition.Y);

                if (_optimizedPointMesh != null)
                {
                    //// Uncomment the following code to test the GetPositionIndexesAroundRay method
                    //var positionIndexes = _optimizedPointMesh.GetPositionIndexesAroundRay(mouseRay, 
                    //                                                                      maxDistance: 0.1f, 
                    //                                                                      orderByDistanceToRayPosition: true, 
                    //                                                                      maxResultsCount: 0);

                    //for (var i = 0; i < positionIndexes.Count; i++)
                    //{
                    //    var wireCrossVisual3D = new WireCrossVisual3D()
                    //    {
                    //        LineColor = Colors.Yellow,
                    //        LineThickness = 1,
                    //        LinesLength = 10,
                    //        Position = _optimizedPointMesh.PositionsArray[positionIndexes[i]].ToWpfPoint3D()
                    //    };

                    //    MainViewport.Children.Add(wireCrossVisual3D);
                    //}

                    //_pixelMaterial.ReadZBuffer = true;
                    //_customRenderableNode.NotifySceneNodeChange(SceneNode.SceneNodeDirtyFlags.MaterialChanged);

                    //return;

                    float positionDistance;
                    var   closestPositionIndex = _optimizedPointMesh.GetClosestPositionIndex(mouseRay, out positionDistance);

                    if (closestPositionIndex != -1)
                    {
                        var position = _optimizedPointMesh.PositionsArray[closestPositionIndex];

                        if (_wireCrossVisual3D == null)
                        {
                            _wireCrossVisual3D = new WireCrossVisual3D()
                            {
                                LineColor     = Colors.Blue,
                                LineThickness = 2,
                                LinesLength   = 2
                            };

                            MainViewport.Children.Add(_wireCrossVisual3D);
                        }
                        else
                        {
                            _wireCrossVisual3D.IsVisible = true;
                        }

                        _wireCrossVisual3D.Position = position.ToWpfPoint3D();

                        // In this sample the Point cloud is rendered without reading and writing to depth buffer (see ShowPositionsArray method).
                        // This means that all the points will be rendered regardless of other 3D objects in the scene.
                        // To correctly show other 3D object (in our case WireCrossVisual3D), we need to enable reading depth buffer.
                        // This will prevent rendering points where some other object is closer to the camera.
                        _pixelMaterial.ReadZBuffer = true;

                        // SceneNode objects do not get automatic notifications when some properties are changed.
                        // We need to do that manually:
                        _customRenderableNode.NotifySceneNodeChange(SceneNode.SceneNodeDirtyFlags.MaterialChanged);
                    }
                    else
                    {
                        MessageBox.Show("No closest position found");
                        _wireCrossVisual3D.IsVisible = false;

                        _pixelMaterial.ReadZBuffer = false;
                        _customRenderableNode.NotifySceneNodeChange(SceneNode.SceneNodeDirtyFlags.MaterialChanged);
                    }
                }
            }
        }

        private void ChangeColorButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_pixelMaterial == null || _customRenderableNode == null)
                return;

            var rnd = new Random();
            _pixelMaterial.PixelColor = System.Windows.Media.Color.FromRgb((byte)rnd.Next(255), (byte)rnd.Next(255), (byte)rnd.Next(255)).ToColor4();
            _customRenderableNode.NotifySceneNodeChange(SceneNode.SceneNodeDirtyFlags.MaterialChanged);
        }

        private void ChangeTransformButton_OnClick(object sender, RoutedEventArgs e)
        {
            _transformCounter++;
            double rotationAngle = _transformCounter * 15;


            // We can apply transformation to DXEngine's _customRenderableNode:

            //_transformationMatrix = Matrix.RotationAxis(new Vector3(0, 1, 0), MathUtil.DegreesToRadians((float) rotationAngle));
            //_hasTransformationMatrix = true;

            //if (_customRenderableNode.Transform == null)
            //    _customRenderableNode.Transform = new Ab3d.DirectX.Transformation(_transformationMatrix);
            //else
            //    _customRenderableNode.Transform.SetMatrix(_transformationMatrix);

            // When we are changing DXEngine's SceneNodes we need to notify the rendering engine about the changes
            // (this is not needed when working with WPF objects as seen below).
            //_customRenderableNode.NotifySceneNodeChange(SceneNode.SceneNodeDirtyFlags.TransformChanged);


            // Or simply add transformation to the _sceneNodeVisual3D:

            if (_axisAngleRotation3D == null)
            {
                _axisAngleRotation3D = new AxisAngleRotation3D(new Vector3D(0, 1, 0), rotationAngle);
                _sceneNodeVisual3D.Transform = new RotateTransform3D(_axisAngleRotation3D);
            }
            else
            {
                _axisAngleRotation3D.Angle = rotationAngle;
            }

            // Save the transformation matrix here so we do not need to read it from DependencyProperties on each rendering:
            _transformationMatrix = _sceneNodeVisual3D.Transform.Value.ToMatrix();
            _hasTransformationMatrix = true;
        }
    }
}
