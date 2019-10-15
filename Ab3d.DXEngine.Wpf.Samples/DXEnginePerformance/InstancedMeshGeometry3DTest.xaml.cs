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
using Ab3d.Visuals;
using SharpDX;
using Ab3d.DirectX;
using Ab3d.Meshes;

namespace Ab3d.DXEngine.Wpf.Samples.DXEnginePerformance
{
    /// <summary>
    /// Interaction logic for InstancedMeshGeometry3DTest.xaml
    /// </summary>
    public partial class InstancedMeshGeometry3DTest : Page
    {
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
                // Load standard Standfor Bunny model (res3) with 11533 position
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
            float alphaColor = useTransparency ? 0.3f : 1.0f; // When we use transparency, we set alpha color to 0.3 (we also need to set UseAlphaBlend to true - see below)

            // The following method prepare InstanceData array with data for each instance (WorldMatrix and Color)
            InstanceData[] instancedData = CreateInstancesData(new Point3D(0, 200, 0), new Size3D(400, 400, 400), (float)modelScaleFactor, 20, selectedInstancesYCount, 20, useTransparency);


            // Create InstancedGeometryVisual3D with selected meshGeometry and InstancesData
            var instancedMeshGeometryVisual3D = new InstancedMeshGeometryVisual3D(meshGeometry3D);
            instancedMeshGeometryVisual3D.InstancesData = instancedData;

            // When we use transparency, we also need to set UseAlphaBlend to true
            instancedMeshGeometryVisual3D.UseAlphaBlend = useTransparency;

            // If we would only change the InstancedData we would need to call Update method (but here this is not needed because we have set the data for the first time)
            //_instancedGeometryVisual3D.Update();


            ObjectsPlaceholder.Children.Clear();
            ObjectsPlaceholder.Children.Add(instancedMeshGeometryVisual3D);


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

            float xStep = (float)(size.X / xCount);
            float yStep = (float)(size.Y / yCount);
            float zStep = (float)(size.Z / zCount);

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

                        instancedData[i].World = new SharpDX.Matrix(modelScaleFactor, 0, 0, 0,
                                                                    0, modelScaleFactor, 0, 0,
                                                                    0, 0, modelScaleFactor, 0,
                                                                    xPos, yPos, zPos, 1);

                        if (useTransparency)
                        {
                            // When we use transparency, we set alpha color to 0.2 (we also need to set InstancedMeshGeometryVisual3D.UseAlphaBlend to true)
                            instancedData[i].DiffuseColor = new SharpDX.Color4(1.0f, 1.0f, 1.0f, 1.0f - yPercent); // White with variable transparency - top objects fully transparent, bottom objects solid
                        }
                        else
                        {
                            // Start with yellow and move to white (multiplied by 1.4 so that white color appear before the top)
                            instancedData[i].DiffuseColor = new SharpDX.Color4(red: 1.0f,
                                                                               green: 1.0f,
                                                                               blue: yPercent * 1.4f,
                                                                               alpha: 1.0f);

                            //instancedData[i].DiffuseColor = new SharpDX.Color4(red: 0.3f + ((float)x / (float)xCount) * 0.7f, 
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
    }
}
