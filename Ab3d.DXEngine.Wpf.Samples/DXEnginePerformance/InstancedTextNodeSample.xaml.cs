using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Ab3d.Common;
using Ab3d.DirectX;
using Ab3d.DirectX.Models;
using Ab3d.Visuals;

#if SHARPDX
using SharpDX;
using Matrix = SharpDX.Matrix;
#endif


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

        private Matrix3D _instancedTextInitialWorldMatrix;
        private int _rotationsCount;

        private List<InstancedText> _addedTexts;

        public InstancedTextNodeSample()
        {
            InitializeComponent();

            var coloredAxisVisual3D = new ColoredAxisVisual3D();
            MainViewport.Children.Add(coloredAxisVisual3D);

            this.Loaded += delegate(object sender, RoutedEventArgs args)
            {
                CreateSimpleDemoScene();
            };

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
                Position               = new Point3D(20, 50, 0),
                PositionType           = PositionTypes.BottomLeft,
                TextDirection          = new Vector3D(1, 0, 0),
                UpDirection            = new Vector3D(0, 1, 0),
                Size                   = new Size(120, 40),
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
            _instancedText = _instancedTextNode.AddText("Ab3d.DXEngine", Colors.Orange, new Point3D(-210, 0, 0), size: 25, hasBackSide: true);
            _instancedTextInitialWorldMatrix = _instancedText.WorldMatrix;

            /*
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
            */

            // Show the InstancedTextNode as any other DXEngine's SceneNode:
            var sceneNodeVisual1 = new SceneNodeVisual3D(_instancedTextNode);
            MainViewport.Children.Add(sceneNodeVisual1);


            // To show text with other font or with other font weight, we need to create another InstancedTextNode
            _instancedTextNode2 = new InstancedTextNode(new FontFamily("Times New Roman"), FontWeights.Bold, fontBitmapSize: 128);
            _instancedTextNode2.AddText("Text with any font", Colors.Gold, new Point3D(30, 10, 0), 30, true);

            var sceneNodeVisual2 = new SceneNodeVisual3D(_instancedTextNode2);
            MainViewport.Children.Add(sceneNodeVisual2);

            _addedTexts = new List<InstancedText>();
        }

        private void ChangeTextButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_instancedText == null)
                return;

            string currentText = _instancedText.Text;

            char newEndChar;
            char lastChar = _instancedText.Text[_instancedText.Text.Length - 1];

            switch (lastChar)
            {
                case '/':
                    newEndChar = '-';
                    break;
                
                case '-':
                    newEndChar = '\\';
                    break;
                
                case '\\':
                    newEndChar = '|';
                    break;
                
                case '|':
                    newEndChar = '/';
                    break;
                
                default:
                    newEndChar = '/';
                    currentText += "  "; // No animated char yet - add 2 spaces so that last space will be replaced by the animated char
                    break;
            }

            string newText = currentText.Substring(0, currentText.Length - 1) + newEndChar;

            _instancedText.ChangeText(newText);
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
        
        private void ChangeOrientationButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_instancedText == null)
                return;

            _rotationsCount++;
            
            // Create new WorldMatrix

            // Use Matrix.RotationAxis to create new rotation matrix without creating new objects (as below in the commented code)
            var rotationAxis = Matrix.RotationAxis(new Vector3(0, 1, 0), MathUtil.DegreesToRadians(_rotationsCount * 30));

            // Multiply the rotation by the initial WorldMatrix for this text
            var newWorldMatrix = rotationAxis.ToWpfMatrix3D() * _instancedTextInitialWorldMatrix;

            // WPF's way to create rotation matrix (this creates 2 objects each time):
            //var rotateTransform3D = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), _rotationsCount * 30));
            //var newWorldMatrix = rotateTransform3D.Value * _instancedTextInitialWorldMatrix;
            
            _instancedText.SetWorldMatrix(newWorldMatrix);
        }
        
        private void AlignWithCameraButton_OnClick(object sender, RoutedEventArgs e)
        {
            // To align text with camera, we first need to get the vectors that define the camera's orientation:
            Vector3D planeNormalVector3D, widthVector3D, heightVector3D;
            Camera1.GetCameraPlaneOrientation(out planeNormalVector3D, out widthVector3D, out heightVector3D);

            // Then we can call the SetOrientation method:
            //_instancedText.SetOrientation(widthVector3D, heightVector3D);

            // If we already have the text normal vector (planeNormalVector3D), it is faster to call the SetOrientation that also takes that.
            // We will also need textSize. If we do not have it, we can calculate it from the current WorldMatrix:
            var textSize = _instancedText.GetTextSize();

            // Call SetOrientation
            _instancedText.SetOrientation(ref widthVector3D, ref heightVector3D, ref planeNormalVector3D, textSize);

            // Here is the full code to set the new WorldMatrix from the orientation vectors and text size:
            //var currentMatrix = _instancedText.WorldMatrix;
            //var textSize = new Vector3D(currentMatrix.M11, currentMatrix.M12, currentMatrix.M13).Length;

            //_instancedText.SetWorldMatrix(new Matrix3D(widthVector3D.X * textSize,       widthVector3D.Y * textSize,       widthVector3D.Z * textSize,       0,
            //                                           heightVector3D.X * textSize,      heightVector3D.Y * textSize,      heightVector3D.Z * textSize,      0,
            //                                           planeNormalVector3D.X * textSize, planeNormalVector3D.Y * textSize, planeNormalVector3D.Z * textSize, 0,
            //                                           currentMatrix.OffsetX,            currentMatrix.OffsetY,            currentMatrix.OffsetZ,            1));
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


            int addedTextCount = _addedTexts.Count + 1;

            _instancedTextNode.SetTextDirection(textDirection: new Vector3D(1, 0, 0), upDirection: new Vector3D(0, 1, 0));
            var instancedText = _instancedTextNode.AddText("Added text " + addedTextCount.ToString(), Colors.Black, new Point3D(-190, 20 + addedTextCount * 10, -5), size: 10, hasBackSide: true);

            _addedTexts.Add(instancedText);
            RemoveTextButton.IsEnabled = true;
        }

        private void RemoveTextButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_addedTexts.Count == 0)
                return;

            var instancedTextToRemove = _addedTexts[_addedTexts.Count - 1];

            _instancedTextNode.RemoveText(instancedTextToRemove);

            _addedTexts.RemoveAt(_addedTexts.Count - 1);

            if (_addedTexts.Count == 0)
                RemoveTextButton.IsEnabled = false;
        }

        private void ShowReportButton_OnClick(object sender, RoutedEventArgs e)
        {
            string reportText = _instancedTextNode.GetReport();

            if (_instancedTextNode2 != null)
                reportText += "\r\n\r\n" + _instancedTextNode2.GetReport(orderByNumberOfInstances: false);

            InfoTextBox.Text = reportText;

            InfoTextBox.Visibility = Visibility.Visible;
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