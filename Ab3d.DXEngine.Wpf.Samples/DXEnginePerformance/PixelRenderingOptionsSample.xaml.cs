using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using Ab3d.DirectX.Effects;
using Ab3d.DirectX.Materials;
using Ab3d.Visuals;
using SharpDX;
using SharpDX.Direct3D;

namespace Ab3d.DXEngine.Wpf.Samples.DXEnginePerformance
{
    /// <summary>
    /// Interaction logic for PixelRenderingOptionsSample.xaml
    /// </summary>
    public partial class PixelRenderingOptionsSample : Page
    {
        private DisposeList _disposables;

        public PixelRenderingOptionsSample()
        {
            InitializeComponent();

            _disposables = new DisposeList();

            CreateScene();

            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                _disposables.Dispose();
                MainDXViewportView.Dispose();
            };
        }

        private void CreateScene()
        {
            int pixelsXCount = 20;
            var positionsArray = PixelRenderingSample.CreatePositionsArray(new Point3D(0, 0, 0), new Size3D(80, 40, 80), pixelsXCount, pixelsXCount / 2, pixelsXCount);

            int pixelsCount = positionsArray.Length;
            var pixelColors = new Color4[pixelsCount];
            for (int i = 0; i < pixelsCount; i++)
            {
                float green = (float)i / (float)pixelsCount;
                pixelColors[i] = new Color4(new Color3((float)(i % pixelsXCount) / (float)pixelsXCount, green, 1.0f - green));
            }

            var pixelsVisual3DWithColors = new PixelsVisual3D(positionsArray);
            pixelsVisual3DWithColors.PixelColors = pixelColors;
            pixelsVisual3DWithColors.PixelColor  = System.Windows.Media.Colors.White; // White (255,255,255) means that no color mask is used and the color specified in PixelColors are used (this is also the default value and is set here only for demonstration purpose)
            pixelsVisual3DWithColors.PixelSize   = 2.0f;                              // Default pixel size

            pixelsVisual3DWithColors.Transform = new TranslateTransform3D(0, 20, -200);

            MainViewport.Children.Add(pixelsVisual3DWithColors);

            // !!! IMPORTANT !!!
            // When PixelsVisual3D is not used any more, it needs to be disposed (we are using DisposeList to dispose all in Unloaded event handler)
            _disposables.Add(pixelsVisual3DWithColors);



            var pixelsVisual3DWithColorsAndColorFilter = new PixelsVisual3D(positionsArray);
            pixelsVisual3DWithColorsAndColorFilter.PixelColors = pixelColors;
            pixelsVisual3DWithColorsAndColorFilter.PixelColor  = System.Windows.Media.Color.FromRgb(0, 255, 0); // color mask is multiplied with each color defined in PixelColors
            pixelsVisual3DWithColorsAndColorFilter.PixelSize   = 2.0f;

            pixelsVisual3DWithColorsAndColorFilter.Transform = new TranslateTransform3D(0, 20, -100);

            MainViewport.Children.Add(pixelsVisual3DWithColorsAndColorFilter);

            _disposables.Add(pixelsVisual3DWithColorsAndColorFilter);


            var pixelSizes = new float[pixelsCount];
            for (int i = 0; i < pixelsCount; i++)
            {
                float percent = (float)i / (float)pixelsCount;
                pixelSizes[i] = (1f - percent) * 4f;
            }

            var pixelsVisual3DWithSize = new PixelsVisual3D(positionsArray);
            pixelsVisual3DWithSize.PixelSizes = pixelSizes;
            pixelsVisual3DWithSize.PixelSize = 1.0f;
            pixelsVisual3DWithSize.PixelColor = System.Windows.Media.Colors.Orange;

            pixelsVisual3DWithSize.Transform = new TranslateTransform3D(0, 20, 0);

            MainViewport.Children.Add(pixelsVisual3DWithSize);

            _disposables.Add(pixelsVisual3DWithSize);


            var pixelsVisual3DWithSizeAndPixelSizeFactor = new PixelsVisual3D(positionsArray);
            pixelsVisual3DWithSizeAndPixelSizeFactor.PixelSizes = pixelSizes;
            pixelsVisual3DWithSizeAndPixelSizeFactor.PixelSize = 3f;
            pixelsVisual3DWithSizeAndPixelSizeFactor.PixelColor = System.Windows.Media.Colors.Orange;

            pixelsVisual3DWithSizeAndPixelSizeFactor.Transform = new TranslateTransform3D(0, 20, 100);

            MainViewport.Children.Add(pixelsVisual3DWithSizeAndPixelSizeFactor);

            _disposables.Add(pixelsVisual3DWithSizeAndPixelSizeFactor);


            var pixelsVisual3DWithColorAndSize = new PixelsVisual3D(positionsArray);
            pixelsVisual3DWithColorAndSize.PixelColors = pixelColors;
            pixelsVisual3DWithColorAndSize.PixelSizes = pixelSizes;
            pixelsVisual3DWithColorAndSize.PixelSize = 1.0f;
            pixelsVisual3DWithColorAndSize.PixelColor = System.Windows.Media.Colors.White;

            pixelsVisual3DWithColorAndSize.Transform = new TranslateTransform3D(0, 20, 200);

            MainViewport.Children.Add(pixelsVisual3DWithColorAndSize);

            _disposables.Add(pixelsVisual3DWithColorAndSize);
        }
    }
}
