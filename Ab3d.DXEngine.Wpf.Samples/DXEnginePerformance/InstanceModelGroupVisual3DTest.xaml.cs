using System;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
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
            instancedModelGroupVisual3D.InstancesData = DXEnginePerformance.InstancedMeshGeometry3DTest.CreateInstancesData(center: new Point3D(0, 200, 0),
                                                                                                                            size: new Size3D(4000, 100, 6500),
                                                                                                                            modelScaleFactor: _modelScaleFactor,
                                                                                                                            xCount: 80,
                                                                                                                            yCount: 1,
                                                                                                                            zCount: 100,
                                                                                                                            useTransparency: false);

            // To allow hit testing (for example with EventManager3D), you need to manually enable it with the following:
            //instancedModelGroupVisual3D.IsWpfHitTestVisible = true;

            ObjectsPlaceholder.Children.Add(instancedModelGroupVisual3D);
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
