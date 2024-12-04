using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
using Ab3d.DirectX;
using Ab3d.DirectX.Materials;
using Ab3d.Utilities;
using Ab3d.Visuals;
using LineCap = Ab3d.Common.Models.LineCap;

namespace Ab3d.DXEngine.Wpf.Samples.PowerToys
{
    /// <summary>
    /// Interaction logic for LineCapsSample.xaml
    /// </summary>
    public partial class LineCapsSample : Page
    {
        private readonly Vector3D[] _multiLinePositions = new Vector3D[]
        {
            new Vector3D(10, 0, 0),
            new Vector3D(10, 60, 0),
            new Vector3D(30, 20, 0),
            new Vector3D(30, 80, 0),
        };

        private Vector3D[] _polyLinePositions;

        private double _selectedLineThickness = 2;

        private bool _isRandomized;


        public LineCapsSample()
        {
            InitializeComponent();

            _polyLinePositions = _multiLinePositions;


            AngleInfoControl.InfoText =
@"Sets the angle of the Arrow line cap in degrees.
The angle of other line types is changed relative to the Arrow line cap.";

            ArrowLengthInfoControl.InfoText =
@"Specifies the maximum arrow length set as fraction of the line length.
For example: 0.333 means that the maximum arrow length will be 1/3 of the line length.
If the line is short so that the arrow length exceeds the amount defined by MaxLineArrowLength,
the arrow is shortened (and the arrow angle is increased).
Default value is 0.333.

To use a different value of MaxLineArrowLength for only a few lines, 
create the ScreenSpaceLineNode (see 'Advanced / Creating ScreenSpaceLineNodes' sample)
and then call SetMaxLineArrowLength method.";

            MinLineStripArrowLengthInfoControl.InfoText =
@"Specifies the minimum arrow length set as a multiplier of the line thickness.
For example 2 means that the line arrow will not be shorter than 2 times the line arrow.
This can be used to line arcs and curves where the line segments are very short.
This is applied after the MaxLineArrowLength property and only for poly-lines and connected lines (IsLineStrip is true).
Default value is 2.
This feature is available only by Ab3d.DXEngine and not with Ab3d.PowerToys.

To use different value of MinLineStripArrowLength for only a few lines, 
create the ScreenSpaceLineNode (see 'Advanced / Creating ScreenSpaceLineNodes' sample)
and then call SetMinLineArrowLength method.";
            
            MinLineListArrowLengthInfoControl.InfoText =
@"This is the same as MinLineStripArrowLength but is used only for individual lines and disconnected lines (IsLineStrip is false).
Default value is 0 (arrow size can be zero).
This feature is available only by Ab3d.DXEngine and not with Ab3d.PowerToys.

To use different value of MinLineStripArrowLength for only a few lines, 
create the ScreenSpaceLineNode (see 'Advanced / Creating ScreenSpaceLineNodes' sample)
and then call SetMinLineArrowLength method.";

            CreateSampleLines();
        }

        private void CreateSampleLines()
        {
            MainViewport.Children.Clear();


            var lineCapNames = Enum.GetNames(typeof(LineCap));

            double lineOffset = 20;
            double startX = -lineCapNames.Length * lineOffset * 4 / 2;

            var position = new Point3D(startX, -100, 0);
            var lineVector = new Vector3D(0, 100, 0);
            var lineOffsetVector = new Vector3D(20, 0, 0);

            for (var i = 0; i < lineCapNames.Length; i++)
            {
                var lineCapName = lineCapNames[i];
                var lineCapValue = (LineCap)Enum.Parse(typeof(LineCap), lineCapName);


                string displayText = lineCapName.Replace("Anchor", "");

                double isOffset = (i % 2) == 0 ? 0 : 1;

                var textBlockVisual3D = new TextBlockVisual3D()
                {
                    Position = position + lineOffsetVector + new Vector3D(0, isOffset * -20, 40 + isOffset * 20),
                    PositionType = PositionTypes.Center,
                    UpDirection = new Vector3D(0, 0.4, -1),
                    Size = new Size(0, 30),
                    TextPadding = new Thickness(5, 2, 5, 2),
                    Foreground = Brushes.Yellow,
                    Background = Brushes.Black,
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(1),
                    Text = displayText
                };

                // Do not dim the color of TextBlockVisual3D by lighting - use solid color instead.
                // This is DXEngine only feature. See PowerToysOther/ImprovedTextBlockVisual3D.xaml sample and code comments for more info.
                textBlockVisual3D.Material.SetDXAttribute(DXAttributeType.UseSolidColorEffect, true);
                textBlockVisual3D.BackMaterial.SetDXAttribute(DXAttributeType.UseSolidColorEffect, true);

                MainViewport.Children.Add(textBlockVisual3D);


                var line = new LineVisual3D()
                {
                    StartPosition = position,
                    EndPosition = position + lineVector,
                    StartLineCap = lineCapValue,
                    LineColor = Colors.Silver,
                    LineThickness = _selectedLineThickness
                };

                MainViewport.Children.Add(line);


                position += lineOffsetVector;

                line = new LineVisual3D()
                {
                    StartPosition = position,
                    EndPosition = position + lineVector,
                    EndLineCap = lineCapValue,
                    LineColor = Colors.Silver,
                    LineThickness = _selectedLineThickness
                };

                MainViewport.Children.Add(line);


                position += lineOffsetVector;

                line = new LineVisual3D()
                {
                    StartPosition = position,
                    EndPosition = position + lineVector,
                    StartLineCap = lineCapValue,
                    EndLineCap = lineCapValue,
                    LineColor = Colors.Silver,
                    LineThickness = _selectedLineThickness
                };

                MainViewport.Children.Add(line);

                position += lineOffsetVector;
                position += lineOffsetVector;


                var offset = new Vector3D(-4 * lineOffset, 120, 0);

                if (ShowMultiLinesCheckBox.IsChecked ?? false)
                {
                    var multiLineVisual3D = new MultiLineVisual3D()
                    {
                        Positions = new Point3DCollection(_multiLinePositions.Select(p => position + p + new Vector3D(-4 * lineOffset, 120, 0)).ToArray()),
                        LineColor = Colors.Yellow,
                        LineThickness = _selectedLineThickness,
                        StartLineCap = lineCapValue,
                        EndLineCap = lineCapValue,
                    };

                    MainViewport.Children.Add(multiLineVisual3D);

                    offset += new Vector3D(0, 100, 0);
                }


                if (ShowPolyLinesCheckBox.IsChecked ?? false)
                {
                    var polyLineVisual3D = new PolyLineVisual3D()
                    {
                        Positions = new Point3DCollection(_polyLinePositions.Select(p => position + p + offset).ToArray()),
                        LineColor = Colors.Orange,
                        LineThickness = _selectedLineThickness,
                        StartLineCap = lineCapValue,
                        EndLineCap = lineCapValue,
                    };

                    MainViewport.Children.Add(polyLineVisual3D);


                    //var lineArcVisual3D = new LineArcVisual3D()
                    //{
                    //    CircleCenterPosition = position + offset + new Vector3D(40, 130, 0),
                    //    Radius = 30,
                    //    StartAngle = 0,
                    //    EndAngle = 90,
                    //    CircleNormal = new Vector3D(0, 0, 1),
                    //    ZeroAngleDirection = new Vector3D(0, -1, 0),
                    //    LineColor = Colors.Red,
                    //    LineThickness = _selectedLineThickness,
                    //    StartLineCap = lineCapValue,
                    //    EndLineCap = lineCapValue,
                    //};

                    //MainViewport.Children.Add(lineArcVisual3D);
                }
            }

            if (_isRandomized)
                RandomizeLineCaps();
        }

        private void RandomizeLineCaps()
        {
            var rnd = new Random();

            int maxValue = Enum.GetValues(typeof(LineCap)).Length;

            foreach (var lineCapVisual3D in MainViewport.Children.OfType<ILineCapVisual3D>())
            {
                lineCapVisual3D.StartLineCap = (LineCap)rnd.Next(maxValue);
                lineCapVisual3D.EndLineCap = (LineCap)rnd.Next(maxValue);
            }

            // Hide TextBlockVisual3D as they are not valid anymore
            foreach (var textBlockVisual3D in MainViewport.Children.OfType<TextBlockVisual3D>())
                textBlockVisual3D.IsVisible = false;
        }


        private void AngleComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            if (MainDXViewportView.DXScene != null)
            {
                // DXEngine rendering:
                // Update the static LineArrowAngle property

                // LineArrowAngle is the angle of the line arrows. Default value is 15 degrees.
                // Note that if the line is short so that the arrow length exceeds the amount defined by MaxLineArrowLength, the arrow is shortened which increased the arrow angle.

                LineMaterial.LineArrowAngle = GetSelectedDoubleValue(AngleComboBox);

                // Changing this value will be used only on lines that are created from that point on, so we need to regenerate the scene.
                CreateSampleLines();
            }
            else
            {
                // WPF 3D rendering:
                LinesUpdater.Instance.LineArrowAngle = GetSelectedDoubleValue(AngleComboBox);
                LinesUpdater.Instance.Refresh();
            }
        }

        private void LengthComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            if (MainDXViewportView.DXScene != null)
            {
                // DXEngine rendering:
                // Update the static MaxLineArrowLength property

                // MaxLineArrowLength specifies the maximum arrow length set as fraction of the line length - e.g. 0.333 means that the maximum arrow length will be 1 / 3 (=0.333) of the line length.
                // If the line is short so that the arrow length exceeds the amount defined by MaxLineArrowLength, the arrow is shortened (the arrow angle is increased).
                // Default value is 0.333 (1 / 3 of the line's length)

                LineMaterial.MaxLineArrowLength = GetSelectedDoubleValue(LengthComboBox);

                // The following two static properties are available ONLY in Ab3d.DXEngine:
                LineMaterial.MinLineListArrowLength = GetSelectedDoubleValue(MinLineListArrowLengthComboBox);
                LineMaterial.MinLineStripArrowLength = GetSelectedDoubleValue(MinLineStripArrowLengthComboBox);

                // To use different value of MaxLineArrowLength or MinLineStripArrowLength for only a few lines, 
                // create the ScreenSpaceLineNode (see 'Advanced / Creating ScreenSpaceLineNodes' sample)
                // and then call SetMaxLineArrowLength or SetMinLineArrowLength method.
                // For example:
                // var lineNode = new ScreenSpaceLineNode(positions, isLineStrip: true, isPolyLine: true, isLineClosed: false, lineColor: Colors.Green.ToColor4(), lineThickness: 2, miterLimit: 2, startLineCap: LineCap.ArrowAnchor, endLineCap: LineCap.Flat);
                // lineNode.SetMinLineArrowLength(4);
                // 
                // var lineNodeVisual = new SceneNodeVisual3D(lineNode);
                // MainViewport.Children.Add(lineNodeVisual);

                // Changing this value will be used only on lines that are created from that point on, so we need to regenerate the scene.
                CreateSampleLines();
            }
            else
            {
                // WPF 3D rendering:
                LinesUpdater.Instance.MaxLineArrowLength = GetSelectedDoubleValue(LengthComboBox);

                // MinLineListArrowLength and MinLineStripArrowLength are not supported

                LinesUpdater.Instance.Refresh();
            }

            CreateSampleLines();
        }

        private void OnLineSettingsChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            CreateSampleLines();
        }

        private void LineThicknessComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            _selectedLineThickness = GetSelectedDoubleValue(LineThicknessComboBox);
            CreateSampleLines();
        }
        
        private float GetSelectedDoubleValue(ComboBox comboBox)
        {
            var comboBoxItem = comboBox.SelectedItem as ComboBoxItem;
            if (comboBoxItem != null)
                return float.Parse((string)comboBoxItem.Content, System.Globalization.CultureInfo.InvariantCulture);

            return 0;
        }

        private void RandomizeButton_OnClick(object sender, RoutedEventArgs e)
        {
            RandomizeLineCaps();
            _isRandomized = true;
        }

        private void OnShowArcLinesCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            if (ShowArcLinesCheckBox.IsChecked ?? false)
            {
                _polyLinePositions = MathUtils.GetArc3DPoints(ellipseCenterPosition: new Point3D(15, 15, 0), 
                                                              ellipseNormal: new Vector3D(0, 0, 1), 
                                                              zeroAngleDirection: new Vector3D(1, 0, 0), 
                                                              xRadius: 15, 
                                                              yRadius: 15, 
                                                              startAngle: 30, 
                                                              endAngle: 300, 
                                                              segments: 30).Select(p => p.ToVector3D()).ToArray();
            }
            else
            {
                _polyLinePositions = _multiLinePositions;
            }

            CreateSampleLines();
        }
    }
}
