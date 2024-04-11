using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using Ab3d.Common.Cameras;
using Ab3d.DirectX;
using Ab3d.DirectX.Client.Settings;
using Ab3d.DirectX.Effects;
using Ab3d.DirectX.Materials;
using Ab3d.Visuals;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using Color = SharpDX.Color;

namespace Ab3d.DXEngine.Wpf.Samples.DXEnginePerformance
{
    /// <summary>
    /// Interaction logic for BillboardsSample.xaml
    /// </summary>
    public partial class BillboardsSample : Page
    {
        private double[,] _heightData;
        private int _xDataDimension, _yDataDimension;
        private ShaderResourceView _pixelTexture;
        private Size2 _pixelTextureSize;
        private PixelsVisual3D _pixelsVisual3D;
        private float[] _savedPixelSizes;

        public BillboardsSample()
        {
            InitializeComponent();

            MainDXViewportView.DXSceneInitialized += delegate (object sender, EventArgs args)
            {
                CreateScene();
            };

            this.Unloaded += delegate (object sender, RoutedEventArgs args)
            {
                MainDXViewportView.Dispose();
            };
        }

        private void CreateScene()
        {
            if (MainDXViewportView.DXScene == null)
                return; // Not yet initialized or using WPF 3D

            Mouse.OverrideCursor = Cursors.Wait;

            AddHeightMap();

            AddRandomTrees(15000);

            Mouse.OverrideCursor = null;
        }

        private void AddRandomTrees(int tresCount)
        {
            var rnd = new Random();

            var positions = new Vector3[tresCount];
            var pixelSizes = new float[tresCount];

            float xFactor = (float)HeightMap1.Size.X;
            float yFactor = (float)HeightMap1.Size.Y;
            float zFactor = (float)HeightMap1.Size.Z;

            float xOffset = xFactor * -0.5f;
            float yOffset = 1;
            float zOffset = zFactor * -0.5f;

            float maxTreeHeight = 0.8f;
            float forestHeight = 0.5f;

            float pixelSize = 2;

            for (int i = 0; i < tresCount; i++)
            {
                var x = (float)rnd.NextDouble();
                var z = (float)rnd.NextDouble();
                var y = (float)GetHeight(x, z);

                if (y > maxTreeHeight)
                {
                    // Skip adding tree above the maxTreeHeight
                    i--;
                    continue;
                }


                float treeSize;

                if (y > forestHeight)
                {
                    // Trees between forestHeight and maxTreeHeight are less common as the height increases
                    // Higher the tree, less probable it is to show it.
                    // Also the size is smaller as the height increases.
                    float percent = (y - forestHeight) / (maxTreeHeight - forestHeight);
                    if (rnd.NextDouble() < percent)
                    {
                        // skip this tree
                        i--;
                        continue;
                    }

                    treeSize = Math.Max(0.3f, 1 - percent);
                }
                else
                {
                    treeSize = (float)rnd.NextDouble() * 1.0f + 0.5f; // random between 0.5 to 1.5
                }

                positions[i] = new Vector3(x * xFactor + xOffset,
                                           y * yFactor + yOffset + (treeSize - 1) * 0.5f * pixelSize,
                                           z * zFactor + zOffset);

                pixelSizes[i] = treeSize;
            }


            if (_pixelsVisual3D == null)
            {
                _pixelsVisual3D = new PixelsVisual3D(positions)
                {
                    PixelSize = pixelSize,
                    IsWorldSize = true,
                    PixelSizes = pixelSizes
                };

                // Load ShaderResourceView from a WPF's Resource:
                //var warningBitmap = new BitmapImage(new Uri("pack://application:,,,/Ab3d.DXEngine.Wpf.Samples;component/Resources/warningIcon.png", UriKind.Absolute));
                //_pixelTexture = WpfMaterial.CreateTexture2D(MainDXViewportView.DXScene.DXDevice, warningBitmap);
                //_pixelTextureSize = new Size2(warningBitmap.PixelWidth, warningBitmap.PixelHeight);

                // To load ShaderResourceView from a file, use the following
                string fileName =
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources/TreeTexture.png");
                _pixelTexture = TextureLoader.LoadShaderResourceView(MainDXViewportView.DXScene.DXDevice.Device,
                    fileName, out TextureInfo textureInfo);
                _pixelTextureSize = new Size2(textureInfo.Width, textureInfo.Height);

                _pixelsVisual3D.SetTexture(_pixelTexture, _pixelTextureSize);

                MainViewport.Children.Add(_pixelsVisual3D);
            }
            else
            {
                // When the number of items in the Positions array is changed, then we need to use the ChangePositionArrays method
                // instead of simply setting the Positions and PixelSizes properties.
                // The reason is that after changing the Positions property the PixelSizes would still have the 
                _pixelsVisual3D.ChangePositionArrays(positions, pixelSizes: pixelSizes);

                //_pixelsVisual3D.PixelSizes = pixelSizes;
                //_pixelsVisual3D.Positions = positions;
            }


            _savedPixelSizes = null;
            HideShowButton.Content = "Hide trees at higher altitude";
        }

        private double GetHeight(double x, double y)
        {
            if (x < 0 || y < 0 || x > 1 || y > 1 || _heightData == null)
                return 0;

            double xPos = x * _xDataDimension;
            double yPos = y * _yDataDimension;

            double xSubPosition = xPos - Math.Floor(xPos);
            int xIndex1 = (int)xPos;
            
            double ySubPosition = yPos - Math.Floor(yPos);
            int yIndex1 = (int)yPos;

            int xIndex2 = (xIndex1 >= _xDataDimension - 1) ? _xDataDimension - 1 : xIndex1 + 1;
            int yIndex2 = (yIndex1 >= _yDataDimension - 1) ? _yDataDimension - 1 : yIndex1 + 1;

            double v00 = _heightData[xIndex1, yIndex1];
            double v10 = _heightData[xIndex2, yIndex1];
            double v11 = _heightData[xIndex2, yIndex2];
            double v01 = _heightData[xIndex1, yIndex2];

            // bilinear interpolation
            // https://math.stackexchange.com/questions/3230376/interpolate-between-4-points-on-a-2d-plane

            double value = (1 - xSubPosition) * (1 - ySubPosition) * v00 + 
                           xSubPosition * (1 - ySubPosition) * v10 +
                           (1 - xSubPosition) * ySubPosition * v01 + 
                           xSubPosition * ySubPosition * v11;

            return value;
        }

        private void AddHeightMap()
        {
            var heightMapFileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources\\HeightMaps\\simpleHeightMap.png");

            BitmapImage heightImage = new BitmapImage(new Uri(heightMapFileName, UriKind.RelativeOrAbsolute));

            // Create height data from bitmap
            // Returns value in range from 0 to 1
            _heightData = OpenHeightMapDataFile(heightImage, invertData: false);

            if (_heightData != null)
            {
                HeightMap1.HeightData = _heightData;

                _xDataDimension = _heightData.GetUpperBound(0);
                _yDataDimension = _heightData.GetUpperBound(1);
                
                GradientStopCollection stops = new GradientStopCollection();
                stops.Add(new GradientStop(Colors.White, 1));
                stops.Add(new GradientStop(Colors.Gray, 0.9));
                stops.Add(new GradientStop(Colors.LightGreen, 0.80));
                stops.Add(new GradientStop(Colors.LightGreen, 0.7));
                stops.Add(new GradientStop(Colors.SandyBrown, 0.65));
                stops.Add(new GradientStop(Colors.SandyBrown, 0));

                // NOTE: We do not have to specify the StartPoint and EndPoint
                // It will be used in the CreateHeightTextureFromGradient method 
                // to create the texture from the actual height map data and with the specified LinearGradientBrush
                LinearGradientBrush gradient = new LinearGradientBrush(stops);

                // When using gradient texture, we get better results when UseHeightValuesAsTextureCoordinates is true.
                // In this case height values are used for texture coordinates - texture coordinate (0, 0.5) is set the minimum height value and texture coordinate (1, 0.5) is set to the maximum height value.
                // This requires a one dimensional gradient texture and usually produces more accurate results than when UseHeightValuesAsTextureCoordinates is false.
                // This should not be used for cases when a bitmap is shown on the height map.
                // See HeightMapSample for more info.
                // Set this value to false to see the difference.
                HeightMap1.UseHeightValuesAsTextureCoordinates = true;

                HeightMap1.CreateHeightTextureFromGradient(gradient);
            }
        }

        public static double[,] OpenHeightMapDataFile(BitmapSource heightImage, bool invertData)
        {
            var width = heightImage.PixelWidth;
            var height = heightImage.PixelHeight;
            var bytesPerPixel = heightImage.Format.BitsPerPixel / 8;

            byte[] heightImageArray = new byte[width * height * bytesPerPixel];
            heightImage.CopyPixels(heightImageArray, width * bytesPerPixel, 0);

            double[,] heightData = new double[width, height];

            double factor = 1.0 / (255.0 * bytesPerPixel); // this will be used to multiply the bytes (multiplying is faster than dividing)
            double offset = 0;

            if (invertData)
            {
                factor = -factor;
                offset = 1;
            }


            int index = 0;

            if (bytesPerPixel == 1) // optimize for 8-bit (one byte) per pixel (remove inner for)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        heightData[x, y] = heightImageArray[index] * factor + offset;
                        index++;
                    }
                }
            }
            else
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int colorsSum = 0;
                        for (int i = 0; i < bytesPerPixel; i++)
                            colorsSum += heightImageArray[index + i];

                        heightData[x, y] = colorsSum * factor + offset;

                        index += bytesPerPixel;
                    }
                }
            }

            return heightData;
        }

        private void TreesCountComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            var selectedItem = (ComboBoxItem)TreesCountComboBox.SelectedItem;
            var selectedText = (string)selectedItem.Content;
           selectedText = selectedText.Replace(",", "");

           int treesCount = Int32.Parse(selectedText);

           AddRandomTrees(treesCount);
        }

        private void OnFixUpVectorCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            if (FixUpVectorCheckBox.IsChecked ?? false)
                _pixelsVisual3D.SetFixedUpVector(new Vector3(0, 1, 0));
            else
                _pixelsVisual3D.ResetFixedUpVector();
        }

        private void HideShowButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_savedPixelSizes != null)
            {
                // Show hidden pixels
                _pixelsVisual3D.PixelSizes = _savedPixelSizes;
                _savedPixelSizes = null;

                HideShowButton.Content = "Hide trees at higher altitude";
            }
            else
            {
                var pixelSizes = _pixelsVisual3D.PixelSizes;
                var positions = _pixelsVisual3D.Positions;
                int count = pixelSizes.Length;

                // Copy existing sizes to _savedPixelSizes
                _savedPixelSizes = new float[count];
                Array.Copy(pixelSizes, _savedPixelSizes, count);

                float minHeight = 0.55f * (float)HeightMap1.Size.Y;

                for (int i = 0; i < count; i++)
                {
                    if (positions[i].Y > minHeight)
                        pixelSizes[i] = 0;
                }

                _pixelsVisual3D.UpdatePixelSizes();

                HideShowButton.Content = "Show trees at higher altitude";
            }

            MainDXViewportView.Refresh();
        }
    }
}
