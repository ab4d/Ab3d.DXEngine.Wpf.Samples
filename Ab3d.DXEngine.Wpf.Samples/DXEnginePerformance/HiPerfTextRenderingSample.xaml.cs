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
using Ab3d.Common.Cameras;
using Ab3d.DirectX.Materials;
using Ab3d.Visuals;

namespace Ab3d.DXEngine.Wpf.Samples.DXEnginePerformance
{
    // TODO:
    // - Implement rendering text textures in multi-threaded way
    // - Merge all plane meshes into one mesh - so only one draw call is required

    /// <summary>
    /// Interaction logic for HiPerfTextRenderingSample.xaml
    /// </summary>
    public partial class HiPerfTextRenderingSample : Page
    {
        private class SampleObject
        {
            public Point3D Position;
            public GeometryModel3D Model3D;

            public string InfoText;

            public GeometryModel3D InfoPlaneModel3D;
            //public PlaneVisual3D InfoPlaneVisual3D;
            public WpfMaterial WpfMaterial;
        }

        private List<SampleObject> _sampleObjects;

        public HiPerfTextRenderingSample()
        {
            InitializeComponent();


            this.Loaded += delegate(object sender, RoutedEventArgs args)
            {
                CreateSampleObjects(15, 20); // Create 300 spheres with info texts

                Camera1.Refresh();
                UpdateInfoVisualsOrientation();
            };

            Camera1.CameraChanged += delegate(object sender, CameraChangedRoutedEventArgs args)
            {
                UpdateInfoVisualsOrientation();
            };

            MainViewport.SizeChanged += delegate(object sender, SizeChangedEventArgs args)
            {
                UpdateInfoVisualsOrientation();
            };
        }

        private void ChangeButton_OnClick(object sender, RoutedEventArgs e)
        {
            
        }

        private void CreateSampleObjects(int xCount, int yCount)
        {
            _sampleObjects = new  List<SampleObject>(xCount * yCount);

            var spheresGroup = new Model3DGroup();
            var infoPlanesGroup = new Model3DGroup();

            var diffuseMaterial = new DiffuseMaterial(Brushes.DeepSkyBlue);
            diffuseMaterial.Freeze();


            double radius = 5;
            double xMargin = 100;
            double yMargin = 80;

            double xStartPos = -xCount * 0.5 * xMargin;
            double yStartPos = -yCount * 0.5 * yMargin;


            // We will reuse the same MeshGeometry3D for spheres and for info planes

            var sphereMesh3D = new Ab3d.Meshes.SphereMesh3D(new Point3D(0, 0, 0), radius, 30).Geometry;
            sphereMesh3D.Freeze();

            // Because the size of all the planes in our sample will be the same, we can define the MeshGeometry3D with this size (50, 10).
            // But if the size would be different for each plane, then we should create the MeshGeometry3D with size set to (1, 1)
            // and then set the final size in the transformation matrix.
            // This is done with multiplying M11 in Matrix3D with ScaleX, M22 with ScaleY and M33 with ScaleZ.
            var planeMesh3D = new Ab3d.Meshes.PlaneMesh3D(centerPosition: new Point3D(0, 0, 0), 
                                                          planeNormal: new Vector3D(0, 0, 1), 
                                                          planeHeightDirection: new Vector3D(0, 1, 0),
                                                          size: new Size(50, 10), 
                                                          widthSegments: 1, 
                                                          heightSegments: 1).Geometry;
            planeMesh3D.Freeze();


            for (int x = 0; x < xCount; x++)
            {
                for (int y = 0; y < yCount; y++)
                {
                    var position = new Point3D(xStartPos + x * xMargin, 0, yStartPos + y * yMargin);


                    //var model3D = Ab3d.Models.Model3DFactory.CreateSphere(position, radius, segments: 20, material: diffuseMaterial);

                    var sphereModel3D = new GeometryModel3D(sphereMesh3D, diffuseMaterial);
                    sphereModel3D.Transform = new TranslateTransform3D(position.X, position.Y, position.Z);

                    var infoPlaneModel3D = new GeometryModel3D(planeMesh3D, diffuseMaterial);
                    infoPlaneModel3D.Transform = new MatrixTransform3D(Matrix3D.Identity); // Matrix will be updated in UpdateInfoVisualsOrientation method

                    //var planeVisual3D = new PlaneVisual3D()
                    //{
                    //    CenterPosition  = new Point3D(0, 0, 0), //position + infoPlaneOffset,
                    //    Size            = new Size(50, 10),
                    //    HeightDirection = new Vector3D(0, 1, 0),
                    //    Normal          = new Vector3D(0, 0, 1),
                    //    Material        = diffuseMaterial,
                    //    Transform       = new MatrixTransform3D(Matrix3D.Identity) // This will be updated when realigning with camera
                    //};

                    var sampleObject = new SampleObject()
                    {
                        Position = position,
                        Model3D  = sphereModel3D,
                        InfoText = string.Format("({0:0} {1:0} {2:0})", position.X, position.Y, position.Z),
                        InfoPlaneModel3D = infoPlaneModel3D
                    };

                    spheresGroup.Children.Add(sphereModel3D);
                    infoPlanesGroup.Children.Add(infoPlaneModel3D);

                    _sampleObjects.Add(sampleObject);
                }
            }


            var modelVisual3D = new ModelVisual3D();
            modelVisual3D.Content = spheresGroup;
            MainViewport.Children.Add(modelVisual3D);

            modelVisual3D = new ModelVisual3D();
            modelVisual3D.Content = infoPlanesGroup;
            MainViewport.Children.Add(modelVisual3D);
        }

        private void UpdateInfoVisualsOrientation()
        {
            Vector3D planeNormalVector3D, widthVector3D, heightVector3D;
            Camera1.GetCameraPlaneOrientation(out planeNormalVector3D, out widthVector3D, out heightVector3D);

            // In Matrix3D the first 3 rows define the basis vectors (new axes vectors) for the coordinate system defined by this matrix.
            // This means that if we get the orientation vectors from the camera, we can use them to create a Matrix3D that will orient the info planes.
            // The last row defines the translation and is set to zero here, but it will be set to actual position in the for loop below.
            var alignmentMatrix = new Matrix3D(widthVector3D.X, widthVector3D.Y, widthVector3D.Z, 0,
                                               heightVector3D.X, heightVector3D.Y, heightVector3D.Z, 0,
                                               planeNormalVector3D.X, planeNormalVector3D.Y, planeNormalVector3D.Z, 0,
                                               0, 0, 0, 1);

            double infoPlaneYOffset = 10;

            for (var i = 0; i < _sampleObjects.Count; i++)
            {
                var sampleObject = _sampleObjects[i];

                if (sampleObject.InfoPlaneModel3D != null)
                {
                    var matrixTransform3D = sampleObject.InfoPlaneModel3D.Transform as MatrixTransform3D;
                    if (matrixTransform3D != null)
                    {
                        // Use rotation part of the Matrix3D from the alignmentMatrix (upper left 3x3 matrix)
                        // and add individual translation part (last row).
                        // If we would also need to specify the size of the final plane, we would also need to multiplying M11 with ScaleX, M22 with ScaleY and M33 with ScaleZ (in this case the plane's mesh must be created with size set to (1, 1)
                        matrixTransform3D.Matrix = new Matrix3D(alignmentMatrix.M11, alignmentMatrix.M12, alignmentMatrix.M13, 0,
                                                                alignmentMatrix.M21, alignmentMatrix.M22, alignmentMatrix.M23, 0,
                                                                alignmentMatrix.M31, alignmentMatrix.M32, alignmentMatrix.M33, 0,
                                                                sampleObject.Position.X, sampleObject.Position.Y + infoPlaneYOffset, sampleObject.Position.Z, 1); 
                    }
                }
            }
        }



    }
}
