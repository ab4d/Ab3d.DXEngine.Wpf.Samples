using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Media.Media3D;
using Ab3d.DirectX;
using Ab3d.Visuals;

namespace Ab3d.DXEngine.Wpf.Samples.PowerToys
{
    // This sample is the same as the sample in Ab3d.PowerToys samples project.
    // The only difference is that instead of using yOffset in the call to CreateContourLinePositions,
    // we can use a proper LineDepthBias to efficiently offset the 3D lines from the 3D models.
    // See UpdateContourLines method for more info.
    //
    // When this sample is rendered by Ab3d.DXEngine, then it is possible to use super-sampling and
    // this produce super smooth 3D lines that look much better then when the scene is rendered only by WPF 3D.
    //
    // See also the ContourLinesSample.xaml.cs to see a new option to define color gradient without interpolating colors.

    /// <summary>
    /// Interaction logic for Plot3DSample.xaml
    /// </summary>
    public partial class Plot3DSample : Page
    {
        private double _minYValue, _maxYValue;

        private double[,] _data;
        
        public Plot3DSample()
        {
            InitializeComponent();

            this.Loaded += Plot3DSample_Loaded;
        }

        void Plot3DSample_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateAll();
        }
        
        private void UpdateAll()
        {
            int arraySize = GetSelectedArraySize();
            
            if (Function1RadioButton.IsChecked ?? false)
                _data = CreateGraphData1(arraySize, arraySize, out _minYValue, out _maxYValue);
            else if (Function2RadioButton.IsChecked ?? false)
                _data = CreateGraphData2(arraySize, arraySize, out _minYValue, out _maxYValue);
            else if (Function3RadioButton.IsChecked ?? false)
                _data = CreateGraphData3(arraySize, arraySize, out _minYValue, out _maxYValue);
            else if (Function4RadioButton.IsChecked ?? false)
                _data = CreateGraphData4(arraySize, arraySize, out _minYValue, out _maxYValue);

            HeightMap1.HeightData = _data;


            AxesBox.SetAxisDataRange(AxesBoxVisual3D.AxisTypes.XAxis, 0, arraySize, 1, 0, false);
            AxesBox.SetAxisDataRange(AxesBoxVisual3D.AxisTypes.YAxis, 0, arraySize, 1, 0, false);

            UpdateGradientTexture();

            UpdateWireBox();
        }
        
        private void UpdateWireBox()
        {
            // Get local values
            Size3D heightMapSize = HeightMap1.Size;
            
            // First we have to get the position and size of the WireBoxVisual3
            // It is not the same as the HeightMapVisual3D because we start and stop the axis on the whole part of the _minYValue and _maxYValue
            // For example if the function's min is 0.8 and its max is 0.9 we would show axis from -1 to +1.

            double axisMinY = Math.Floor(_minYValue);
            double axisMaxY = Math.Ceiling(_maxYValue);

            double dataValuesRange = _maxYValue - _minYValue;

            double heightMapYSize = heightMapSize.Y; // This is defined by the height slider

            // First get the center
            double centerY = ((_maxYValue + _minYValue) / 2) * heightMapYSize;

            // Now calculate for how much we extend the y size to go from _minYValue to axisMinY and from _maxYValue to axisMaxY

            double axisSizeY;

            if (dataValuesRange > 0)
            {
                axisSizeY = heightMapYSize * dataValuesRange +
                            (heightMapYSize * Math.Abs(_minYValue - axisMinY)) + // extent for the axisMinY - _minYValue
                            (heightMapYSize * Math.Abs(axisMaxY - _maxYValue)); // extent for the axisMaxY - _maxYValue
            }
            else
            {
                axisSizeY = 0;
            }


            // Update the position, size and ZAxis value range
            AxesBox.CenterPosition = new Point3D(0, centerY, 0);
            AxesBox.Size = new Size3D(100, axisSizeY, 100);

            AxesBox.SetAxisDataRange(AxesBoxVisual3D.AxisTypes.ZAxis, minimumValue: axisMinY, maximumValue: axisMaxY, majorTicksStep: 1, minorTicksStep: 0, snapMaximumValueToMajorTicks: true);


            // Create contour lines
            UpdateContourLines();
        }

        private void UpdateContourLines()
        {
            double minValue = AxesBox.ZAxis1.MinimumValue;
            double maxValue = AxesBox.ZAxis1.MaximumValue;

            // First define values for each contour line
            var contourLineValues = SetupContourLineValues(minValue, maxValue, step: (maxValue - minValue) / 15);

            // Then use CreateContourLinePositions method to create positions for all contour lines and returns them in one Point3DCollection.
            
            // No yOffset when rendered by DXEngine because we will use LineDepthBias instead (see a few lines below):
            //var contourLinePositions = HeightMap1.CreateContourLinePositions(contourLineValues, yOffset: 0.05); // offset the contour lines so they are drawn just slightly on top of the 3D height map
            var contourLinePositions = HeightMap1.CreateContourLinePositions(contourLineValues); 

            ContourLinesVisual3D.Positions = contourLinePositions;

            // Use line depth bias to move the lines closer to the camera so the lines are rendered on top of solid model and are not partially occluded by it.
            // See DXEngineVisuals/LineDepthBiasSample for more info.
            ContourLinesVisual3D.SetDXAttribute(DXAttributeType.LineDepthBias, 0.1);
            ContourLinesVisual3D.SetDXAttribute(DXAttributeType.LineDynamicDepthBiasFactor, 0.02);


            BottomContourLinesVisual3D.Children.Clear();
            AxesBox.ShowBottomConnectionLines = false;

            // Show flattened contour lines at the bottom of the graph
            if (ShowBottomGridCheckBox.IsChecked ?? false)
            {
                if (ContourLinesRadioButton.IsChecked ?? false)
                {
                    // Get bottom y position of the AxesBox
                    double yPosition = AxesBox.CenterPosition.Y - AxesBox.Size.Y * 0.5;

                    // CreateContourLinePositions creates positions for individual contour lines and returns them in an array of Point3DCollection
                    // (each element in the array represents positions for contour lines each value in _contourLineValues).
                    var individualContourLinePositions = HeightMap1.CreateMultiContourLinePositions(contourLineValues, yOffset: 0.05); // offset the contour lines so they are drawn just slightly on top of the 3D height map


                    for (var i = 0; i < contourLineValues.Length; i++)
                    {
                        // Update all position's Y value to yPosition
                        var positions = individualContourLinePositions[i];
                        for (var j = 0; j < positions.Count; j++)
                            positions[j] = new Point3D(positions[j].X, yPosition, positions[j].Z);

                        var color = HeightMap1.GetHeightTextureColor(contourLineValues[i]);

                        var multiLineVisual3D = new MultiLineVisual3D()
                        {
                            Positions = positions,
                            LineThickness = 2,
                            LineColor = color
                        };
                        
                        BottomContourLinesVisual3D.Children.Add(multiLineVisual3D);
                    }
                }
                else if(WireframeRadioButton.IsChecked ?? false)
                {
                    AxesBox.ShowBottomConnectionLines = true;
                }
            }
        }

        private double[] SetupContourLineValues(double minValue, double maxValue, double step)
        {
            if (maxValue < minValue || step < 0)
                return null;

            int count = (int)((maxValue - minValue) / step);
            var contourLineValues = new List<double>(count + 1);

            double oneValue = minValue;
            while (oneValue <= maxValue)
            {
                contourLineValues.Add(oneValue);
                oneValue += step;
            }

            return contourLineValues.ToArray();
        }

        private void UpdateGradientTexture()
        {
            Rectangle selectedRectangle;

            if (Gradient1RadioButton.IsChecked ?? false)
                selectedRectangle = Rectangle1;
            else if (Gradient2RadioButton.IsChecked ?? false)
                selectedRectangle = Rectangle2;
            else // if (Gradient1RadioButton.IsChecked ?? false)
                selectedRectangle = Rectangle3;


            // The following call will create the magic:
            // It will use the height data and the LinearGradientBrush on the selected rectangle
            // and create the texture bitmap from the data
            HeightMap1.CreateHeightTextureFromGradient((LinearGradientBrush)selectedRectangle.Fill);
        }

        private int GetSelectedArraySize()
        {
            int arraySize;

            if (ArraySize1RadioButton.IsChecked ?? false)
                arraySize = 20;
            else if (ArraySize2RadioButton.IsChecked ?? false)
                arraySize = 40;
            else // (ArraySize3RadioButton.IsChecked ?? false)
                arraySize = 80;

            return arraySize;
        }

        private void ChangeDataButton_OnClick(object sender, RoutedEventArgs e)
        {
            // This method demonstrates how to update the graph 3D mesh after the data are changed
            // Similar code can be used to create dynamic height graphs.
            // But be careful about performance especially when showing 3D lines in height map.
            // Much better performance can be achieved when 3D lines are not shown.

            int arraySize = GetSelectedArraySize();

            double[,] heightData = HeightMap1.HeightData;

            for (int z = 0; z < arraySize; z++)
            {
                for (int x = 0; x < arraySize; x++)
                {
                    heightData[z, x] *= 0.95;
                }
            }

            // UpdateContent will recreate the 3D mesh
            HeightMap1.UpdateContent();

            UpdateContourLines();
        }

        private void OnBottomLinesSettingsChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            UpdateContourLines();
        }

        private void GradientRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            UpdateGradientTexture();
        }

        private void Rectangle1_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Gradient1RadioButton.IsChecked = true;
        }

        private void Rectangle2_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Gradient2RadioButton.IsChecked = true;
        }

        private void Rectangle3_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Gradient3RadioButton.IsChecked = true;
        }

        private void FunctionRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            UpdateAll();
        }

        private void ArraySizeRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            UpdateAll();
        }

        private void ExportToImageButton_Click(object sender, RoutedEventArgs e)
        {
            var fileDialog = new Microsoft.Win32.SaveFileDialog
            {
                DefaultExt = "png",
                Filter = "Png files(*.png)|*.png",
                Title = "Select output png file",
                FileName = "Plot3D.png"
            };

            if (fileDialog.ShowDialog() ?? false)
            {
                // NOTE: To get superior image quality with super-smooth 3D lines use Ab3d.DXEngine and call DXViewportView.RenderToBitmap method instead or camera.RenderToBitmap
                BitmapSource plotImage = Camera1.RenderToBitmap(customWidth: 1920, customHeight: 1200, antialiasingLevel: 4, backgroundBrush: MainGrid.Background);

                if (plotImage != null)
                {
                    using (var fileStream = new System.IO.FileStream(fileDialog.FileName, System.IO.FileMode.Create))
                    {
                        BitmapEncoder encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(plotImage));
                        encoder.Save(fileStream);
                    }

                    // Open the saved image in the default image viewer
                    // For Core3 we need to set UseShellExecute to true (see https://github.com/dotnet/corefx/issues/33714)
                    var processStartInfo = new ProcessStartInfo
                    {
                        FileName = fileDialog.FileName,
                        UseShellExecute = true
                    };

                    Process.Start(processStartInfo);
                }
            }
        }

        #region Math functions

        // cos(x*z)*(x^2-z^2)/2
        private static double[,] CreateGraphData1(int arrayWidth, int arrayHeight, out double minYValue, out double maxYValue)
        {
            double[,] data = new double[arrayWidth, arrayHeight];


            double xMin = -2;
            double xMax = 2;
            double zMin = -2;
            double zMax = 2;

            double xStep = (xMax - xMin) / arrayWidth;
            double zStep = (zMax - zMin) / arrayHeight;

            double xValue;
            double zValue = zMin;


            double yValue;
            minYValue = double.MaxValue;
            maxYValue = double.MinValue;


            for (int z = 0; z < arrayHeight; z++)
            {
                xValue = xMin;

                for (int x = 0; x < arrayWidth; x++)
                {
                    // cos(x*y)*(x^2-y^2)
                    yValue = Math.Cos(xValue * zValue) * (xValue * xValue - zValue * zValue) / 2;

                    data[x, z] = yValue;

                    if (yValue > maxYValue)
                        maxYValue = yValue;

                    if (yValue < minYValue)
                        minYValue = yValue;

                    xValue += xStep;
                }

                zValue += zStep;
            }

            return data;
        }

        // sin(sqrt(x*x + z*z))
        private static double[,] CreateGraphData2(int arrayWidth, int arrayHeight, out double minYValue, out double maxYValue)
        {
            double[,] data = new double[arrayWidth, arrayHeight];


            double xMin = -10;
            double xMax = 10;
            double zMin = -10;
            double zMax = 10;

            double xStep = (xMax - xMin) / arrayWidth;
            double zStep = (zMax - zMin) / arrayHeight;

            double xValue;
            double zValue = zMin;


            double yValue;
            minYValue = double.MaxValue;
            maxYValue = double.MinValue;


            for (int z = 0; z < arrayHeight; z++)
            {
                xValue = xMin;

                for (int x = 0; x < arrayWidth; x++)
                {
                    yValue = Math.Sin(Math.Sqrt(xValue * xValue + zValue * zValue));

                    data[x, z] = yValue;

                    if (yValue > maxYValue)
                        maxYValue = yValue;

                    if (yValue < minYValue)
                        minYValue = yValue;

                    xValue += xStep;
                }

                zValue += zStep;
            }

            return data;
        }

        // (x * z^3 - z * x^3) * 2
        private static double[,] CreateGraphData3(int arrayWidth, int arrayHeight, out double minYValue, out double maxYValue)
        {
            double[,] data = new double[arrayWidth, arrayHeight];


            double xMin = -1;
            double xMax = 1;
            double zMin = -1;
            double zMax = 1;

            double xStep = (xMax - xMin) / arrayWidth;
            double zStep = (zMax - zMin) / arrayHeight;

            double xValue;
            double zValue = zMin;


            double yValue;
            minYValue = double.MaxValue;
            maxYValue = double.MinValue;


            for (int z = 0; z < arrayHeight; z++)
            {
                xValue = xMin;

                for (int x = 0; x < arrayWidth; x++)    
                {
                    yValue = (xValue * zValue * zValue * zValue - zValue * xValue * xValue * xValue) * 2;

                    data[x, z] = yValue;

                    if (yValue > maxYValue)
                        maxYValue = yValue;

                    if (yValue < minYValue)
                        minYValue = yValue;

                    xValue += xStep;
                }

                zValue += zStep;
            }

            return data;
        }

        // cos(abs(x)+abs(z))*(abs(x)+abs(z))
        private static double[,] CreateGraphData4(int arrayWidth, int arrayHeight, out double minYValue, out double maxYValue)
        {
            double[,] data = new double[arrayWidth, arrayHeight];


            double xMin = -1;
            double xMax = 1;
            double zMin = -1;
            double zMax = 1;

            double xStep = (xMax - xMin) / arrayWidth;
            double zStep = (zMax - zMin) / arrayHeight;

            double xValue;
            double zValue = zMin;


            double yValue;
            minYValue = double.MaxValue;
            maxYValue = double.MinValue;


            for (int z = 0; z < arrayHeight; z++)
            {
                xValue = xMin;

                for (int x = 0; x < arrayWidth; x++)
                {
                    yValue = Math.Cos(Math.Abs(xValue) + Math.Abs(zValue)) * (Math.Abs(xValue) + Math.Abs(zValue));

                    data[x, z] = yValue;

                    if (yValue > maxYValue)
                        maxYValue = yValue;

                    if (yValue < minYValue)
                        minYValue = yValue;

                    xValue += xStep;
                }

                zValue += zStep;
            }

            return data;
        }
        #endregion
    }
}
