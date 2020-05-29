using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
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
using Ab3d.DirectX;
using Ab3d.DirectX.Cameras;
using Ab3d.DirectX.Controls;
using Ab3d.DirectX.Materials;
using Ab3d.DirectX.Models;
using Ab3d.Visuals;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using BaseCamera = Ab3d.Cameras.BaseCamera;
using Buffer = SharpDX.Direct3D11.Buffer;


namespace Ab3d.DXEngine.Wpf.Samples.DXEngineAdvanced
{
    // TODO:
    // - Fix segments frustum culling


    /// <summary>
    /// Interaction logic for ShadedPointCloudSample.xaml
    /// </summary>
    public partial class ShadedPointCloudSample : Page
    {
        // SAMPLE SETTINGS:

        // It is possible to load data from a text file. 
        // The first line in the file needs to be a number that specifies the number of points.
        // Then each line represents one point with 6 float values (in Invariant culture - dot is decimal sign) separated with tab '\t'
        // The first three values represents x,y and z value of the point 's position, the next 3 values represent the point's normal.

        //private const string SampleDataFile = @"C:\...\abnahmewerkzeug.normal-tab.csv";
        private const string SampleDataFile = null; // When null, sample cylinder is created


        // SegmentsCount NOTE: Increasing number of segments increases the number of required draw calls
        // For example Draw render times: 100 segments = 0,88;  10 segments = 0,19
        // When showing all segments, the CompleteRenderTimeMs does not change significantly.
        // Because positions are not ideally segmented, it is better to have lower number of segments.
        private const int SegmentsCount = 20;

        private ShadedPointCloudEffect _shadedPointCloudEffect;

        private DisposeList _pointCloudDisposables; // This will hold the objects that need to be disposed when the point could data are changed or this sample is unloaded

        private PositionNormal[] _vertexBuffer;
        private EffectMaterial _effectMaterial;

        private Random _rnd = new Random();

        private SharpDX.Direct3D11.Buffer[] _indexBuffers;

        private List<OptimizedPointMesh<PositionNormal>> _optimizedPointMeshes;

        public ShadedPointCloudSample()
        {
            InitializeComponent();

            // First create an instance of AssemblyShaderBytecodeProvider.
            // This will allow using EffectsManager to cache and get the shaders from the assembly's EmbeddedResources.
            // See ShadedPointCloudEffect.EnsureShaders method for more info.
            var resourceAssembly = this.GetType().Assembly;
            var assemblyShaderBytecodeProvider = new AssemblyShaderBytecodeProvider(resourceAssembly, resourceAssembly.GetName().Name + ".Resources.Shaders.");

            EffectsManager.RegisterShaderResourceStatic(assemblyShaderBytecodeProvider);

            MainDXViewportView.PresentationType = DXView.PresentationTypes.DirectXImage;

            //MainDXViewportView.GraphicsProfiles = new GraphicsProfile[] { GraphicsProfile.LowQualityHardwareRendering };

            //Ab3d.DirectX.Controls.D3DHost.RenderAsManyFramesAsPossible = true;



            // Subscribe to DXSceneDeviceCreated event - there the DirectX 11 device was already created
            MainDXViewportView.DXSceneInitialized += delegate(object sender, EventArgs args)
            {
                var dxScene = MainDXViewportView.DXScene;

                if (dxScene == null) // When null, then we are probably using WPF 3D rendering
                    return;

                // Create a new instance of ShadedPointCloudEffect
                _shadedPointCloudEffect = new ShadedPointCloudEffect();

                // Register effect with EffectsManager - this will also initialize the effect with calling OnInitializeResources method in the ShadedPointCloudEffect class
                dxScene.DXDevice.EffectsManager.RegisterEffect(_shadedPointCloudEffect);


                // Set global effect settings
                _shadedPointCloudEffect.DiffuseColor = Colors.Orange.ToColor4();
                _shadedPointCloudEffect.SpecularColor = Color3.White;
                _shadedPointCloudEffect.SpecularPower = 64;
                _shadedPointCloudEffect.PointSize = (float)PointSizeComboBox.SelectedItem;


                // Create new material from the effect
                _effectMaterial = new EffectMaterial(_shadedPointCloudEffect);
                //_disposables.Add(_effectMaterial); // is not added to disposables because it is disposed separately and can be disposed after the number of positions is changed in the DropDown


                // Create the demo data and show them
                RecreatePointCloud();
            };



            ModelsCountComboBox.ItemsSource = new int[] { 1, 2, 3, 4, 5, 10, 20, 50, 60, 100, 200, 500 };
            ModelsCountComboBox.SelectedIndex = 2;

            PointsCountComboBox.ItemsSource = new int[] {100000, 500000, 1000000, 2000000, 5000000, 10000000, 20000000, 30000000, 40000000, 50000000, 60000000};
            PointsCountComboBox.SelectedIndex = 2;

            PointSizeComboBox.ItemsSource = new float[] { 0.001f, 0.005f, 0.01f, 0.02f, 0.05f, 0.1f, 0.2f, 0.5f, 1.0f, 2.0f, 5.0f, 10.0f };
            PointSizeComboBox.SelectedIndex = 8;


            if (SampleDataFile != null)
            {
                PointsCountTextBlock.Visibility = Visibility.Collapsed;
                PointsCountComboBox.Visibility = Visibility.Collapsed;
            }


            Camera1.StartRotation(45, 0);
            StartStopCameraButton.Content = "Stop camera rotation";

            Camera1.CameraChanged += delegate (object sender, CameraChangedRoutedEventArgs args)
            {
                if (MainDXViewportView.DXScene == null)
                    return;

                MainDXViewportView.DXScene.Camera.Update();
                double pixelSize = OptimizedPointMesh<PositionNormal>.GetPixel3DSize(MainDXViewportView.DXScene.Camera, MainDXViewportView.DXScene.Width, new Vector3(0, 0, 0));

                if (Camera1.CameraType == BaseCamera.CameraTypes.OrthographicCamera)
                    InfoTextBlock.Text = $"CameraWidth:  {Camera1.CameraWidth:F0}; Pixel size: {pixelSize:F2}";
                else
                    InfoTextBlock.Text = $"Camera Distance:  {Camera1.Distance:F0}; Pixel size: {pixelSize:F2}";
            };

            this.Unloaded += (sender, args) => Dispose();
        }

        private void DisposePointDataObjects()
        {
            if (_pointCloudDisposables != null)
            {
                _pointCloudDisposables.Dispose();
                _pointCloudDisposables = null;
            }

            if (_indexBuffers != null)
            {
                for (var i = 0; i < _indexBuffers.Length; i++)
                    _indexBuffers[i].Dispose();

                _indexBuffers = null;
            }

            _optimizedPointMeshes = null;
        }

        private void Dispose()
        {
            DisposePointDataObjects();

            if (_effectMaterial != null)
            {
                _effectMaterial.Dispose();
                _effectMaterial = null;
            }

            if (_shadedPointCloudEffect != null)
            {
                _shadedPointCloudEffect.Dispose();
                _shadedPointCloudEffect = null;
            }

            MainDXViewportView.Dispose();
        }

        private void RecreatePointCloud()
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                // Create the demo data
                if (string.IsNullOrEmpty(SampleDataFile))
                    _vertexBuffer = CreatePointCloudData();
                else
                    _vertexBuffer = LoadPointCloudData(SampleDataFile);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

            // Create mesh and scene node that will show the data
            CreatePointCloudNode();
        }

        private void CreatePointCloudNode()
        {
            if (_vertexBuffer == null)
                return;


            DisposePointDataObjects();
            PointCloudRootVisual3D.Children.Clear();


            // Start with new DisposeList
            _pointCloudDisposables = new DisposeList();

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                var modelsCount = (int)ModelsCountComboBox.SelectedItem;
                for (int j = 0; j < modelsCount; j++)
                {
                    var transform = new TranslateTransform3D(j * 100, 0, 0);

                    if (ShowSegmentsCheckBox.IsChecked ?? false)
                        AddPointCloudWithColorSegments(SegmentsCount, transform);
                    else
                        AddPointCloudNode(transform);
                }
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void AddPointCloudWithColorSegments(int segmentsCount, Transform3D transform)
        {
            int totalPositionsCount = _vertexBuffer.Length;
            int oneSegmentSize = totalPositionsCount / segmentsCount;

            float selectedPointSize = (float)PointSizeComboBox.SelectedItem;

            for (int segmentIndex = 0; segmentIndex < segmentsCount; segmentIndex++)
            {
                int segmentStartIndex = segmentIndex * oneSegmentSize;

                int segmentEndIndex;
                if (segmentIndex == segmentsCount - 1)
                    segmentEndIndex = totalPositionsCount - 1; // Last segments contains the positions until the end of the originalPositions (previous segments may be smaller because of rounding when divided by segmentsCount
                else
                    segmentEndIndex = segmentStartIndex + oneSegmentSize - 1;


                int positionsCount = segmentEndIndex - segmentStartIndex + 1;
                var positions = new Vector3[positionsCount];

                int posIndex = 0;
                for (int i = segmentStartIndex + 1; i <= segmentEndIndex; i++)
                {
                    positions[posIndex] = _vertexBuffer[i].Position;
                    posIndex++;
                }


                var randomColor = System.Windows.Media.Color.FromRgb((byte) _rnd.Next(255), (byte) _rnd.Next(255), (byte) _rnd.Next(255));

                var pixelsVisual3D = new Ab3d.Visuals.PixelsVisual3D()
                {
                    Positions = positions,
                    PixelColor = randomColor,
                    PixelSize = selectedPointSize,
                    Transform = transform
                };

                PointCloudRootVisual3D.Children.Add(pixelsVisual3D);
            }
        }

        private void AddPointCloudNode(Transform3D transform)
        {
            int positionsCount = _vertexBuffer.Length;
            var positions = new Vector3[positionsCount];

            for (int i = 0; i < positionsCount; i++)
                positions[i] = _vertexBuffer[i].Position;


            var boundingBox = BoundingBox.FromPoints(positions);


            var optimizedPointMesh = new OptimizedPointMesh<PositionNormal>(_vertexBuffer, 
                                                                            positions, 
                                                                            InputLayoutType.Position | InputLayoutType.Normal,
                                                                            boundingBox, 
                                                                            segmentsCount: SegmentsCount,
                                                                            name: "ShaderOptimizedPointMesh");


            float selectedPointSize = (float)PointSizeComboBox.SelectedItem;

            if (!MainDXViewportView.DXScene.BuffersInitialized)
                throw new Exception("Cannot create OptimizedPointMesh without know DXScene Size");

            // Use size from DXScene, because this also takes DPI settings into account and gives us the most accurate amount of available pixels (better then DXViewportView.ActualWidth / Height)
            optimizedPointMesh.Optimize(new SharpDX.Size2(MainDXViewportView.DXScene.Width, MainDXViewportView.DXScene.Height), selectedPointSize);

            optimizedPointMesh.InitializeResources(MainDXViewportView.DXScene.DXDevice);


            _pointCloudDisposables.Add(optimizedPointMesh); // _pointsMesh is not added to disposables because it is disposed separately and can be disposed after the number of positions is changed in the DropDown


            if (_optimizedPointMeshes == null)
                _optimizedPointMeshes = new List<OptimizedPointMesh<PositionNormal>>();

            _optimizedPointMeshes.Add(optimizedPointMesh);


            _shadedPointCloudEffect.DiffuseColor = Colors.Orange.ToColor4();


            var customRenderableNode = new CustomRenderableNode(RenderAction, new Bounds(boundingBox), optimizedPointMesh, _effectMaterial);
            customRenderableNode.Name = "CustomRenderableNode";

            _pointCloudDisposables.Add(customRenderableNode);

            var sceneNodeVisual3D = new SceneNodeVisual3D(customRenderableNode);
            sceneNodeVisual3D.Transform = transform;

            PointCloudRootVisual3D.Children.Add(sceneNodeVisual3D);
        }

        private void RenderAction(RenderingContext renderingContext, CustomRenderableNode customRenderableNode, object objectToRender)
        {
            SharpDX.Matrix worldViewProjectionMatrix = renderingContext.UsedCamera.GetViewProjection();

            if (customRenderableNode.Transform != null && !customRenderableNode.Transform.IsIdentity)
                worldViewProjectionMatrix = customRenderableNode.Transform.Value * worldViewProjectionMatrix;

            var optimizedPointMesh = (OptimizedPointMesh<PositionNormal>)objectToRender;

            optimizedPointMesh.UpdateVisibleSegments(worldViewProjectionMatrix);
            optimizedPointMesh.RenderGeometry(renderingContext);
        }


        private void ShowShadedPointsCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            CreatePointCloudNode();
        }


        private void PointsCountComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            RecreatePointCloud();
        }

        private void PointSizeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            //RecreatePointCloud();

            var selectedValue = (float)PointSizeComboBox.SelectedItem;
            _shadedPointCloudEffect.PointSize = selectedValue;

            MainDXViewportView.Refresh(); // Render the scene again
        }

        private void OnSettingsChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            if (_optimizedPointMeshes != null)
            {
                foreach (var optimizedPointMesh in _optimizedPointMeshes)
                {
                    optimizedPointMesh.OptimizePositions = OptimizePositionsCheckBox.IsChecked ?? false;
                    optimizedPointMesh.RenderOnlyVisibleSegments = RenderOnlyVisibleSegmentsCheckBox.IsChecked ?? false;
                }
            }

            if (_shadedPointCloudEffect != null)
            {
                if (SpecularLightingCheckBox.IsChecked ?? false)
                    _shadedPointCloudEffect.SpecularPower = 64;
                else
                    _shadedPointCloudEffect.SpecularPower = 0;
            }

            MainDXViewportView.Refresh(); // Render the scene again
        }
        
        private void StartStopCameraButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (Camera1.IsRotating)
            {
                Camera1.StopRotation();
                StartStopCameraButton.Content = "Start camera rotation";
            }
            else
            {
                Camera1.StartRotation(30, 0);
                StartStopCameraButton.Content = "Stop camera rotation";
            }
        }

        private void OnShowSegmentsCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            OptimizePositionsCheckBox.IsEnabled = !(ShowSegmentsCheckBox.IsChecked ?? false);
            RenderOnlyVisibleSegmentsCheckBox.IsEnabled = !(ShowSegmentsCheckBox.IsChecked ?? false);

            RecreatePointCloud();
        }


        // Load position and normal data from a text file.
        // The first line in the file must be number of points.
        // Then each line represents one point with tab separated values (floats are in InvariantCulture with . as decimal separator). 
        private PositionNormal[] LoadPointCloudData(string fileName)
        {
            PositionNormal[] shadedPoints = null;

            using (var fs = System.IO.File.OpenText(fileName))
            {
                int index = -1;

                try
                {
                    string oneLine = fs.ReadLine();

                    // First line should have number of rows
                    int rowsCount = Int32.Parse(oneLine);
                    index++;

                    shadedPoints = new PositionNormal[rowsCount];

                    for (int i = 0; i < rowsCount; i++)
                    {
                        oneLine = fs.ReadLine();
                        var oneLineParts = oneLine.Split('\t');

                        // IMPORTANT: In WPF and DXEngine y is up so we need to swap y and z values

                        shadedPoints[index].Position = new Vector3(float.Parse(oneLineParts[0], System.Globalization.CultureInfo.InvariantCulture),
                                                                   float.Parse(oneLineParts[2], System.Globalization.CultureInfo.InvariantCulture),
                                                                   float.Parse(oneLineParts[1], System.Globalization.CultureInfo.InvariantCulture));

                        shadedPoints[index].Normal = new Vector3(float.Parse(oneLineParts[3], System.Globalization.CultureInfo.InvariantCulture),
                                                                 float.Parse(oneLineParts[5], System.Globalization.CultureInfo.InvariantCulture),
                                                                 float.Parse(oneLineParts[4], System.Globalization.CultureInfo.InvariantCulture));
                        index++;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format("Error reading point cloud data in line {0} of file:\r\n{1}\r\n\r\nError message:\r\n{2}", 
                                                  index + 1, fileName, ex.Message));

                    return shadedPoints;
                }
            }

            return shadedPoints;
        }

        private PositionNormal[] CreatePointCloudData()
        {
            // Performance note for NVIDIA GTX 970 with 4 GB ram:
            // 20 mio points: CompleteRenderTime takes around 10 ms
            // 10 mio points: CompleteRenderTime is less than 0.1 ms
            //
            // On NVIDIA 1080 GTX with 8 GB ram the big performance drop happens around 40 mio points.


            int overallPointCount = (int)PointsCountComboBox.SelectedItem;
            //int overallPointCount = 100000;
            int sliceCount = 500; // 500 points in one horizontal circle

            if ((long)overallPointCount * (long)PositionNormal.SizeInBytes > (long)Int32.MaxValue)
                throw new Exception("shadedPoints array too big. Split data into multiple arrays.");


            var shadedPoints = new PositionNormal[overallPointCount];

            //for (int i = 0; i < overallPointCount / sliceCount; i++)

            // Parallel improves performance of 50 mio points from 4378ms to 821ms
            Parallel.For(0, overallPointCount / sliceCount, (i) =>
            {
                for (int j = 0; j < sliceCount; j++)
                {
                    int index = sliceCount * i + j;

                    float x = 30.0f * (float) Math.Cos(2 * Math.PI * (double) j / sliceCount);
                    float y = 0.02f * i;
                    float z = 30.0f * (float) Math.Sin(2 * Math.PI * (double) j / sliceCount);

                    var normal = new Vector3(x, 0, z);
                    normal.Normalize();

                    shadedPoints[index] = new PositionNormal()
                    {
                        Position = new Vector3(x, y, z),
                        Normal = normal
                    };
                }
            });

            return shadedPoints;
        }

        private void SetColorInPointData(PositionNormalColor[] shadedPoints, Color3 color)
        {
            var length = shadedPoints.Length;
            for (int i = 0; i < length; i++)
                shadedPoints[i].Color = color;
        }

        private PositionNormalColor[] AddColorToPointData(PositionNormal[] shadedPoints, Color3 color)
        {
            var length = shadedPoints.Length;

            if ((long)length * (long)PositionNormalColor.SizeInBytes > (long)Int32.MaxValue)
                throw new Exception("shadedPoints array too big. Split data into multiple arrays.");

            var shadedPointsWithColor = new PositionNormalColor[length];

            for (int i = 0; i < length; i++)
            {
                shadedPointsWithColor[i].Position = shadedPoints[i].Position;
                shadedPointsWithColor[i].Normal   = shadedPoints[i].Normal;
                shadedPointsWithColor[i].Color    = color;
            }

            return shadedPointsWithColor;
        }

        // The following struct will be part of the next version of DXEngine

        /// <summary>
        /// PositionNormalTexture is a struct used for vertex buffer that defines Position, Normal and Color
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct PositionNormalColor
        {
            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="position">position</param>
            /// <param name="normal">normal</param>
            /// <param name="color">color</param>
            public PositionNormalColor(Vector3 position, Vector3 normal, Color3 color)
            {
                Position = position;
                Normal = normal;
                Color = color;
            }

            /// <summary>
            /// Position
            /// </summary>
            public Vector3 Position;

            /// <summary>
            /// Normal
            /// </summary>
            public Vector3 Normal;

            /// <summary>
            /// Color
            /// </summary>
            public Color3 Color;

            /// <summary>
            /// Size in floats
            /// </summary>
            public const int SizeInFloats = 3 + 3 + 3;

            /// <summary>
            /// Size in bytes
            /// </summary>
            public const int SizeInBytes = SizeInFloats * 4;

            /// <summary>
            /// ToString
            /// </summary>
            /// <returns>string</returns>
            public override string ToString()
            {
                return string.Format("Pos: {0}; Normal: {1}; Color: {2}", Position, Normal, Color);
            }
        }
    }
}
