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
using Ab3d.Common;
using Ab3d.DirectX;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    /// <summary>
    /// Interaction logic for LinesWithPatternSample.xaml
    /// </summary>
    public partial class LinesWithPatternSample : Page
    {
        private Point3D _lineStartPosition;

        public LinesWithPatternSample()
        {
            InitializeComponent();

            _lineStartPosition = new Point3D(0, 0, 100);


            AddLineWithText(linePattern: 0x5555, linePatternOffset: 0, linePatternScale: 1); // 0x5555 is 0101010101010101 - note that the pattern starts on the right side with the first bit - has value 1.
            AddLineWithText(0x3333, 0, 1); // 0x3333 is 0011001100110011
            AddLineWithText(0x1010, 0, 1); // 0x1010 is 1000000010000000
            AddLineWithText(0xF0F0, 0, 1); // 0xF0F0 is 1111000011110000

            AddLineWithText(0xF0F0, linePatternOffset: 2.0f / 16.0f, linePatternScale: 1); // offset for 1 bit in a 16 bit pattern
            AddLineWithText(0xF0F0, linePatternOffset: 4.0f / 16.0f, linePatternScale: 1); 
            AddLineWithText(0x5555, linePatternOffset: 0, linePatternScale: 2);
            AddLineWithText(0x5555, linePatternOffset: 0, linePatternScale: 4);
            AddLineWithText(0x5555, linePatternOffset: 0, linePatternScale: 0.5f);

            AddLineWithText(0xFFFF, 0, 1); // Solid line - no pattern
        }

        private void AddLineWithText(int linePattern, float linePatternOffset, float linePatternScale)
        {
            string lineInfo = string.Format("Pattern: 0x{0:X}", linePattern);

            if (linePatternOffset != 0)
                lineInfo = string.Format("{0}; Offset: {1}/16", lineInfo, (int)(linePatternOffset * 16));

            if (linePatternScale != 1)
                lineInfo = string.Format("{0}; Scale: {1}", lineInfo, linePatternScale);

            var textBlockVisual3D = new Ab3d.Visuals.TextBlockVisual3D()
            {
                Text = lineInfo,
                Position = _lineStartPosition - new Vector3D(10, 0, 0),
                PositionType = PositionTypes.Right,
                Size = new Size(0, 10),
                TextDirection = new Vector3D(1, 0, 0),
                UpDirection = new Vector3D(0, 1, 0),
                Foreground = Brushes.Black,
                RenderBitmapSize = new Size(256, 64) // Optionally we can specify the render size to reduce the size of rendered bitmap
            };

            MainViewport.Children.Add(textBlockVisual3D);


            var lineVisual3D = new Ab3d.Visuals.LineVisual3D()
            {
                StartPosition = _lineStartPosition,
                EndPosition = _lineStartPosition + new Vector3D(100, 0, 0),
                LineThickness = 4,
                LineColor = Colors.Orange
            };

            // LinePattern is an int value that defines the 16 bit int value that defines the line pattern - if bit is 1 then line is drawn, when 0 line is not drawn.
            // For example value 0xFFFF means full line without any dots or dashes. Value 0x5555 means line with dots - one full dot follows one empty dot.
            lineVisual3D.SetDXAttribute(DXAttributeType.LinePattern, linePattern);

            // LinePatternScale is a float value that sets the pattern scale factor. Value 1 does not scale the pattern. Values bigger then 1 increase the pattern length; values smaller then 1 decrease the pattern length (making it more dense).
            lineVisual3D.SetDXAttribute(DXAttributeType.LinePatternScale, linePatternScale);

            // LinePatternOffset is a float value that sets a pattern offset. This value is usually between 0 and 1 - 0 value means no offset, 1 means offset for the whole patter which is the same as no offset. Value 0.1 means that the line will begin with the pattern advanced by 10%.
            lineVisual3D.SetDXAttribute(DXAttributeType.LinePatternOffset, linePatternOffset);

            MainViewport.Children.Add(lineVisual3D);


            _lineStartPosition += new Vector3D(0, 0, -20);
        }
    }
}
