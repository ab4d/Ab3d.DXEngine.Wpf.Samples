using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Ab3d.Visuals;
using ComboBoxItem = System.Windows.Controls.ComboBoxItem;

namespace Ab3d.DXEngine.Wpf.Samples.PowerToysOther
{
    /// <summary>
    /// Interaction logic for AxisWith3DLabelsSample.xaml
    /// </summary>
    public partial class AxisWith3DLabelsSample : Page
    {
        private bool _isDemoAxesShown;

        public AxisWith3DLabelsSample()
        {
            InitializeComponent();

            ResolutionInfoControl.InfoText =
@"AxisWith3DLabelsVisual3D uses many TextBlockVisual3D objects that are used to show 3D texts for title and value labels. The TextBlockVisual3D has a RenderBitmapSize property that can be set to specify the size of the rendered bitmap. When rendered by DXEngine the value of RenderBitmapSize is set to 512 x 256.

Because rendering text to big bitmaps can take some time, the AxisWith3DLabelsVisual3D by default optimizes the size of the text bitmaps. The default value for rendering title text is 512 x 64 (usually a longer text in one line) and for value labels teh default value is 128 x 64 (shorted text in one line).

With setting the TitleRenderBitmapSize and ValueLabelsRenderBitmapSize you can customize the bitmap size values to provide higher resolution of text (increasing size) or optimize initialization time and memory usage (decreasing size).

Note that when TextBlockVisual3D is rendered by WPF 3D, then the RenderBitmapSize is by default set to Size.Empty - this uses WPF's VisualBrush to render the text. This setting is not supported when using Ab3d.DXEngine.";


            ShowDemoAxes();
            //ShowAllAxes();

            this.Unloaded += delegate (object sender, RoutedEventArgs args)
            {
                // It is recommended to call Dispose method when the AxisWith3DLabelsVisual3D is they are no longer used.
                // This releases the native memory that is used by the RenderTargetBitmap objects that can be created by the TextBlockVisual3D object.
                foreach (var axisWith3DLabelsVisual3D in MainViewport.Children.OfType<AxisWith3DLabelsVisual3D>())
                {
                    axisWith3DLabelsVisual3D.Dispose();
                }
            };
        }

        private void ShowDemoAxes()
        {
            MainViewport.Children.Clear();

            var defaultAxis = new AxisWith3DLabelsVisual3D
            {
                Camera = Camera1,
                AxisStartPosition = new Point3D(120, 0, 0),
                AxisEndPosition = new Point3D(120, 100, 0),
                AxisTitle = "Default axis",
            };

            MainViewport.Children.Add(defaultAxis);

            
            var changedValuesRangeAxis = new AxisWith3DLabelsVisual3D()
            {
                Camera = Camera1,
                AxisStartPosition = new Point3D(60, 0, 0),
                AxisEndPosition = new Point3D(60, 100, 0),
                AxisTitle = "Changed range and ticks step",
                MinimumValue = -50,
                MaximumValue = 50,
                MajorTicksStep = 10,
                MinorTicksStep = 5
            };

            MainViewport.Children.Add(changedValuesRangeAxis);


            var changedTicksAxis = new AxisWith3DLabelsVisual3D()
            {
                Camera = Camera1,
                AxisStartPosition = new Point3D(0, 0, 0),
                AxisEndPosition = new Point3D(0, 100, 0),
                AxisTitle = "Changed display format",
                MinimumValue = 0,
                MaximumValue = 100,
                MajorTicksStep = 20,
                MinorTicksStep = 2.5,             // to hide minor ticks set MinorTicksStep to 0
                ValueDisplayFormatString = "$0.0M" // Change format to always display 2 decimals. Default value is "#,##0".
            };

            // You can also set custom culture to format the values:
            changedTicksAxis.ValueDisplayCulture = System.Globalization.CultureInfo.InvariantCulture;

            MainViewport.Children.Add(changedTicksAxis);


            var customValuesLabelsAxis = new AxisWith3DLabelsVisual3D()
            {
                Camera = Camera1,
                AxisStartPosition = new Point3D(-60, 0, 0),
                AxisEndPosition = new Point3D(-60, 100, 0),
                AxisTitle = "Custom value labels",
                MinimumValue = 1,
                MaximumValue = 5,
                MajorTicksStep = 1,
                MinorTicksStep = 0, // Hide minor ticks; we could also call: customValuesLabelsAxis.SetCustomMinorTickValues(null);
            };

            // one value label is shown for each major tick
            // So set the same number of string as there is the number of ticks.
            // You can get the count by:
            //var majorTicks = customValuesLabelsAxis.GetMajorTickValues();

            customValuesLabelsAxis.SetCustomValueLabels(new string[] { "lowest", "low", "normal", "high", "highest" });

            MainViewport.Children.Add(customValuesLabelsAxis);


            var customValuesAxis = new AxisWith3DLabelsVisual3D()
            {
                Camera = Camera1,
                AxisStartPosition = new Point3D(-120, 0, 0),
                AxisEndPosition = new Point3D(-120, 100, 0),
                AxisTitle = "Logarithmic scale",
                MinimumValue = 0,
                MaximumValue = 100,
                MinorTicksStep = 0, // Hide minor ticks
            };

            // Create custom major tick values (this will position the major ticks along the axis)
            // But we will display custom values for each major tick - see below.
            customValuesAxis.SetCustomMajorTickValues(new double[] { 0.0, 33.3, 66.6, 100.0 });
            customValuesAxis.SetCustomValueLabels(new string[] { "1", "10", "100", "1000" });

            // Set minor ticks to show log values from 1 to 10
            var minorValues = new List<double>();
            for (int i = 0; i <= 10; i++)
                minorValues.Add(Math.Log10(i) * 33.3); // multiply by 33.3 as this is the "position" of the value 10 on the axis (see code a few lines back)

            customValuesAxis.SetCustomMinorTickValues(minorValues.ToArray());

            MainViewport.Children.Add(customValuesAxis);


            var horizontalAxis1 = new AxisWith3DLabelsVisual3D
            {
                Camera = Camera1,
                AxisStartPosition = new Point3D(0, 0, 80),
                AxisEndPosition   = new Point3D(-100, 0, 80),
                RightDirectionVector3D = new Vector3D(0, 0, -1), // RightDirectionVector3D is the direction in which the text is drawn. By default RightDirectionVector3D points to the right (1, 0, 0). We need to change that because this is also this axis direction.
                IsRenderingOnRightSideOfAxis = true,
                AxisTitle = "Horizontal axis",
            };

            MainViewport.Children.Add(horizontalAxis1);


            // Clone the axis
            var offsetVector = new Vector3D(0, 0, 20);

            var horizontalAxis2 = horizontalAxis1.Clone();
            horizontalAxis2.AxisStartPosition += offsetVector;
            horizontalAxis2.AxisEndPosition += offsetVector;
            horizontalAxis2.AxisTitle = "Cloned and flipped horizontal axis";
            horizontalAxis2.IsRenderingOnRightSideOfAxis = !horizontalAxis1.IsRenderingOnRightSideOfAxis; // flip side on which the ticks and labels are rendered

            MainViewport.Children.Add(horizontalAxis2);

            _isDemoAxesShown = true;


            // NOTE:
            // Many additional customizations are possible by deriving your class from AxisWith3DLabelsVisual3D
            // and by overriding the virtual methods. The derived class can also access many protected properties
            // and change the shown TextBlockVisual3D and TextBlock and line objects.
        }

        private void ShowAllAxes()
        {
            MainViewport.Children.Clear();

            // Add z axis
            var zAxis = new AxisWith3DLabelsVisual3D();
            zAxis.BeginInit(); // It is faster to initialize TextBlockVisual3D with using BeginInit / EndInit - this way the inner objects are not updated on each set property but only once when calling EndInit
            zAxis.Camera = Camera1;
            zAxis.AxisStartPosition = new Point3D(-50, 0, -50);
            zAxis.AxisEndPosition = new Point3D(-50, 100, -50);
            zAxis.MajorTicksStep = 20;
            zAxis.MinorTicksStep = 10;
            zAxis.IsRenderingOnRightSideOfAxis = false;
            zAxis.AxisTitle = "Z axis [°C]";
            zAxis.AxisTitleBrush = Brushes.Orange;
            zAxis.AxisTitleFontWeight = FontWeights.Bold;
            zAxis.FontFamily = new FontFamily("Consolas");
            zAxis.AxisTitlePadding = -15; // By default the title is positioned for AxisTitlePadding (=5 by default) away from the longest data label. In out case the "100 (max)" label (defined below) is very long, so we need to move the title inwards so it is not to far away. 
            zAxis.EndInit();

            // Customize the value labels:
            var valueLabels = zAxis.GetValueLabels();         // first get default values
            valueLabels[0] = "";                              // remove "0" text because it is displayed on the same spot as "1,000"
            valueLabels[valueLabels.Length - 1] += " (max)";  // add custom text to existing label; Note that this will move the axis title to the left (max value label length is used to offset the position of the title)
            zAxis.SetCustomValueLabels(valueLabels);

            MainViewport.Children.Add(zAxis);


            // Clone the axis
            var zAxis2 = zAxis.Clone();
            zAxis2.AxisStartPosition = new Point3D(50, 0, 50);
            zAxis2.AxisEndPosition = new Point3D(50, 100, 50);
            zAxis2.AxisTitle = null;
            zAxis2.IsRenderingOnRightSideOfAxis = !zAxis.IsRenderingOnRightSideOfAxis; // flip side on which the ticks and labels are rendered

            MainViewport.Children.Add(zAxis2);



            // Add x axis
            var xAxis = new AxisWith3DLabelsVisual3D();
            xAxis.BeginInit();
            xAxis.Camera = Camera1;
            xAxis.AxisStartPosition = new Point3D(-50, 0, 50);
            xAxis.AxisEndPosition = new Point3D(50, 0, 50);
            xAxis.RightDirectionVector3D = new Vector3D(0, 0, 1);
            xAxis.MinimumValue = -5;
            xAxis.MaximumValue = 5;
            xAxis.MajorTicksStep = 1;
            xAxis.MinorTicksStep = 0.25;
            xAxis.IsRenderingOnRightSideOfAxis = true;
            xAxis.AxisTitle = "X axis [m]";
            xAxis.EndInit();

            MainViewport.Children.Add(xAxis);


            // Clone the axis
            var xAxis2 = xAxis.Clone();
            xAxis2.AxisStartPosition = new Point3D(-50, 100, -50);
            xAxis2.AxisEndPosition = new Point3D(50, 100, -50);
            xAxis2.AxisTitle = null;
            xAxis2.IsRenderingOnRightSideOfAxis = !xAxis.IsRenderingOnRightSideOfAxis; // flip side on which the ticks and labels are rendered

            MainViewport.Children.Add(xAxis2);



            // Add z axis
            var yAxis = new AxisWith3DLabelsVisual3D();
            yAxis.BeginInit();
            yAxis.Camera = Camera1;
            yAxis.AxisStartPosition = new Point3D(-50, 0, 50);
            yAxis.AxisEndPosition = new Point3D(-50, 0, -50);
            yAxis.RightDirectionVector3D = new Vector3D(1, 0, 0);
            yAxis.MinimumValue = 0;
            yAxis.MaximumValue = 100;
            yAxis.IsRenderingOnRightSideOfAxis = false;
            yAxis.AxisTitle = "Y axis: log(10)";
            yAxis.EndInit();
            
            yAxis.SetCustomMajorTickValues(new double[] { 0.0, 33.3, 66.6, 100.0 });
            yAxis.SetCustomValueLabels(    new string[] { "1", "10", "100", "1000" });

            // Set minor ticks to show log values from 1 to 10
            var minorValues = new List<double>();
            for (int i = 0; i <= 10; i++)
                minorValues.Add(Math.Log10(i) * 33.3); // multiply by 33.3 as this is the "position" of the value 10 on the axis (see code a few lines back)

            yAxis.SetCustomMinorTickValues(minorValues.ToArray());

            // To Hide the minor ticks we could set MinorTicksLength to 0 or call:
            //zAxis.SetCustomMinorTickValues(null); 

            MainViewport.Children.Add(yAxis);


            // Clone the axis
            var yAxis2 = yAxis.Clone();
            yAxis2.AxisStartPosition = new Point3D(50, 100, 50);
            yAxis2.AxisEndPosition = new Point3D(50, 100, -50);
            yAxis2.AxisTitle = null;
            yAxis2.IsRenderingOnRightSideOfAxis = !yAxis.IsRenderingOnRightSideOfAxis; // flip side on which the ticks and labels are rendered

            // Remove value label "1" because it overlaps with top y axis
            valueLabels = yAxis2.GetValueLabels();
            valueLabels[0] = "";   
            yAxis2.SetCustomValueLabels(valueLabels);

            MainViewport.Children.Add(yAxis2);

            _isDemoAxesShown = false;
        }

        private void SwitchAxesButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_isDemoAxesShown)
            {
                Camera1.Heading = 30;

                ShowAllAxes();
                SwitchAxesButton.Content = "Show demo axes";
            }
            else
            {
                Camera1.Heading = -20;

                ShowDemoAxes();
                SwitchAxesButton.Content = "Show connected axes";
            }

            Camera1.Attitude = -30;
            Camera1.Distance = 430;
            Camera1.Offset = new Vector3D(0, 0, 0);
        }

        private void TitleRenderResolutionComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            var size = GetRenderingResolution(TitleRenderResolutionComboBox);

            foreach (var axisWith3DLabelsVisual3D in MainViewport.Children.OfType<AxisWith3DLabelsVisual3D>())
                axisWith3DLabelsVisual3D.TitleRenderBitmapSize = size;
        }
        
        private void ValueLabelsRenderResolutionComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            var size = GetRenderingResolution(ValueLabelsRenderResolutionComboBox);

            foreach (var axisWith3DLabelsVisual3D in MainViewport.Children.OfType<AxisWith3DLabelsVisual3D>())
                axisWith3DLabelsVisual3D.ValueLabelsRenderBitmapSize = size;
        }

        private Size GetRenderingResolution(ComboBox comboBox)
        {
            var comboBoxSelectedItem = comboBox.SelectedItem as ComboBoxItem;
            if (comboBoxSelectedItem == null)
                return Size.Empty;

            var selectedText = (string)comboBoxSelectedItem.Content;

            if (!selectedText.Contains('x'))
                return Size.Empty;

            var textParts = selectedText.Split('x');

            int width = Int32.Parse(textParts[0]);
            int height = Int32.Parse(textParts[1]);

            return new Size(width, height);
        }
    }
}
