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


// TODO:
// - Add support for back faces - add List<int> _backFacedInstances. - if Count = 0 - no back face; if Count the same as frontFaced, then the same instanceBuffer can be used; otherwise create a new InstanceBuffer for back faces
// - Add support for updating InstancedText

namespace Ab3d.DXEngine.Wpf.Samples.DXEnginePerformance
{
    /// <summary>
    /// Interaction logic for InstancedTextNodeSample.xaml
    /// </summary>
    public partial class InstancedTextNodeSample : Page
    {
        private InstancedTextNode _instancedTextNode;
        private InstancedTextNode _instancedTextNode2;
        
        private InstancedText _instancedText;

        private int _addedTextCount;

        public InstancedTextNodeSample()
        {
            InitializeComponent();


            AlphaClipThresholdInfoControl.InfoText =
@"AlphaClipThreshold is used to correctly render the textures with rendered characters and transparent background. It specifies at which alpha value the pixels will be clipped (skipped from rendering and their depth will not be written to the depth buffer).

When set to 0, then alpha-clipping is disabled and in this case the characters may not be rendered correctly.
Default value is 0.15.

See 'Improved visuals / Alpha clipping' sample and comments in its code for more info.";

            CreateSimpleDemoScene();

            var coloredAxisVisual3D = new ColoredAxisVisual3D();
            MainViewport.Children.Add(coloredAxisVisual3D);

            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                DisposeInstancedTextNodes();

                MainDXViewportView.Dispose();
            };
        }


        private void CreateSimpleDemoScene()
        {
            // When using Ab3d.PowerToys and Ab3d.DXEngine, then the standard way to render text is to use TextBlockVisual3D.
            // It provides many options and easy positioning with Position / PositionType properties. See sample in the Ab3d.PowerToys samples project for more info.
            //
            // Here we also show how to setup the alpha-clip threshold that can be used when rendering with Ab3d.DXEngine.
            // This way we do not need to sort the TextBlockVisual3D by their distance to render them correctly.

            var textBlockVisual3D = new TextBlockVisual3D()
            {
                Text                   = "TextBlockVisual3D",
                Position               = new Point3D(-190, -50, 0),
                PositionType           = PositionTypes.BottomLeft,
                TextDirection          = new Vector3D(1, 0, 0),
                UpDirection            = new Vector3D(0, 1, 0),
                Size                   = new Size(80, 40),
                Background             = Brushes.Transparent,
                RenderBitmapSize       = new Size(256, 128),
                TextPadding            = new Thickness(5, 0, 5, 0),
                BorderBrush            = Brushes.Yellow,
                BorderThickness        = new Thickness(0, 2, 0, 2),
                IsBackSidedTextFlipped = true
            };

            // Because TextBlockVisual3D usually use transparent background the rules and limitations of rendering transparent objects apply when using multiple TextBlockVisual3D objects (see Alpha clipping sample for more info).
            // With Ab3d.DXEngine a very fast transparent objects sorting can be enabled by:
            // MainDXViewportView.DXScene.IsTransparencySortingEnabled
            // 
            // But instead of using transparency sorting, we enable alpha-clipping (see Alpha clipping sample for more info):
            textBlockVisual3D.SetDXAttribute(DXAttributeType.Texture_AlphaClipThreshold, 0.15f);

            MainViewport.Children.Add(textBlockVisual3D);


            // It is also possible to show 3D text with using TextVisual3D.
            // This objects renders text with using 3D lines.
            // Its disadvantage is that it supports only an old style font that was used for plotters and cannot render all the characters.


            // TextBlockVisual3D renders the text with first rendering the text with the specified border to a texture.
            // This this texture is rendered into a plane 3D object.
            // This works very well when there are not a lot of texts.
            // But if many texts need to be rendered, then the it takes long to render all texts to textures and also they take a lot of memory.
            // 
            // In this case the InstancedTextNode can be used because it only rendered individual characters and then reuses 
            // the textures with rendered characters to show the texts. To make rendering even more efficient object instancing is used.
            //
            //
            // The first set in using the InstancedTextNode is to create its instance.
            //
            // There we define the FontFamily and FontWeight that will be the same for all added texts.
            //
            // We also define the size of the texture that will be used to render the characters.
            // It is recommended to use size that is power of 2 - for example 64, 128, 256, etc.
            // By default the fontBitmapSize is set to 128 that rendered characters to 128 x 128 texture.
            //
            // We can also set the useDynamicInstanceBuffer to true. This would create dynamic instance buffer.
            // This is recommended when the text data is changed very ofter (color, position or visibility is changed in each frame or similar).
            // Here we change data only occasionally, so preserve the useDynamicInstanceBuffer in its default value (this is better for GPU access to the buffer)
            _instancedTextNode = new InstancedTextNode(fontFamily: new FontFamily("Arial"), 
                                                       fontWeight: FontWeights.Normal, 
                                                       fontBitmapSize: 128, 
                                                       useDynamicInstanceBuffer: false);


            // Than we can call AddText to add individual text to the InstancedTextNode.
            // Note that to be able to show text from the back side, we need to set hasBackSide to true (this rendered twice as many objects).
            // AddText method returns an instance of InstancedText object that can be used to change the color, position, show or hide text.
            _instancedText = _instancedTextNode.AddText("Ab3d.DXEngine", Colors.Orange, new Point3D(-190, 0, 0), size: 25, hasBackSide: true);

            // To change direction and orientation of text, we can call the SetTextDirection method.
            _instancedTextNode.SetTextDirection(textDirection: new Vector3D(1, 0, 0), upDirection: new Vector3D(0, 0, -1));

            // All characters from font are supported. Also new line is supported.
            _instancedTextNode.AddText("All chars:\n@üßščžç☯", Colors.Black, new Point3D(-100, 1, 50), size: 30, hasBackSide: true);


            _instancedTextNode.SetTextDirection(textDirection: new Vector3D(1, 0, 0), upDirection: new Vector3D(0, 1, 0));
            _instancedTextNode.AddText("Right->", Colors.Red, new Point3D(0, 30, 0), size: 30, hasBackSide: true);

            _instancedTextNode.SetTextDirection(textDirection: new Vector3D(0, 0, -1), upDirection: new Vector3D(0, 1, 0));
            _instancedTextNode.AddText("Forward->", Colors.Blue, new Point3D(0, 30, 0), size: 30, hasBackSide: true);

            _instancedTextNode.SetTextDirection(textDirection: new Vector3D(0, 1, 0), upDirection: new Vector3D(-1, 0, 0));
            _instancedTextNode.AddText("UP->", Colors.Green, new Point3D(0, 70, 0), size: 30, hasBackSide: true);


            // Sample on how to create text that is flipped on the back side (so it can be correctly read from the back side)
            var startPosition = new Point3D(10, 0, 0);
            _instancedTextNode.SetTextDirection(textDirection: new Vector3D(1, 0, 0), upDirection: new Vector3D(0, 1, 0));
            var flippedFrontFaceInstancedText = _instancedTextNode.AddText("FlippedBackSideText", Colors.Green, startPosition, size: 25, hasBackSide: false);

            // Based on size of previous text (instancedText.TextBounds), we can calculate the start position for the flipped back face text:
            var backFaceStartPosition = new Point3D(startPosition.X + flippedFrontFaceInstancedText.TextBounds.SizeX, startPosition.Y, startPosition.Z);
            _instancedTextNode.SetTextDirection(textDirection: new Vector3D(-1, 0, 0), upDirection: new Vector3D(0, 1, 0)); // flip textDirection, preserve upDirection
            var flippedBackFaceInstancedText = _instancedTextNode.AddText(flippedFrontFaceInstancedText.Text, flippedFrontFaceInstancedText.Color, backFaceStartPosition, size: 25, hasBackSide: false);


            // It is also possible to call AddText that takes transformation matrix instead of position and scale.
            // This method does not use current text direction that is set with SetTextDirection:

            var transform3DGroup = new Transform3DGroup();
            transform3DGroup.Children.Add(new ScaleTransform3D(20, 70, 20)); // Note that initially the font size is 1, so we need to scale it !!!
            transform3DGroup.Children.Add(new TranslateTransform3D(-120, -120, 0));

            _instancedTextNode.AddText("Custom transform", Colors.Gray, transform3DGroup.Value, hasBackSide: true);


            // If we want to immediately create character textures, we can call the InitializeResources method.
            // Note that MainDXViewportView.DXScene must not be null (this can be called in MainDXViewportView.DXSceneDeviceCreated or MainDXViewportView.DXSceneInitialized event handler)
            //_instancedTextNode.InitializeResources(MainDXViewportView.DXScene);

            
            // Show the InstancedTextNode as any other DXEngine's SceneNode:
            var sceneNodeVisual1 = new SceneNodeVisual3D(_instancedTextNode);
            MainViewport.Children.Add(sceneNodeVisual1);


            // To show text with other font or with other font weight, we need to create another InstancedTextNode
            _instancedTextNode2 = new InstancedTextNode(new FontFamily("Times New Roman"), FontWeights.Bold, fontBitmapSize: 128);
            _instancedTextNode2.AddText("Text with any font", Colors.Gold, new Point3D(100, -100, 0), 30, true);

            var sceneNodeVisual2 = new SceneNodeVisual3D(_instancedTextNode2);
            MainViewport.Children.Add(sceneNodeVisual2);


            SetupDemoSceneButtons(isDemoSceneShown: true);

            UpdateCharactersCountInfo();
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

        private void SetupDemoSceneButtons(bool isDemoSceneShown)
        {
            ChangeColorButton.IsEnabled    = isDemoSceneShown;
            ChangePositionButton.IsEnabled = isDemoSceneShown;
            ShowHideButton.IsEnabled       = isDemoSceneShown;
            AddTextButton.IsEnabled        = isDemoSceneShown;
        }

        private void UpdateCharactersCountInfo()
        {
            int charactersCount = _instancedTextNode.CharactersCount;
            if (_instancedTextNode2 != null)
                charactersCount += _instancedTextNode2.CharactersCount;

            CharactersCountTextBlock.Text = string.Format("Chars count: {0:#,##0}", charactersCount);
        }

        private void SceneTypeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;


            MainViewport.Children.Clear();
            DisposeInstancedTextNodes();

            InfoTextBox.Text = "";
            InfoTextBox.Visibility = Visibility.Collapsed;


            if (SceneTypeComboBox.SelectedIndex == 0)
            {
                CreateSimpleDemoScene();

                Camera1.Heading        = 30;
                Camera1.Attitude       = -20;
                Camera1.Distance       = 600;
                Camera1.TargetPosition = new Point3D(0, 0, 0);
            }
            else
            {
                _instancedTextNode = new InstancedTextNode(new FontFamily("Consolas"), FontWeights.Normal, fontBitmapSize: 128);

                // Reset direction
                _instancedTextNode.SetTextDirection(textDirection: new Vector3D(1, 0, 0), upDirection: new Vector3D(0, 1, 0));


                try
                {
                    Mouse.OverrideCursor = Cursors.Wait;

                    if (SceneTypeComboBox.SelectedIndex == 1)
                        CreateInstanceText(_instancedTextNode, centerPosition: new Point3D(0, 0, 0), size: new Size3D(1000, 500, 2000), xCount: 10, yCount: 20, zCount: 40, textColor: Colors.Black, textSize: 10);
                    else if (SceneTypeComboBox.SelectedIndex == 2)
                        CreateInstanceText(_instancedTextNode, centerPosition: new Point3D(0, 0, 0), size: new Size3D(2000, 2000, 2000), xCount: 20, yCount: 100, zCount: 100, textColor: Colors.Black, textSize: 10);
                    else if (SceneTypeComboBox.SelectedIndex == 3)
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


                SetupDemoSceneButtons(isDemoSceneShown: false);
            }
        }

        private void ChangeColorButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_instancedText == null)
                return;

            if (_instancedText.Color == Colors.Orange)
                _instancedText.ChangeColor(Colors.Red);
            else
                _instancedText.ChangeColor(Colors.Orange);
        }

        private void ChangePositionButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_instancedText == null)
                return;

            _instancedText.Move(new Vector3D(0, 10, 0));
        }
        
        private void ShowHideButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_instancedText == null)
                return;

            if (_instancedText.IsVisible)
            {
                _instancedText.Hide();
                ShowHideButton.Content = "Show";
            }
            else
            {
                _instancedText.Show();
                ShowHideButton.Content = "Hide";
            }
        }
        
        private void AddTextButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_instancedText == null)
                return;


            _addedTextCount++;

            _instancedTextNode.SetTextDirection(textDirection: new Vector3D(1, 0, 0), upDirection: new Vector3D(0, 1, 0));
            _instancedTextNode.AddText("Added text " + _addedTextCount.ToString(), Colors.Black, new Point3D(-190, 50 + _addedTextCount * 10, -5), size: 10, hasBackSide: true);

            UpdateCharactersCountInfo();
        }

        private void ShowReportButton_OnClick(object sender, RoutedEventArgs e)
        {
            string reportText = _instancedTextNode.GetReport();

            if (_instancedTextNode2 != null)
                reportText += "\r\n\r\n" + _instancedTextNode2.GetReport(orderByNumberOfInstances: false);

            InfoTextBox.Text = reportText;

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

            if (_instancedTextNode2 != null)
            {
                _instancedTextNode2.Dispose();
                _instancedTextNode2 = null;
            }
        }
    }
}
