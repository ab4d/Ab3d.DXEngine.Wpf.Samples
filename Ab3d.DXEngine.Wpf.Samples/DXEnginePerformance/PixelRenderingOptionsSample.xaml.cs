using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Ab3d.DirectX;
using Ab3d.Visuals;

#if SHARPDX
using SharpDX;
using SharpDX.Direct3D11;
#endif

namespace Ab3d.DXEngine.Wpf.Samples.DXEnginePerformance
{
    /// <summary>
    /// Interaction logic for PixelRenderingOptionsSample.xaml
    /// </summary>
    public partial class PixelRenderingOptionsSample : Page
    {
        private DisposeList _disposables;

        private ShaderResourceView _pixelTexture;
        private Size2 _pixelTextureSize;

        public PixelRenderingOptionsSample()
        {
            InitializeComponent();

            _disposables = new DisposeList();

            CreateScene();

            HasTransparentPixelColorsInfoControl.InfoText = 
@"Checking this CheckBox sets half the pixel colors to transparent colors.
Note that to correctly render pixels with transparent colors the pixels would need to be sorted so that
the pixels that are farther away from the camera are rendered before the pixels that are closer to the camera.
See transparency related samples in the 'Improved visuals' section. 
Ab3d.DXEngin does not support sorting positions based on camera distance.";

            FixUpVectorInfoControl.InfoText = 
@"When unchecked then pixels always face the camera.
When checked then pixels are always oriented up
(this is supported only when IsWorldSize is checked)";

            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                _disposables.Dispose();
                MainDXViewportView.Dispose();
            };
        }

        private void CreateScene()
        {
            PixelsRootVisual3D.Children.Clear();

            int pixelsXCount = 10;
            var positionsArray = PixelRenderingSample.CreatePositionsArray(new Point3D(0, 0, 0), new Size3D(70, 40, 70), pixelsXCount, pixelsXCount / 2, pixelsXCount);

            int pixelsCount = positionsArray.Length;
            var pixelColors = new Color4[pixelsCount];

            bool hasTransparentPixelColors = HasTransparentPixelColorsCheckBox.IsChecked ?? false;
            bool isCircularPixel = IsCircularPixelCheckBox.IsChecked ?? false;
            bool isWorldSize = IsWorldSizeCheckBox.IsChecked ?? false;
            bool isFixedUpVector = FixUpVectorCheckBox.IsChecked ?? false;

            int transparentPixelsStartIndex = hasTransparentPixelColors ? pixelsCount / 2 : 0;// int.MaxValue;

            for (int i = 0; i < pixelsCount; i++)
            {
                float green = 1.0f - ((float)i / (float)pixelsCount);
                pixelColors[i] = new Color4(new Color3((float)(i % pixelsXCount) / (float)pixelsXCount, green, 1.0f - green), i < transparentPixelsStartIndex ? 0.2f : 1);
            }

            var pixelSizes = new float[pixelsCount];
            for (int i = 0; i < pixelsCount; i++)
            {
                float percent = (float)i / (float)pixelsCount;
                pixelSizes[i] = (1f - percent) * 8f;
            }

            
            ShaderResourceView pixelTexture;
            Size2 pixelTextureSize;

            if (UseTextureCheckBox.IsChecked ?? false)
            {
                GetPixelTexture(out pixelTexture, out pixelTextureSize);
            }
            else
            {
                pixelTexture = null;
                pixelTextureSize = Size2.Empty;
            }
            

            var pixelsVisual3DWithColorAndSize = new PixelsVisual3D(positionsArray)
            {
                PixelColors = pixelColors,
                PixelSizes = pixelSizes,
                PixelSize = 1.0f,
                PixelColor = Colors.White,
                HasTransparentPixelColors = hasTransparentPixelColors,
                IsCircularPixel = isCircularPixel,
                IsWorldSize = isWorldSize,
                Transform = new TranslateTransform3D(0, 20, 200),
            };

            if (pixelTexture != null)
                pixelsVisual3DWithColorAndSize.SetTexture(pixelTexture, pixelTextureSize, colorMask: Colors.White);

            if (isFixedUpVector)
                pixelsVisual3DWithColorAndSize.SetFixedUpVector(new Vector3(0, 1, 0));

            PixelsRootVisual3D.Children.Add(pixelsVisual3DWithColorAndSize);

            // !!! IMPORTANT !!!
            // When PixelsVisual3D is not used any more, it needs to be disposed (we are using DisposeList to dispose all in Unloaded event handler)
            _disposables.Add(pixelsVisual3DWithColorAndSize);


            var pixelsVisual3DWithColors = new PixelsVisual3D(positionsArray)
            {
                PixelColors = pixelColors,
                PixelColor = Colors.White, // White (255,255,255) means that no color mask is used and the color specified in PixelColors are used (this is also the default value and is set here only for demonstration purpose)
                PixelSize = 4.0f,                              // Default pixel size
                IsCircularPixel = isCircularPixel,
                IsWorldSize = isWorldSize,

                // When the pixelColors contain any transparent colors (alpha < 1),
                // then HasTransparentPixelColors must be set to true to enable alpha blending.
                HasTransparentPixelColors = hasTransparentPixelColors,

                Transform = new TranslateTransform3D(0, 20, 100)
            };

            if (pixelTexture != null)
                pixelsVisual3DWithColors.SetTexture(pixelTexture, pixelTextureSize, colorMask: Colors.White);

            if (isFixedUpVector)
                pixelsVisual3DWithColors.SetFixedUpVector(new Vector3(0, 1, 0));

            PixelsRootVisual3D.Children.Add(pixelsVisual3DWithColors);

            _disposables.Add(pixelsVisual3DWithColors);


            var pixelsVisual3DWithColorsAndColorFilter = new PixelsVisual3D(positionsArray)
            {
                PixelColors = pixelColors,
                PixelColor = System.Windows.Media.Color.FromRgb(0, 255, 0), // color mask is multiplied with each color defined in PixelColors
                PixelSize = 4.0f,
                HasTransparentPixelColors = hasTransparentPixelColors,
                IsCircularPixel = isCircularPixel,
                IsWorldSize = isWorldSize,

                Transform = new TranslateTransform3D(0, 20, 0),
            };

            if (pixelTexture != null)
                pixelsVisual3DWithColorsAndColorFilter.SetTexture(pixelTexture, pixelTextureSize, colorMask: Colors.White);

            if (isFixedUpVector)
                pixelsVisual3DWithColorsAndColorFilter.SetFixedUpVector(new Vector3(0, 1, 0));

            PixelsRootVisual3D.Children.Add(pixelsVisual3DWithColorsAndColorFilter);

            _disposables.Add(pixelsVisual3DWithColorsAndColorFilter);
            

            var pixelsVisual3DWithSize = new PixelsVisual3D(positionsArray)
            {
                PixelSizes = pixelSizes,
                PixelSize = 1.0f,
                PixelColor = Colors.Orange,
                IsCircularPixel = isCircularPixel,
                IsWorldSize = isWorldSize,

                // Setting HasTransparentPixelColors is not needed because pixelsVisual3DWithSize does not use PixelColors
                //HasTransparentPixelColors = hasTransparentPixelColors,

                Transform = new TranslateTransform3D(0, 20, -100)
            };

            if (pixelTexture != null)
                pixelsVisual3DWithSize.SetTexture(pixelTexture, pixelTextureSize, colorMask: Colors.White);

            if (isFixedUpVector)
                pixelsVisual3DWithSize.SetFixedUpVector(new Vector3(0, 1, 0));

            PixelsRootVisual3D.Children.Add(pixelsVisual3DWithSize);

            _disposables.Add(pixelsVisual3DWithSize);

            
            var pixelsVisual3DWithSizeAndPixelSizeFactor = new PixelsVisual3D(positionsArray)
            {
                PixelSizes = pixelSizes,
                PixelSize = 2f,                                    // When using PixelSizes, then each pixel size multiplied by PixelSize
                PixelColor = Colors.Orange,
                IsCircularPixel = isCircularPixel,
                IsWorldSize = isWorldSize,

                Transform = new TranslateTransform3D(0, 20, -200)
            };

            if (pixelTexture != null)
                pixelsVisual3DWithSizeAndPixelSizeFactor.SetTexture(pixelTexture, pixelTextureSize, colorMask: Colors.White);

            if (isFixedUpVector)
                pixelsVisual3DWithSizeAndPixelSizeFactor.SetFixedUpVector(new Vector3(0, 1, 0));

            PixelsRootVisual3D.Children.Add(pixelsVisual3DWithSizeAndPixelSizeFactor);

            _disposables.Add(pixelsVisual3DWithSizeAndPixelSizeFactor);
        }

        private void GetPixelTexture(out ShaderResourceView pixelTexture, out Size2 pixelTextureSize)
        {
            if (_pixelTexture == null)
            {
                // Load ShaderResourceView from a WPF's Resource:
                //var warningBitmap = new BitmapImage(new Uri("pack://application:,,,/Ab3d.DXEngine.Wpf.Samples;component/Resources/warningIcon.png", UriKind.Absolute));
                //_pixelTexture = WpfMaterial.CreateTexture2D(MainDXViewportView.DXScene.DXDevice, warningBitmap);
                //_pixelTextureSize = new Size2(warningBitmap.PixelWidth, warningBitmap.PixelHeight);

                // To load ShaderResourceView from a file, use the following
                string fileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources/TreeTexture.png");
                _pixelTexture = TextureLoader.LoadShaderResourceView(MainDXViewportView.DXScene.DXDevice.Device, fileName, out TextureInfo textureInfo);
                _pixelTextureSize = new Size2(textureInfo.Width, textureInfo.Height);
            }

            pixelTexture = _pixelTexture;
            pixelTextureSize = _pixelTextureSize;
        }

        private void OnIsCircularPixelCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            CreateScene();
        }
        
        private void OnIsWorldSizeCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            CreateScene();

            // We can fix up vector only when IsWorldSize is true
            FixUpVectorCheckBox.IsEnabled = IsWorldSizeCheckBox.IsChecked ?? false;
        }
        
        private void OnUseTextureCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            CreateScene();
        }

        private void OnHasTransparentPixelColorsCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            CreateScene();
        }

        private void OnFixUpVectorCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            CreateScene();
        }
    }
}