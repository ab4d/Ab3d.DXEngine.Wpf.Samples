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
using System.Windows.Threading;
using Ab3d.DirectX;
using Ab3d.DirectX.Materials;
using Ab3d.DXEngine.Wpf.Samples.Controls;
using Ab3d.Meshes;
using SharpDX;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    /// <summary>
    /// Interaction logic for LandscapeGeneratorSample.xaml
    /// </summary>
    public partial class LandscapeGeneratorSample : Page
    {
        private double[,] _heightData;
        private MeshGeometry3D _heightMapMesh3D;

        private Color4[] _gradientColor4Array;

        private int _lastUsedGridSize;

        private DispatcherTimer _dispatcherTimer;
        private VertexColorMaterial _vertexColorMaterial;

        public LandscapeGeneratorSample()
        {
            InitializeComponent();


            // Setup RadioBoxes and Comboboxes
            FillPossibleLandscapeSizes();

            // Create linear gradient that will be used for Legent and for coloring vertexes
            var linearGradientBrush = CreateDataGradientBrush();
            _gradientColor4Array = CreateGradientColorsArray(linearGradientBrush);

            AddLegendControl(linearGradientBrush);


            // Adjust the CameraAxis control to show a coordinate system with Z up (more standard when showing height map).
            // Note: WPF uses right handed coordinate system with Y up.
            CameraAxisPanel1.CustomizeAxes(new Vector3D(1, 0, 0), "X", Colors.Red,
                                           new Vector3D(0, 1, 0), "Z", Colors.Green,
                                           new Vector3D(0, 0, -1), "Y", Colors.Blue);


            Camera1.StartRotation(10, 0);

            this.Loaded += delegate(object sender, RoutedEventArgs args)
            {
                GoToNextRandomSeed(); // Set new random seed - after seed text will be changed, the CreateLandscape will be called
            };

            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                if (_vertexColorMaterial != null)
                    _vertexColorMaterial.Dispose();

                MainDXViewportView.Dispose();
            };
        }

        private void CreateLandscape()
        {
            MainViewport.Children.Clear();

            int gridSize = GetSelectedSize();
            int seed = GetRandomSeed();

            if (seed >= 0)
            {
                CreateLandscape(gridSize: gridSize, noise: 0.5f, randomSeed: seed); // gridSize should be of the form (2 ^ n) + 1

                // Check if we are automatically changing the landscape
                var maxSize = GetSelectedMaxSize();

                if (gridSize == maxSize)
                {
                    if (AutoChangeLandscapeCheckBox.IsChecked ?? false)
                        ScheduleNextAction(2000, ShowNextLandscape); // We need to show next landscape with new random seed - wait 2 seconds with the last grid size
                }
                else if (AnimateLandscapeGenerationCheckBox.IsChecked ?? false)
                {
                    ScheduleNextAction(300, ShowNextLandscape); // We need to increase the grid size - wait 0.3 seconds to do that
                }
            }
        }

        private void CreateLandscape(int gridSize, float noise, int randomSeed = 0) // is randomSeed is 0 or less, then random seed is used
        {
            _lastUsedGridSize = gridSize;

            // Generate height data with using DiamondSquare algorithm
            float[,] floatHeightData = DiamondSquareGrid(gridSize, seed: randomSeed, rMin: 0, rMax: 1, noise: noise);

            // To show height map we will use HeightMapMesh3D from Ab3d.PowerToys library
            // This class requires data in array of doubles.
            // NOTE: In the future version of DXEngine there will be an optimized version of HeightMesh that will take floats

            _heightData = new double[gridSize, gridSize];

            for (int rowIndex = 0; rowIndex < gridSize; rowIndex++)
                for (int columnIndex = 0; columnIndex < gridSize; columnIndex++)
                    _heightData[rowIndex, columnIndex] = (double)floatHeightData[rowIndex, columnIndex];


            // We can use HeightMapMesh3D object from Ab3d.PowerToys library to create a MeshGeometry3D object from 2D array
            // We create Mesh with size 1 x 1 x 1 
            // This will allow us to scale it later to any size with ScaleTransform3D
            _heightMapMesh3D = new Meshes.HeightMapMesh3D(new Point3D(0, 0, 0), new Size3D(1, 1, 1), _heightData).Geometry;


            var vertexColorDiffuseMaterial = new DiffuseMaterial();

            int positionsCount = _heightMapMesh3D.Positions.Count;


            // Create vertexColorsArray
            var vertexColorsArray = new Color4[positionsCount];

            // Fill the vertexColorsArray with color values based on the data from _heightData
            CalculateVertexColors(_heightData, vertexColorsArray);

            // To show per-vertex color, we need to use a special VertexColorMaterial
            // Reuse VertexColorMaterial if possible.

            // Dispose old VertexColorMaterial and create a new one
            if (_vertexColorMaterial != null)
                _vertexColorMaterial.Dispose();

            _vertexColorMaterial = new VertexColorMaterial()
            {
                PositionColors = vertexColorsArray
            };

            // This material needs to be set to the WPF vertexColorDiffuseMaterial as UsedDXMaterial.
            // This means that when DXEngine will need to show WPF vertexColorDiffuseMaterial, it will use the vertexColorMaterial.
            vertexColorDiffuseMaterial.SetUsedDXMaterial(_vertexColorMaterial);


            // Now create GeometryModel3D and ModelVisual3D
            var vertexColorGeometryModel3D = new GeometryModel3D(_heightMapMesh3D, vertexColorDiffuseMaterial);
            vertexColorGeometryModel3D.Transform = new ScaleTransform3D(200, 200, 200);

            // To make the landscape colors visible from front and also back side, uncomment the line below (and comment the GeometryModel3D generation a few lines below):
            //vertexColorGeometryModel3D.BackMaterial = vertexColorDiffuseMaterial;

            var vertexColorModelVisual3D = new ModelVisual3D()
            {
                Content = vertexColorGeometryModel3D
            };

            MainViewport.Children.Add(vertexColorModelVisual3D);


            // Add a new GeometryModel3D with the same _heightMapMesh3D
            // But this time render only back faces with gray material
            var backGeometryModel3D = new GeometryModel3D
            {
                Geometry = vertexColorGeometryModel3D.Geometry, // Same geometry as vertexColorGeometryModel3D
                Material = null,  // Do not render front-faced triangles
                BackMaterial = new DiffuseMaterial(Brushes.DimGray), // Render only back faces
                Transform = vertexColorGeometryModel3D.Transform // Same scale transform as vertexColorGeometryModel3D
            };

            var backModelVisual3D = new ModelVisual3D()
            {
                Content = backGeometryModel3D
            };

            MainViewport.Children.Add(backModelVisual3D);


            // Refresh the camera in case we have removed the camera's light with MainViewport.Children.Clear();
            Camera1.Refresh();
        }

        private LinearGradientBrush CreateDataGradientBrush()
        {
            var gradientStopCollection = new GradientStopCollection();

            gradientStopCollection.Add(new GradientStop(Colors.Red, 1));
            gradientStopCollection.Add(new GradientStop(Colors.Yellow, 0.75));
            gradientStopCollection.Add(new GradientStop(Colors.Lime, 0.5));
            gradientStopCollection.Add(new GradientStop(Colors.Aqua, 0.25));
            gradientStopCollection.Add(new GradientStop(Colors.Blue, 0));

            var linearGradientBrush = new LinearGradientBrush(gradientStopCollection,
                                                              new System.Windows.Point(0, 1),  // startPoint (offset == 0) - note that y axis is down (so 1 is bottom)
                                                              new System.Windows.Point(0, 0)); // endPoint (offset == 1)

            return linearGradientBrush;
        }

        private void CalculateVertexColors(double[,] heightData, Color4[] vertexColorsArray)
        {
            int rowsCount = heightData.GetLength(0);
            int columnsCount = heightData.GetLength(1);

            if (rowsCount * columnsCount != vertexColorsArray.Length)
                throw new Exception("Size of positionColorsArray is invalid");

            double minValue = 0;
            double maxValue = 1;
            double dataRange = maxValue - minValue;

            int lastColorIndex = _gradientColor4Array.Length - 1;

            int vertexColorsArrayIndex = 0;
            for (int rowIndex = 0; rowIndex < rowsCount; rowIndex++)
            {
                for (int columnIndex = 0; columnIndex < columnsCount; columnIndex++)
                {
                    // Get one height value
                    double oneValue = heightData[rowIndex, columnIndex];

                    // get color index for this height value
                    double relativeValue = (oneValue - minValue) / dataRange;
                    int colorIndex = (int) (relativeValue * lastColorIndex);

                    if (colorIndex < 0) colorIndex = 0;
                    else if (colorIndex > lastColorIndex) colorIndex = lastColorIndex;

                    // write color
                    vertexColorsArray[vertexColorsArrayIndex] = _gradientColor4Array[colorIndex];

                    // and proceed to the next color
                    vertexColorsArrayIndex++;
                }
            }
        }

        private Color4[] CreateGradientColorsArray(LinearGradientBrush linearGradientBrush)
        {
            // We use HeightMapMesh3D.GetGradientColorsArray to create an array with color values created from the gradient. The array size is 50.
            var gradientColorsArray = Ab3d.Meshes.HeightMapMesh3D.GetGradientColorsArray(linearGradientBrush, 50);

            // Convert WPF colors to SharpDX Color4 used by DXEngine (and DirectX)
            var gradientColor4Array = new Color4[gradientColorsArray.Length];
            for (var i = 0; i < gradientColorsArray.Length; i++)
                gradientColor4Array[i] = gradientColorsArray[i].ToColor4();

            return gradientColor4Array;
        }


        #region HeightMap data generation

        /*  Implemented by https://github.com/eogas/DiamondSquare/
                 *	Generates a grid of VectorPositionColor elements as a 2D greyscale representation of terrain by the
                 *	Diamond-square algorithm: http://en.wikipedia.org/wiki/Diamond-square_algorithm
                 * 
                 *	Arguments: 
                 *		int size - the width or height of the grid being passed in.  Should be of the form (2 ^ n) + 1
                 *		int seed - an optional seed for the random generator
                 *		float rMin/rMax - the min and max height values for the terrain (defaults to 0 - 255 for greyscale)
                 *		float noise - the roughness of the resulting terrain
                 * */

        // My changes to original code: changed float[][] into float[,]
        // This makes access to the array elements faster because CPU does not need to get inner's array object
        private float[,] DiamondSquareGrid(int size, int seed = 0, float rMin = 0, float rMax = 255, float noise = 0.0f)
        {
            // Fail if grid size is not of the form (2 ^ n) - 1 or if min/max values are invalid
            int s = size - 1;
            if (!IsPow2(s) || rMin >= rMax)
                return null;

            // init the grid
            float[,] grid = new float[size, size];

            // Seed the first four corners
            Random rand = (seed == 0 ? new Random() : new Random(seed));
            grid[0, 0] = RandRange(rand, rMin, rMax);
            grid[s, 0] = RandRange(rand, rMin, rMax);
            grid[0, s] = RandRange(rand, rMin, rMax);
            grid[s, s] = RandRange(rand, rMin, rMax);


            /*
            * Use temporary named variables to simplify equations
            * 
            * s0 . d0. s1
            *  . . . . . 
            * d1 . cn. d2
            *  . . . . . 
            * s2 . d3. s3
            * 
            * */
            float s0, s1, s2, s3, d0, d1, d2, d3, cn;

            for (int i = s; i > 1; i /= 2)
            {
                // reduce the random range at each step
                float modNoise = (rMax - rMin) * noise * ((float)i / s);

                // diamonds
                for (int y = 0; y < s; y += i)
                {
                    for (int x = 0; x < s; x += i)
                    {
                        s0 = grid[x, y];
                        s1 = grid[x + i, y];
                        s2 = grid[x, y + i];
                        s3 = grid[x + i, y + i];

                        // cn
                        grid[x + (i / 2), y + (i / 2)] = ((s0 + s1 + s2 + s3) / 4.0f)
                            + RandRange(rand, -modNoise, modNoise);
                    }
                }

                // squares
                for (int y = 0; y < s; y += i)
                {
                    for (int x = 0; x < s; x += i)
                    {
                        s0 = grid[x, y];
                        s1 = grid[x + i, y];
                        s2 = grid[x, y + i];
                        s3 = grid[x + i, y + i];
                        cn = grid[x + (i / 2), y + (i / 2)];

                        d0 = y <= 0 ? (s0 + s1 + cn) / 3.0f : (s0 + s1 + cn + grid[x + (i / 2), y - (i / 2)]) / 4.0f;
                        d1 = x <= 0 ? (s0 + cn + s2) / 3.0f : (s0 + cn + s2 + grid[x - (i / 2), y + (i / 2)]) / 4.0f;
                        d2 = x >= s - i ? (s1 + cn + s3) / 3.0f :
                            (s1 + cn + s3 + grid[x + i + (i / 2), y + (i / 2)]) / 4.0f;
                        d3 = y >= s - i ? (cn + s2 + s3) / 3.0f :
                            (cn + s2 + s3 + grid[x + (i / 2), y + i + (i / 2)]) / 4.0f;

                        grid[x + (i / 2), y] = d0 + RandRange(rand, -modNoise, modNoise);
                        grid[x, y + (i / 2)] = d1 + RandRange(rand, -modNoise, modNoise);
                        grid[x + i, y + (i / 2)] = d2 + RandRange(rand, -modNoise, modNoise);
                        grid[x + (i / 2), y + i] = d3 + RandRange(rand, -modNoise, modNoise);
                    }
                }
            }

            return grid;
        }

        // HELPER FUNCTIONS

        // Returns true if a is a power of 2, else false
        private static bool IsPow2(int a)
        {
            return (a & (a - 1)) == 0;
        }
        private float RandRange(Random r, float rMin, float rMax)
        {
            return rMin + (float)r.NextDouble() * (rMax - rMin);
        }

        #endregion 

        #region Animation

        private void ScheduleNextAction(double timer, Action action)
        {
            if (_dispatcherTimer != null)
            {
                _dispatcherTimer.Stop();
                _dispatcherTimer = null;
            }

            _dispatcherTimer = new DispatcherTimer()
            {
                Interval = TimeSpan.FromMilliseconds(timer)
            };

            _dispatcherTimer.Tick += delegate(object sender, EventArgs args)
            {
                _dispatcherTimer.Stop();
                _dispatcherTimer = null;

                if (((AnimateLandscapeGenerationCheckBox.IsChecked ?? false) || (AutoChangeLandscapeCheckBox.IsChecked ?? false)) &&  // If we are still animating
                     action != null) 
                {
                    action();
                }
            };

            _dispatcherTimer.Start();
        }

        private void ShowNextLandscape()
        {
            int gridSize = GetSelectedSize();
            var maxSize = GetSelectedMaxSize();

            if (gridSize < maxSize)
            {
                // Get to the next grid size:
                gridSize = (gridSize - 1) * 2 + 1; // gridSize should be of the form (2 ^ n) + 1

                SetSelectedSize(gridSize); // This will also recreate the Landscape
            }
            else
            {
                // Go to next seed

                // When AnimateLandscapeGenerationCheckBox is checked we start with smallest size,
                // When animation is disabled, we show landscapes only in the biggerst size
                int startGridSize = (AnimateLandscapeGenerationCheckBox.IsChecked ?? false) ? 3 : GetSelectedMaxSize();
                SetSelectedSize(startGridSize); 

                GoToNextRandomSeed(); // This will also recreate the Landscape
            }
        }

        #endregion

        #region UI

        private void AddLegendControl(LinearGradientBrush linearGradientBrush)
        {
            // Create Legend control
            var gradientColorLegend = new GradientColorLegend()
            {
                Width = 70,
                Height = 200,
                Margin = new Thickness(5, 5, 5, 5)
            };

            gradientColorLegend.GradientBrush = linearGradientBrush;

            gradientColorLegend.LegendLabels.Add(new GradientColorLegend.LegendLabel(0, "0"));
            gradientColorLegend.LegendLabels.Add(new GradientColorLegend.LegendLabel(0.2, "200"));
            gradientColorLegend.LegendLabels.Add(new GradientColorLegend.LegendLabel(0.5, "500"));
            gradientColorLegend.LegendLabels.Add(new GradientColorLegend.LegendLabel(0.9, "900"));
            gradientColorLegend.LegendLabels.Add(new GradientColorLegend.LegendLabel(1.0, "1000"));

            // gradientColorLegend.UpdateLagendLabels(); // This needs to be called after the LegendLabels values are changed; but here we can comment that because this will be called when the GradientColorLegend is loaded


            var legendTitleTextBlock = new TextBlock()
            {
                Text = "Height in m",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Black
            };


            var stackPanel = new StackPanel()
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 5, 5)
            };

            stackPanel.Children.Add(legendTitleTextBlock);
            stackPanel.Children.Add(gradientColorLegend);


            BottomRightPanel.Children.Add(stackPanel);
        }

        private void FillPossibleLandscapeSizes()
        {
            int initialMaxSize = 513;

            SizeRadioBoxesPanel.Children.Clear();
            MaxSizeComboBox.Items.Clear();


            int i = 1;
            int landscapeSize = 0;

            while (landscapeSize < 2048)
            {
                landscapeSize = (int)Math.Pow(2, i) + 1; // gridSize should be of the form (2 ^ n) + 1

                var radioButton = new RadioButton()
                {
                    Content = string.Format("{0} x {0}", landscapeSize),
                    Tag = landscapeSize,
                    Margin = new Thickness(0, 0, 3, 0),
                    GroupName = "CurrentSize"
                };

                if (landscapeSize == 3 && (AnimateLandscapeGenerationCheckBox.IsChecked ?? false))
                    radioButton.IsChecked = true;
                else if (landscapeSize == initialMaxSize && !(AnimateLandscapeGenerationCheckBox.IsChecked ?? false))
                    radioButton.IsChecked = true;


                radioButton.Checked += CurrentSizeRadioButtonOnChecked;

                SizeRadioBoxesPanel.Children.Add(radioButton);


                if (landscapeSize >= 512)
                {
                    var comboBoxItem = new ComboBoxItem()
                    {
                        Content = string.Format("{0} x {0}", landscapeSize),
                        Tag = landscapeSize
                    };

                    if (landscapeSize == initialMaxSize)
                        comboBoxItem.IsSelected = true;

                    MaxSizeComboBox.Items.Add(comboBoxItem);
                }

                i++;
            }

            UpdateMaxSize(initialMaxSize);
        }


        private void UpdateMaxSize(int maxSize)
        {
            RadioButton lastEnabledRadioButton = null;

            foreach (var radioButton in SizeRadioBoxesPanel.Children.OfType<RadioButton>())
            {
                int radioButtonSize = (int)radioButton.Tag;

                if (radioButtonSize <= maxSize)
                {
                    radioButton.IsEnabled = true;
                    lastEnabledRadioButton = radioButton;
                }
                else
                {
                    if (radioButton.IsChecked ?? false)
                    {
                        radioButton.IsChecked = false;

                        if (lastEnabledRadioButton != null)
                            lastEnabledRadioButton.IsChecked = true;
                    }

                    radioButton.IsEnabled = false;
                }
            }
        }

        private int GetSelectedMaxSize()
        {
            var comboBoxItem = MaxSizeComboBox.SelectedItem as ComboBoxItem;

            if (comboBoxItem != null)
                return (int)comboBoxItem.Tag;

            return 0; // default
        }

        private int GetSelectedSize()
        {
            foreach (var radioButton in SizeRadioBoxesPanel.Children.OfType<RadioButton>())
            {
                if (radioButton.IsChecked ?? false)
                    return (int) radioButton.Tag;
            }

            return 0;
        }

        private void SetSelectedSize(int gridSize)
        {
            foreach (var radioButton in SizeRadioBoxesPanel.Children.OfType<RadioButton>())
            {
                int radioButtonSize = (int)radioButton.Tag;

                if (radioButtonSize == gridSize)
                    radioButton.IsChecked = true;
                else if (radioButton.IsChecked ?? false)
                    radioButton.IsChecked = false;
            }
        }

        private int GetRandomSeed()
        {
            int seed;

            if (RandomSeedTextBox.Text == "")
                seed = 0; // This will use Random without any seed
            else if (!Int32.TryParse(RandomSeedTextBox.Text, out seed))
                seed = -1; // Invalid value - nothing will be rendered

            if (seed < 0)
                RandomSeedTextBox.Foreground = Brushes.Red;
            else
                RandomSeedTextBox.ClearValue(ForegroundProperty);

            return seed;
        }

        private void GoToNextRandomSeed()
        {
            var random = new Random();
            var newSeed = random.Next(9999);

            RandomSeedTextBox.Text = newSeed.ToString(); // Changing text will also recreate the Landscape
        }

        private void MaxSizeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            int maxSize = GetSelectedMaxSize();
            UpdateMaxSize(maxSize);
        }

        private void CurrentSizeRadioButtonOnChecked(object sender, RoutedEventArgs routedEventArgs)
        {
            if (!this.IsLoaded)
                return;

            CreateLandscape();
        }

        private void RandomSeedTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            CreateLandscape();
        }

        private void NextSeedButton_OnClick(object sender, RoutedEventArgs e)
        {
            GoToNextRandomSeed();
        }

        private void AnimateLandscapeGenerationCheckBox_OnChecked(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            CreateLandscape();
        }

        private void AnimateLandscapeGenerationCheckBox_OnUnchecked(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            var selectedMaxSize = GetSelectedMaxSize();
            if (_lastUsedGridSize != selectedMaxSize)
                SetSelectedSize(selectedMaxSize);

            CreateLandscape();
        }

        private void AutoChangeLandscapeCheckBox_OnChecked(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            var selectedMaxSize = GetSelectedMaxSize();
            if (_lastUsedGridSize != selectedMaxSize)
                SetSelectedSize(selectedMaxSize);

            CreateLandscape();
        }
        #endregion
    }
}
