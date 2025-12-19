using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using Ab3d.DirectX;
using Ab3d.Visuals;

#if SHARPDX
using SharpDX;
#endif

namespace Ab3d.DXEngine.Wpf.Samples.DXEnginePerformance
{
    /// <summary>
    /// Interaction logic for InstancedMeshGeometry3DTest.xaml
    /// </summary>
    public partial class InstancedMeshGeometry3DTest : Page
    {
        private int _hiddenInstancesStartIndex;
        private int _hiddenInstancesCount;

        private InstancedMeshGeometryVisual3D _instancedMeshGeometryVisual3D;

        public InstancedMeshGeometry3DTest()
        {
            InitializeComponent();

            // You may also try to use HighSpeedNormalQualityHardwareRendering graphics profile (with uncommenting the following line)
            // This will use a per-vertex lighting calculations (lighting colors are calculated for each vertex and then interpolated to the pixels; in per-pixel rendering the calculations are done for each pixel)
            // and still preserve the 4-times antialiasing and anisotropic texture sampling.
            // This might give improved performance especially on slower GPUs.
            // Even faster rendering would be with using LowQualityHardwareRendering.
            //
            // MainDXViewportView.GraphicsProfiles = new GraphicsProfile[] { GraphicsProfile.LowQualityHardwareRendering };

            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            CreateInstances();


            // To render instanced objects by using wireframe rendering, uncomment the following code:
            //MainDXViewportView.DXSceneInitialized += delegate (object sender, EventArgs args)
            //{
            //    // Create a new RenderObjectsRenderingStep that will render only wireframe instanced objects.
            //    //
            //    // If you are not rendering any other objects or want to render all objects as wireframe, then you do not need to add this new RenderObjectsRenderingStep
            //    // and can just set the OverrideRasterizerState on the DefaultRenderObjectsRenderingStep.

            //    var renderWireframeInstancesRenderingStep = new RenderObjectsRenderingStep("RenderWireframeInstances");

            //    // Override RasterizerState to render objects are wireframe instead of solid objects (not that line thickness is always 1 in this case)
            //    renderWireframeInstancesRenderingStep.OverrideRasterizerState = MainDXViewportView.DXScene.DXDevice.CommonStates.WireframeMultisampleCullNone;

            //    // Render only objects in the ComplexGeometryRenderingQueue (instanced objects are always put into this rendering queue).
            //    // Note that if you use multiple instanced objects or some other complex objects with many positions or lines
            //    // (defined by DXScene.MeshTriangleIndicesCountRequiredForComplexGeometry and DXScene.LinesCountRequiredForComplexGeometry -
            //    // you can also increase those two numbers to prevent putting other objects into ComplexGeometryRenderingQueue)
            //    // then you will need to create another RenderObjectsRenderingStep and also set the FilterObjectsFunction (one will show only instances with wireframe and the other other complex objects).
            //    renderWireframeInstancesRenderingStep.FilterRenderingQueuesFunction = queue => queue == MainDXViewportView.DXScene.ComplexGeometryRenderingQueue;

            //    MainDXViewportView.DXScene.RenderingSteps.AddBefore(MainDXViewportView.DXScene.DefaultRenderObjectsRenderingStep, renderWireframeInstancesRenderingStep);


            //    // Update the DefaultRenderObjectsRenderingStep to prevent rendering ComplexGeometryRenderingQueue
            //    // This will render other objects normally.
            //    MainDXViewportView.DXScene.DefaultRenderObjectsRenderingStep.FilterRenderingQueuesFunction = queue => queue != MainDXViewportView.DXScene.ComplexGeometryRenderingQueue;


            //    // To render wireframe with solid color (without shading the lines) also set the "_instancedMeshGeometryVisual3D.IsSolidColorMaterial = true;" in the code below.
            //};


            // IMPORTANT:
            // It is very important to call Dispose method on DXSceneView after the control is not used any more (see help file for more info)
            this.Unloaded += (sender, args) => MainDXViewportView.Dispose();
        }

        private void CreateInstances()
        {
            MeshGeometry3D meshGeometry3D;
            double modelScaleFactor;

            if (MeshTypeComboBox.SelectedIndex == 2) // Bunnies
            {
                // Load standard Stanford Bunny model (res3) with 11533 position
                meshGeometry3D = LoadMeshFromObjFile("bun_zipper_res3.obj");

                var bounds = meshGeometry3D.Bounds;

                double diagonalSize = Math.Sqrt(bounds.SizeX * bounds.SizeX + bounds.SizeY * bounds.SizeY + bounds.SizeZ * bounds.SizeZ);
                modelScaleFactor = 20 / diagonalSize; // Scale model so that its diagonal is 20 units big
            }
            else if (MeshTypeComboBox.SelectedIndex == 1) // Spheres
            {
                // Sphere with 382
                meshGeometry3D = new Ab3d.Meshes.SphereMesh3D(centerPosition: new Point3D(0, 0, 0), radius: 5, segments: 20, generateTextureCoordinates: false).Geometry;
                modelScaleFactor = 1;
            }
            else
            {
                // Box with 24 positions (for each corner we need 3 positions to create sharp edges - we need 3 normal vectors for each edge)
                meshGeometry3D = new Ab3d.Meshes.BoxMesh3D(centerPosition: new Point3D(0, 0, 0), size: new Size3D(10, 10, 10), xSegments: 1, ySegments: 1, zSegments: 1).Geometry;
                modelScaleFactor = 1;
            }

            int selectedInstancesYCount = GetSelectedInstancesYCount();


            bool useTransparency = UseTransparencyCheckBox.IsChecked ?? false;

            // The following method prepare InstanceData array with data for each instance (WorldMatrix and Color)
            InstanceData[] instancedData = CreateInstancesData(new Point3D(0, 200, 0), new Size3D(400, 400, 400), (float)modelScaleFactor, 20, selectedInstancesYCount, 20, useTransparency);

            if (_hiddenInstancesCount > 0)
            {
                _hiddenInstancesCount = 0;
                ShowHideInstancesButton.Content = "Hide some instances";
            }


            // Create InstancedGeometryVisual3D with selected meshGeometry and InstancesData
            _instancedMeshGeometryVisual3D = new InstancedMeshGeometryVisual3D(meshGeometry3D);
            _instancedMeshGeometryVisual3D.InstancesData = instancedData;

            // When we use transparency, we also need to set UseAlphaBlend to true
            _instancedMeshGeometryVisual3D.UseAlphaBlend = useTransparency;

            //_instancedMeshGeometryVisual3D.IsSolidColorMaterial = true; // uncommenting this line will render the objects with solid color (without any shading based on lighting and camera position).

            // If we would only change the InstancedData we would need to call Update method (but here this is not needed because we have set the data for the first time)
            //_instancedGeometryVisual3D.Update();


            ObjectsPlaceholder.Children.Clear();
            ObjectsPlaceholder.Children.Add(_instancedMeshGeometryVisual3D);


            // Update statistics:
            //PositionsPerMeshTextBlock.Text = string.Format("Positions per mesh: {0:#,##0}", meshGeometry3D.Positions.Count);
            TotalTextBlock.Text = string.Format("Total positions: {0:#,##0} * {1:#,##0} = {2:#,##0}", meshGeometry3D.Positions.Count, 20 * 20 * selectedInstancesYCount, meshGeometry3D.Positions.Count * selectedInstancesYCount * 20 * 20);
        }

        private int GetSelectedInstancesYCount()
        {
            int yInstancesCount;

            switch (InstancesCountComboBox.SelectedIndex)
            {
                case 0:
                    yInstancesCount = 4;
                    break;

                case 1:
                    yInstancesCount = 10;
                    break;

                case 2:
                    yInstancesCount = 20;
                    break;

                case 3:
                    yInstancesCount = 40;
                    break;

                case 4:
                    yInstancesCount = 2000;
                    break;

                default:
                    yInstancesCount = 1;
                    break;
            }

            return yInstancesCount;
        }


        public static InstanceData[] CreateInstancesData(Point3D center, Size3D size, float modelScaleFactor, int xCount, int yCount, int zCount, bool useTransparency)
        {
            var instancedData = new InstanceData[xCount * yCount * zCount];

            float xStep = xCount <= 1 ? 0 : (float)(size.X / (xCount - 1));
            float yStep = yCount <= 1 ? 0 : (float)(size.Y / (yCount - 1));
            float zStep = zCount <= 1 ? 0 : (float)(size.Z / (zCount - 1));

            int i = 0;
            for (int z = 0; z < zCount; z++)
            {
                float zPos = (float)(center.Z - (size.Z / 2.0) + (z * zStep));

                for (int y = 0; y < yCount; y++)
                {
                    float yPos = (float)(center.Y - (size.Y / 2.0) + (y * yStep));

                    float yPercent = (float)y / (float)yCount;

                    for (int x = 0; x < xCount; x++)
                    {
                        float xPos = (float)(center.X - (size.X / 2.0) + (x * xStep));

                        instancedData[i].World = new Matrix(modelScaleFactor, 0, 0, 0,
                                                                    0, modelScaleFactor, 0, 0,
                                                                    0, 0, modelScaleFactor, 0,
                                                                    xPos, yPos, zPos, 1);

                        if (useTransparency)
                        {
                            // When we use transparency, we set alpha color to 0.2 (we also need to set InstancedMeshGeometryVisual3D.UseAlphaBlend to true)
                            instancedData[i].DiffuseColor = new Color4(1.0f, 1.0f, 1.0f, 1.0f - yPercent); // White with variable transparency - top objects fully transparent, bottom objects solid
                        }
                        else
                        {
                            // Start with yellow and move to white (multiplied by 1.4 so that white color appear before the top)
                            instancedData[i].DiffuseColor = new Color4(red: 1.0f,
                                                                               green: 1.0f,
                                                                               blue: yPercent * 1.4f,
                                                                               alpha: 1.0f);

                            //instancedData[i].DiffuseColor = new Color4(red: 0.3f + ((float)x / (float)xCount) * 0.7f, 
                            //                                                   green: 0.3f + yPercent * 0.7f, 
                            //                                                   blue: 0.3f + yPercent * 0.7f, 
                            //                                                   alpha: 1.0f);

                            // Use WPF's Orange color:
                            //instancedData[i].Color = Colors.Orange.ToColor4();
                        }

                        i++;
                    }
                }
            }

            return instancedData;
        }


        private MeshGeometry3D LoadMeshFromObjFile(string fileName)
        {
            if (!System.IO.Path.IsPathRooted(fileName))
                fileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources\\Models\\" + fileName);

            var readerObj = new Ab3d.ReaderObj();
            var readModel3D = readerObj.ReadModel3D(fileName) as GeometryModel3D;

            if (readModel3D == null)
                return null;

            return readModel3D.Geometry as MeshGeometry3D;
        }

        private void OnSettingsChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded || DesignerProperties.GetIsInDesignMode(this))
                return;

            CreateInstances();
        }

        private void UseTransparencyCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded || DesignerProperties.GetIsInDesignMode(this))
                return;

            CreateInstances();
        }
        
        private void ShowHideInstancesButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_hiddenInstancesCount > 0)
            {
                // Show hidden instances
                ChangeAlphaColor(_hiddenInstancesStartIndex, _hiddenInstancesCount, newAlpha: 1);

                _hiddenInstancesCount = 0;
                ShowHideInstancesButton.Content = "Hide some instances";
            }
            else
            {
                // Hide instances
                // To quickly discard some instances set their color's alpha value to 0.
                // Then call _instancedMeshGeometryVisual3D.Update method.
                
                int instancesCount = _instancedMeshGeometryVisual3D.InstancesData.Length;

                _hiddenInstancesCount = (int)(instancesCount * 0.25); // Hide 1/4 of instances

                var rnd = new Random();
                _hiddenInstancesStartIndex = rnd.Next(instancesCount - _hiddenInstancesCount - 1);

                ChangeAlphaColor(_hiddenInstancesStartIndex, _hiddenInstancesCount, newAlpha: 0);

                ShowHideInstancesButton.Content = "Show hidden instances";
            }
        }

        private void ChangeAlphaColor(int startIndex, int count, float newAlpha)
        {
            var instancedData = _instancedMeshGeometryVisual3D.InstancesData;

            int endIndex = startIndex + count;
            for (int i = startIndex; i < endIndex; i++)
            {
                var color = instancedData[i].DiffuseColor;
                instancedData[i].DiffuseColor = new Color4(color.Red, color.Green, color.Blue, newAlpha);
            }

            // When only some instances data are changed, then provide startIndex and count to Update method.
            _instancedMeshGeometryVisual3D.Update(startIndex, count, updateBounds: false); // If actual bounds are slightly smaller, then this is not a problem, so preserve the bounds
        }
    }
}