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
using Ab3d.Cameras;
using Ab3d.Common;
using Ab3d.DirectX;
using Ab3d.DirectX.Models;
using Ab3d.Utilities;
using Ab3d.Visuals;
using SharpDX;
using Color = System.Windows.Media.Color;


namespace Ab3d.DXEngine.Wpf.Samples.DXEnginePerformance
{
    /// <summary>
    /// Interaction logic for InstancedTextNodePerformanceSample.xaml
    /// </summary>
    public partial class InstancedTextNodePerformanceSample : Page
    {
        private InstancedTextNode _instancedTextNode;
        
        public InstancedTextNodePerformanceSample()
        {
            InitializeComponent();


            AlphaClipThresholdInfoControl.InfoText =
@"AlphaClipThreshold is used to correctly render the textures with rendered characters and transparent background. It specifies at which alpha value the pixels will be clipped (skipped from rendering and their depth will not be written to the depth buffer).

When set to 0, then alpha-clipping is disabled and in this case the characters may not be rendered correctly.
Default value is 0.15.

See 'Improved visuals / Alpha clipping' sample and comments in its code for more info.";

            var coloredAxisVisual3D = new ColoredAxisVisual3D();
            MainViewport.Children.Add(coloredAxisVisual3D);

            this.Loaded += delegate(object sender, RoutedEventArgs args)
            {
                ShowCurrentDemo();
            };

            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                DisposeInstancedTextNodes();

                MainDXViewportView.Dispose();
            };
        }
        
        private void CreateInstanceText(InstancedTextNode instancedTextNode, Point3D centerPosition, Size3D size, int xCount, int yCount, int zCount, Color textColor, double textSize, string stringFormat = "({0:0} {1:0} {2:0})")
        {
            float xStep = xCount <= 1 ? 0 : (float)(size.X / (xCount - 1));
            float yStep = yCount <= 1 ? 0 : (float)(size.Y / (yCount - 1));
            float zStep = zCount <= 1 ? 0 : (float)(size.Z / (zCount - 1));

            for (int z = 0; z < zCount; z++)
            {
                float zPos = (float)(centerPosition.Z - (size.Z / 2.0) + (z * zStep));

                for (int y = 0; y < yCount; y++)
                {
                    float yPos = (float)(centerPosition.Y - (size.Y / 2.0) + (y * yStep));

                    for (int x = 0; x < xCount; x++)
                    {
                        float xPos = (float)(centerPosition.X - (size.X / 2.0) + (x * xStep));

                        string infoText = string.Format(stringFormat, xPos, yPos, zPos);

                        instancedTextNode.AddText(infoText, textColor, new Point3D(xPos, yPos, zPos), textSize, hasBackSide: true);
                    }
                }
            }
        }

        private void UpdateCharactersCountInfo()
        {
            int charactersCount = _instancedTextNode.CharactersCount;
            CharactersCountTextBlock.Text = string.Format(System.Globalization.CultureInfo.InvariantCulture, "Chars count: {0:#,##0}", charactersCount);
        }

        private void SceneTypeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            ShowCurrentDemo();
        }

        private void ShowCurrentDemo()
        {
            MainViewport.Children.Clear();
            DisposeInstancedTextNodes();

            InfoTextBox.Text = "";
            InfoTextBox.Visibility = Visibility.Collapsed;


            _instancedTextNode = new InstancedTextNode(new FontFamily("Consolas"), FontWeights.Normal, fontBitmapSize: 128);

            // Reset direction
            _instancedTextNode.SetTextDirection(textDirection: new Vector3D(1, 0, 0), upDirection: new Vector3D(0, 1, 0));


            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                if (SceneTypeComboBox.SelectedIndex == 0)
                    CreateInstanceText(_instancedTextNode, centerPosition: new Point3D(0, 0, 0), size: new Size3D(1000, 500, 2000), xCount: 10, yCount: 20, zCount: 40, textColor: Colors.Black, textSize: 10);
                else if (SceneTypeComboBox.SelectedIndex == 1)
                    CreateInstanceText(_instancedTextNode, centerPosition: new Point3D(0, 0, 0), size: new Size3D(2000, 2000, 2000), xCount: 20, yCount: 100, zCount: 100, textColor: Colors.Black, textSize: 10);
                else if (SceneTypeComboBox.SelectedIndex == 2)
                    CreateInstanceText(_instancedTextNode, centerPosition: new Point3D(0, 0, 0), size: new Size3D(2000, 2000, 10000), xCount: 20, yCount: 100, zCount: 500, textColor: Colors.Black, textSize: 10);

                var sceneNodeVisual1 = new SceneNodeVisual3D(_instancedTextNode);
                MainViewport.Children.Add(sceneNodeVisual1);


                Camera1.Heading        = -8.2881686066695;
                Camera1.Attitude       = 3.35596244333162;
                Camera1.Distance       = 1131.38948539394;
                Camera1.TargetPosition = new Point3D(67.7795992281885, 14.1717311898692, 1.24683504967857);

                UpdateCharactersCountInfo();
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void ShowReportButton_OnClick(object sender, RoutedEventArgs e)
        {
            InfoTextBox.Text = _instancedTextNode.GetReport();
            InfoTextBox.Visibility = Visibility.Visible;
        }

        private void AlphaClipThresholdSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_instancedTextNode == null)
                return;

            var newValue = (float)AlphaClipThresholdSlider.Value;

            if (MathUtils.IsZero(newValue))
                AlphaClipThresholdValueTextBlock.Text = "disabled";
            else
                AlphaClipThresholdValueTextBlock.Text = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.00}", newValue);

            
            _instancedTextNode.AlphaClipThreshold = newValue;

            MainDXViewportView.Refresh();
        }

        private void DisposeInstancedTextNodes()
        {
            if (_instancedTextNode != null)
            {
                _instancedTextNode.Dispose();
                _instancedTextNode = null;
            }
        }
    }
}
