using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
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
using Ab3d.Common;
using Ab3d.Common.Cameras;
using Ab3d.DirectX;
using Ab3d.DirectX.Materials;
using Ab3d.Utilities;
using Ab3d.Visuals;
using SharpDX.Direct3D11;
using Material = Ab3d.DirectX.Material;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    // To get detailed description on alpha-clipping and alpha-to-coverage see the comments in the SetMaterialsDXAttributes method below.

    /// <summary>
    /// Interaction logic for AlphaClippingSample.xaml
    /// </summary>
    public partial class AlphaClippingSample : Page
    {
        private DisposeList _disposables;

        private List<int> _savedObjectsOrder;
        private List<Visual3D> _originalObjects;

        // Create a new BillboardVisual3D that is the same as PlaneVisual3D.
        // But with a new class, we know which PlaneVisual3D objects we need to orient towards the camera and which should stay as they are.
        public class BillboardVisual3D : PlaneVisual3D
        {
        }

        public AlphaClippingSample()
        {
            InitializeComponent();

            StandardAlphaBlendingInfoControl.InfoText =
@"Alpha blending is the standard way of rendering transparent objects in 3D graphics.
With alpha blending the transparent pixels are blended with the colors of the already
rendered 3D objects - the alpha color value defines the amount of the color that is 
taken from the transparent object compared to the already rendered color.

This technique can provide very accurate results. 
But this requires that the objects are rendered in the correct order - 
first all opaque objects are rendered and then the transparent objects from the most distant objects 
(from the camera's position) to the objects closer to the camera are rendered.

Sorting transparent objects is required for 2 reasons:
1) when rendering a transparent pixel, the color of the objects behind this pixel need to be final 
   for the blending to be correct.
2) usually when rendering transparent objects they also write to the depth buffer - this means that
   after they have been rendered no other objects that is farther away from the camera will be rendered
   (depth test will prevent rendering those objects).

Therefore it is essential first to render the opaque objects, 
then sort transparent objects and render them.";


            AlphaClippingThresholdInfoControl.InfoText =
@"Alpha clipping is a technique that can be used when rendering textures with some pixels opaque and some transparent.
In this case, the user can select an alpha clip threshold - this is a value that specifies at which alpha value the 
pixels will be clipped (not rendered and their depth will not be written to the depth buffer).

For example, if AlphaClipThreshold is set to 0.2 then all pixels with alpha value less then 0.2 will be clipped.
This is useful when textures have very distinctive fully transparent and fully opaque areas.
In this case it may not be necessary to sort transparent objects, but it is still recommended because the
pixels that are not clipped are still rendered with alpha blending.

The problem with this technique is that at the border of the transparent area some artifacts may appear -
adjusting the threshold value may help to solve that - check the green trees and adjust the threshold value to see that.";

            AlphaToCoverageInfoControl.InfoText =
@"Alpha-to-coverage is a special blending technique that can be used to render textures with transparent and semi-transparent
pixels and does not require objects to be sorted by their camera distance.

When using alpha-to-coverage then the graphics card determines if the pixel is transparent or opaque based on the alpha value
(when alpha is less the 0.5 then the pixel is considered to be fully transparent; otherwise the pixel is considered to be fully opaque).

What is more, when using MSAA (multi-sample anti-aliasing) then the level of transparency can be defined more accurately 
with making some sub-pixel samples transparent and some opaque.
For, example when using 8 x MSAA then each pixel's color is calculated with combining 8 sub-pixel samples -
when alpha-to-coverage is enabled and the alpha value is 0.25 (=2/8) then 2 of the samples will be transparent and 6 will be opaque.

This way it is possible to create smoother transitions from fully transparent to fully opaque regions.
This technique does not produce as accurate results as standard alpha blending, but a big advantage is that it does not require 
objects to be sorted (and rendered) from those that are farthest away to those that are closest to the camera 
(and the results are still very good for some use cases - especially when the textures has small transitions from transparent to opaque).";



                AlphaClippingThresholdComboBox.ItemsSource = new string[]
            {
                "0 (disabled)", // alpha clipping is disabled: no color will be written to the pixel, but the pixels depth information will be written to depth buffer and this will prevent rendering objects that are rendered after this object and are farther away from the camera (even when behind fully transparent pixels).
                "0.01", // discard only pixels with alpha less then 0.01 - this may leave some semi-transparent border around the object
                "0.02",
                "0.05",
                "0.1",
                "0.2",
                "0.5",
                "0.8",
                "0.9",
                "0.99" // discard all non-opaque pixels (pixels with alpha less then 0.99 are discarded) - this leaves no semi-transparent border around the object but my clip too much pixels
            };

            AlphaClippingThresholdComboBox.SelectedIndex = 4; // = "0.1"

            MainDXViewportView.IsTransparencySortingEnabled = false; // Disable sorting by camera distance

            MainDXViewportView.DXSceneDeviceCreated += delegate(object sender, EventArgs args)
            {
                CreateTestSemiTransparentObjects();
            };


            Camera1.CameraChanged += Camera1OnCameraChanged;
            
            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                if (_disposables != null)
                {
                    _disposables.Dispose();
                    _disposables = null;
                }

                MainDXViewportView.Dispose();
            };
        }

        private void CreateTestSemiTransparentObjects()
        {
            if (_disposables != null)
                _disposables.Dispose();

            _disposables  = new DisposeList();

            SemiTransparentRootVisual3D.Children.Clear();

            string texturesFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\");

            AddSmallForest(new Point3D(0, 20, -100), texturesFolder + @"AlphaTextures\tree0.dds");

            AddSmallForest(new Point3D(-100, 20, -100), texturesFolder + "TreeTexture.png");


            var wireFenceMaterial = CreateDiffuseMaterial(texturesFolder + @"AlphaTextures\WireFence.dds");
            SetMaterialsDXAttributes(wireFenceMaterial);

            var boxVisual3D = new BoxVisual3D()
            {
                CenterPosition = new Point3D(100, 20, -100),
                Size = new Size3D(40, 40, 40),
                Material = wireFenceMaterial,
                BackMaterial = wireFenceMaterial
            };

            SemiTransparentRootVisual3D.Children.Add(boxVisual3D);


            AddPlaneGroup(new Point3D(0, 30, 70), new Vector3D(0, 0, 20), 5, texturesFolder + "SemiTransparentFrame.png");


            var textBlockVisual1 = new TextBlockVisual3D()
            {
                Text = "ABCDE\r\nFGHIJ",
                Position = new Point3D(-100, 20, 0),
                PositionType = PositionTypes.Bottom,
                Size = new Size(60, 40),
                Background = Brushes.Transparent,
                RenderBitmapSize = new Size(256, 128),
                TextPadding = new Thickness(5, 0, 5, 0),
                BorderBrush = Brushes.Yellow,
                BorderThickness = new Thickness(0, 2, 0, 2)
            };

            SetMaterialsDXAttributes(textBlockVisual1.Material);
            SetMaterialsDXAttributes(textBlockVisual1.BackMaterial);

            SemiTransparentRootVisual3D.Children.Add(textBlockVisual1);



            var textBlockVisual2 = new TextBlockVisual3D()
            {
                Text = "12345\r\n67890",
                Position = new Point3D(100, 20, 0),
                PositionType = PositionTypes.Bottom,
                Size = new Size(60, 40),
                Background = Brushes.Transparent,
                RenderBitmapSize = new Size(256, 128),
                TextPadding = new Thickness(5, 0, 5, 0),
                BorderBrush = Brushes.Yellow,
                BorderThickness = new Thickness(0, 2, 0, 2)
            };

            SetMaterialsDXAttributes(textBlockVisual2.Material);
            SetMaterialsDXAttributes(textBlockVisual2.BackMaterial);


            SemiTransparentRootVisual3D.Children.Add(textBlockVisual2);


            var gradientBrush = new RadialGradientBrush(Colors.White, Color.FromArgb(0, 255, 255, 255)); // Gradient from White to fully transparent
            var gradientMaterial = new DiffuseMaterial(gradientBrush);
            SetMaterialsDXAttributes(gradientMaterial);

            var planeVisual3D = new PlaneVisual3D()
            {
                CenterPosition = new Point3D(0, 60, 0),
                Size = new Size(80, 80),
                Normal = new Vector3D(0, 0, 1),
                HeightDirection = new Vector3D(0, 1, 0),
                Material = gradientMaterial,
                BackMaterial = gradientMaterial
            };

            SemiTransparentRootVisual3D.Children.Add(planeVisual3D);

            AlignBillboards();


            _originalObjects = SemiTransparentRootVisual3D.Children.ToList();

            // if there was a saved objects order because of previous sorting or randomizing, we apply that same objects order now
            ApplySavedObjectsOrder();
        }

        private void AddPlaneGroup(Point3D firstPlaneCenterPosition, Vector3D direction, int count, string textureFileName)
        {
            var material = CreateDiffuseMaterial(textureFileName);

            var currentPosition = firstPlaneCenterPosition;
            var planeSize = new Size(70, 30);

            for (int i = 0; i < count; i++)
            {
                AddPlaneVisual3D(currentPosition, material, planeSize, isBillboard: false);
                currentPosition += direction;
            }
        }

        private void AddSmallForest(Point3D centerPosition, string textureFileName)
        {
            var material = CreateDiffuseMaterial(textureFileName);
            var planeSize = new Size(20, 40);

            AddPlaneVisual3D(centerPosition + new Vector3D(-20, 0, -20), material, planeSize, isBillboard: true);
            AddPlaneVisual3D(centerPosition + new Vector3D(-20, 0, 20),  material, planeSize, isBillboard: true);
            AddPlaneVisual3D(centerPosition + new Vector3D(20, 0, 20),   material, planeSize, isBillboard: true);
            AddPlaneVisual3D(centerPosition + new Vector3D(20, 0, -20),  material, planeSize, isBillboard: true);
        }

        private DiffuseMaterial CreateDiffuseMaterial(string textureFileName)
        {
            DiffuseMaterial material;

            if (textureFileName.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
            {
                var dxScene = MainDXViewportView.DXScene;
                if (dxScene == null) // In case of WPF 3D rendering
                    return null;


                // The easiest way to load image file and in the same time create a material with the loaded texture is to use the CreateStandardTextureMaterial method.
                var standardMaterial = Ab3d.DirectX.TextureLoader.CreateStandardTextureMaterial(MainDXViewportView.DXScene.DXDevice, textureFileName);

                // We need to manually dispose the created StandardMaterial and ShaderResourceView
                _disposables.Add(standardMaterial);
                _disposables.Add(standardMaterial.DiffuseTextures[0]);


                bool isAlphaToCoverageEnabled = AlphaToCoverageRadioButton.IsChecked ?? false;
                bool isAlphaClippingEnabled   = AlphaClippingRadioButton.IsChecked ?? false;

                standardMaterial.AlphaClipThreshold = isAlphaClippingEnabled
                                                        ? GetAlphaClippingThreshold() 
                                                        : 0; // 0 disables alpha clipping

                // When AlphaToCoverage is enabled, then we need to set the TextureBlendState to AlphaToCoverage.
                // If this is not done (if TextureBlendState is null), then the bend state is set to Opaque or PremultipliedAlphaBlend (when the texture has transparency)
                if (isAlphaToCoverageEnabled)
                    standardMaterial.TextureBlendState = dxScene.DXDevice.CommonStates.AlphaToCoverage; 

                material = new DiffuseMaterial();
                material.SetUsedDXMaterial(standardMaterial);
            }
            else
            {
                var bitmapImage = new BitmapImage(new Uri(textureFileName, UriKind.Absolute));
                material = new DiffuseMaterial(new ImageBrush(bitmapImage));

                // When using WPF material, we need to set special DXEngine attributes with using SetDXAttribute (this is done in the following method):
                SetMaterialsDXAttributes(material);
            }
            
            return material;
        }

        private void AddPlaneVisual3D(Point3D centerPosition, System.Windows.Media.Media3D.Material material, Size planeSize, bool isBillboard)
        {
            PlaneVisual3D planeVisual3D;

            if (isBillboard)
                planeVisual3D = new BillboardVisual3D(); // BillboardVisual3D is defined in this sample and is the same as PlaneVisual3D but when BillboardVisual3D is there, we know that it needs to be aligned with the camera on each camera change
            else
                planeVisual3D = new PlaneVisual3D();

            planeVisual3D.CenterPosition = centerPosition;
            planeVisual3D.Size = planeSize;
            planeVisual3D.Normal = new Vector3D(0, 0, 1);
            planeVisual3D.HeightDirection = new Vector3D(0, 1, 0);
            planeVisual3D.Material = material;

            if (!isBillboard)
                planeVisual3D.BackMaterial = material;

            SemiTransparentRootVisual3D.Children.Add(planeVisual3D);
        }

        private void Camera1OnCameraChanged(object sender, CameraChangedRoutedEventArgs cameraChangedRoutedEventArgs)
        {
            // On each change of camera we will update the orientation of billboard objects.
            // When using PlaneVisual3D, TextBlockVisual3D or TextVisual3D we can simply call the AlignWithCamera method.

            // If we did some changes to the Camera1 in the code,
            // it is recommended to call Refresh before calling AlignWithCamera method:
            // Camera1.Refresh();

            AlignBillboards();
        }

        private void AlignBillboards()
        {
            // Update all tree objects
            foreach (var billboardVisual3D in SemiTransparentRootVisual3D.Children.OfType<BillboardVisual3D>())
                billboardVisual3D.AlignWithCamera(Camera1);
        }

        private void OnAnimateCameraCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            if (AnimateCameraCheckBox.IsChecked ?? false)
                Camera1.StartRotation(30, 0);
            else
                Camera1.StopRotation();
        }

        private void OnAlphaBlendingTypeChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            // Recreate the 3D scene (we cannot change the alpha clipping values and alpha-to-coverage values after the materials are initialized)
            CreateTestSemiTransparentObjects();
        }

        private void SetMaterialsDXAttributes(System.Windows.Media.Media3D.Material wpfMaterial)
        {
            if (wpfMaterial == null)
                return;

            // Texture_AlphaClipThreshold attribute description:
            // This attribute can be set to a float value between 0 and 1.
            // When set to a float value that is bigger then 0, then alpha clipping is enabled.
            // This means that pixels with alpha color values below this value will be clipped (not rendered and their depth will not be written to depth buffer).
            // When alpha clipping is disabled (this attribute is not set or is set to 0) this means that also pixels with alpha value 0 are fully processed (they are not visible but its depth value is still written so objects that are rendered afterwards and are behind the pixel will not be visible).

            // Texture_UseAlphaToCoverage  attribute description:
            // When set to true, then the texture is rendered with using CommonStates.AlphaToCoverage blend state that can be used to render
            // textures with transparent and semi-transparent pixels and does not require objects to be sorted by their camera distance.
            //
            // When using alpha-to-coverage then the graphics card determines if the pixel is transparent or opaque based on the color's alpha value
            // (when alpha is less the 0.5 then pixel is fully transparent; otherwise the pixel is fully opaque).
            // 
            // What is more, when using MSAA (multi-sample anti-aliasing) then the level of transparency can be defined more accurately with making some sub-pixel samples transparent and some opaque
            // (for example when using 8 x MSAA then each pixel's color is calculated with combining 8 sub-pixel samples; when alpha-to-coverage is enabled and alpha value is 0.25 (=2/8) then 2 of the samples will be transparent and 6 will be opaque).
            // This way it is possible to create smoother transitions from fully transparent to fully opaque regions.
            // This technique does not produce as accurate results as standard alpha blending, but a big advantage is that it does not require objects to be sorted (and rendered) from those that are farthest away to those that are closest to the camera (and the results are still very good for some use cases - especially when the textures has small transitions from transparent to opaque).

            // NOTE:
            // Texture_AlphaClipThreshold and Texture_UseAlphaToCoverage DXAttributes are read
            // only when the material is initialized by the DXEngine. Changes after initialization are not read.
            // 
            // It is possible to enable both alpha-to-coverage and alpha clipping. But this is not needed.

            bool isAlphaToCoverageEnabled = AlphaToCoverageRadioButton.IsChecked ?? false;
            bool isAlphaClippingEnabled   = AlphaClippingRadioButton.IsChecked ?? false;

            if (isAlphaClippingEnabled)
            {
                float alphaClippingThreshold = GetAlphaClippingThreshold();
                wpfMaterial.SetDXAttribute(DXAttributeType.Texture_AlphaClipThreshold, alphaClippingThreshold);

                // When using DXEngine's StandardMaterial, you can change alpha-clip threshold with setting its AlphaClipThreshold value.
                // This field is provided with the IDiffuseTextureMaterial interface that StandardMaterial implements.
                // For example:
                //var standardMaterial = new StandardMaterial()
                //{
                //    // ... set other properties
                //    AlphaClipThreshold = alphaClippingThreshold
                //};
            }

            if (isAlphaToCoverageEnabled)
            {
                wpfMaterial.SetDXAttribute(DXAttributeType.Texture_UseAlphaToCoverage, true);

                // To alpha-to-coverage in DXEngine's StandardMaterial, set its TextureBlendState value to AlphaToCoverage blend state.
                // This field is provided with the IDiffuseTextureMaterial interface that StandardMaterial implements.
                // For example:
                //var standardMaterial = new StandardMaterial()
                //{
                //    // ... set other properties
                //    DiffuseTextures = new ShaderResourceView[] { textureShaderResourceView },
                //    TextureBlendState = MainDXViewportView.DXScene.DXDevice.CommonStates.AlphaToCoverage
                //};
            }
        }

        private float GetAlphaClippingThreshold()
        {
            var selectedText = (string) AlphaClippingThresholdComboBox.SelectedValue;
            var selectedTextParts = selectedText.Split(' ');

            float selectedValue = float.Parse(selectedTextParts[0], NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);

            return selectedValue;
        }

        private void AlphaClippingThresholdComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            CreateTestSemiTransparentObjects();
        }

        private void RandomObjectsOrderButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (IsCameraDistanceSortingEnabledCheckBox.IsChecked ?? false)
            {
                var result = MessageBox.Show("Sorting objects by camera distance is enabled. Therefore the randomize objects order will not have any visual effect.\r\n\r\nDo you want to disable sorting before randomizing objects order?",
                    "", MessageBoxButton.YesNo, MessageBoxImage.Stop);

                if (result == MessageBoxResult.Yes)
                    IsCameraDistanceSortingEnabledCheckBox.IsChecked = false;
            }

            GenerateRandomObjectsOrder();
            ApplySavedObjectsOrder();
        }

        private void GenerateRandomObjectsOrder()
        {
            if (_originalObjects == null)
                return; // just preserve the existing order

            var availableIndexes = Enumerable.Range(0, _originalObjects.Count).ToList();

            if (_savedObjectsOrder == null)
                _savedObjectsOrder = new List<int>(availableIndexes.Count);
            else
                _savedObjectsOrder.Clear();

            var rnd = new Random();
            while (availableIndexes.Count > 0)
            {
                int index = rnd.Next(availableIndexes.Count); // random between 0 and Count-1 (upper limit is exclusive)

                _savedObjectsOrder.Add(availableIndexes[index]);
                availableIndexes.RemoveAt(index);
            }
        }

        private void SaveObjectsOrder()
        {
            if (_originalObjects == null)
                return; // just preserve the existing order

            if (_originalObjects.Count != SemiTransparentRootVisual3D.Children.Count)
                throw new InvalidOperationException();

            if (_savedObjectsOrder == null)
                _savedObjectsOrder = new List<int>(_originalObjects.Count);
            else
                _savedObjectsOrder.Clear();

            for (var i = 0; i < SemiTransparentRootVisual3D.Children.Count; i++)
            {
                int index = _originalObjects.IndexOf(SemiTransparentRootVisual3D.Children[i]);
                _savedObjectsOrder.Add(index);
            }
        }

        private void ApplySavedObjectsOrder()
        {
            if (_savedObjectsOrder == null || _originalObjects == null)
                return; // just preserve the existing order

            if (_savedObjectsOrder.Count != SemiTransparentRootVisual3D.Children.Count)
                throw new InvalidOperationException();


            // First clear the SemiTransparentRootVisual3D.Children ...
            SemiTransparentRootVisual3D.Children.Clear();

            for (int i = 0; i < _originalObjects.Count; i++)
            {
                int newIndex = _savedObjectsOrder[i];
                SemiTransparentRootVisual3D.Children.Add(_originalObjects[newIndex]);
            }
        }

        private void OnIsCameraDistanceSortingEnabledCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (MainDXViewportView.DXScene == null)
                return;

            bool isSortingEnabled = IsCameraDistanceSortingEnabledCheckBox.IsChecked ?? false;

            MainDXViewportView.IsTransparencySortingEnabled = isSortingEnabled;

            if (!isSortingEnabled)
                MainDXViewportView.Refresh(); // If we check the sorting CheckBox then render the scene again to sort the objects
        }
    }
}
