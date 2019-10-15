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
using Ab3d.Common.EventManager3D;
using Ab3d.DirectX;
using Ab3d.Visuals;

namespace Ab3d.DXEngine.Wpf.Samples.DXEnginePerformance
{
    /// <summary>
    /// Interaction logic for InstanceModelGroupVisual3DTest.xaml
    /// </summary>
    public partial class InstanceModelGroupVisual3DTest : Page
    {
        private float _modelScaleFactor = 1.0f;

        public InstanceModelGroupVisual3DTest()
        {
            InitializeComponent();

            // You may also try to use HighSpeedNormalQualityHardwareRendering graphics profile (with uncommenting the following line)
            // This will use a per-vertex lighting calculations (lighting colors are calculated for each vertex and then interpolated to the pixels; in per-pixel rendering the calculations are done for each pixel)
            // and still preserve the 4-times antialiasing and anisotropic texture sampling.
            // This might give improved performance especially on slower GPUs.
            // Even faster rendering would be with using LowQualityHardwareRendering.
            //
            // MainDXViewportView.GraphicsProfiles = new GraphicsProfile[] { GraphicsProfile.HighSpeedNormalQualityHardwareRendering };

            CreateModelGroupInstances();

            // IMPORTANT:
            // It is very important to call Dispose method on DXSceneView after the control is not used any more (see help file for more info)
            this.Unloaded += (sender, args) => MainDXViewportView.Dispose();
        }

        private void CreateModelGroupInstances()
        {
            // PersonModel and HouseWithTreesModel are defined in App.xaml
            var readModel = this.FindResource("HouseWithTreesModel") as Model3D;

            // We could also load some other model:
            //var readerObj = new Ab3d.ReaderObj();
            //var readModel = readerObj.ReadModel3D(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources\\Models\\robotarm.obj"));


            double modelSize = Math.Sqrt(readModel.Bounds.SizeX * readModel.Bounds.SizeX +
                                         readModel.Bounds.SizeY * readModel.Bounds.SizeY +
                                         readModel.Bounds.SizeZ * readModel.Bounds.SizeZ);

            _modelScaleFactor = 80.0f / (float)modelSize;


            // It is highly recommended to optimize the read model. 
            // This can improve performance with combing the geometry with the same material.
            // It also transforms all positions - Model3D with Transformations are not supported.
            // With using OptimizeAll the read model is:
            // - flattened (hierarchy is removed), 
            // - all the positions are transformed and Transform property is set to null
            // - all GeometryModel3D objects with the same material are combined
            // - all GeometryModel3D are forzen
            readModel = Ab3d.Utilities.ModelOptimizer.OptimizeAll(readModel);


            var model3DGroup = readModel as Model3DGroup;

            if (model3DGroup == null)
                return; // In this case we could use simple InstancedGeometryVisual3D

            var instancedModelGroupVisual3D = new InstancedModelGroupVisual3D(model3DGroup);
            instancedModelGroupVisual3D.InstancesData = CreateInstancesData(center: new Point3D(0, 200, 0),
                                                                            size: new Size3D(4000, 100, 6500),
                                                                            modelScaleFactor: _modelScaleFactor,
                                                                            xCount: 80,
                                                                            yCount: 1,
                                                                            zCount: 100);

            // To allow hit testing (for example with EventManager3D), you need to manually enable it with the following:
            //instancedModelGroupVisual3D.IsWpfHitTestVisible = true;

            ObjectsPlaceholder.Children.Add(instancedModelGroupVisual3D);
        }

        private InstanceData[] CreateInstancesData(Point3D center, Size3D size, float modelScaleFactor, int xCount, int yCount, int zCount)
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

                        i++;
                    }
                }
            }

            return instancedData;
        }

        private Model3D LoadObjFile(string fileName)
        {
            if (!System.IO.Path.IsPathRooted(fileName))
                fileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);

            var readerObj = new Ab3d.ReaderObj();
            var readModel = readerObj.ReadModel3D(fileName);

            return readModel;
        }
    }
}
