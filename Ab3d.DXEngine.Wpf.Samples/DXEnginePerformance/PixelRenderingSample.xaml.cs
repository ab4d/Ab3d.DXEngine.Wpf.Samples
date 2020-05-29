using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using Ab3d.Common.Cameras;
using Ab3d.DirectX;
using Ab3d.DirectX.Effects;
using Ab3d.DirectX.Materials;
using Ab3d.Visuals;
using SharpDX;
using SharpDX.Direct3D;

namespace Ab3d.DXEngine.Wpf.Samples.DXEnginePerformance
{
    /// <summary>
    /// Interaction logic for PixelRenderingSample.xaml
    /// </summary>
    public partial class PixelRenderingSample : Page
    {
        private const bool _isUsingPixelsVisual3D = true; // Set to false to use low lever DXEngine objects (MeshObjectNode and PixelMaterial) instead of PixelsVisual3D

        private DisposeList _disposables;

        private PixelEffect _pixelEffect;

        private bool _isPixelSizeChanged;

        private bool _isInternalChange;

        private PixelMaterial _pixelMaterial;
        private MeshObjectNode _meshObjectNode;

        public PixelRenderingSample()
        {
            InitializeComponent();

            PixelSizeComboBox.ItemsSource = new float[] {0.1f, 0.5f, 1, 2, 4, 8};
            PixelSizeComboBox.SelectedIndex = 3;

            _disposables = new DisposeList();

            MainDXViewportView.DXSceneInitialized += delegate(object sender, EventArgs args)
            {
                CreateScene();
            };

            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                _disposables.Dispose();

                if (_pixelEffect != null)
                {
                    _pixelEffect.Dispose();
                    _pixelEffect = null;
                }

                MainDXViewportView.Dispose();
            };
        }

        private void CreateScene()
        {
            if (MainDXViewportView.DXScene == null)
                return; // Not yet initialized or using WPF 3D

            Mouse.OverrideCursor = Cursors.Wait;

            MainViewport.Children.Clear();
            _disposables.Dispose(); // Dispose previously used resources

            _disposables = new DisposeList(); // Start with a fresh DisposeList


            float pixelSize = (float)PixelSizeComboBox.SelectedItem;

            switch (SceneTypeComboBox.SelectedIndex)
            {
                case 0: // Box
                    var boxModel3D = Ab3d.Models.Model3DFactory.CreateBox(new Point3D(0, 0, 0), new Size3D(100, 100, 100), 10, 10, 10, new DiffuseMaterial(Brushes.Green));
                    ShowGeometryModel3D(boxModel3D, pixelSize);
                    break;

                case 1: // Sphere
                    var sphereModel3D = Ab3d.Models.Model3DFactory.CreateSphere(new Point3D(0, 0, 0), 80, 50, new DiffuseMaterial(Brushes.DeepSkyBlue));
                    ShowGeometryModel3D(sphereModel3D, pixelSize);
                    break;

                case 2: // Dragon model
                    var readerObj = new Ab3d.ReaderObj();
                    var readModel3D = readerObj.ReadModel3D(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\Models\dragon_vrip_res3.obj")) as GeometryModel3D;

                    var transform3DGroup = new Transform3DGroup();
                    transform3DGroup.Children.Add(new ScaleTransform3D(1000, 1000, 1000));
                    transform3DGroup.Children.Add(new TranslateTransform3D(0, -120, 0));

                    readModel3D.Transform = transform3DGroup;

                    // This will be available in the next version of Ab3d.PowerToys
                    //Ab3d.Utilities.ModelUtils.CenterAndScaleModel3D(readModel3D, new Point3D(-200, 0, 0), new Size3D(200, 200, 200), preserveAspectRatio: true, preserveCurrentTransformation: true);

                    Ab3d.Utilities.ModelUtils.ChangeMaterial(readModel3D, newMaterial: new DiffuseMaterial(Brushes.Gold), newBackMaterial: null);

                    ShowGeometryModel3D(readModel3D, pixelSize);
                    break;

                case 3: // 10,000 pixels (100 x 1 x 100)
                    var positionsArray001 = CreatePositionsArray(new Point3D(0, 0, 0), new Size3D(300, 1, 300), 100, 1, 100);
                    ShowPositionsArray(positionsArray001, pixelSize, Colors.Red.ToColor4(), new Point3D(0, 0, 0), new Size3D(300, 1, 300));
                    break;

                case 4: // 1 million pixels (100 x 100 x 100)
                    var positionsArray1 = CreatePositionsArray(new Point3D(0, 0, 0), new Size3D(220, 220, 220), 100, 100, 100);
                    ShowPositionsArray(positionsArray1, pixelSize, Colors.Red.ToColor4(), new Point3D(0, 0, 0), new Size3D(220, 220, 220));
                    break;

                case 5: // 9 million pixels (9 x 1M)
                    AddMillionBlocks(3, 3, new Size3D(80, 80, 80), pixelSize, Colors.Red.ToColor4());
                    break;

                case 6: // 25 million pixels (5 x 5 x 1M)
                    AddMillionBlocks(5, 5, new Size3D(60, 60, 60), pixelSize, Colors.Red.ToColor4());
                    break;

                case 7: // 100 million pixels (10 x 10 x 1M)
                    AddMillionBlocks(10, 10, new Size3D(30, 30, 30), pixelSize, Colors.Red.ToColor4());
                    break;
            }

            Mouse.OverrideCursor = null;
        }

        private void AddMillionBlocks(int xCount, int zCount, Size3D blockSize, float pixelSize, Color4 pixelColor)
        {
            AdjustPixelSize(ref pixelSize, 0.5f);

            double totalSizeX = xCount * blockSize.X * 1.5; // multiply by 1.5 to add half blockSize margin between blocks
            double totalSizeZ = zCount * blockSize.Z * 1.5;

            double x = -(totalSizeX - blockSize.X) / 2;

            for (int ix = 0; ix < xCount; ix++)
            {
                double z = -(totalSizeZ - blockSize.Z) / 2;

                for (int iz = 0; iz < zCount; iz++)
                {
                    var positionsArray = CreatePositionsArray(new Point3D(x, 0, z), blockSize, 100, 100, 100);
                    ShowPositionsArray(positionsArray, pixelSize, pixelColor, new Point3D(x, 0, z), blockSize);

                    z += 1.5 * blockSize.Z;
                }

                x += 1.5 * blockSize.X;
            }
        }

        private void AdjustPixelSize(ref float pixelSize, float desiredPixelSize)
        {
            // If user did not yet changed the pixel size (so pixel size is still 2),
            // we change it to desiredPixelSize to better see the number of pixels
            if (!_isPixelSizeChanged)
            {
                _isInternalChange = true;
                PixelSizeComboBox.SelectedItem = desiredPixelSize;
                pixelSize = desiredPixelSize;
                _isInternalChange = false;
            }
        }

        private void ShowPositionsArray(Vector3[] positionsArray, float pixelSize, Color4 pixelColor, Point3D centerPosition, Size3D size)
        {
            var positionBounds = new Bounds(new Vector3((float) (centerPosition.X - size.X * 0.5), (float) (centerPosition.Y - size.Y * 0.5), (float) (centerPosition.Z - size.Z * 0.5)),
                                            new Vector3((float) (centerPosition.X + size.X * 0.5), (float) (centerPosition.Y + size.Y * 0.5), (float) (centerPosition.Z + size.Z * 0.5)));

            ShowPositionsArray(positionsArray, pixelSize, pixelColor, positionBounds);  
        }

        private void ShowPositionsArray(Vector3[] positionsArray, float pixelSize, Color4 pixelColor, Bounds positionBounds)
        {
            if (_isUsingPixelsVisual3D)
            {
                // The easiest way to show many pixels is to use PixelsVisual3D.
                var pixelsVisual3D = new PixelsVisual3D()
                {
                    Positions = positionsArray,
                    PixelColor = pixelColor.ToWpfColor(),
                    PixelSize = pixelSize
                };

                // It is highly recommended to manually set the PositionsBounds.
                // If this is not done, the bounds are calculated by the DXEngine with checking all the positions.
                pixelsVisual3D.PositionsBounds = positionBounds;

                MainViewport.Children.Add(pixelsVisual3D);

                // !!! IMPORTANT !!!
                // When PixelsVisual3D is not used any more, it needs to be disposed (we are using DisposeList to dispose all in Unloaded event handler)
                _disposables.Add(pixelsVisual3D);

                return;
            }


            // First stop in showing positions in the positionsArray as pixels is to create a SimpleMesh<Vector3>.
            // This will create a DirectX VertexBuffer that will be passed to the shaders.
            var simpleMesh = new SimpleMesh<Vector3>(vertexBufferArray: positionsArray,
                                                     indexBufferArray: null,
                                                     inputLayoutType: InputLayoutType.Position);

            simpleMesh.PrimitiveTopology = PrimitiveTopology.PointList; // We need to change the default PrimitiveTopology.TriangleList to PointList

            // To correctly set the Camera's Near and Far distance, we need to provide the correct bounds of each shown 3D model.
            
            if (positionBounds != null && !positionBounds.IsEmpty)
            {
                // It is highly recommended to manually set the Bounds.
                simpleMesh.Bounds = positionBounds;
            }
            else
            {
                // if we do not manually set the Bounds, then we need to call CalculateBounds to calculate the bounds
                simpleMesh.CalculateBounds();
            }

            simpleMesh.CalculateBounds();


            // We will need to dispose the SimpleMesh
            _disposables.Add(simpleMesh);


            // Create a new PixelMaterial
            _pixelMaterial = new PixelMaterial()
            {
                PixelColor = pixelColor,
                PixelSize = pixelSize
            };

            _pixelMaterial.InitializeResources(MainDXViewportView.DXScene.DXDevice);

            _disposables.Add(_pixelMaterial);


            // Now create a new MeshObjectNode
            _meshObjectNode = new Ab3d.DirectX.MeshObjectNode(simpleMesh, _pixelMaterial);

            _disposables.Add(_meshObjectNode);

            // To be able to add the MeshObjectNode (or any other SceneNode) to WPF's Viewport3D,
            // we need to create a SceneNodeVisual3D
            var sceneNodeVisual3D = new SceneNodeVisual3D(_meshObjectNode);

            MainViewport.Children.Add(sceneNodeVisual3D);
        }

        private void ShowGeometryModel3D(GeometryModel3D model3D, float pixelSize)
        {
            MainViewport.Children.Clear();
            _disposables.Dispose(); // Dispose previously used resources

            _disposables = new DisposeList(); // Start with a fresh DisposeList

            if (_pixelEffect == null)
            {
                // Get an instance of PixelEffect (it is used to provide the correct shaders to render specified postions as pixels)
                _pixelEffect = MainDXViewportView.DXScene.DXDevice.EffectsManager.GetEffect<PixelEffect>(createNewEffectInstanceIfNotFound: true);

                // Do not forget to dispose the effect when it is not used any more - we will do that in the Unloaded event handler
            }


            // We will render the GeometryModel3D with overriding the usage of standard effect
            // that renders DiffuseMaterial and other standard materials
            // with custom effect: PixelEffect.
            // This will use different shaders to render the provided triangles.
            //
            // Because the standard material does not provide pixel size,
            // we need to set the fallback pixel size value to the PixelEffect.
            _pixelEffect.PixelSize = pixelSize;


            // To override the used material, we first need to create a new WpfMaterial from the WPF material.
            var wpfMaterial = new WpfMaterial(model3D.Material);

            // then set the Effect to it ...
            wpfMaterial.Effect = _pixelEffect;

            // and finally specify the WpfMaterial to be used whenever the model3D.Material is used.
            model3D.Material.SetUsedDXMaterial(wpfMaterial);

            _disposables.Add(wpfMaterial);


            // Now just add the model3D to the MainViewport
            var modelVisual3D = new ModelVisual3D();
            modelVisual3D.Content = model3D;

            MainViewport.Children.Add(modelVisual3D);


            // IMPORTANT:
            // The above method of showing 3D model with pixels is not optimal.
            // The reason for this is that this way the pixles are rendered for each triangle in the model.
            // But because the same positions are usually used by multiple triangles, those positions will be rendered multiple times.
            //
            // A better way to render the models is to extract the positions from MeshGeometry3D 
            // and render the pixels are a list of positions.
            // This is shown in the commented code below:
            //
            // The code above is provided to also show a way to render a 3D model with a different effect.

            //var meshGeometry3D = (MeshGeometry3D)model3D.Geometry;
            //var positions = meshGeometry3D.Positions;
            //var positionsCount = positions.Count;

            //var positionsArray = new Vector3[positionsCount];

            //if (model3D.Transform == null || model3D.Transform.Value.IsIdentity)
            //{
            //    for (int i = 0; i < positionsCount; i++)
            //        positionsArray[i] = positions[i].ToVector3();
            //}
            //else
            //{
            //    for (int i = 0; i < positionsCount; i++)
            //        positionsArray[i] = model3D.Transform.Transform(positions[i]).ToVector3();
            //}

            //// Extract pixel color from material (use Red as fallback)
            //Color4 pixelColor = Colors.Red.ToColor4();
            //var diffuseMaterial = model3D.Material as DiffuseMaterial;
            //if (diffuseMaterial != null)
            //{
            //    var solidColorBrush = diffuseMaterial.Brush as SolidColorBrush;
            //    if (solidColorBrush != null)
            //        pixelColor = solidColorBrush.Color.ToColor4();
            //}

            //ShowPositionsArray(positionsArray, pixelSize, pixelColor, model3D.Bounds.ToDXEngineBounds());
        }

        private void OnPixelSizeChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded || DesignerProperties.GetIsInDesignMode(this) || _isInternalChange || _pixelEffect == null)
                return;

            _isPixelSizeChanged = true;

            var newPixelSize = (float)PixelSizeComboBox.SelectedItem;

            if (_isUsingPixelsVisual3D)
            {
                foreach (var pixelsVisual3D in MainViewport.Children.OfType<PixelsVisual3D>())
                    pixelsVisual3D.PixelSize = newPixelSize;
            }
            else
            {
                foreach (var sceneNodeVisual3D in MainViewport.Children.OfType<SceneNodeVisual3D>())
                {
                    var meshObjectNode = sceneNodeVisual3D.SceneNode as MeshObjectNode;
                    if (meshObjectNode != null)
                    {
                        var pixelMaterial = meshObjectNode.Materials[0] as PixelMaterial;
                        if (pixelMaterial != null)
                        {
                            pixelMaterial.PixelSize = newPixelSize;
                            meshObjectNode.NotifySceneNodeChange(SceneNode.SceneNodeDirtyFlags.MaterialChanged);
                        }
                    }
                }
            }


            _pixelEffect.PixelSize = newPixelSize;

            // Re-render the scene
            // Manually calling Refresh is needed in case ShowGeometryModel3D method was used to show pixels.
            // In this case we have only changed the _pixelEffect.PixelSize and this will not trigger automatic scene re-render.
            MainDXViewportView.Refresh();
        }

        private void OnSceneTypeChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded || DesignerProperties.GetIsInDesignMode(this))
                return;

            CreateScene();
        }

        public static Vector3[] CreatePositionsArray(Point3D center, Size3D size, int xCount, int yCount, int zCount)
        {
            var positionsArray = new Vector3[xCount * yCount * zCount];

            float xStep = xCount <= 1 ? 0 : (float)(size.X / (xCount - 1));
            float yStep = yCount <= 1 ? 0 : (float)(size.Y / (yCount - 1));
            float zStep = zCount <= 1 ? 0 : (float)(size.Z / (zCount - 1));

            float xStart = (float)center.X - ((float)size.X / 2.0f);
            float yStart = (float)center.Y - ((float)size.Y / 2.0f);
            float zStart = (float)center.Z - ((float)size.Z / 2.0f);

            int i = 0;
            for (int z = 0; z < zCount; z++)
            {
                float zPos = zStart + (z * zStep);

                for (int y = 0; y < yCount; y++)
                {
                    float yPos = yStart + (y * yStep);

                    for (int x = 0; x < xCount; x++)
                    {
                        float xPos = xStart + (x * xStep);

                        positionsArray[i] = new Vector3(xPos, yPos, zPos);
                        i++;
                    }
                }
            }

            return positionsArray;
        }
    }
}
