using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Point = System.Windows.Point;
using System.Windows.Shapes;
using Ab3d.Utilities;
using Ab3d.Common.Cameras;
using Ab3d.DirectX;
using Ab3d.DirectX.Models;
using Ab3d.Visuals;


#if SHARPDX
using SharpDX;
#endif

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineOther
{
    /// <summary>
    /// Interaction logic for FastPoint3DTo2DSample.xaml
    /// </summary>
    public partial class FastPoint3DTo2DSample : Page, ICompositionRenderingSubscriber
    {
        private bool _isAnimating;

        private Vector2 _oldSphere1ScreenPosition;
        private Vector2 _oldBox1ScreenMinPos;
        private Vector2 _oldBox1ScreenMaxPos;

        private Vector3[] _boundingBoxCorners = new Vector3[8];
        private Vector2[] _boundingBoxScreenCorners = new Vector2[8];
        private Vector2[] _screenVertices;

        private Ellipse[] _sphere2Ellipses;

        private DateTime _lastTime;


        public FastPoint3DTo2DSample()
        {
            InitializeComponent();


            Camera1.CameraChanged += delegate (object sender, CameraChangedRoutedEventArgs args)
            {
                // Update the positions of the 2D elements on the OverlayCanvas on every camera change
                UpdateOverlayData();
            };

            MainViewport.SizeChanged += delegate (object sender, SizeChangedEventArgs args)
            {
                // Update the positions of the 2D elements on the OverlayCanvas when the size of Viewport3D is changed
                UpdateOverlayData();
            };


            this.Loaded += delegate(object o, RoutedEventArgs args)
            {
                StartAnimation();
            };

            this.Unloaded += delegate(object o, RoutedEventArgs args)
            {
                StopAnimation();
            };
        }

        private void StartAnimation()
        {
            if (_isAnimating)
                return;

            // Use CompositionRenderingHelper to subscribe to CompositionTarget.Rendering event
            // This is much safer because in case we forget to unsubscribe from Rendering, the CompositionRenderingHelper will unsubscribe us automatically
            // This allows to collect this class will Grabage collector and prevents infinite calling of Rendering handler.
            // After subscribing the ICompositionRenderingSubscriber.OnRendering method will be called on each CompositionTarget.Rendering event
            CompositionRenderingHelper.Instance.Subscribe(this);

            _isAnimating = true;
        }

        private void StopAnimation()
        {
            if (!_isAnimating)
                return;

            CompositionRenderingHelper.Instance.Unsubscribe(this);

            _isAnimating = false;

            _lastTime = DateTime.MinValue;
        }

        void ICompositionRenderingSubscriber.OnRendering(EventArgs e)
        {
            AnimateObjects();
            UpdateOverlayData();
        }

        private void AnimateObjects()
        {
            DateTime now = DateTime.Now;

            if (_lastTime == DateTime.MinValue)
            {
                _lastTime = now;
                return;
            }

            double secondsDiff = (now - _lastTime).TotalSeconds;
            double speed = SpeedSlider.Value;

            AnimateObjects(secondsDiff, speed);

            _lastTime = now;
        }

        public void AnimateObjects(double time, double speed)
        {
            AnimateTranslateTransform3D(Box1TranslateTransform3D,    time, speed);
            AnimateTranslateTransform3D(Sphere1TranslateTransform3D, time, speed);
            AnimateTranslateTransform3D(Sphere2TranslateTransform3D, time, speed);
        }

        private void AnimateTranslateTransform3D(TranslateTransform3D translateTransform3D, double time, double speed)
        {
            Point3D boxPosition = new Point3D(translateTransform3D.OffsetX, translateTransform3D.OffsetY, translateTransform3D.OffsetZ);

            // Rotate the box position for the secondsDiff * SpeedSlider.Value degrees
            AxisAngleRotation3D axisAngleRotation3D = new AxisAngleRotation3D(new Vector3D(0, 1, 0), time * speed);
            RotateTransform3D   rotateTransform3D   = new RotateTransform3D(axisAngleRotation3D);

            Point3D rotatedPosition = rotateTransform3D.Transform(boxPosition);

            translateTransform3D.OffsetX = rotatedPosition.X;
            translateTransform3D.OffsetY = rotatedPosition.Y;
            translateTransform3D.OffsetZ = rotatedPosition.Z;
        }

        private void UpdateOverlayData()
        {
            var dxScene = MainDXViewportView.DXScene;

            //
            // Convert Point3D or Vector3 (sphere's CenterPosition) to 2D position on the screen
            //
            var sphereCenterPosition = Sphere1Visual3D.CenterPosition;
            sphereCenterPosition = Sphere1Visual3D.Transform.Transform(sphereCenterPosition);

            // We can call Point3DTo2D on the Camera object...
            //var spheresCenterOnScreen = Camera1.Point3DTo2D(sphereCenterPosition);

            // or call Point3DTo2D on DXScene object. The later takes Vector3 as parameter instead of Point3D.
            // Note that because we cannot cache the worldToViewportMatrix matrix, the DXScene.Point3DTo2D
            // is slower than camera.Point3DTo2D (but Points3DTo2D that takes arrays is much faster when the same method on camera).
            var spheresCenterOnScreen = dxScene.Point3DTo2D(sphereCenterPosition.ToVector3());


            bool isSphere1ScreenPositionChangedSignificantly = Math.Abs(spheresCenterOnScreen.X - _oldSphere1ScreenPosition.X) >= 1.0 ||
                                                               Math.Abs(spheresCenterOnScreen.Y - _oldSphere1ScreenPosition.Y) >= 1.0;

            // Update UI only when the difference is significant (more than 1 pixel)
            if (isSphere1ScreenPositionChangedSignificantly)
            {
                Sphere1ConnectionLine.X1 = spheresCenterOnScreen.X;
                Sphere1ConnectionLine.Y1 = spheresCenterOnScreen.Y;                
                
                Sphere1ConnectionLine.X2 = Sphere1ConnectionLine.X1 + 30;
                Sphere1ConnectionLine.Y2 = Sphere1ConnectionLine.Y1 - 15;


                Sphere1InfoTextBlock.Text = string.Format("Screen position\r\nby Point3DTo2D:\r\nx:{0:0} y:{1:0}",
                    spheresCenterOnScreen.X, spheresCenterOnScreen.Y);

                Canvas.SetLeft(Sphere1InfoBorder, spheresCenterOnScreen.X + 29);
                Canvas.SetTop(Sphere1InfoBorder, spheresCenterOnScreen.Y - Sphere1InfoBorder.ActualHeight - 14);

                _oldSphere1ScreenPosition = spheresCenterOnScreen;
            }


            // 
            // Convert Rect3D or BoundingBox to 2D positions on the screen
            //
            var boxBounds3D = Box1Visual3D.Content.Bounds;
            boxBounds3D = Box1Visual3D.Transform.TransformBounds(boxBounds3D);

            // We can call Rect3DTo2D on the Camera object...
            //Rect screenBoxBounds2D = Camera1.Rect3DTo2D(boxBounds3D);

            // ... or convert Rect3D to BoundingBox and then call BoundingBoxTo2D on DXScene object
            // (the conversion is done so the sample is similar to the original sample from Ab3d.PowerToys library.
            //  Otherwise, the following code would be used if you have a BoundingBox object instead of Rect3D):

            var boundingBox = boxBounds3D.ToBoundingBox();
            boundingBox.GetCorners(_boundingBoxCorners);

            // dxScene.Points3DTo2D is significantly faster than Camera1.Rect3DTo2D
            dxScene.Points3DTo2D(_boundingBoxCorners, _boundingBoxScreenCorners);

            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;

            for (int i = 0; i < 8; i++)
            {
                var x = _boundingBoxScreenCorners[i].X;
                var y = _boundingBoxScreenCorners[i].Y;

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }


            // Update UI only when the difference is significant (more than 1 pixel)
            if (Math.Abs(minX - _oldBox1ScreenMinPos.X) >= 1.0 ||
                Math.Abs(minY - _oldBox1ScreenMinPos.Y) >= 1.0 ||
                Math.Abs(maxX  - _oldBox1ScreenMaxPos.X) >= 1.0 ||
                Math.Abs(maxY - _oldBox1ScreenMaxPos.Y) >= 1.0)
            {
                Canvas.SetLeft(Box1OverlayRectangle, minX);
                Canvas.SetTop(Box1OverlayRectangle, minY);
                Box1OverlayRectangle.Width = maxX - minX;
                Box1OverlayRectangle.Height = maxY - minY;

                Box1ConnectionLine.X1 = maxX - 1;
                Box1ConnectionLine.Y1 = minY + 1;
                Box1ConnectionLine.X2 = maxX + 19;
                Box1ConnectionLine.Y2 = minY - 9;

                Box1InfoTextBlock.Text = string.Format("Screen bounds by\r\nRect3DTo2D:\r\nx:{0:0} y:{1:0}\r\nw:{2:0} h:{3:0}", minX, minY, maxX - minX, maxY - minY);

                Canvas.SetLeft(Box1InfoBorder, maxX + 18);
                Canvas.SetTop(Box1InfoBorder, minY - Box1InfoBorder.ActualHeight - 8);

                _oldBox1ScreenMinPos = new Vector2(minX, minY);
                _oldBox1ScreenMaxPos = new Vector2(maxX, maxY);
            }


            if (isSphere1ScreenPositionChangedSignificantly) // reuse the test done for Sphere1
            {
                var sphereGeometryModel3D = Sphere2Visual3D.Content as GeometryModel3D;

                if (sphereGeometryModel3D != null)
                {
                    var sceneNode = MainDXViewportView.GetSceneNodeForWpfObject(sphereGeometryModel3D) as WpfGeometryModel3DNode;

                    if (sceneNode != null)
                    {
                        var dxMesh = sceneNode.DXMesh;
                        dxMesh.GetVertexAndIndexBuffers(out PositionNormalTexture[] vertices, out int[] indices);

                        if (_screenVertices == null || _screenVertices.Length != vertices.Length)
                            _screenVertices = new Vector2[vertices.Length];

                        var transformMatrix = Sphere2Visual3D.Transform.Value.ToMatrix();

                        // We can pass an array of Vector3 or PositionNormalTexture items.
                        // dxScene.Points3DTo2D is significantly faster than Camera1.Points3DTo2D
                        dxScene.Points3DTo2D(vertices, _screenVertices, ref transformMatrix, useParallelFor: false);


                        var positionsCount = _screenVertices.Length;

                        if (_sphere2Ellipses == null || _sphere2Ellipses.Length != _screenVertices.Length)
                        {
                            // Do not create new array on each UI update
                            _sphere2Ellipses = new Ellipse[positionsCount];
                            for (int i = 0; i < positionsCount; i++)
                            {
                                var ellipse = new Ellipse()
                                {
                                    Fill = Brushes.Yellow,
                                    Width = 4,
                                    Height = 4
                                };

                                OverlayCanvas.Children.Add(ellipse);

                                _sphere2Ellipses[i] = ellipse;
                            }
                        }

                        double halfEllipseWidth = _sphere2Ellipses[0].Width / 2;
                        double halfEllipseHeight = _sphere2Ellipses[0].Width / 2;

                        for (var i = 0; i < _sphere2Ellipses.Length; i++)
                        {
                            var ellipse = _sphere2Ellipses[i];

                            float x = _screenVertices[i].X;
                            float y = _screenVertices[i].Y;

                            if (float.IsNaN(x) || float.IsNaN(y)) // This may happen when object is on the camera's near plane
                                continue;

                            Canvas.SetLeft(ellipse, x - halfEllipseWidth);
                            Canvas.SetTop(ellipse, y - halfEllipseHeight);
                        }


                        var averageX = _screenVertices.Average(p => p.X);
                        var averageY = _screenVertices.Average(p => p.Y);

                        Sphere2ConnectionLine.X1 = averageX;
                        Sphere2ConnectionLine.Y1 = averageY;
                        Sphere2ConnectionLine.X2 = averageX + 49;
                        Sphere2ConnectionLine.Y2 = averageY - 25;

                        Canvas.SetLeft(Sphere2InfoBorder, averageX + 49);
                        Canvas.SetTop(Sphere2InfoBorder, averageY - Sphere2InfoBorder.ActualHeight - 24);
                    }
                }
            }
        }


        private void BenchmarkButton_OnClick(object sender, RoutedEventArgs e)
        {
            SetupBenchmark();
            RunBenchmarks();

            var resultsText = string.Format("Points3DTo2D for {0:N0} positions.\r\n\r\nAb3d.DXEngine DScene.Points3DTo2D results:\r\nAverage: {1:F4} ms (stdev: {2:F2}; mean: {3:F2})\r\n\r\nAb3d.PowerToys Camera.Points3DTo2D results:\r\nAverage: {4:F2} ms (stdev: {5:F2}; mean: {6:F2})\r\n",
                _benchmarkPositionsCount,
                _dxEngineResults["average"],_dxEngineResults["stdev"],_dxEngineResults["q2"],
                _powerToysResults["average"],_powerToysResults["stdev"],_powerToysResults["q2"]);

            MessageBox.Show(resultsText, "Benchmark results");
        }


        private const int WarmupIterations = 20;
        private const int BenchmarkIterations = 100;
        private const bool IsMultiThreadedBenchmark = false;

        private Stopwatch _benchmarkStopwatch;
        private int _benchmarkPositionsCount;

        private Point3D[] _benchmarkPoints3D;
        private Vector3[] _benchmarkVectors3D;

        private Point[] _benchmarkScreenPoints2D;
        private Vector2[] _benchmarkScreenVectors2D;

        private double[] _benchmarkResults;
        private Dictionary<string, double> _dxEngineResults;
        private Dictionary<string, double> _powerToysResults;


        private void SetupBenchmark()
        {
            //var sphereMesh = new Ab3d.Meshes.SphereMesh3D(centerPosition: new Point3D(0, 0, 0), radius: 100, segments: 100); // 10k positions
            var sphereMesh = new Ab3d.Meshes.SphereMesh3D(centerPosition: new Point3D(0, 0, 0), radius: 100, segments: 500); // 251k positions
            //var sphereMesh = new Ab3d.Meshes.SphereMesh3D(centerPosition: new Point3D(0, 0, 0), radius: 100, segments: 1000); // 1 MIO positions
            
            
            _benchmarkPoints3D = sphereMesh.Geometry.Positions.ToArray();
            _benchmarkVectors3D = _benchmarkPoints3D.Select(p => p.ToVector3()).ToArray();
            _benchmarkPositionsCount = _benchmarkVectors3D.Length;

            _benchmarkScreenPoints2D = new Point[_benchmarkPositionsCount];
            _benchmarkScreenVectors2D = new Vector2[_benchmarkPositionsCount];

            _benchmarkResults = new double[BenchmarkIterations];

            _benchmarkStopwatch = new Stopwatch();
        }

        private void RunBenchmarks()
        {
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                RunVector3Benchmark(WarmupIterations, IsMultiThreadedBenchmark);
                RunVector3Benchmark(BenchmarkIterations, IsMultiThreadedBenchmark);

                _dxEngineResults = Ab3d.DirectX.Client.Diagnostics.PerformanceAnalyzer.CalculateCommonStatistics(_benchmarkResults);


                RunPoint3DBenchmark(WarmupIterations, IsMultiThreadedBenchmark);
                RunPoint3DBenchmark(BenchmarkIterations, IsMultiThreadedBenchmark);

                _powerToysResults = Ab3d.DirectX.Client.Diagnostics.PerformanceAnalyzer.CalculateCommonStatistics(_benchmarkResults);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }
        
        private void RunVector3Benchmark(int count, bool isMultiThreading)
        {
            double time = 0;

            var dxScene = MainDXViewportView.DXScene;

            _benchmarkStopwatch.Restart();

            for (int i = 0; i < count; i++)
            {
                dxScene.Points3DTo2D(_benchmarkVectors3D, _benchmarkScreenVectors2D, useParallelFor: isMultiThreading);

                var newTime = _benchmarkStopwatch.Elapsed.TotalMilliseconds;

                _benchmarkResults[i] = newTime - time;
                time = newTime;
            }
            
            _benchmarkStopwatch.Stop();
        }
        
        private void RunPoint3DBenchmark(int count, bool isMultiThreading)
        {
            double time = 0;

            _benchmarkStopwatch.Restart();

            for (int i = 0; i < count; i++)
            {
                Camera1.Points3DTo2D(_benchmarkPoints3D, _benchmarkScreenPoints2D, useParallelFor: isMultiThreading);

                var newTime = _benchmarkStopwatch.Elapsed.TotalMilliseconds;

                _benchmarkResults[i] = newTime - time;
                time = newTime;
            }
            
            _benchmarkStopwatch.Stop();
        }
    }
}
