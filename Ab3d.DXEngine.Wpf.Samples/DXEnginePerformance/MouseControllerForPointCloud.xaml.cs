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
using System.Windows.Navigation;
using System.Windows.Shapes;
using Ab3d.Common.Cameras;
using Ab3d.DirectX;
using Ab3d.DirectX.Effects;
using Ab3d.DirectX.Materials;
using Ab3d.DXEngine.Wpf.Samples.Common;
using Ab3d.Visuals;
using SharpDX;
using SharpDX.Direct3D;

namespace Ab3d.DXEngine.Wpf.Samples.DXEnginePerformance
{
    /// <summary>
    /// Interaction logic for MouseControllerForPointCloud.xaml
    /// </summary>
    public partial class MouseControllerForPointCloud : Page
    {
        private DisposeList _disposables;
        private PixelMaterial _pixelMaterial;

        private OptimizedPointMesh<Vector3> _optimizedPointMesh;

        public MouseControllerForPointCloud()
        {
            InitializeComponent();

            _disposables = new DisposeList();

            // Wait until DXScene is initialized and then create the data
            MainDXViewportView.DXSceneInitialized += delegate (object sender, EventArgs args)
            {
                Mouse.OverrideCursor = Cursors.Wait;

                try
                {
                    Vector3[] positions;
                    var positionsBounds = new BoundingBox(new Vector3(-100, 0, -100), new Vector3(100, 20, 100));

                    GenerateSinusPointCloudData(xCount: 300, yCount: 300,
                                                bounds: positionsBounds,
                                                positions: out positions);


                    var linearGradientBrush = CreateDataGradientBrush();
                    var gradientColorsArray = CreateGradientColorsArray(linearGradientBrush);

                    // Setup offsets and factors so that each position will be converted
                    // into a value from 0 to 1 based on the y position.
                    // This value will be used to define the color from the gradientColorsArray.

                    float minValue = positionsBounds.Minimum.Y;
                    float maxValue = positionsBounds.Maximum.Y;

                    var offsets = new Vector3(0, -minValue, 0);
                    var factors = new Vector3(0, 1.0f / (maxValue - minValue), 0);
                    Color4[] positionColors = CreatePositionColorsArray(positions, offsets, factors, gradientColorsArray);


                    InitializePointCloud(positions, positionsBounds, positionColors);


                    // PointCloudMouseCameraController class is available with full source code (see PointCloudMouseCameraController.cs in Common folder).
                    // Set DXScene and OptimizedPointMesh to PointCloudMouseCameraController1
                    PointCloudMouseCameraController1.DXScene = MainDXViewportView.DXScene;
                    PointCloudMouseCameraController1.OptimizedPointMesh = _optimizedPointMesh;

                    PointCloudMouseCameraController1.MaxDistanceToAnyPosition = 1.0f;
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



        private void InitializePointCloud(Vector3[] positions, BoundingBox positionsBounds, Color4[] positionColors)
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
            };

            _pixelMaterial.InitializeResources(MainDXViewportView.DXScene.DXDevice);

            _disposables.Add(_pixelMaterial);


            // Now set up the mesh and create SceneNode to show it
            _optimizedPointMesh = new OptimizedPointMesh<Vector3>(positions,
                                                                  positionsBounds,
                                                                  segmentsCount: 100);

            // NOTE that you can also use OptimizedPointMesh that takes more complex vertex struct for example PositionColor or PositionNormal. In this case use the other constructor.

            _optimizedPointMesh.OptimizationIndicesNumberThreshold = 100000; // We are satisfied with reducing the number of shown positions to 100000 (no need to optimize further - higher number reduced the initialization time)
            _optimizedPointMesh.MaxOptimizationViewsCount = 10;     // Maximum number of created data sub-sets. The actual number can be lower when we hit the OptimizationIndicesNumberThreshold or when all vertices needs to be shown.

            _optimizedPointMesh.Optimize(new SharpDX.Size2(MainDXViewportView.DXScene.Width, MainDXViewportView.DXScene.Height), standardPointSize: 1);

            _optimizedPointMesh.InitializeResources(MainDXViewportView.DXScene.DXDevice);

            _disposables.Add(_optimizedPointMesh);


            // To render OptimizedPointMesh we need to use CustomRenderableNode that provides custom rendering callback action.
            var customRenderableNode = new CustomRenderableNode(RenderAction, _optimizedPointMesh.Bounds, _optimizedPointMesh, _pixelMaterial);
            customRenderableNode.Name = "CustomRenderableNode";
            //customRenderableNode.CustomRenderingQueue = MainDXViewportView.DXScene.BackgroundRenderingQueue;

            _disposables.Add(customRenderableNode);

            var sceneNodeVisual3D = new SceneNodeVisual3D(customRenderableNode);
            //sceneNodeVisual3D.Transform = transform;

            MainViewport.Children.Add(sceneNodeVisual3D);
           

            Camera1.TargetPosition = positionsBounds.Center.ToWpfPoint3D();
            Camera1.Distance = positionsBounds.ToRect3D().GetDiagonalLength() * 0.5;
        }

        private void RenderAction(RenderingContext renderingContext, CustomRenderableNode customRenderableNode, object objectToRender)
        {
            SharpDX.Matrix worldViewProjectionMatrix = renderingContext.UsedCamera.GetViewProjection();

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
                    float height = (float)Math.Sin(x * 0.002 * 2 * Math.PI) * (float)Math.Sin(y * 0.002 * 2 * Math.PI);
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
    }
}
