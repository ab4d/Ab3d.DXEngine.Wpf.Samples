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
using Ab3d.DirectX;
using SharpDX.Direct3D;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    /// <summary>
    /// Interaction logic for SupersamplingSample.xaml
    /// </summary>
    public partial class SupersamplingSample : Page
    {
        private bool _isInternalChange;

        public SupersamplingSample()
        {
            InitializeComponent();


            MultisampledDXViewportView.GraphicsProfiles = new GraphicsProfile[] { GraphicsProfile.HighQualityHardwareRendering, GraphicsProfile.HighQualitySoftwareRendering, GraphicsProfile.Wpf3D };

            // For SupersampledDXViewportView we will use UltraQualityHardwareRendering (the same as HighQualityHardwareRendering but has ExecutePixelShaderPerSample set to true).
            // There is no UltraQualitySoftwareRendering so we create one from HighQualitySoftwareRendering
            var ultraQualitySoftwareRendering = GraphicsProfile.HighQualitySoftwareRendering.Clone();
            ultraQualitySoftwareRendering.ExecutePixelShaderPerSample = true;

            SupersampledDXViewportView.GraphicsProfiles = new GraphicsProfile[] { GraphicsProfile.UltraQualityHardwareRendering, ultraQualitySoftwareRendering, GraphicsProfile.Wpf3D };


            var sceneModelVisual1 = CreateScene();
            SupersampledViewport.Children.Add(sceneModelVisual1);

            var sceneModelVisual2 = CreateScene();
            MultisampledViewport.Children.Add(sceneModelVisual2);


            // We can turn on supersampling after the DXScene is created and initialized
            SupersampledDXViewportView.DXSceneInitialized += delegate(object sender, EventArgs args)
            {
                // When DXEngine falls back to WPF 3D rendering, the DXScene is null; we could also check for MainDXViewportView.UsedGraphicsProfile.DriverType != GraphicsProfile.DriverTypes.Wpf3D
                if (SupersampledDXViewportView.DXScene == null)
                {
                    SupersamplingTitleTextBlock.Text = "Supersampling not supported";
                    MessageBox.Show("This sample is not supported in WPF 3D rendering");
                    return;
                }


                if (SupersampledDXViewportView.DXScene.DXDevice.DeviceCapabilities.FeatureLevel <= FeatureLevel.Level_10_1)
                {
                    SupersamplingTitleTextBlock.Text = "Supersampling not supported";
                    MessageBox.Show("Your graphics card does not support supersampling created with executing pixel shader for each sample. This is supported only in shader model 4.1 and later. This required that graphics card supports at least feature level 10.1.");
                    return;
                }


                int multisamplingCount = SupersampledDXViewportView.DXScene.MultisampleCount; // Get the actually used multisampling count


                // Update titles
                MultisamplingTitleTextBlock.Text = string.Format("Standard {0}x Multisampling (MSAA)", multisamplingCount);
                SupersamplingTitleTextBlock.Text = string.Format("{0}x Supersampling (SSAA)", multisamplingCount);

                SupersamplingDetailsTextBlock.Text = SupersamplingDetailsTextBlock.Text.Replace("{0}", multisamplingCount.ToString());


                if (multisamplingCount <= 1)
                {
                    MessageBox.Show("The currently used GraphicsProfile does not use multisampling.\r\n\r\nBecause of this the sample cannot show the advantage of using supersampling.\r\nPlease select another GraphicsProfile or run the sample on computer that support multisampling.");
                    return;
                }


                // Now we can turn on supersampling
                // This is done with setting ExecutePixelShaderPerSample to true.
                // When ExecutePixelShaderPerSample is set to true, the pixel shader is executed for each multisampling sample instead of only once per pixel .
                // This turns multisampling into supersampling which can improve rendering quality but can significantly reduce performance (especially for multiple point or spot lights; it is recommended to use up to 3 directional lights + ambient light).
                // See help file for DXScene.ExecutePixelShaderPerSample for more info.
                // 
                // In the future there will be support for custom supersampling count that could be combined with custom multisampling count.
                // For example: 4x supersampling + 8x multisampling.
                // This is also a reason that the ExecutePixelShaderPerSample is not called EnableSupersampling because this would complicate API in the future.
                SupersampledDXViewportView.DXScene.ExecutePixelShaderPerSample = true;
            };
        }

        private ModelVisual3D CreateScene()
        {
            var supersamplingModel = (GeometryModel3D) this.FindResource("SupersamplingModel");
            supersamplingModel = supersamplingModel.Clone(); // Clone the model so that each DXViewportView (and each DirectX device) have its own model

            var rootModelGroup = new Model3DGroup();

            for (int i = 0; i < 10; i++)
            {
                var model3DGroup = new Model3DGroup();
                model3DGroup.Children.Add(supersamplingModel);
                model3DGroup.Transform = new TranslateTransform3D(0, 0, i * -100); // distribute the models so that each of them is longer from the camera

                rootModelGroup.Children.Add(model3DGroup);
            }

            var modelVisual3D = new ModelVisual3D();
            modelVisual3D.Content = rootModelGroup;

            return modelVisual3D;
        }

        private void Camera1_OnCameraChanged(object sender, CameraChangedRoutedEventArgs e)
        {
            if (_isInternalChange) // Prevent infinite call or Camera1 / 2 change handlers
                return;

            _isInternalChange = true;

            Camera2.BeginInit();
            Camera2.Heading  = Camera1.Heading;
            Camera2.Attitude = Camera1.Attitude;
            Camera2.Distance = Camera1.Distance;
            Camera2.Offset   = Camera1.Offset;
            Camera2.EndInit();

            _isInternalChange = false;
        }

        private void Camera2_OnCameraChanged(object sender, CameraChangedRoutedEventArgs e)
        {
            if (_isInternalChange) // Prevent infinite call or Camera1 / 2 change handlers
                return;

            _isInternalChange = true;

            Camera1.BeginInit();
            Camera1.Heading = Camera2.Heading;
            Camera1.Attitude = Camera2.Attitude;
            Camera1.Distance = Camera2.Distance;
            Camera1.Offset = Camera2.Offset;
            Camera1.EndInit();

            _isInternalChange = false;
        }
    }
}
