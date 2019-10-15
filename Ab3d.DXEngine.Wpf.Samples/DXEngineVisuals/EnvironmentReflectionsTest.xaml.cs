using System;
using System.Collections.Generic;
using System.IO;
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
using Ab3d.DirectX;
using Ab3d.DirectX.Common;
using Ab3d.DirectX.Controls;
using Ab3d.DirectX.Effects;
using Ab3d.DirectX.Materials;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    /// <summary>
    /// Interaction logic for EnvironmentReflectionsTest.xaml
    /// </summary>
    public partial class EnvironmentReflectionsTest : Page
    {
        private DXCubeMap _dxCubeMap;

        public EnvironmentReflectionsTest()
        {
            InitializeComponent();

            string packUriPrefix = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources\\SkyboxTextures\\");
            
            //string packUriPrefix = "pack://siteoforigin:,,,/Resources/SkyboxTextures/"; // This throws "InvalidDeployment" exception (internally caught by .Net)
            //string packUriPrefix = string.Format("pack://application:,,,/{0};component/Resources/SkyboxTextures/", this.GetType().Assembly.GetName().Name); // When textures are defined as Resources

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
                                   flipLeftBitmapType:  DXCubeMap.FlipBitmapType.None, 
                                   flipUpBitmapType:    DXCubeMap.FlipBitmapType.None, 
                                   flipDownBitmapType:  DXCubeMap.FlipBitmapType.FlipXY, 
                                   flipFrontBitmapType: DXCubeMap.FlipBitmapType.None, 
                                   flipBackBitmapType:  DXCubeMap.FlipBitmapType.None);

            // You can also create DXCubeMap from a dds file that already contains all 6 textures
            // For example a dds file with cube map can be created with:
            // - NVIDIA's Photoshop plugin for saving DDS files and cubemaps
            // - DirectX Texture Tool (comes with June 2010 DirectX SDK)
            // - Terragen (http://planetside.co.uk/products/tg3-product-comparison)
            //
            // The following code shows how to create cube map from dds file (this is used in the ReflectionMapSample):
            // 
            //_dxCubeMap = new DXCubeMap(AppDomain.CurrentDomain.BaseDirectory + @"Resources\SkyboxTextures\sunsetcube1024.dds");


            // This sample is showing 4 teapot models. We set different reflectionFactors to each of them.
            // Front left Teapot:
            // full reflection (reflectionFactor = 1.0f)
            SetEnvironmentMap(TeapotVisual1.Content, _dxCubeMap, reflectionFactor: 1.0f);

            // Front right Teapot:
            // half reflection and half diffuse color (reflectionFactor = 0.5f)
            SetEnvironmentMap(TeapotVisual2.Content, _dxCubeMap, reflectionFactor: 0.5f);

            // Back left Teapot:
            // full reflection on blue color and half reflection of green and blue (reflectionFactor = new SharpDX.Color3(0.5f, 0.5f, 1.0f))
            SetEnvironmentMap(TeapotVisual3.Content, _dxCubeMap, reflectionFactor: new SharpDX.Color3(0.5f, 0.5f, 1.0f));

            // Back right Teapot:
            // no reflection and full diffuse color (reflectionFactor = 0.0f)
            SetEnvironmentMap(TeapotVisual4.Content, _dxCubeMap, reflectionFactor: 0.0f);


            this.Unloaded += delegate(object sender, RoutedEventArgs args)
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

        // reflectionFactor type can be:
        // float, double and SharpDX.Color3
        // When Color3 is used, then it is specifies the reflection factor for each color component
        private void SetEnvironmentMap(Model3D rootModel3D, DXCubeMap dxCubeMap, object reflectionFactor)
        {
            // We are using ModelIterator to change material on every GeometryModel3D inside the rootModel3D.
            Ab3d.Utilities.ModelIterator.IterateGeometryModel3DObjects(rootModel3D, parentTransform3D: null, callback: delegate (GeometryModel3D model3D, Transform3D transform3D)
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
                usedMaterial.SetDXAttribute(DXAttributeType.Material_EnvironmentMap, dxCubeMap);

                // Material_ReflectionFactor attribute specifies how much environmental map (reflection) the material will show:
                // 1.0 specifies full reflection
                // 0.5 specifies half reflection and half diffuse color
                // 0.0 specifies no reflection and full diffuse color
                //
                // Allowed value types are:
                // float, double and SharpDX.Color3
                // When Color3 is used, then it is specifies the reflection factor for each color component
                // 
                usedMaterial.SetDXAttribute(DXAttributeType.Material_ReflectionFactor, reflectionFactor);
            });
        }
    }
}
