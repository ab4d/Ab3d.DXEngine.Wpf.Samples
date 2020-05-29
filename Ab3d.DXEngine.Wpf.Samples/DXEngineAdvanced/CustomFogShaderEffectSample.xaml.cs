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
using Ab3d.DirectX.Models;
using Ab3d.Visuals;
using SharpDX;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineAdvanced
{
    /// <summary>
    /// Interaction logic for CustomFogShaderEffectSample.xaml
    /// </summary>
    public partial class CustomFogShaderEffectSample : Page
    {
        private FogEffect _fogEffect;

        private Effect _defaultStandardEffect;

        private List<WpfGeometryModel3DNode> _changedSceneNodes;

        private Ab3d.DirectX.Material _changedDXMaterial;

        private DiffuseMaterial _yellowFogWpfMaterial;

        public CustomFogShaderEffectSample()
        {
            InitializeComponent();

            _changedSceneNodes = new List<WpfGeometryModel3DNode>();

            MainDXViewportView.DXSceneDeviceCreated += delegate(object sender, EventArgs args)
            {
                var dxScene = MainDXViewportView.DXScene;

                // Create a new instance of FogEffect
                _fogEffect = new FogEffect();

                // Register effect with EffectsManager - this will also initialize the effect with calling OnInitializeResources method in the FogEffect class
                dxScene.DXDevice.EffectsManager.RegisterEffect(_fogEffect);

                // After the _logEffect has been initialized, we can set up the shaders.
                // First we load the compiled vertex and pixel shaders into byte arrays.
                // The compiled shaders are created with compiling the "FogShader.hlsl" file with executing the "CompileFogShader.bat" file (both in "Resources\Shaders\" folder).
                // To see how the hlsl shader file is created, see the "Ab3d.DirectX.ShaderFactory" project that has a step by step guide on how to create FogShader
                byte[] fogVertexShaderBytecode = System.IO.File.ReadAllBytes(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources\\Shaders\\FogShader.vs"));
                byte[] fogPixelShaderBytecode = System.IO.File.ReadAllBytes(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources\\Shaders\\FogShader.ps"));

                // When we have shader byte arrays we can send than to FogEffect
                _fogEffect.SetShaders(fogVertexShaderBytecode, fogPixelShaderBytecode);

                // Update fog distances and colors
                UpdateFogSettings();

                UpdateFogUsage();
            };

            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                // Because _fogEffect was created here, we also need to dispose it here
                if (_fogEffect != null)
                {
                    _fogEffect.Dispose();
                    _fogEffect = null;
                }

                // We also need ot dispose the _defaultStandardEffect to reduce the reference count on it
                if (_defaultStandardEffect != null)
                {
                    _defaultStandardEffect.Dispose();
                    _defaultStandardEffect = null;
                }

                MainDXViewportView.Dispose();
            };
        }

        private void UpdateFogSettings()
        {
            if (MainDXViewportView.DXScene == null) // probably WPF 3D rendering is used
                return;

            double fogStartDistance = FogStartSlider.Value;
            double fullFogDistance = FullFogSlider.Value;

            FullFogValueRun.Text = string.Format("{0:0}", fogStartDistance + fullFogDistance);


            System.Windows.Media.Color fogColor;

            var comboBoxItem = ColorCombobox.SelectedItem as ComboBoxItem;
            if (comboBoxItem != null)
                fogColor = ((SolidColorBrush)comboBoxItem.Background).Color;
            else
                fogColor = Colors.White;

            _fogEffect.SetFogData((float)fogStartDistance, (float)(fogStartDistance + fullFogDistance), fogColor.ToColor3());


            MainDXViewportView.BackgroundColor = fogColor;
        }

        private void UpdateFogUsage()
        {
            if (MainDXViewportView.DXScene == null) // probably WPF 3D rendering is used
                return;


            // First make sure that we have the StandardEffect that is used by default
            if (_defaultStandardEffect == null)
                _defaultStandardEffect = MainDXViewportView.DXScene.DXDevice.EffectsManager.GetStandardEffect();


            // First reset the changes of previously changed SceneNodes
            if (_changedDXMaterial != null)
            {
                _changedDXMaterial.Effect = null;
                _changedDXMaterial = null;
            }

            if (_yellowFogWpfMaterial != null)
            {
                // Replace _yellowFogWpfMaterial with default YellowMaterial
                var yellowMaterial = this.FindResource("YellowMaterial") as DiffuseMaterial;

                foreach (var baseModelVisual3D in MainViewport.Children.OfType<BaseModelVisual3D>())
                {
                    if (ReferenceEquals(baseModelVisual3D.Material, _yellowFogWpfMaterial))
                        baseModelVisual3D.Material = yellowMaterial;
                }

                _yellowFogWpfMaterial = null;
            }


            // NOTES:
            // We can use two ways to use custom FogEffect:
            //
            // 1) Set FogEffect as StandardEffect on EffectsManager.
            //    StandardEffect is used when DXEngine is rendering standard WPF materials (diffuse, specular, emissive).
            //    This means that if we change the StandardEffect, then all standard objects will be rendered with FogEffect.
            //    Other objects like 3D lines will still be rendered with their special effects.
            //
            // 2) Set FogEffect to the Effect property on the WpfMaterial object that is a DXEngine's material object created from WPF's material.
            //    When setting Effect property on DXEngine's material, the material will be rendered with the specified effect (instead of StandardEffect).


            // Additional notes:
            // 1) EffectsManager is created when the DXDevice is created. 
            //    This means that you cannot access it in the constructor of this class.
            //    If you need to change the StandardEffect (or some other setting on DXDevice) before the first frame is rendered,
            //    you need to use DXSceneDeviceCreated or DXSceneInitialized event handler to do that (as in this case).


            Effect newStandardEffect;

            if (StandardEffectRadioButton.IsChecked ?? false)
            {
                newStandardEffect = _fogEffect;
            }
            else if (RootBoxRadioButton.IsChecked ?? false)
            {
                newStandardEffect = _defaultStandardEffect;

                // Get the DXEngine's material that is created from the WPF's Material used for BaseBoxVisual3D
                var dxMaterial = Ab3d.DirectX.Materials.WpfMaterial.GetUsedDXMaterial(BaseBoxVisual3D.Material, MainDXViewportView.DXScene.DXDevice);

                if (dxMaterial != null)
                {
                    // Now we can specify the exact effect that will be used to render dxMaterial (if this is not specified, then StandardEffect is used)
                    dxMaterial.Effect = _fogEffect;
                    _changedDXMaterial = dxMaterial;
                }
            }
            else if (ReplaceRadioButton.IsChecked ?? false)
            {
                newStandardEffect = _defaultStandardEffect;

                // Create a clone of YellowMaterial
                var yellowMaterial = this.FindResource("YellowMaterial") as DiffuseMaterial;

                if (yellowMaterial != null)
                {
                    _yellowFogWpfMaterial = yellowMaterial.Clone();

                    // Now create a DXEngine's material that is created from WPF's material:
                    var yellowFogDXEngineMaterial = new Ab3d.DirectX.Materials.WpfMaterial(_yellowFogWpfMaterial);

                    // Specify the exact effect that will be used to render this material (if not specified, then the StandardEffect is used)
                    yellowFogDXEngineMaterial.Effect = _fogEffect;

                    // Now that we have both WPF material and DXEngine's material, 
                    // we can specify that whenever the _yellowFogWpfMaterial will be used (and when our DXDevice is used),
                    // we should use the specified yellowFogDXEngineMaterial.
                    // This is done with the static SetUsedDXMaterial method.
                    //
                    // TIP: The same approach can be used to use some other rendering technique to render on material - for example to use ModelColorLineEffect, ThickLineEffect or SolidColorEffect.
                    Ab3d.DirectX.Materials.WpfMaterial.SetUsedDXMaterial(_yellowFogWpfMaterial, yellowFogDXEngineMaterial);

                    // NOTE:
                    // Instead of calling SetUsedDXMaterial, we could also specify the DXDevice in the WpfMaterial constructor.
                    // This would call the SetUsedDXMaterial behind the scenes. But we did it here in the long way to better demonstrate what is going on.
                    // The following code would call SetUsedDXMaterial in the constructor:
                    // var yellowFogDXEngineMaterial = new Ab3d.DirectX.Materials.WpfMaterial(_yellowFogWpfMaterial, MainDXViewportView.DXScene.DXDevice);

                    // After the _yellowFogWpfMaterial is "connected" to the yellowFogDXEngineMaterial, we can assign it insetad of yellow material:
                    foreach (var baseModelVisual3D in MainViewport.Children.OfType<BaseModelVisual3D>())
                    {
                        if (ReferenceEquals(baseModelVisual3D.Material, yellowMaterial))
                            baseModelVisual3D.Material = _yellowFogWpfMaterial;
                    }
                }
            }
            else
            {
                newStandardEffect = _defaultStandardEffect;
            }

            MainDXViewportView.DXScene.DXDevice.EffectsManager.SetStandardEffect(newStandardEffect);
        }


        private void OnFogSettingsChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            UpdateFogSettings();

            // After we change the effect data, we need to manually re-render the scene
            MainDXViewportView.Refresh();
        }

        private void OnFogUsageChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            UpdateFogUsage();

            // Render the scene again
            MainDXViewportView.Refresh();
        }
    }
}
