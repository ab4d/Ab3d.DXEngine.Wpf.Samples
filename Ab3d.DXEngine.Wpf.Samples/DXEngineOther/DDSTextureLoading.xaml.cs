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
using Ab3d.DirectX;
using Ab3d.DirectX.Materials;
using SharpDX;
using SharpDX.Direct3D11;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineOther
{
    /// <summary>
    /// Interaction logic for DDSTextureLoading.xaml
    /// </summary>
    public partial class DDSTextureLoading : Page
    {
        private StandardMaterial _standardMaterial;

        public DDSTextureLoading()
        {
            InitializeComponent();


            // Path to png and dds files
            string pngLogoFileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources/ab4d-logo-220x220.png");
            string ddsLogoFileName = System.IO.Path.ChangeExtension(pngLogoFileName, ".dds");


            // When big textures are used (for example 4k textures),
            // then it can take a long time to load the textures.
            // This process can also use a lot of memory.
            // Most of the time and memory is spend when the loaded file if converted into DirectX ShaderResourceView
            // and when mip-maps are created (smaller version of the image that are required for texture sampling).
            //
            // This process can be significantly improved when DDS files are used to store textures.
            // DDS file is a DirectDraw Surface container file format that is optimized in such a way 
            // that graphics card can read and process the file in the most efficient way.
            //
            // There are many ways to covert standard image files into DDS files.
            // 1) Open file in Visual Studio 2015 + and then save the file as .dds.
            // 2) Photoshop DDS exporter - https://developer.nvidia.com/nvidia-texture-tools-adobe-photoshop
            // 3) Paint.Net - search for "DDS FileType Plus" plugin - https://forums.getpaint.net/topic/111731-dds-filetype-plus-2017-09-27/
            // 4) Download texconv.exe from https://github.com/Microsoft/DirectXTex/releases
            //
            // I would recommend using texconv.exe utility because it gives you the most options.
            // For example the dds file that is used in this sample was created with the following command line:
            // texconv ab4d-logo-220x220.png -f BC7_UNORM -pmalpha -bcmax -y
            //
            // When adding -r switch, it is also possible to use wildcard characters (? or *)
            //
            // Note that dds files can be very big on the disk. 
            // But they can be significantly compressed with any compression algorithm - for example in the installer.
            //
            //
            //
            // When you have dds files prepared, you can load them into DXEngine in 2 possible ways:

            // 1) 
            // First way to load dds file is to provide both png and dds files and then set static WpfMaterial.LoadDdsIfAvailable to true.
            //
            // When LoadDdsIfAvailable is set to true (false by default) the texture loader will check 
            // if there is a dds file with the same name but with dds file extension.
            // If dds file exist, it is loaded instead of the specified file.
            //
            // The check is done only on BitmapImages that do not have CacheOption set to OnLoad - in this case the bitmap is already loaded into BitmapImage.
            //
            // In this case the content of the png file will not be loaded into memory (except the file header that is always loaded).

            WpfMaterial.LoadDdsIfAvailable = true;

            var bitmapImage = new BitmapImage(new Uri(pngLogoFileName));
            var diffuseMaterial = new DiffuseMaterial(new ImageBrush(bitmapImage));

            PlaneVisual1.Material = diffuseMaterial;


            // 2)
            // Manually load dds file and create DXEngine's StandardMaterial with loaded texture

            // We need a DirectX device to load DDS file.
            // We can subscribe to DXSceneDeviceCreated event to be notified when the DirectX device is created.
            MainDXViewportView.DXSceneDeviceCreated += delegate (object sender, EventArgs args)
            {
                // The easiest way to load image file and in the same time create a material with the loaded texture is to use the CreateStandardTextureMaterial method.
                // Note that we need to manually dispose the created StandardMaterial and ShaderResourceView (this is done in the Unloaded event)
                _standardMaterial = Ab3d.DirectX.TextureLoader.CreateStandardTextureMaterial(MainDXViewportView.DXScene.DXDevice, ddsLogoFileName);
                
                
                // If we want more control over the material creation process, we can use the following code:

                //// To load a texture from file, you can use the TextureLoader.LoadShaderResourceView (this supports loading standard image files and also loading dds files).
                //// This method returns a ShaderResourceView and it can also set a textureInfo parameter that defines some of the properties of the loaded texture (bitmap size, dpi, format, hasTransparency).
                //TextureInfo textureInfo;
                //_loadedShaderResourceView = Ab3d.DirectX.TextureLoader.LoadShaderResourceView(MainDXViewportView.DXScene.Device,
                //                                                                              ddsLogoFileName,
                //                                                                              out textureInfo);


                //// Get recommended BlendState based on HasTransparency and HasPreMultipliedAlpha values.
                //// Possible values are: CommonStates.Opaque, CommonStates.PremultipliedAlphaBlend or CommonStates.NonPremultipliedAlphaBlend.
                //var recommendedBlendState = MainDXViewportView.DXScene.DXDevice.CommonStates.GetRecommendedBlendState(textureInfo.HasTransparency, textureInfo.HasPremultipliedAlpha);

                //// Now we can create a DXEngine's StandardMaterial
                //_standardMaterial = new StandardMaterial()
                //{
                //    // Set ShaderResourceView into array of diffuse textures
                //    DiffuseTextures   = new ShaderResourceView[] { _loadedShaderResourceView },
                //    TextureBlendState = recommendedBlendState,

                //    HasTransparency = textureInfo.HasTransparency,

                //    // When showing texture, the DiffuseColor represents a color mask - each color from texture is multiplied with DiffuseColor (White preserves the original color)
                //    DiffuseColor = Color3.White
                //};

                // Use SetUsedDXMaterial extension method to specify the created standardMaterial to be used instead of WPF material
                PlaneVisual2.Material.SetUsedDXMaterial(_standardMaterial);
            };

            // To create ShaderResourceView from WPF's Bitmap source or file, you can also use the following two static method:
            //var shaderResourceView = Ab3d.DirectX.Materials.WpfMaterial.CreateTexture2D(dxDevice, wpfBitmapSource);
            //var shaderResourceView = Ab3d.DirectX.Materials.WpfMaterial.LoadTexture2D(dxDevice, fileName);


            MainDXViewportView.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                if (_standardMaterial != null)
                {
                    if (_standardMaterial.DiffuseTextures != null && _standardMaterial.DiffuseTextures.Length > 0)
                        _standardMaterial.DiffuseTextures[0].Dispose();

                    _standardMaterial.Dispose();
                    _standardMaterial = null;
                }
            };
        }
    }
}
