using System.Runtime.InteropServices;
using System.Windows.Media.Media3D;
using Ab3d.Meshes;
using Ab3d.Visuals;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using Ab3d.DirectX;
using Ab3d.Utilities;
using SharpDX;
using InstanceData = Ab3d.DirectX.InstanceData;
using Point = System.Windows.Point;

namespace Ab3d.DXEngine.Wpf.Samples.DXEnginePerformance
{
    /// <summary>
    /// Interaction logic for InstancedArrowsSample.xaml
    /// </summary>
    public partial class InstancedArrowsSample : Page, ICompositionRenderingSubscriber
    {
        private InstanceData[] _instanceData;

        private int _xCount;
        private int _yCount;
        private float _xSize;
        private float _ySize;
        private float _arrowsLength;

        private double _minDistance;
        private double _maxDistance;

        private Vector3 _sphereStartPosition;
        private Vector3 _spherePosition;

        private Color4[] _gradientColors;

        private DateTime _startTime;
        private TranslateTransform3D _sphereTranslate;

        private int _cameraIndex;

        private int _lastSecond;
        private int _framesPerSecond;
        private InstancedMeshGeometryVisual3D _instancedMeshGeometryVisual3D;


        private Stopwatch _stopwatch;

        private double _totalUpdateDataTime;
        private int _updateDataSamplesCount;

        private double _totalRenderingTime;
        private int _renderingTimeSamplesCount;

        public InstancedArrowsSample()
        {
            InitializeComponent();


            _xSize = 2000;
            _ySize = 2000;

            _arrowsLength = 30;

            _sphereStartPosition = new Vector3(0, 200, 0);

            Camera1.TargetPosition = new Point3D(0, _sphereStartPosition.Y * 0.3, 0); // target y = 1/3 of the sphere start height


            // Min and max distance will be used to get the color from the current arrow distance
            _minDistance = _sphereStartPosition.Y;

            double dx = Math.Abs(_sphereStartPosition.X) + (_xSize / 2);
            double dz = Math.Abs(_sphereStartPosition.Z) + (_ySize / 2);

            _maxDistance = Math.Sqrt(dx * dx + _sphereStartPosition.Y * _sphereStartPosition.Y + dz * dz);


            _gradientColors = CreateDistanceColors();


            DXDiagnostics.IsCollectingStatistics = true; // Collect rendering time and other statistics

            MainDXViewportView.SceneRendered += delegate(object sender, EventArgs args)
            {
                if (MainDXViewportView.DXScene != null)
                    _totalRenderingTime = MainDXViewportView.DXScene.Statistics.TotalRenderTimeMs;

                _renderingTimeSamplesCount++;
            };


            this.Loaded += delegate(object o, RoutedEventArgs args)
            {
                CreateArrows();

                _startTime = DateTime.Now;

                // Use CompositionRenderingHelper to subscribe to CompositionTarget.Rendering event
                // This is much safer because in case we forget to unsubscribe from Rendering, the CompositionRenderingHelper will unsubscribe us automatically
                // This allows to collect this class will Grabage collector and prevents infinite calling of Rendering handler.
                // After subscribing the ICompositionRenderingSubscriber.OnRendering method will be called on each CompositionTarget.Rendering event
                CompositionRenderingHelper.Instance.Subscribe(this);
            };

            // IMPORTANT:
            // It is very important to call Dispose method on DXSceneView after the control is not used any more (see help file for more info)
            this.Unloaded += delegate
            {
                CompositionRenderingHelper.Instance.Unsubscribe(this);
                MainDXViewportView.Dispose();
            };
        }

        void ICompositionRenderingSubscriber.OnRendering(EventArgs e)
        {
            double elapsedSeconds = (DateTime.Now - _startTime).TotalSeconds;

            // Update statistics only once per second
            if (DateTime.Now.Second != _lastSecond)
            {
                double averageUpdateTime = _updateDataSamplesCount > 0 ? _totalUpdateDataTime / (double) _updateDataSamplesCount : 0;
                double averageRenderTime = _renderingTimeSamplesCount > 0 ? _totalRenderingTime / (double)_renderingTimeSamplesCount : 0;

                RenderingStatsTextBlock.Text = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "Update InstanceData time: {0:#,##0.00} ms;  DXEngine rendering time: {1:#,##0.00} ms",
                    averageUpdateTime,
                    averageRenderTime);

                _totalUpdateDataTime = 0;
                _updateDataSamplesCount = 0;
                _totalRenderingTime = 0;
                _renderingTimeSamplesCount = 0;
                _framesPerSecond = 0;

                _lastSecond = DateTime.Now.Second;
            }
            else
            {
                _framesPerSecond ++;
            }

            if (!(RunAnimationCheckBox.IsChecked ?? false))
                return;

            double x, y, z;

            x = _sphereStartPosition.X;
            y = _sphereStartPosition.Y;
            z = _sphereStartPosition.Z;

            // Rotate on xz plane
            x += Math.Sin(elapsedSeconds * 3) * _xSize * 0.1;
            z += Math.Cos(elapsedSeconds * 3) * _ySize * 0.1;

            // Rotate on xy plane
            x += Math.Sin(elapsedSeconds) * _xSize * 0.2;
            y += Math.Cos(elapsedSeconds) * 90;
            
            // Rotate on yz plane
            y += Math.Sin(elapsedSeconds * 5) * 50;
            z += Math.Cos(elapsedSeconds * 0.3) * _ySize * 0.2;

            
            _sphereTranslate.OffsetX = x;
            _sphereTranslate.OffsetY = y;
            _sphereTranslate.OffsetZ = z;

            _spherePosition = new Vector3((float)x, (float)y, (float)z);

            // After we have a new sphere position, we can update the instances data
            UpdateInstanceData();

            _instancedMeshGeometryVisual3D.Update(0, _instanceData.Length, updateBounds: false);
        }


        private void CreateArrows()
        {
            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            try
            {
                Mouse.OverrideCursor = Cursors.Hand;

                MainViewport.Children.Clear();


                var selectedArrowsNumber = GetSelectedArrowsNumber();

                _xCount = selectedArrowsNumber;
                _yCount = selectedArrowsNumber;


                var sphereVisual3D = new SphereVisual3D()
                {
                    CenterPosition = new Point3D(0, 0, 0),
                    Radius         = 10,
                    Material       = new DiffuseMaterial(Brushes.Gold)
                };

                _sphereTranslate         = new TranslateTransform3D(_sphereStartPosition.ToWpfVector3D());
                sphereVisual3D.Transform = _sphereTranslate;


                MainViewport.Children.Add(sphereVisual3D);




                _instanceData = new InstanceData[_xCount * _yCount];
                UpdateInstanceData();

                var arrowMesh3D = new Ab3d.Meshes.ArrowMesh3D(new Point3D(0, 0, 0), new Point3D(1, 0, 0), 1.0 / 15.0, 2.0 / 15.0, 45, 10, false).Geometry;

                _instancedMeshGeometryVisual3D               = new InstancedMeshGeometryVisual3D(arrowMesh3D);
                _instancedMeshGeometryVisual3D.InstancesData = _instanceData;

                MainViewport.Children.Add(_instancedMeshGeometryVisual3D);


                Camera1.Refresh(); // This will regenerate camera light if it was removed with MainViewport.Children.Clear()

                TotalTextBlock.Text = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "Total positions: {0:#,###}; total triangles: {1:#,###}",
                    _xCount * _yCount * _instancedMeshGeometryVisual3D.MeshGeometry3D.Positions.Count,
                    _xCount * _yCount * _instancedMeshGeometryVisual3D.MeshGeometry3D.TriangleIndices.Count / 3);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void UpdateInstanceData()
        {
            float xStep = _xSize / _xCount;
            float yStep = _ySize / _yCount;

            var instancedData = _instanceData;
            

            if (_stopwatch == null)
                _stopwatch = new Stopwatch();

            _stopwatch.Restart();


            // The following is the initial (unoptimized) code to update the matrices:

            //float x = -(_xSize / 2);
            //for (int xi = 0; xi < _xCount; xi++)
            //{
            //    float y = -(_ySize / 2);

            //    for (int yi = 0; yi < _yCount; yi++)
            //    {
            //        var arrowDirection     = GetArrowDirection(x, y);
            //        var arrowStartPosition = new Vector3(x, 0, y);

            //        instancedData[instanceIndex].World = GetMatrixFromDirection(arrowDirection, arrowStartPosition, arrowScale);

            //        double distance = GetDistance(x, y);
            //        instancedData[instanceIndex].DiffuseColor = GetColorForDistance(distance);

            //        y += yStep;
            //        instanceIndex++;
            //    }

            //    x += xStep;
            //}


            // Below is fully optimized code:

            // PERFORMANCE STATS 
            // System: i7 6700; Release build 
            // Scene: 1 million arrows (1000 x 1000)

            // Initial (unoptimized) for loop: 194 ms
            // For loop with inlined GetArrowDirection, GetDistance and value for _spherePosition 166 ms
            // Parallel.For: 33 ms !!!!
            // Parallel.For with inlining GetMatrixFromDirection (using only one float for arrowsLength): 23 ms !!!
            // Paraller.For inlined GetColorForDistance: 19 ms !!!
            // for loop: 102 ms
            // for loop - inlined Vector3.Cross: 89 ms
            // Paraller.For - inlined Vector3.Cross: 16.09 ms
            // Paraller.For - updated calculation of yAxis (horizontalVector.Y is always 0): 15.66 ms
            // Paraller.For - inlining first normalize and replaced arrowDirection with dx, dy, dz (also removed the second distance calculation) - 14.37 !!!
            // Paraller.For - converted horizontalVector into hx, hy and hz and inlined its normalization: 14.01 ms


            var spherePosition = _spherePosition;
            var arrowsLength = _arrowsLength;
            var gradientColors = _gradientColors;

            //for (int xi = 0; xi < _xCount; xi++)
            Parallel.For(0, _xCount, xi =>
            {
                float x = xi * xStep -(_xSize / 2);
                float y = -(_ySize / 2);

                int instanceIndex = xi * _yCount;

                for (int yi = 0; yi < _yCount; yi++)
                {
                    //var arrowDirection = GetArrowDirection(x, y);
                    //var arrowDirection = new Vector3(spherePosition.X - x, spherePosition.Y, spherePosition.Z - y);
                    //arrowDirection.Normalize();

                    float dx = spherePosition.X - x;
                    float dy = spherePosition.Y;
                    float dz = spherePosition.Z - y;

                    float sphereDistance = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);

                    if (sphereDistance > 1E-10)
                    {
                        float denum = 1.0f / sphereDistance;
                        dx *= denum;
                        dy *= denum;
                        dz *= denum;
                    }

 
                    //var arrowStartPosition = new Vector3(x, 0, y);

                    //instancedData[instanceIndex].World = GetMatrixFromDirection(arrowDirection, arrowStartPosition, arrowScale);

                    //var xAxis = normalizedDirectionVector;
                    //var yAxis = CalculateUpDirection(arrowDirection);

                    //var horizontalVector = SharpDX.Vector3.Cross(SharpDX.Vector3.Up, arrowDirection);

                    //var horizontalVector = new Vector3(
                    //    1 * arrowDirection.Z - 0 * arrowDirection.Y,
                    //    0 * arrowDirection.X - 0 * arrowDirection.Z,
                    //    0 * arrowDirection.Y - 1 * arrowDirection.X);

                    //var horizontalVector = new Vector3(
                    //    1 * dz,
                    //    0,
                    //    -1 * dx);

                    float hx = dz;
                    //float hy = 0;
                    float hz = -dx;

                    float length = (float)Math.Sqrt(hx * hx + hz * hz);

                    if (length > 0.00000001)
                    {
                        float denum = 1.0f / length;
                        hx *= denum;
                        hz *= denum;
                    }
                    else
                    {
                        hx = 0;
                        //hy = 0;
                        hz = 1;
                    }


                    // First we need to check for edge case - the look direction is in the UpVector direction - the length of horizontalVector is 0 (or almost zero)

                    //if (horizontalVector.LengthSquared() < 0.0001) // we can use LengthSquared to avoid costly sqrt
                    //    horizontalVector = SharpDX.Vector3.UnitZ; // Any vector on xz plane could be used

                    //yAxis = SharpDX.Vector3.Cross(arrowDirection, horizontalVector);

                    //yAxis = new Vector3(
                    //    arrowDirection.Y * horizontalVector.Z - arrowDirection.Z * horizontalVector.Y, 
                    //    arrowDirection.Z * horizontalVector.X - arrowDirection.X * horizontalVector.Z,
                    //    arrowDirection.X * horizontalVector.Y - arrowDirection.Y * horizontalVector.X);

                    // horizontalVector.Y is always 0
                    var yAxis = new Vector3(dy * hz, 
                                            dz * hx - dx * hz,
                                            -dy * hx);

                    //var zAxis = SharpDX.Vector3.Cross(arrowDirection, yAxis);

                    var zAxis = new Vector3(dy * yAxis.Z - dz * yAxis.Y,
                                            dz * yAxis.X - dx * yAxis.Z,
                                            dx * yAxis.Y - dy * yAxis.X);


                    // For more info see comments in GetRotationMatrixFromDirection
                    // NOTE: The following math works only for uniform scale (scale factor for x, y and z is the same - arrowsLength in our case)
                    instancedData[instanceIndex].World = new SharpDX.Matrix(dx * arrowsLength,               dy * arrowsLength,               dz * arrowsLength,      0,
                                                                            yAxis.X * arrowsLength,          yAxis.Y * arrowsLength,          yAxis.Z * arrowsLength, 0,
                                                                            zAxis.X * arrowsLength,          zAxis.Y * arrowsLength,          zAxis.Z * arrowsLength, 0,
                                                                            x,                               0,                               y,                      1);



                    //double distance = GetDistance(x, y);
                    //float dx = spherePosition.X - x;
                    //float dz = spherePosition.Z - y;
                    //float distance = (float)Math.Sqrt(dx * dx + spherePosition.Y * spherePosition.Y + dz * dz);

                    //instancedData[instanceIndex].DiffuseColor = GetColorForDistance(distance);

                    int materialIndex;

                    if (sphereDistance <= _minDistance)
                        materialIndex = 0;
                    else if (sphereDistance >= _maxDistance)
                        materialIndex = _gradientColors.Length - 1;
                    else
                        materialIndex = (int)((sphereDistance - _minDistance) * (_gradientColors.Length - 1) / (_maxDistance - _minDistance));

                    instancedData[instanceIndex].DiffuseColor = gradientColors[materialIndex];


                    y += yStep;
                    instanceIndex++;
                }
            } );


            _stopwatch.Stop();
            _totalUpdateDataTime += _stopwatch.Elapsed.TotalMilliseconds;
            _updateDataSamplesCount++;
        }

        private Vector3 GetArrowDirection(float x, float y)
        {
            var direction = new Vector3(_spherePosition.X - x, _spherePosition.Y, _spherePosition.Z - y);
            direction.Normalize();
            //direction *= _arrowsLength;

            return direction;
        }

        private double GetDistance(double x, double y)
        {
            double dx = _spherePosition.X - x;
            double dz = _spherePosition.Z - y;

            double distance = Math.Sqrt(dx * dx + _spherePosition.Y * _spherePosition.Y + dz * dz);

            return distance;
        }

        private Color4 GetColorForDistance(double distance)
        {
            if (_gradientColors == null)
                _gradientColors = CreateDistanceColors();

            int materialIndex;

            if (distance <= _minDistance)
                materialIndex = 0;
            else if (distance >= _maxDistance)
                materialIndex = _gradientColors.Length - 1;
            else
                materialIndex = Convert.ToInt32((distance - _minDistance) * (_gradientColors.Length - 1) / (_maxDistance - _minDistance));

            return _gradientColors[materialIndex];
        }

        private Color4[] CreateDistanceColors()
        {
            // Here we prepare list of materials that will be used to for arrows on different distances from the gold sphere

            // We use HeightMapMesh3D.GetGradientColorsArray to create an array with color values created from the gradient. The array size is 30.
            var gradientStopCollection = new GradientStopCollection();
            gradientStopCollection.Add(new GradientStop(Colors.Red, 0));          // uses with min distance - closest to the object
            gradientStopCollection.Add(new GradientStop(Colors.Orange, 0.2));     // uses with min distance - closest to the object
            gradientStopCollection.Add(new GradientStop(Colors.Yellow, 0.4));     // uses with min distance - closest to the object
            gradientStopCollection.Add(new GradientStop(Colors.Green, 0.6));      // uses with min distance - closest to the object
            gradientStopCollection.Add(new GradientStop(Colors.DarkBlue, 0.8));   // used with max distance
            gradientStopCollection.Add(new GradientStop(Colors.DodgerBlue, 1));   // used with max distance

            var linearGradientBrush = new LinearGradientBrush(gradientStopCollection, new Point(0, 0), new Point(0, 1));

            // Use linearGradientBrush to create an array with 128 Colors
            var gradientColorsArray = HeightMapMesh3D.GetGradientColorsArray(linearGradientBrush, 128);

            var gradientColors = new Color4[gradientColorsArray.Length];

            for (int i = 0; i < gradientColorsArray.Length; i++)
                gradientColors[i] = gradientColorsArray[i].ToColor4();

            return gradientColors;
        }

        private int GetSelectedArrowsNumber()
        {
            var comboBoxItem = ArrowsNumberComboBox.SelectedItem as ComboBoxItem;
            string selectedText = (string)comboBoxItem.Content;

            var parts = selectedText.Split(' ');

            int arrowsNumber = Int32.Parse(parts[0]);

            return arrowsNumber;
        }

        private void ArrowsNumberComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;
            
            CreateArrows();
        }

        private void ChangeCameraButton_OnClick(object sender, RoutedEventArgs e)
        {
            _cameraIndex++;

            switch (_cameraIndex)
            {
                case 1:
                    Camera1.BeginInit();
                    Camera1.Heading = 47;
                    Camera1.Attitude = -8.6;
                    Camera1.Distance = 1200;
                    Camera1.TargetPosition = new Point3D(0, 60, 0);
                    Camera1.Offset = new Vector3D(-46, -227, 66);
                    Camera1.EndInit();
                    break;

                case 2:
                    Camera1.BeginInit();
                    Camera1.Heading = -1.4;
                    Camera1.Attitude = -4;
                    Camera1.Distance = 1776;
                    Camera1.TargetPosition = new Point3D(0, 60, 0);
                    Camera1.Offset = new Vector3D(-16, -109, 37);
                    Camera1.EndInit();
                    break;

                case 3:
                    Camera1.BeginInit();
                    Camera1.Heading = 0;
                    Camera1.Attitude = -31;
                    Camera1.Distance = 1325;
                    Camera1.TargetPosition = new Point3D(0, 60, 0);
                    Camera1.Offset = new Vector3D(10, -134, -130);
                    Camera1.EndInit();
                    break;

                case 4:
                    Camera1.BeginInit();
                    Camera1.Heading        = -0.57;
                    Camera1.Attitude       = -89;
                    Camera1.Distance       = 4275;
                    Camera1.TargetPosition = new Point3D(0, 60, 0);
                    Camera1.Offset         = new Vector3D(-16, -109, 37);
                    Camera1.EndInit();
                    break;

                default:
                    Camera1.BeginInit();
                    Camera1.Heading = 30;
                    Camera1.Attitude = -20;
                    Camera1.Distance = 2500;
                    Camera1.TargetPosition = new Point3D(0, 60, 0);
                    Camera1.Offset = new Vector3D(0, 0, 0);
                    Camera1.EndInit();

                    _cameraIndex = 0;
                    break;
            }
        }
    }
}
