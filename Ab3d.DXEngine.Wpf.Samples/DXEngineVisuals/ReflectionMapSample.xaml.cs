using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using Ab3d.DirectX;
using Ab3d.DirectX.Materials;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    /// <summary>
    /// Interaction logic for ReflectionMapSample.xaml
    /// </summary>
    public partial class ReflectionMapSample : Page
    {
        private DXCubeMap _dxCubeMap;
        private Model3D _teapotModel;

        public ReflectionMapSample()
        {
            InitializeComponent();

            //Ab3d.DirectX.DXDiagnostics.CreateDebugDirectXDevice = true;

            MainDXViewportView.DXSceneInitialized += delegate (object sender, EventArgs args)
            {
                if (MainDXViewportView.UsedGraphicsProfile.DriverType == GraphicsProfile.DriverTypes.Wpf3D)
                    return; // WPF 3D rendering

                LoadTeapot();
                SetupCubeMapMaterial();
            };

            this.Unloaded += delegate (object sender, RoutedEventArgs args)
            {
                // We need to dispose all the DirectX resources created here:
                if (_dxCubeMap != null)
                {
                    _dxCubeMap.Dispose();
                    _dxCubeMap = null;
                }

                // Then dispose the MainDXViewportView
                MainDXViewportView.Dispose();
            };
        }

        private void LoadTeapot()
        {
            var readerObj = new ReaderObj();
            var fileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources/Models/Teapot with material.obj");
            
            _teapotModel = readerObj.ReadModel3D(fileName);

            Ab3d.Utilities.ModelUtils.CenterAndScaleModel3D(_teapotModel, centerPosition: new Point3D(0, 0, 0), finalSize: new Size3D(80, 80, 80));

            MainViewport.Children.Add(_teapotModel.CreateModelVisual3D());
        }

        private void SetupCubeMapMaterial()
        {
            if (_teapotModel == null)
                return;


            // We create the CubeMap from 6 bitmap images
            string packUriPrefix = string.Format("pack://application:,,,/{0};component/Resources/SkyboxTextures/", this.GetType().Assembly.GetName().Name);

            // Create DXCubeMap with specifying 6 bitmaps for all sides of the cube
            _dxCubeMap = new DXCubeMap(packUriPrefix,
                                       "CloudyLightRaysRight512.png",
                                       "CloudyLightRaysLeft512.png",
                                       "CloudyLightRaysUp512.png",
                                       "CloudyLightRaysDown512.png",
                                       "CloudyLightRaysFront512.png",
                                       "CloudyLightRaysBack512.png");

            // To show the environment map correctly for our bitmaps we need to flip bottom bitmap horizontally and vertically
            _dxCubeMap.FlipBitmaps(flipRightBitmapType: DXCubeMap.FlipBitmapType.None,
                flipLeftBitmapType: DXCubeMap.FlipBitmapType.None,
                flipUpBitmapType: DXCubeMap.FlipBitmapType.None,
                flipDownBitmapType: DXCubeMap.FlipBitmapType.FlipXY,
                flipFrontBitmapType: DXCubeMap.FlipBitmapType.None,
                flipBackBitmapType: DXCubeMap.FlipBitmapType.None);


            // We are using ModelIterator to change material on every GeometryModel3D inside the rootModel3D.
            Ab3d.Utilities.ModelIterator.IterateGeometryModel3DObjects(_teapotModel, parentTransform3D: null, callback: delegate (GeometryModel3D model3D, Transform3D transform3D)
            {
                // This code is called for each GeometryModel3D inside rootModel3D:

                System.Windows.Media.Media3D.Material usedMaterial;

                // Check i fthe material is frozen (this prevents any changes on the material)
                // In this case clone the material into non-frozen version.
                // This will allow adding additional DXEngine's attributes to the WPF material - in the lines below this if
                if (model3D.Material.IsFrozen)
                {
                    usedMaterial = model3D.Material.Clone(); // Clone to non-forzen object ...
                    model3D.Material = usedMaterial; // ... and replace the frozen material with the clone
                }
                else
                {
                    usedMaterial = model3D.Material;
                }

                // Existing WPF materials do not support EnvironmentMaps.
                // Therefore DXEngine support adding additional attributes to the WPF's materials.
                // To add Environmental map to the existing material we call SetDXAttribute and pass Material_EnvironmentMap as attribute type and DXCubeMap as parameter (we could also pass ShaderResourceView).
                // Because this adds a DependencyObject to the material, the material must not be frozen (therefore we have the code in the previous lines).
                usedMaterial.SetDXAttribute(DXAttributeType.Material_EnvironmentMap, _dxCubeMap);

                // Instead of specifying a single ReflectionFactor, we specify a ReflectionMap that is a 2D texture
                // that for each pixel in the texture specifies its Reflection Factor
                //usedMaterial.SetDXAttribute(DXAttributeType.Material_ReflectionFactor, 0.9f);

                var bitmapImage = new BitmapImage(new Uri(AppDomain.CurrentDomain.BaseDirectory + @"Resources\Models\teapot-reflection.png"));
                usedMaterial.SetDXAttribute(DXAttributeType. Material_ReflectionMap, bitmapImage);
            });
        }
    }
}
