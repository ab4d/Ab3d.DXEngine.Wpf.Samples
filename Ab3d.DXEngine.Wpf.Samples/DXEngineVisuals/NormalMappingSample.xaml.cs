using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
using Ab3d.DirectX.Materials;
using Ab3d.Visuals;
using SharpDX;
using SharpDX.Direct3D11;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    /// <summary>
    /// Interaction logic for NormalMappingSample.xaml
    /// </summary>
    public partial class NormalMappingSample : Page
    {
        private MultiMapMaterial _multiMapMaterial;
        private ShaderResourceView _diffuseShaderResourceView;
        private ShaderResourceView _normalShaderResourceView;
        private ShaderResourceView _specularShaderResourceView;

        private DisposeList _disposables;

        public NormalMappingSample()
        {
            InitializeComponent();

            _disposables = new DisposeList();

            MainDXViewportView.DXSceneDeviceCreated += OnDXSceneDeviceCreated;

            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                _disposables.Dispose();
                MainDXViewportView.Dispose();
            };
        }

        private void OnDXSceneDeviceCreated(object sender, EventArgs eventArgs)
        {
            var d3dDevice = MainDXViewportView.DXScene.Device;

            string textureBaseFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\BricksMaps\");
            string diffuseTextureFilePath  = textureBaseFolder + "bricks.png";
            string normalTextureFilePath   = textureBaseFolder + "bricks_normal.png";
            string specularTextureFilePath = textureBaseFolder + "bricks_specular.png";

            TextureInfo textureInfo;
            _diffuseShaderResourceView  = Ab3d.DirectX.TextureLoader.LoadShaderResourceView(d3dDevice, diffuseTextureFilePath, out textureInfo);
            _normalShaderResourceView   = Ab3d.DirectX.TextureLoader.LoadShaderResourceView(d3dDevice, normalTextureFilePath, loadDdsIfPresent: false, convertTo32bppPRGBA: false);
            _specularShaderResourceView = Ab3d.DirectX.TextureLoader.LoadShaderResourceView(d3dDevice, specularTextureFilePath, loadDdsIfPresent: false, convertTo32bppPRGBA: false);

            _disposables.Add(_diffuseShaderResourceView);
            _disposables.Add(_normalShaderResourceView);
            _disposables.Add(_specularShaderResourceView);


            _multiMapMaterial = new MultiMapMaterial();

            _disposables.Add(_multiMapMaterial);

            // When using Diffuse texture, the DiffuseColor is used as color mask - colors from diffuse texture are multiplied with DiffuseColor
            _multiMapMaterial.DiffuseColor = Colors.White.ToColor3();

            // Set specular power and specular color mask
            _multiMapMaterial.SpecularPower = 64;
            _multiMapMaterial.SpecularColor = Colors.White.ToColor3();

            _multiMapMaterial.HasTransparency = textureInfo.HasTransparency;

            // Get recommended BlendState based on HasTransparency and HasPreMultipliedAlpha values.
            // Possible values are: CommonStates.Opaque, CommonStates.PremultipliedAlphaBlend or CommonStates.NonPremultipliedAlphaBlend.
            _multiMapMaterial.BlendState = MainDXViewportView.DXScene.DXDevice.CommonStates.GetRecommendedBlendState(textureInfo.HasTransparency, textureInfo.HasPremultipliedAlpha);

            // We could manually set texture maps, but this will be done in the UpdateSelectedMaps method that is called below
            //_multiMapMaterial.TextureMaps.Add(new TextureMapInfo(TextureMapTypes.DiffuseColor, _diffuseShaderResourceView, null, diffuseTextureFilePath));
            //_multiMapMaterial.TextureMaps.Add(new TextureMapInfo(TextureMapTypes.NormalMap, _normalShaderResourceView, null, normalTextureFilePath));
            //_multiMapMaterial.TextureMaps.Add(new TextureMapInfo(TextureMapTypes.SpecularColor, _specularShaderResourceView, null, specularTextureFilePath));


            UpdateSelectedMaps();


            // MultiMapMaterial also supports rendering environment map.
            // The following commented code shows how to specify it:

            //string packUriPrefix = string.Format("pack://application:,,,/{0};component/Resources/SkyboxTextures/", this.GetType().Assembly.GetName().Name);

            //// Create DXCubeMap with specifying 6 bitmaps for all sides of the cube
            //var dxCubeMap = new DXCubeMap(packUriPrefix,
            //    "CloudyLightRaysRight512.png",
            //    "CloudyLightRaysLeft512.png",
            //    "CloudyLightRaysUp512.png",
            //    "CloudyLightRaysDown512.png",
            //    "CloudyLightRaysFront512.png",
            //    "CloudyLightRaysBack512.png",
            //    MainDXViewportView.DXScene.DXDevice);

            //// To show the environment map correctly for our bitmaps we need to flip bottom bitmap horizontally and vertically
            //dxCubeMap.FlipBitmaps(flipRightBitmapType: DXCubeMap.FlipBitmapType.None,
            //    flipLeftBitmapType: DXCubeMap.FlipBitmapType.None,
            //    flipUpBitmapType: DXCubeMap.FlipBitmapType.None,
            //    flipDownBitmapType: DXCubeMap.FlipBitmapType.FlipXY,
            //    flipFrontBitmapType: DXCubeMap.FlipBitmapType.None,
            //    flipBackBitmapType: DXCubeMap.FlipBitmapType.None);

            //_disposables.Add(dxCubeMap);

            //multiMapMaterial.TextureMaps.Add(new TextureMapInfo(TextureMapTypes.ReflectionMap, specularShaderResourceView, null));
            //multiMapMaterial.TextureMaps.Add(new TextureMapInfo(TextureMapTypes.EnvironmentCubeMap, dxCubeMap.ShaderResourceView, null));


            // Use SetUsedDXMaterial to specify _multiMapMaterial to be used instead of the WPF material specified for the Plane1
            Plane1.Material.SetUsedDXMaterial(_multiMapMaterial);


            // Rendering normal (bump) maps require tangent vectors.
            // The following code will generate tangent vectors and assign them to the MeshGeometry3D that form our 3D model.
            // If tangent vectors are not provided, they will be calculated on-demand in the pixel shader (slightly reducing performance).

            Ab3d.Utilities.ModelIterator.IterateGeometryModel3DObjects(Plane1, delegate (GeometryModel3D geometryModel3D, Transform3D transform3D)
            {
                // This code is called for each GeometryModel3D inside Plane1
                var tangentVectors = Ab3d.DirectX.Utilities.MeshUtils.CalculateTangentVectors((MeshGeometry3D)geometryModel3D.Geometry);

                // Assign tangent array to the MeshGeometry3D
                geometryModel3D.Geometry.SetDXAttribute(DXAttributeType.MeshTangentArray, tangentVectors);
            });
        }


        private void UpdateSelectedMaps()
        {
            if (!this.IsLoaded)
                return;

            bool isChanged  = UpdateMap(_multiMapMaterial, (DiffuseMapCheckBox.IsChecked ?? false),  TextureMapTypes.DiffuseColor, _diffuseShaderResourceView);
                 isChanged |= UpdateMap(_multiMapMaterial, (NormalMapCheckBox.IsChecked ?? false),   TextureMapTypes.NormalMap, _normalShaderResourceView);
                 isChanged |= UpdateMap(_multiMapMaterial, (SpecularMapCheckBox.IsChecked ?? false), TextureMapTypes.SpecularColor, _specularShaderResourceView);

            if (isChanged)
            {
                // When we change DXEngine's material, we need to notify the SceneNode that is using the material about the change.
                var plane1SceneNode = MainDXViewportView.GetSceneNodeForWpfObject(Plane1);
                if (plane1SceneNode != null)
                    plane1SceneNode.NotifySceneNodeChange(SceneNode.SceneNodeDirtyFlags.MaterialChanged);
            }
        }

        private void OnMapCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            UpdateSelectedMaps();
        }


        private bool UpdateMap(MultiMapMaterial multiMapMaterial, bool isMapUsed, TextureMapTypes mapType, ShaderResourceView shaderResourceView)
        {
            bool isChanged;

            if (isMapUsed)
                isChanged = AddMapIfNotPresent(multiMapMaterial, mapType, shaderResourceView);
            else
                isChanged = RemoveMap(multiMapMaterial, mapType);

            return isChanged;
        }


        private bool RemoveMap(MultiMapMaterial multiMapMaterial, TextureMapTypes mapType)
        {
            bool isChanged = false;

            if (multiMapMaterial != null)
            {
                for (var i = multiMapMaterial.TextureMaps.Count - 1; i >= 0; i--)
                {
                    if (multiMapMaterial.TextureMaps[i].MapType == mapType)
                    {
                        multiMapMaterial.TextureMaps.RemoveAt(i);
                        isChanged = true;
                    }
                }
            }

            return isChanged;
        }

        private bool AddMapIfNotPresent(MultiMapMaterial multiMapMaterial, TextureMapTypes mapType, ShaderResourceView shaderResourceView)
        {
            bool isChanged = false;

            if (multiMapMaterial != null && multiMapMaterial.TextureMaps.All(m => m.MapType != mapType)) // If this map type is not yet added, then add it now
            {
                multiMapMaterial.TextureMaps.Add(new TextureMapInfo(mapType, shaderResourceView));
                isChanged = true;
            }

            return isChanged;
        }
    }
}
