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
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Ab3d.DirectX;
using Ab3d.DirectX.Materials;
using SharpDX;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineAdvanced
{
    /// <summary>
    /// Interaction logic for CustomShaderMaterialSample.xaml
    /// </summary>
    public partial class CustomShaderMaterialSample : Page
    {
        private MeshGeometry3D _meshGeometry3D;
        private MeshNormalEffect _wpfMaterialEffect;

        private Ab3d.DirectX.DisposeList _disposeList;

        public CustomShaderMaterialSample()
        {
            InitializeComponent();

            _disposeList = new DisposeList();


            // Load Stanford bunny 3D model
            var readerObj = new Ab3d.ReaderObj();
            var bunnyModel3D = readerObj.ReadModel3D(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources\\Models\\bun_zipper_res3.obj"));

            // Get MeshGeometry3D

            var geometryModel3D = bunnyModel3D as GeometryModel3D;
            if (geometryModel3D != null)
                _meshGeometry3D = geometryModel3D.Geometry as MeshGeometry3D;
            else
                _meshGeometry3D = null;

            if (_meshGeometry3D == null)
                return;


            // Add 4 models with MeshNormalMaterial
            AddMeshNormalMaterialModel(_meshGeometry3D, new Point3D(-0.3, 0, 0), new Color3(1.0f, 1.0f, 1.0f));

            AddMeshNormalMaterialModel(_meshGeometry3D, new Point3D(-0.1, 0, 0), new Color3(1.0f, 0.0f, 0.0f));
            AddMeshNormalMaterialModel(_meshGeometry3D, new Point3D(0.1, 0, 0), new Color3(0.0f, 1.0f, 0.0f));
            AddMeshNormalMaterialModel(_meshGeometry3D, new Point3D(0.3, 0, 0), new Color3(0.0f, 0.0f, 1.0f));


            // Behind those 4 bunnies we add one bunny with standard WPF material:
            AddStandardModel(_meshGeometry3D, new Point3D(-0.2, 0, -0.3), new DiffuseMaterial(Brushes.LightSlateGray));

            // And then add one with standard WPF material but with overridden Effect - this is done inside the DXSceneDeviceCreated event handler (because we need an instance of MeshNormalEffect)


            // We use DXViewportView.DXSceneDeviceCreated to:
            // 1) Register DirectoryShaderBytecodeProvider that will provide shaders from local folder
            // 2) Create a bunny with overridden effect
            MainDXViewportView.DXSceneDeviceCreated += delegate(object sender, EventArgs args)
            {
                var dxScene = MainDXViewportView.DXScene;

                // DirectoryShaderBytecodeProvider will provide easy access to shaders that are stored in a local folder.
                // In case the shaders are stored in assembly as EmbeddedResources, you can also use AssemblyShaderBytecodeProvider (another option is to use DictionaryShaderBytecodeProvider)
                string shadersFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources\\Shaders");
                var directoryShaderBytecodeProvider = new DirectoryShaderBytecodeProvider(shadersFolder);

                // We register the directoryShaderBytecodeProvider by EffectsManager.
                // This way we can use GetVertexShader, GetPixelShader, GetShaders and other methods to simply create the shaders.
                // This also gives us flexibility because we can move shaders to other location later without the need to update the code in the Effect (update the location of the shaders).
                dxScene.DXDevice.EffectsManager.RegisterShaderResource(directoryShaderBytecodeProvider);


                // To override the effect, we first need to get an instance of MeshNormalEffect
                _wpfMaterialEffect = dxScene.DXDevice.EffectsManager.GetEffect<MeshNormalEffect>();

                if (_wpfMaterialEffect != null)
                {
                    // Create standard GeometryModel3D
                    var model3D = AddStandardModel(_meshGeometry3D, new Point3D(0.2, 0, -0.3), new DiffuseMaterial(Brushes.LightSlateGray));

                    // Add standard WPF effects are converted into WpfMaterial before they can be used in DXEngine.
                    // WpfMaterial will read all WPF material properties and write them into DXEngine's material properties.
                    var wpfMaterial = new WpfMaterial(model3D.Material);

                    // To render this model with providing standard WPF material data (diffuse color, etc.) and
                    // render the model with custom effect, we set the Effect property.
                    // This can be used when you do not need custom material properties and just want to provide custom rendering of standard materials.
                    // Though, it is recommended that in case of custom effect, you create a custom Material class (to use standard properties, you can derive the class from Ab3d.DirectX.Materials.StandardMaterial)
                    wpfMaterial.Effect = _wpfMaterialEffect;

                    model3D.Material.SetUsedDXMaterial(wpfMaterial);
                }
            };

            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                if (_wpfMaterialEffect != null)
                {
                    _wpfMaterialEffect.Dispose();
                    _wpfMaterialEffect = null;
                }

                _disposeList.Dispose();
            };

            LoadShaderText();
        }

        private GeometryModel3D AddMeshNormalMaterialModel(MeshGeometry3D mesh, Point3D position, Color3 colorMask)
        {
            var diffuseMaterial = new DiffuseMaterial(Brushes.Red);

            var meshNormalMaterial = new MeshNormalMaterial()
            {
                ColorMask = colorMask
            };

            diffuseMaterial.SetUsedDXMaterial(meshNormalMaterial);

            _disposeList.Add(meshNormalMaterial);


            var model3D = new GeometryModel3D(mesh, diffuseMaterial);
            model3D.Transform = new TranslateTransform3D(position.X, position.Y, position.Z);

            var modelVisual3D = new ModelVisual3D()
            {
                Content = model3D
            };

            MainViewport.Children.Add(modelVisual3D);

            return model3D;
        }

        private GeometryModel3D AddStandardModel(MeshGeometry3D mesh, Point3D position, System.Windows.Media.Media3D.Material wpfMaterial)
        {
            var model3D = new GeometryModel3D(mesh, wpfMaterial);
            model3D.Transform = new TranslateTransform3D(position.X, position.Y, position.Z);

            var modelVisual3D = new ModelVisual3D()
            {
                Content = model3D
            };

            MainViewport.Children.Add(modelVisual3D);

            return model3D;
        }

        private void LoadShaderText()
        {
            string shadersFileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\Resources\Shaders\MeshNormalShader.hlsl");
            if (System.IO.File.Exists(shadersFileName))
            {
                string shaderContent = System.IO.File.ReadAllText(shadersFileName);
                ShaderTextBox.Text = shaderContent;
            }
            else
            {
                ShaderTextBox.Text = "Cannot find MeshNormalShader.hlsl";
            }
        }
    }
}
