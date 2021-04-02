using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
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
using Ab3d.Cameras;
using Ab3d.Common;
using Ab3d.Common.Cameras;
using Ab3d.DirectX;
using Ab3d.DirectX.Controls;
using Ab3d.DXEngine.Wpf.Samples.Common;
using Ab3d.Utilities;
using Ab3d.Visuals;

namespace Ab3d.DXEngine.Wpf.Samples.PowerToysOther
{
    /// <summary>
    /// Interaction logic for ImprovedTextBlockVisual3D.xaml
    /// </summary>
    public partial class ImprovedTextBlockVisual3D : Page
    {
        private TestScene _dxTestScene;
        private TestScene _wpfTestScene;
        private int[] _randomIndexes;

        private const float UsedAlphaClipThreshold = 0.1f;

        public ImprovedTextBlockVisual3D()
        {
            InitializeComponent();

            var rnd = new Random();
            _randomIndexes = Enumerable.Range(0, 10).OrderBy(n => rnd.Next(100)).ToArray(); // randomize an array of 10 integers

            _wpfTestScene = new TestScene(WpfViewport3D, isInDXEngine: false);
            _wpfTestScene.CreatesTestScene(_randomIndexes);

            _dxTestScene = new TestScene(DXViewport3D, isInDXEngine: true);
            _dxTestScene.CreatesTestScene(_randomIndexes);

            var alphaClipThreshold = (AlphaClipCheckBox.IsChecked ?? false) ? UsedAlphaClipThreshold : 0f;
            _dxTestScene.SetDXEngineSettings(UseSolidColorEffectCheckBox.IsChecked ?? false, alphaClipThreshold);


            CopyWpfCamera(); // Copy settings from WpfCamera to DXCamera

            WpfCamera.StartRotation(20, 0);

            this.Unloaded += (sender, args) => MainDXViewportView.Dispose();
        }

        private void DXCamera_OnCameraChanged(object sender, CameraChangedRoutedEventArgs e)
        {
            CopyDXCamera();
        }

        private void WpfCamera_OnCameraChanged(object sender, CameraChangedRoutedEventArgs e)
        {
            CopyWpfCamera();
        }

        private void CopyDXCamera()
        {
            WpfCamera.Heading = DXCamera.Heading;
            WpfCamera.Attitude = DXCamera.Attitude;
            WpfCamera.Distance = DXCamera.Distance;
            WpfCamera.Offset = DXCamera.Offset;
        }

        private void CopyWpfCamera()
        {
            DXCamera.Heading = WpfCamera.Heading;
            DXCamera.Attitude = WpfCamera.Attitude;
            DXCamera.Distance = WpfCamera.Distance;
            DXCamera.Offset = WpfCamera.Offset;
        }


        class TestScene
        {
            public readonly Viewport3D TargetViewport3D;
            public readonly bool IsInDXEngine;

            public TestScene(Viewport3D targetViewport3D, bool isInDXEngine)
            {
                if (targetViewport3D == null) throw new ArgumentNullException("targetViewport3D");

                TargetViewport3D   = targetViewport3D;
                IsInDXEngine = isInDXEngine;
            }
            
            public void CreatesTestScene(int[] randomIndexes)
            {
                for (int i = 0; i < randomIndexes.Length; i++)
                {
                    int index = randomIndexes[i];

                    var textBlockVisual3D = new TextBlockVisual3D()
                    {
                        Text             = "TextBlockVisual3D_" + (index + 1).ToString(),
                        Position         = new Point3D(0, 0, -200 + index * 40),
                        PositionType     = PositionTypes.Center,
                        Size             = new Size(200, 50),
                        Foreground       = Brushes.Yellow,
                        RenderBitmapSize = new Size(512, 128), // Rendering text to bitmap will run the code much faster (when rendered by WPF, then by default (RenderBitmapSize is Empty) VisualBrush is used to show the text - this gives more details when text is closer to the camera, but it is much slower and requires much more cpu time; When rendered by DXEngine, then RenderBitmapSize is set to 512x256 by default)
                        IsBackSidedTextFlipped = true
                    };

                    TargetViewport3D.Children.Add(textBlockVisual3D);
                }


                // Adding ambient light can help to prevent darkening the TextBlockVisual3D too much,
                // but it also reduces the shading effect and when adding too much ambient light
                // the objects may not appear shaded anymore.
                //
                //var ambientLight = new AmbientLight(Color.FromRgb(50, 50, 50));
                //TargetViewport3D.Children.Add(ambientLight.CreateModelVisual3D());
            }

            public void SortByCameraDistance(BaseCamera camera)
            {
                TransparencySorter.SortByCameraDistance(TargetViewport3D, camera);
            }

            public void SetDXEngineSettings(bool useSolidColorEffect, float alphaClipThreshold)
            {
                if (!IsInDXEngine)
                    return;

                foreach (var textBlockVisual3D in TargetViewport3D.Children.OfType<TextBlockVisual3D>())
                {
                    // When UseSolidColorEffect attribute is set to true, then the material is rendered by a SolidColorEffect
                    // This means that the material is not affected by lighting calculations and is always fully illuminated even if it is not facing the light.
                    // Note that UseSolidColorEffect must be set before the DXEngine's material is initialized (changes after that does not have any effect).
                    if (useSolidColorEffect)
                    {
                        textBlockVisual3D.Material.SetDXAttribute(DXAttributeType.UseSolidColorEffect, true);
                        textBlockVisual3D.BackMaterial.SetDXAttribute(DXAttributeType.UseSolidColorEffect, true);
                    }
                    else
                    {
                        textBlockVisual3D.Material.ClearDXAttribute(DXAttributeType.UseSolidColorEffect);
                        textBlockVisual3D.BackMaterial.ClearDXAttribute(DXAttributeType.UseSolidColorEffect);
                    }

                    // When AlphaClipThreshold is set to a value that is bigger then 0, then alpha clipping is enabled.
                    // This means that pixels with alpha color values below this value will be clipped (not rendered and their depth will not be written to depth buffer).
                    // When alpha clipping is disabled (this attribute is not set or is set to 0) this means that also pixels with alpha value 0 are fully processed (they are not visible but its depth value is still written so objects that are rendered afterwards and are behind the pixel will not be visible).
                    // 
                    // See also AlphaClippingSample for more info.
                    if (alphaClipThreshold > 0)
                    {
                        textBlockVisual3D.Material.SetDXAttribute(DXAttributeType.Texture_AlphaClipThreshold, alphaClipThreshold);
                        textBlockVisual3D.BackMaterial.SetDXAttribute(DXAttributeType.Texture_AlphaClipThreshold, alphaClipThreshold);
                    }
                    else
                    {
                        textBlockVisual3D.Material.ClearDXAttribute(DXAttributeType.Texture_AlphaClipThreshold);
                        textBlockVisual3D.BackMaterial.ClearDXAttribute(DXAttributeType.Texture_AlphaClipThreshold);
                    }
                }
            }
        }

        private void WpfSortByCameraDistanceButton_OnClick(object sender, RoutedEventArgs e)
        {
            _wpfTestScene.SortByCameraDistance(WpfCamera);
        }

        private void OnDXEngineSettingsCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;


            // When changing the value of UseSolidColorEffect and Texture_AlphaClipThreshold we need to recreate the 3D scene 
            // because their value is only read when the DXEngine's material is created.
            _dxTestScene.TargetViewport3D.Children.Clear();

            _dxTestScene.CreatesTestScene(_randomIndexes);

            var alphaClipThreshold = (AlphaClipCheckBox.IsChecked ?? false) ? UsedAlphaClipThreshold : 0f;
            _dxTestScene.SetDXEngineSettings(UseSolidColorEffectCheckBox.IsChecked ?? false, alphaClipThreshold);

            if (MainDXViewportView.DXScene != null)
            {
                // Enable / disable highly efficient sorting by camera distance
                MainDXViewportView.DXScene.IsTransparencySortingEnabled = DXEngineSortByCameraDistanceEffectCheckBox.IsChecked ?? false;
            }
        }
    }
}
